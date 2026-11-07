"""Compare independent and shared equipment loot QL offline using reviewed RK dyna observations."""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
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


def simulate(cases, pools, variance, trials, seed):
    coverage = {item_hash: pools.pool(item_hash) for spells in cases for item_hash, _ in spells}
    scaling_hashes = {item_hash for item_hash, leaves in coverage.items()
                      if any(leaf.minimum < leaf.maximum for leaf in leaves)}
    results = {}
    for shared in (False, True):
        rng = Random(seed)
        counts = Counter()
        scaling_qls = Counter()
        results_by_hash = defaultdict(Counter)
        for spells in cases:
            for _ in range(trials):
                shared_rolls = {}
                reached_scaling = []
                count = 0
                for item_hash, center in spells:
                    if shared and item_hash in scaling_hashes and center in shared_rolls:
                        desired = shared_rolls[center]
                    else:
                        low, high = quality_window(center, variance)
                        desired = rng.randint(low, high)
                        if shared and item_hash in scaling_hashes:
                            shared_rolls[center] = desired
                    winners = pools.highest(item_hash, desired)
                    if not winners:
                        continue
                    # All tied leaves reach the same quality, so family tie-breaking cannot affect these metrics.
                    quality = winners[0][1]
                    if all(leaf.minimum < quality < leaf.maximum for leaf, _ in winners):
                        reached_scaling.append(quality)
                    results_by_hash[item_hash][quality] += 1
                    count += 1
                counts[count] += 1
                if len(reached_scaling) >= 2:
                    scaling_qls["AllScalingItemsAtSameQl" if len(set(reached_scaling)) == 1 else "MixedScalingQl"] += 1
        results["AfterSharedQl" if shared else "BeforeIndependentQl"] = {
            "ItemCountHistogram": dict(counts),
            "CorpsesWithMultipleUnclampedScalingDrops": sum(scaling_qls.values()),
            "UnclampedScalingQlAgreement": dict(scaling_qls),
            "PerHashItemQlHistogram": {key: dict(value) for key, value in results_by_hash.items()},
        }
    results["PoolCoverage"] = {
        key: {"KnownLeaves": len({leaf.path[-1] for leaf in leaves}),
              "HasScalingLeaf": key in scaling_hashes, "CatalogIssues": pools.inspect(key)[1]}
        for key, leaves in coverage.items()
    }
    return results


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
    return simulate([spells], pools, 25, trials, seed)


def observed_distribution(corpses, drops, qualities, variance):
    scaling_agreement = Counter()
    above_window = 0
    for corpse, observation in corpses.items():
        scaling_qls = []
        _, high = quality_window(int(observation["NpcLevel"]), variance)
        for row in drops[corpse]:
            low_id, high_id, quality = (int(row[key]) for key in ("LowId", "HighId", "Ql"))
            above_window += quality > high
            if qualities[low_id] < quality < qualities[high_id]:
                scaling_qls.append(quality)
        if len(scaling_qls) >= 2:
            scaling_agreement["AllScalingItemsAtSameQl" if len(set(scaling_qls)) == 1 else "MixedScalingQl"] += 1

    return {"Corpses": len(corpses), "ItemSlots": sum(len(drops[key]) for key in corpses),
            "ItemSlotsAboveLevelWindow": above_window,
            "ItemCountHistogram": dict(Counter(int(row["ItemSlots"]) for row in corpses.values())),
            "CorpsesWithMultipleUnclampedScalingDrops": sum(scaling_agreement.values()),
            "UnclampedScalingQlAgreement": dict(scaling_agreement)}


def run(args):
    evidence = args.evidence_root.resolve(strict=True)
    output = args.output.resolve()
    if output.parent != evidence or output.name != "rk-dyna-equipment-simulation.json":
        raise ValueError("Use rk-dyna-equipment-simulation.json directly in the private evidence root")
    data = args.gamedata_root.resolve(strict=True)
    if evidence.is_relative_to(data):
        raise ValueError("The evidence root must be outside canonical GameData")

    metadata_dir = args.item_metadata.resolve(strict=True)
    item_path = data / "ItemTemplates.json"
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
        cases.append(spells)

    config = read_json(args.gameplay_config)
    variance = config["loot"]["qualityVariancePercent"]
    inputs = [metadata_dir / "item-metadata-source.json", item_path, args.gameplay_config, *npc_inputs]
    levels = [int(row["NpcLevel"]) for row in observations]
    report = {
        "SchemaVersion": 1,
        "Model": "Offline equipment-only simulation. Exact retail pool membership and roll weights are unverified.",
        "Sampling": "One case per reviewed capture, conditioned on its NPC level and matching aura. NPC template is a canonical equipment candidate.",
        "Seed": args.seed,
        "TrialsPerCase": args.trials,
        "VariancePercent": variance,
        "CaptureInputs": capture_inputs,
        "InputsSha256": {path.name if path.name != "Spawns.json" else f"Playfields/{path.parent.name}/Spawns.json": sha256(path)
                         for path in inputs},
        "Observed": observed_distribution(corpses, drops, qualities, variance),
        "ObservedNpcLevelRange": [min(levels), max(levels)],
        "ObservedModifierCounts": dict(modifier_counts),
        "CanonicalCandidateBands": [{"Hash": key, "Level": level} for key, level in band_keys],
        "CanonicalPools": simulate(cases, pools, variance, args.trials, args.seed),
        "SyntheticControl": {"Model": "Synthetic nested scaling, fixed-QL and clamped drops, independent of RK pool membership.",
                             **synthetic_control(args.trials, args.seed)},
    }
    partial = output.with_suffix(".json.partial")
    partial.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    partial.replace(output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gamedata-root", type=Path, default=Path("GameData"))
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
