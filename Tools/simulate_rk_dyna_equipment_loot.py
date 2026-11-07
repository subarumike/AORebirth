"""Compare current and reconstructed RK dyna equipment loot against reviewed observations."""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from functools import cache
import json
from pathlib import Path
from random import Random

from reconcile_rk_dyna_loot import (
    BOSS_COHORT, Equipment, HashPools, load_captures, load_item_metadata,
    quality_window, read_json, sha256,
)


def equipment_candidates(data, observations):
    npcs_path = data / "NpcTemplates.json"
    npcs = read_json(npcs_path)
    spawn_paths = [data / "Playfields" / str(playfield) / "Spawns.json"
                   for playfield in sorted({int(row["Playfield"]) for row in observations})]
    placed_hashes = {spawn["hashText"] for path in spawn_paths for spawn in read_json(path)["spawns"]}
    bands = [(key, band) for key in sorted(placed_hashes) for band in npcs.get(key, {}).get("Templates", [])
             if band.get("Stats", {}).get("0", 0) & 2 and band.get("Equipment")]
    return bands, [npcs_path, *spawn_paths]


def representative_equipment(bands, names, modifiers):
    variants = defaultdict(list)
    for key, band in bands:
        equipment = tuple(tuple(pair) if len(pair) > 1 else (pair[0], pair[0])
                          for pair in band["Equipment"])
        equipped_names = {names.get(low, "") for low, _ in equipment}
        if modifiers <= equipped_names:
            variants[equipment].append((key, band["Level"]))
    if len(variants) != 1:
        raise ValueError(f"Expected one common canonical equipment variant for the observed modifiers, found {len(variants)}")
    return next(iter(variants.items()))


def level_band(level):
    return next(label for limit, label in ((24, "1-24"), (49, "25-49"), (99, "50-99"),
                                           (149, "100-149"), (199, "150-199"), (float("inf"), "200+"))
                if level <= limit)


def endpoint_key(low, high):
    return f"{low}:{high}"


def record_scaling_agreement(counts, qualities):
    if len(qualities) >= 2:
        counts["AllScalingItemsAtSameQl" if len(set(qualities)) == 1 else "MixedScalingQl"] += 1


def equipment_rules(pools, roots):
    rules = {}
    for root in roots:
        record = pools.records.get(root, {})
        if "EquipmentLoot" not in record:
            continue
        children = record.get("Children", [])
        authored = record["EquipmentLoot"]
        if not children or len(children) != len(set(children)) or set(children) != set(authored):
            raise ValueError(f"EquipmentLoot rules must correspond to every unique child of {root}")
        rules[root] = []
        for child in children:
            leaves, issues = pools.inspect(child, equipment=True)
            rule = authored[child]
            weight, low, high = (rule[field] for field in ("Weight", "MinCenter", "MaxCenter"))
            if (issues or len(leaves) != 1 or leaves[0].path != (child,)
                    or any(type(value) is not int for value in (weight, low, high))
                    or weight < 1 or not 1 <= low <= high):
                raise ValueError(f"Invalid EquipmentLoot family {root}/{child}")
            rules[root].append((leaves[0], weight, low, high))
    return rules


def weighted_families(rules, center, desired):
    return tuple((leaf, min(desired, leaf.maximum) if leaf.minimum < leaf.maximum else leaf.minimum, weight)
                 for leaf, weight, low, high in rules
                 if low <= center <= high and (leaf.minimum == leaf.maximum or leaf.minimum <= desired))


def roll_family(families, random):
    total = sum(weight for _, _, weight in families)
    if total == 0:
        return None
    roll = random.randrange(total)
    for leaf, quality, weight in families:
        if roll < weight:
            return leaf, quality
        roll -= weight
    raise AssertionError("Weighted family roll exceeded its total")


def simulate(cases, pools, variance, trials, seed, *, shared):
    roots = {item_hash for spells, _, _ in cases for item_hash, _ in spells}
    rules = equipment_rules(pools, roots) if shared else {}
    coverage = {item_hash: pools.inspect(item_hash, equipment=item_hash in rules) for item_hash in sorted(roots)}
    scaling_hashes = {item_hash for item_hash, (leaves, _) in coverage.items()
                      if any(leaf.minimum < leaf.maximum for leaf in leaves)}

    @cache
    def candidates(item_hash, center, desired):
        if item_hash in rules:
            return weighted_families(rules[item_hash], center, desired)
        return tuple((leaf, quality, 1) for leaf, quality in pools.highest(item_hash, desired))

    quality_random = Random(seed)
    family_random = Random(seed + 1)
    counts = Counter()
    counts_by_modifier = defaultdict(Counter)
    counts_by_level = defaultdict(Counter)
    scaling_qls = Counter()
    item_qls = Counter()
    item_qls_by_level = defaultdict(Counter)
    results_by_hash = defaultdict(Counter)
    endpoint_occurrences = Counter()
    endpoint_corpses = Counter()
    endpoint_sources = defaultdict(Counter)
    family_occurrences = Counter()
    roll_opportunities = Counter()
    for spells, modifier, level in cases:
        band = level_band(level)
        for _ in range(trials):
            shared_rolls = {}
            reached_scaling = []
            corpse_endpoints = set()
            count = 0
            for item_hash, center in spells:
                if shared and item_hash in scaling_hashes and center in shared_rolls:
                    desired = shared_rolls[center]
                else:
                    low, high = quality_window(center, variance)
                    desired = quality_random.randint(low, high)
                    if shared and item_hash in scaling_hashes:
                        shared_rolls[center] = desired
                context = (item_hash, center, desired)
                roll_opportunities[context] += 1
                selected = roll_family(candidates(*context), family_random)
                if selected is None:
                    continue
                leaf, quality = selected
                if leaf.minimum < quality < leaf.maximum:
                    reached_scaling.append(quality)
                endpoints = endpoint_key(*leaf.ids_at(quality))
                family = f"{item_hash}/{leaf.path[-1]}"
                endpoint_occurrences[endpoints] += 1
                endpoint_sources[endpoints][family] += 1
                corpse_endpoints.add(endpoints)
                family_occurrences[family] += 1
                results_by_hash[item_hash][quality] += 1
                item_qls[quality] += 1
                item_qls_by_level[band][quality] += 1
                count += 1
            counts[count] += 1
            counts_by_modifier[modifier][count] += 1
            counts_by_level[band][count] += 1
            endpoint_corpses.update(corpse_endpoints)
            record_scaling_agreement(scaling_qls, reached_scaling)

    eligible_families = Counter()
    for (item_hash, center, desired), occurrences in roll_opportunities.items():
        for leaf, _, _ in candidates(item_hash, center, desired):
            eligible_families[f"{item_hash}/{leaf.path[-1]}"] += occurrences

    return {
        "ItemCountHistogram": counts,
        "ItemCountHistogramByModifier": counts_by_modifier,
        "ItemCountHistogramByLevelBand": counts_by_level,
        "CorpsesWithMultipleUnclampedScalingDrops": sum(scaling_qls.values()),
        "UnclampedScalingQlAgreement": scaling_qls,
        "ItemQlHistogram": item_qls,
        "ItemQlHistogramByLevelBand": item_qls_by_level,
        "PerHashItemQlHistogram": results_by_hash,
        "PerEndpointPairSlots": endpoint_occurrences,
        "PerEndpointPairCorpses": endpoint_corpses,
        "PerEndpointPairSources": endpoint_sources,
        "PerHashFamilySlots": family_occurrences,
        "PerHashFamilyEligibleRolls": eligible_families,
        "PoolCoverage": {key: {"KnownLeaves": len({leaf.path[-1] for leaf in leaves}),
                               "HasScalingLeaf": key in scaling_hashes, "CatalogIssues": issues,
                               "UsesWeightedEquipmentPolicy": key in rules}
                         for key, (leaves, issues) in coverage.items()},
    }


def synthetic_control(trials, seed):
    records = {
        "ROOT": {"Children": ["NEST"]},
        "NEST": {"Children": ["GEAR"]},
        "GEAR": {"Templates": [1, 2]},
        "FIXD": {"Templates": [3]},
        "CAPD": {"Templates": [4, 5]},
    }
    qualities = {1: 1, 2: 100, 3: 20, 4: 1, 5: 25}
    pools = HashPools(records, qualities, {})
    spells = [("ROOT", 40), ("GEAR", 40), ("FIXD", 40), ("CAPD", 40)]
    cases = [(spells, "Synthetic", 40)]
    return {"BeforeIndependentQl": simulate(cases, pools, 25, trials, seed, shared=False),
            "AfterSharedQl": simulate(cases, pools, 25, trials, seed, shared=True)}


def observed_distribution(corpses, modifiers, drops, qualities, variance):
    scaling_agreement = Counter()
    endpoint_occurrences = Counter()
    endpoint_corpses = Counter()
    counts_by_modifier = defaultdict(Counter)
    counts_by_level = defaultdict(Counter)
    item_qls = Counter()
    item_qls_by_level = defaultdict(Counter)
    above_window = 0
    for (corpse, observation), modifier in zip(corpses.items(), modifiers, strict=True):
        scaling_qls = []
        level = int(observation["NpcLevel"])
        band = level_band(level)
        _, high = quality_window(level, variance)
        corpse_endpoints = set()
        for row in drops[corpse]:
            low_id, high_id, quality = (int(row[key]) for key in ("LowId", "HighId", "Ql"))
            above_window += quality > high
            endpoints = endpoint_key(low_id, high_id)
            endpoint_occurrences[endpoints] += 1
            corpse_endpoints.add(endpoints)
            item_qls[quality] += 1
            item_qls_by_level[band][quality] += 1
            if qualities[low_id] < quality < qualities[high_id]:
                scaling_qls.append(quality)
        count = len(drops[corpse])
        counts_by_modifier[modifier][count] += 1
        counts_by_level[band][count] += 1
        endpoint_corpses.update(corpse_endpoints)
        record_scaling_agreement(scaling_agreement, scaling_qls)

    return {"Corpses": len(corpses), "ItemSlots": sum(len(drops[key]) for key in corpses),
            "ItemSlotsAboveLevelWindow": above_window,
            "ItemCountHistogram": dict(Counter(int(row["ItemSlots"]) for row in corpses.values())),
            "ItemCountHistogramByModifier": counts_by_modifier,
            "ItemCountHistogramByLevelBand": counts_by_level,
            "ItemQlHistogram": item_qls,
            "ItemQlHistogramByLevelBand": item_qls_by_level,
            "PerEndpointPairSlots": endpoint_occurrences,
            "PerEndpointPairCorpses": endpoint_corpses,
            "CorpsesWithMultipleUnclampedScalingDrops": sum(scaling_agreement.values()),
            "UnclampedScalingQlAgreement": dict(scaling_agreement)}


def run(args):
    evidence = args.evidence_root.resolve(strict=True)
    output = args.output.resolve()
    name = "rk-dyna-reconstruction-simulation.json" if args.overlay else "rk-dyna-equipment-simulation.json"
    if output.parent != evidence or output.name != name:
        raise ValueError(f"Use {name} directly in the private evidence root")
    data = args.gamedata_root.resolve(strict=True)
    if evidence.is_relative_to(data):
        raise ValueError("The evidence root must be outside canonical GameData")

    metadata_dir = args.item_metadata.resolve(strict=True)
    item_path = args.base_item_templates or data / "ItemTemplates.json"
    all_corpses, drops, capture_inputs = load_captures([evidence])
    corpses = {key: row for key, row in sorted(all_corpses.items()) if row["Cohort"] == BOSS_COHORT}
    if not corpses:
        raise ValueError("Expected reviewed boss observations")
    observations = list(corpses.values())

    bands, npc_inputs = equipment_candidates(data, observations)
    equipment_ids = {item for _, band in bands for pair in band["Equipment"] for item in pair}
    spawn_args = {}
    functions = []
    qualities, flags, names, casts = load_item_metadata(
        metadata_dir, data / "items.dat", equipment_ids, spawn_args, functions)
    if not functions:
        raise ValueError("Use the items.dat metadata export with SpawnItem string arguments")
    item_records = read_json(item_path)
    equipment = Equipment(functions, qualities, names, casts, spawn_args, item_records)
    matched_modifiers = []
    for row in observations:
        modifier_names, status = equipment.match_auras(map(int, filter(None, row["LiveScfuNanoIds"].split(";"))))
        if len(modifier_names) != 1 or status != "AURA_CANDIDATE":
            raise ValueError(f"A reviewed observation lacks a unique matching Boss Modifier aura: {row['NpcIdentity']}")
        matched_modifiers.append(next(iter(modifier_names)))
    modifier_counts = Counter(matched_modifiers)
    equipment_pairs, band_keys = representative_equipment(bands, names, set(modifier_counts))
    pools = HashPools(item_records, qualities, flags)
    cases = []
    for row, modifier in zip(observations, matched_modifiers):
        level = int(row["NpcLevel"])
        spells = []
        for pair in equipment_pairs:
            name = names.get(pair[0])
            if name in equipment.modifier_names and name != modifier:
                continue
            source_pair = equipment.pair(pair)
            if source_pair:
                spells.extend((item_hash, center) for _, item_hash, center in equipment.functions_at(source_pair, level))
            elif spawn_args.get(pair[0]) or spawn_args.get(pair[1]):
                raise ValueError(f"Missing equipped SpawnItem evidence for {pair}")
        cases.append((spells, modifier, level))

    config = read_json(args.gameplay_config)
    variance = config["loot"]["qualityVariancePercent"]
    inputs = [metadata_dir / "item-metadata-source.json", item_path, args.gameplay_config, *npc_inputs]
    levels = [int(row["NpcLevel"]) for row in observations]
    report = {
        "SchemaVersion": 2,
        "Model": "Offline equipment-only simulation. Exact retail pool membership and roll weights are unverified.",
        "Sampling": "One case per reviewed capture, conditioned on its NPC level and matching aura. NPC template is a canonical equipment candidate.",
        "Seed": args.seed,
        "TrialsPerCase": args.trials,
        "VariancePercent": variance,
        "CaptureInputs": capture_inputs,
        "InputsSha256": {path.name if path.name != "Spawns.json" else f"Playfields/{path.parent.name}/Spawns.json": sha256(path)
                         for path in inputs},
        "Observed": observed_distribution(corpses, matched_modifiers, drops, qualities, variance),
        "ObservedNpcLevelRange": [min(levels), max(levels)],
        "ObservedModifierCounts": dict(modifier_counts),
        "CanonicalCandidateBands": [{"Hash": key, "Level": level} for key, level in band_keys],
        "SyntheticControl": {"Model": "Synthetic nested scaling, fixed-QL and clamped drops, independent of RK pool membership.",
                             **synthetic_control(args.trials, args.seed)},
    }
    before = simulate(cases, pools, variance, args.trials, args.seed, shared=True)
    if args.overlay:
        overlay = read_json(args.overlay)
        if not isinstance(overlay, dict) or not overlay or item_records.keys() & overlay.keys():
            raise ValueError("The replacement overlay must add keys to an unchanged base catalog")
        reconstructed = HashPools({**item_records, **overlay}, qualities, flags)
        after = simulate(cases, reconstructed, variance, args.trials, args.seed, shared=True)
        observed = set(report["Observed"]["PerEndpointPairSlots"])
        outside_observed = sorted(after["PerEndpointPairSlots"].keys() - observed)
        report["ReconstructionComparison"] = {
            "OverlaySha256": sha256(args.overlay),
            "BeforeSharedQl": before,
            "AfterReconstructed": after,
            "ObservedEndpointPairs": len(observed),
            "ObservedEndpointPairsSeenInBeforeTrials": len(observed & before["PerEndpointPairSlots"].keys()),
            "ObservedEndpointPairsSeenInAfterTrials": len(observed & after["PerEndpointPairSlots"].keys()),
            "AfterTrialEndpointPairsOutsideObserved": outside_observed,
            "AfterTrialEndpointPairsOutsideObservedSources": {pair: after["PerEndpointPairSources"][pair]
                                                               for pair in outside_observed},
        }
    else:
        report["CanonicalPools"] = {"AfterSharedQl": before,
                                    "BeforeIndependentQl": simulate(cases, pools, variance, args.trials, args.seed, shared=False)}
    partial = output.with_suffix(".json.partial")
    partial.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    partial.replace(output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gamedata-root", type=Path, default=Path("GameData"))
    parser.add_argument("--base-item-templates", type=Path, help="Original catalog for comparison after applying an overlay")
    parser.add_argument("--overlay", type=Path, help="Additive reconstructed item catalog definitions")
    parser.add_argument("--gameplay-config", type=Path, default=Path("AORebirth/Config/Gameplay.json"))
    parser.add_argument("--evidence-root", type=Path, required=True)
    parser.add_argument("--item-metadata", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--trials", type=int, default=128, help="Seeded trials per observed corpse")
    parser.add_argument("--seed", type=int, default=20261107)
    args = parser.parse_args()
    if args.trials < 1:
        parser.error("--trials must be positive")
    try:
        run(args)
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
