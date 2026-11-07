"""Audit reconstructed RK dyna weights against captured equipment opportunities offline."""

import argparse
from collections import Counter, defaultdict
from dataclasses import dataclass
from functools import cache
import json
import math
from pathlib import Path

from reconcile_rk_dyna_loot import (
    HashPools, key, load_item_metadata, provenance, quality_window, read_csv, read_json, write_csv,
)
from reconstruct_rk_dyna_loot import ordered_sources
from simulate_rk_dyna_equipment_loot import endpoint_key, equipment_rules, level_band, simulate, weighted_families


@dataclass(frozen=True)
class Choice:
    corpse: tuple
    playfield: str
    level: int
    root: str
    center: int
    desired: int | None
    family: str


def make_risks(rules, variance):
    @cache
    def risks(root, center, desired=None):
        low, high = quality_window(center, variance)
        values = (desired,) if desired is not None else range(low, high + 1)
        return tuple(tuple(leaf.path[-1] for leaf, _, _ in weighted_families(rules[root], center, value))
                     for value in values)

    return risks


def draw_exposure(states, weights, copies=1):
    """Expected slots and per-corpse incidence when repeated calls share one requested QL."""
    eligible_mass = Counter()
    expected_slots = Counter()
    expected_corpses = Counter()
    for eligible in states:
        total = sum(weights.get(family, 1) for family in eligible)
        for family in eligible:
            probability = weights.get(family, 1) / total
            eligible_mass[family] += copies / len(states)
            expected_slots[family] += copies * probability / len(states)
            expected_corpses[family] += (1 - (1 - probability) ** copies) / len(states)
    return eligible_mass, expected_slots, expected_corpses


def captured_choices(bosses, profiles, observed, assignments, qualities, rules, variance):
    witnessed = defaultdict(set)
    observed_slots = {}
    for row in observed:
        slot = (*key(row), int(row["Slot"]))
        low, high, quality = (int(row[name]) for name in ("LowId", "HighId", "Ql"))
        if slot in observed_slots or key(row) not in bosses:
            raise ValueError(f"Unexpected or repeated observed slot: {slot}")
        observed_slots[slot] = (low, high, quality, int(row["Count"]))
        if qualities[low] < quality < qualities[high]:
            witnessed[key(row)].add(quality)
    if any(len(values) != 1 for values in witnessed.values()):
        raise ValueError("Conflicting interior scaling QLs cannot condition this model")

    desired_by_corpse = {corpse: next(iter(values)) for corpse, values in witnessed.items()}
    choices = []
    for row in assignments:
        slot = (*key(row), int(row["Slot"]))
        descriptor = tuple(int(row[field]) for field in ("LowId", "HighId", "Ql", "Count"))
        if observed_slots.pop(slot, None) != descriptor:
            raise ValueError(f"Membership evidence does not match the observed slot: {slot}")
        family = row["FamilyHash"]
        if not family:
            continue
        corpse = key(row)
        root = row["AssignedHash"]
        candidates = {(source["hash"], int(source["center"])) for source in json.loads(row["CandidateSources"])}
        if len(candidates) != 1:
            raise ValueError(f"Assigned family lacks one root and center: {corpse}, slot {row['Slot']}")
        source_root, center = next(iter(candidates))
        if source_root != root or (root, center) not in profiles[corpse] or not any(leaf.path[-1] == family for leaf, *_ in rules[root]):
            raise ValueError(f"Assigned family does not match equipment and installed policy: {root}/{family}")

        desired = desired_by_corpse.get(corpse)
        if desired is not None:
            if len({value for _, value in profiles[corpse]}) != 1:
                raise ValueError(f"Cannot attribute one observed desired QL to multiple centers: {corpse}")
            low, high = quality_window(center, variance)
            if not low <= desired <= high:
                raise ValueError(f"Observed requested QL outside its modeled window: {corpse}")
        boss = bosses[corpse]
        choices.append(Choice(corpse, boss["Playfield"], int(boss["NpcLevel"]), root, center, desired, family))
    if observed_slots:
        raise ValueError("Membership evidence is missing observed slots")
    return choices, desired_by_corpse


def candidate_weight(count, mode):
    return 1 if mode == "neutral" else max(1, count) + 1


def predictive_folds(choices, risks, candidate_roots, mode):
    results = []
    for playfield in sorted({choice.playfield for choice in choices}):
        train = [choice for choice in choices if choice.playfield != playfield]
        test = [choice for choice in choices if choice.playfield == playfield]
        counts = defaultdict(Counter)
        uniform_expected = defaultdict(Counter)
        for choice in train:
            counts[choice.root][choice.family] += 1
            _, expected, _ = draw_exposure(risks(choice.root, choice.center, choice.desired), {})
            uniform_expected[choice.root].update(expected)

        @cache
        def policy_expected(root, center, desired, policy):
            states = risks(root, center, desired)
            if policy == "Neutral":
                weights = {}
            elif policy == "Candidate" and root in candidate_roots:
                weights = {family: candidate_weight(counts[root][family], mode)
                           for family in {value for state in states for value in state}}
            elif policy == "AddOnePrior":
                weights = {family: count + 1 for family, count in counts[root].items()}
            elif policy == "ExposureAdjusted":
                weights = {family: (counts[root][family] + 1) / (uniform_expected[root][family] + 1)
                           for family in {value for state in states for value in state}}
            else:
                weights = counts[root]
                weights = {family: max(1, count) for family, count in weights.items()}
            return draw_exposure(states, weights)[1]

        fold = defaultdict(lambda: {"Slots": 0, "SeenInTrainingSlots": 0, "LogLoss": Counter(),
                                    "SeenInTrainingLogLoss": Counter()})
        for choice in test:
            seen = counts[choice.root][choice.family] > 0
            row = fold[choice.root]
            row["Slots"] += 1
            row["SeenInTrainingSlots"] += seen
            for policy in ("TrainingCount", "Neutral", "AddOnePrior", "ExposureAdjusted", "Candidate"):
                expected = policy_expected(choice.root, choice.center, choice.desired, policy)
                probability = expected[choice.family]
                if probability <= 0:
                    raise ValueError(f"Observed choice is starved under {policy}: {choice}")
                loss = -math.log(probability)
                row["LogLoss"][policy] += loss
                if seen:
                    row["SeenInTrainingLogLoss"][policy] += loss
        results.extend({"Playfield": playfield, "Hash": root, **row} for root, row in sorted(fold.items()))
    return results


def opportunity_exposure(profiles, bosses, risks, weights, desired_by_corpse):
    fields = defaultdict(Counter)
    strata = Counter()
    for corpse, sources in profiles.items():
        absence = defaultdict(lambda: 1.0)
        band = level_band(int(bosses[corpse]["NpcLevel"]))
        for (root, center), copies in Counter(sources).items():
            if root not in weights:
                continue
            states = risks(root, center)
            strata[root, center, copies, len(states), sum(not state for state in states)] += 1
            mass, slots, corpses = draw_exposure(states, weights[root], copies)
            _, conditioned, _ = draw_exposure(risks(root, center, desired_by_corpse.get(corpse)), weights[root], copies)
            for family in mass:
                member = (root, family)
                fields["EligibleRollsAtAnyQl"][member] += copies
                fields["EligibleQlMass"][member] += mass[family]
                fields["ExpectedSlots"][member] += slots[family]
                fields["ExpectedSlotsByLevelBand"][root, family, band] += slots[family]
                fields["ExpectedSlotsConditionedOnKnownQl"][member] += conditioned[family]
                absence[member] *= 1 - corpses[family]
        for member, probability in absence.items():
            fields["ExpectedCorpses"][member] += 1 - probability
    return fields, strata


def family_rows(rules, choices, current, candidate, current_weights, candidate_weights):
    observed = defaultdict(list)
    for choice in choices:
        observed[choice.root, choice.family].append(choice)
    rows = []
    for root, members in sorted(rules.items()):
        for leaf, _, low, high in members:
            family = leaf.path[-1]
            member = (root, family)
            support = observed[member]
            rows.append({"Hash": root, "FamilyHash": family, "NativeQlMin": leaf.minimum, "NativeQlMax": leaf.maximum,
                         "MinCenter": low, "MaxCenter": high, "CurrentWeight": current_weights[root][family],
                         "CandidateWeight": candidate_weights[root][family], "ObservedSlots": len(support),
                         "ObservedCorpses": len({choice.corpse for choice in support}),
                         "ObservedPlayfields": len({choice.playfield for choice in support}),
                         "ObservedCenters": ";".join(map(str, sorted({choice.center for choice in support}))),
                         "ObservedBossLevels": ";".join(map(str, sorted({choice.level for choice in support}))),
                         **{field: round(current[field][member], 8) for field in ("EligibleRollsAtAnyQl", "EligibleQlMass")},
                         **{f"{policy}{field}": round(values[field][member], 8)
                            for policy, values in (("Current", current), ("Candidate", candidate))
                            for field in ("ExpectedSlots", "ExpectedSlotsConditionedOnKnownQl", "ExpectedCorpses")}})
    return rows


def support_by_level(families, models, roots):
    observed = {(row["Hash"], row["FamilyHash"]): row["ObservedSlots"] for row in families}
    result = Counter()
    for label, exposure in models.items():
        for (root, family, band), expected in exposure["ExpectedSlotsByLevelBand"].items():
            if root in roots:
                group = "Singleton" if observed[root, family] == 1 else "Repeated"
                result[root, band, group, label] += expected
    return [{"Hash": root, "LevelBand": band, "ObservedSupport": group, "Model": model,
             "ExpectedSlots": round(value, 8)} for (root, band, group, model), value in sorted(result.items())]


def simulated_family_types(families, simulations):
    by_family = {f"{row['Hash']}/{row['FamilyHash']}": row for row in families}
    result = defaultdict(Counter)
    for model, simulation in simulations.items():
        for family, count in simulation["PerHashFamilySlots"].items():
            row = by_family.get(family)
            kind = "Legacy" if row is None else "Fixed" if row["NativeQlMin"] == row["NativeQlMax"] else "Scaling"
            result[model][kind] += count
    return result


def run(args):
    evidence = args.evidence_root.resolve(strict=True)
    data = args.gamedata_root.resolve(strict=True)
    output = args.output.resolve()
    if output.parent != evidence or output.is_relative_to(data):
        raise ValueError("Use a private audit output directory directly under the evidence root")
    paths = [evidence / "reconciliation" / filename for filename in ("bosses.csv", "items.csv")]
    paths += [evidence / "reconstruction" / "membership-evidence.csv", args.item_templates or data / "ItemTemplates.json",
              args.gameplay_config, args.item_metadata / "item-metadata-source.json"]
    bosses = {key(row): row for row in read_csv(paths[0])}
    if not bosses:
        raise ValueError("No reviewed equipment profiles")
    profiles = {}
    cases = []
    configurations = Counter()
    for corpse, boss in sorted(bosses.items()):
        sources = ordered_sources(boss)
        if not sources:
            raise ValueError(f"Equipment profile is not uniquely usable: {corpse}")
        profile = tuple((source.root, source.center) for source in sources)
        profiles[corpse] = profile
        modifier = ":".join(map(str, next(source.equipment for source in sources if source.role == 1)))
        level = int(boss["NpcLevel"])
        cases.append((profile, modifier, level))
        configurations.update((root, center, level, modifier, boss["Playfield"]) for root, center in profile)

    records = read_json(paths[3])
    variance = read_json(paths[4])["loot"]["qualityVariancePercent"]
    qualities, flags, _, _ = load_item_metadata(args.item_metadata, data / "items.dat", set())
    pools = HashPools(records, qualities, flags)
    rules = equipment_rules(pools, {root for profile in profiles.values() for root, _ in profile})
    candidate_roots = set(args.candidate_root)
    if not candidate_roots <= rules.keys():
        raise ValueError("Each candidate root must have an installed reconstruction policy")
    risks = make_risks(rules, variance)
    observed = read_csv(paths[1])
    choices, desired = captured_choices(bosses, profiles, observed, read_csv(paths[2]), qualities, rules, variance)
    if not choices:
        raise ValueError("No reviewed family choices")

    current_weights = {root: {leaf.path[-1]: weight for leaf, weight, _, _ in members}
                       for root, members in rules.items()}
    candidate_weights = {root: {family: candidate_weight(weight, args.candidate_weight) if root in candidate_roots else weight
                                for family, weight in members.items()}
                         for root, members in current_weights.items()}
    candidate_records = dict(records)
    for root in sorted(candidate_roots):
        record = records[root]
        candidate_records[root] = {**record, "EquipmentLoot": {family: {**rule, "Weight": candidate_weights[root][family]}
                                                              for family, rule in record["EquipmentLoot"].items()}}

    current, strata = opportunity_exposure(profiles, bosses, risks, current_weights, desired)
    candidate, _ = opportunity_exposure(profiles, bosses, risks, candidate_weights, desired)
    families = family_rows(rules, choices, current, candidate, current_weights, candidate_weights)
    comparison = predictive_folds(choices, risks, candidate_roots, args.candidate_weight)
    variants = {"Current": pools, "Candidate": HashPools(candidate_records, qualities, flags)}
    simulations = {variant: simulate(cases, variant_pools, variance, args.trials, args.seed, shared=True)
                   for variant, variant_pools in variants.items()}
    observed_pairs = {endpoint_key(row["LowId"], row["HighId"]) for row in observed}
    stats = {
        "Corpses": len(bosses), "ObservedMappedSlots": len(choices), "CorpsesWithKnownDesiredQl": len(desired),
        "MappedSlotsWithKnownDesiredQl": sum(choice.desired is not None for choice in choices),
        "FamilyMemberships": len(families), "CandidateRoots": sorted(candidate_roots), "CandidateWeight": args.candidate_weight,
        "ObservedSupportHistogram": Counter(row["ObservedSlots"] for row in families),
        "ZeroEligibleFamilies": sum(not row["EligibleRollsAtAnyQl"] for row in families),
        "FamiliesEligibleOnAtMostThreeRolls": sum(row["EligibleRollsAtAnyQl"] <= 3 for row in families),
        "FamiliesWithCurrentExpectedSlotsBelowHalf": sum(row["CurrentExpectedSlots"] < 0.5 for row in families),
        "NominalEquipmentRolls": sum(configurations.values()),
        "NominalReconstructedRolls": sum(count for (root, *_), count in configurations.items() if root in rules),
        "OpportunityGroupsWithAnyEmptyQlState": sum(count for (*_, empty), count in strata.items() if empty),
        "PredictionScope": "Memberships and QL restrictions fixed using all evidence. Weights trained outside each held-out playfield. The same folds informed the candidate roots, so this is model selection on this corpus.",
        "CandidateScope": "Comparison only. Positive candidate weights leave family and QL eligibility unchanged.",
        "VariancePercent": variance, "TrialsPerCorpse": args.trials, "Seed": args.seed,
        "Inputs": [provenance(path) for path in paths], "BlockedPrediction": comparison, "Simulations": simulations,
        "ExpectedSupportByLevelBand": support_by_level(families, {"Current": current, "Candidate": candidate}, candidate_roots),
        "SimulatedFamilyTypeSlots": simulated_family_types(families, simulations),
        "ObservedEndpointPairs": len(observed_pairs),
        "ObservedEndpointPairsSeenInTrials": {model: len(observed_pairs & simulation["PerEndpointPairSlots"].keys())
                                              for model, simulation in simulations.items()},
    }
    output.mkdir(exist_ok=True)
    write_csv(output / "family-exposure.csv", tuple(families[0]), families)
    opportunities = []
    for (root, center, level, modifier, playfield), count in sorted(configurations.items()):
        low, high = quality_window(center, variance)
        opportunities.append({"Hash": root, "Center": center, "BossLevel": level, "ModifierEndpoints": modifier,
                              "RequestedQlMin": low, "RequestedQlMax": high, "Playfield": playfield,
                              "RollOpportunities": count})
    write_csv(output / "equipment-opportunities.csv", tuple(opportunities[0]), opportunities)
    partial = output / "weight-audit.json.partial"
    partial.write_text(json.dumps(stats, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    partial.replace(output / "weight-audit.json")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gamedata-root", type=Path, default=Path("GameData"))
    parser.add_argument("--item-templates", type=Path, help="Use a saved installed catalog for the current model")
    parser.add_argument("--gameplay-config", type=Path, default=Path("AORebirth/Config/Gameplay.json"))
    parser.add_argument("--evidence-root", type=Path, required=True)
    parser.add_argument("--item-metadata", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--candidate-root", action="append", required=True)
    parser.add_argument("--candidate-weight", choices=("neutral", "count-plus-one"), required=True)
    parser.add_argument("--trials", type=int, default=128)
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
