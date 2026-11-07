"""Propose private RK dyna equipment loot definitions from reconciled captures."""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from dataclasses import dataclass
from itertools import combinations
import json
from pathlib import Path

from reconcile_rk_dyna_loot import (
    BOSS_COHORT, ITEM_FIELDS, PACKET_KEY, add_unique, json_cell, key,
    load_item_metadata, provenance, read_csv, read_json, write_csv,
)


REPLACEMENT_ROOTS = frozenset(("AABC", "ARMO", "DIST", "INDI", "ITDC", "MELE", "OUDY"))
CONTROL_ROOTS = frozenset(("CLOH", "COOH"))
MAX_INT = 2**31 - 1


@dataclass(frozen=True, order=True)
class Source:
    role: int
    equipment: tuple
    position: int
    root: str
    center: int


def ordered_sources(boss):
    """All candidate NPC bindings must agree on the wear functions and their order."""
    variants = defaultdict(set)
    for row in json.loads(boss["LootSources"]):
        if row["mechanism"] != "equipment":
            continue
        name = row["equipmentName"]
        if not name.startswith(("Lootgiver", "Boss Modifier Item")):
            raise ValueError(f"Unexpected equipment loot source {name} for {key(boss)}")
        if row["hash"] not in REPLACEMENT_ROOTS | CONTROL_ROOTS:
            raise ValueError(f"Unsupported equipment loot hash {row['hash']} for {key(boss)}")
        center = int(row["center"])
        if not 1 <= center <= MAX_INT:
            raise ValueError(f"Invalid equipment loot center for {key(boss)}")
        source = Source(0 if name.startswith("Lootgiver") else 1, tuple(row["equipmentIds"]),
                        int(row["functionPosition"]), row["hash"], center)
        variants[row.get("npcHash")].add(source)
    profiles = {tuple(sorted(sources)) for sources in variants.values()}
    if len(profiles) != 1:
        return ()
    profile = profiles.pop()
    positions = {(source.role, source.equipment, source.position) for source in profile}
    equipment = {(source.role, source.equipment) for source in profile}
    if len(positions) != len(profile) or len(equipment) != 2 or {role for role, _ in equipment} != {0, 1}:
        return ()
    return profile


def observed_pair(row):
    return int(row["LowId"]), int(row["HighId"])


def known_control_roots(row):
    roots = set()
    for field in ("RuntimeEligibleSources", "KnownMembershipOnly"):
        for match in json.loads(row[field]):
            if match["mechanism"] == "equipment" and match["hash"] in CONTROL_ROOTS:
                roots.add(match["hash"])
    return roots


def collect_rows(boss_path, slot_path, qualities):
    bosses = {}
    slots = {}
    for boss in read_csv(boss_path):
        if boss["Cohort"] != BOSS_COHORT:
            raise ValueError(f"Unconfirmed boss in reconciliation input: {key(boss)}")
        add_unique(bosses, key(boss), boss, ("Cohort", "NpcIdentity", "NpcLevel", "ItemSlots", "LootSources"))
    for row in read_csv(slot_path):
        identity = key(row)
        boss = bosses.get(identity)
        if not boss or row["Cohort"] != BOSS_COHORT or row["NpcIdentity"] != boss["NpcIdentity"]:
            raise ValueError(f"Slot is not linked to a confirmed boss: {identity}")
        low, high = observed_pair(row)
        quality = int(row["Ql"])
        if (row["ItemIdsInItemsDat"] != "True" or not 0 < low <= MAX_INT or not 0 < high <= MAX_INT
                or not qualities.get(low, MAX_INT) <= quality <= qualities.get(high, 0)
                or int(row["Count"]) < 1):
            raise ValueError(f"Invalid observed template pair, QL or count: {identity}, slot {row['Slot']}")
        add_unique(slots, (*identity, int(row["Slot"])), row, ITEM_FIELDS)

    grouped = defaultdict(list)
    for identity, row in sorted(slots.items()):
        grouped[identity[:-1]].append(row)
    for identity, boss in bosses.items():
        indexes = [int(row["Slot"]) for row in grouped[identity]]
        if indexes != list(range(int(boss["ItemSlots"]))):
            raise ValueError(f"Slot indexes do not match the reconciled inventory: {identity}")
    return bosses, grouped


def assign_slots(bosses, grouped):
    assignments = []
    short = []
    supported = defaultdict(set)
    opportunities = defaultdict(list)
    for identity, boss in sorted(bosses.items()):
        profile = ordered_sources(boss)
        rows = grouped[identity]
        for source in profile:
            opportunities[source.root].append((identity, source.center))
        if profile and len(profile) == len(rows):
            matches = list(zip(rows, profile, strict=True))
            if all(not (controls := known_control_roots(row)) or source.root in controls for row, source in matches):
                for row, source in matches:
                    supported[observed_pair(row)].add(source.root)
                    assignments.append((row, (source,), "FULL_ORDERED_PROFILE"))
                continue
        short.append((profile, rows))

    # Short inventories never teach the model how to align other short inventories.
    for profile, rows in short:
        alignments = []
        if 0 < len(rows) < len(profile):
            for aligned in combinations(profile, len(rows)):
                if all(not (known := known_control_roots(row) or supported[observed_pair(row)])
                       or source.root in known for row, source in zip(rows, aligned, strict=True)):
                    alignments.append(aligned)
        for index, row in enumerate(rows):
            candidates = tuple(sorted({aligned[index] for aligned in alignments}))
            outcomes = {(source.root, source.center) for source in candidates}
            basis = "UNIQUE_SHORT_ALIGNMENT" if len(outcomes) == 1 else "UNRESOLVED_ALIGNMENT"
            assignments.append((row, candidates, basis))
    return sorted(assignments, key=lambda entry: (*key(entry[0]), int(entry[0]["Slot"]))), opportunities


def canonical_leaves(records, qualities):
    leaves = {}
    by_id = defaultdict(set)
    for name, entry in sorted(records.items()):
        templates = entry.get("Templates") or []
        if not templates or entry.get("Children") or entry.get("EquipmentLootOnly"):
            continue
        if any(template not in qualities for template in templates):
            continue
        bands = tuple(sorted(templates, key=qualities.__getitem__))
        if len({qualities[template] for template in bands}) != len(bands):
            continue
        leaves[name] = bands
        for template in bands:
            by_id[template].add(name)
    return leaves, by_id


def pair_at(templates, qualities, quality):
    if len(templates) == 1:
        return templates[0], templates[0]
    return next(((lo, hi) for lo, hi in zip(templates, templates[1:])
                 if qualities[lo] <= quality <= qualities[hi]), None)


def choose_canonical_leaf(names, leaves):
    """Multiple keys with identical ordered templates describe the same family."""
    if names and len({leaves[name] for name in names}) == 1:
        return min(names)
    return None


def add_alias(templates, records, leaves, helpers):
    name = f"RKDY_{templates[0]}_{templates[-1]}"
    definition = {"Templates": list(templates), "EquipmentLootOnly": True}
    existing = records[name] if name in records else helpers.get(name)
    if existing is not None and existing != definition:
        raise ValueError(f"Observed family collides with a different catalog record: {name}")
    helpers[name] = definition
    leaves[name] = templates
    return name


def combine_contiguous_aliases(assignments, selected, records, leaves, helpers, qualities):
    by_root = defaultdict(dict)
    for index, (family, _) in selected.items():
        if family in helpers:
            by_root[assignments[index][1][0].root].setdefault(family, []).append(index)
    for aliases in by_root.values():
        pending = set(aliases)
        while pending:
            first = min(pending)
            pending.remove(first)
            component = {first}
            ids = set(leaves[first])
            while connected := {family for family in pending if ids.intersection(leaves[family])}:
                pending.difference_update(connected)
                component.update(connected)
                ids.update(template for family in connected for template in leaves[family])
            if len(component) < 2:
                continue
            bands = tuple(sorted(ids, key=lambda template: (qualities[template], template)))
            if len({qualities[template] for template in bands}) != len(bands):
                continue
            indexes = sorted(index for family in component for index in aliases[family])
            rows = [assignments[index][0] for index in indexes]
            if any(pair_at(bands, qualities, int(row["Ql"])) != observed_pair(row) for row in rows):
                continue
            family = add_alias(bands, records, leaves, helpers)
            for index in indexes:
                selected[index] = family, "OBSERVED_CONTIGUOUS_WIRE_BANDS"


def resolve_families(assignments, records, qualities):
    leaves, by_id = canonical_leaves(records, qualities)
    helpers = {}
    selected = {}
    observed_scaling = defaultdict(set)

    def pick(row):
        low, high = observed_pair(row)
        quality = int(row["Ql"])
        names = {name for name in by_id[low] & by_id[high] if pair_at(leaves[name], qualities, quality) == (low, high)}
        name = choose_canonical_leaf(names, leaves)
        if name:
            return name, "CANONICAL_EXACT_PAIR"
        templates = (low,) if low == high else (low, high)
        return add_alias(templates, records, leaves, helpers), "OBSERVED_EXACT_PAIR_ALIAS"

    for index, (row, candidates, basis) in enumerate(assignments):
        if basis == "UNRESOLVED_ALIGNMENT" or not candidates or candidates[0].root not in REPLACEMENT_ROOTS:
            continue
        low, high = observed_pair(row)
        if low != high:
            family, relation = pick(row)
            selected[index] = (family, relation)
            for endpoint in (leaves[family][0], leaves[family][-1]):
                observed_scaling[candidates[0].root, endpoint].add(family)

    for index, (row, candidates, basis) in enumerate(assignments):
        if basis == "UNRESOLVED_ALIGNMENT" or not candidates or candidates[0].root not in REPLACEMENT_ROOTS:
            continue
        low, high = observed_pair(row)
        if low != high:
            continue
        family = choose_canonical_leaf(observed_scaling[candidates[0].root, high], leaves)
        if not family:
            endpoints = {name for name in by_id[high]
                         if len(leaves[name]) > 1 and leaves[name][-1] == high}
            family = choose_canonical_leaf(endpoints, leaves)
        selected[index] = (family, "FAMILY_QL_EQUIVALENCE") if family else pick(row)
    combine_contiguous_aliases(assignments, selected, records, leaves, helpers, qualities)
    in_use = {family for family, _ in selected.values()}
    helpers = {family: entry for family, entry in helpers.items() if family in in_use}
    return selected, leaves, helpers


def center_bounds(minimum, maximum, variance, observations):
    lower = (100 * minimum + 99 + variance) // (100 + variance)
    upper = (100 * (maximum + 1) - 1) // (100 - variance) if variance < 100 else MAX_INT
    return min(lower, min(observations)), min(MAX_INT, max(upper, max(observations)))


def build_outputs(assignments, opportunities, selected, leaves, helpers, records, qualities, variance, smoothed_roots=()):
    counts = Counter()
    support = defaultdict(list)
    matrix = []
    for index, (row, candidates, basis) in enumerate(assignments):
        family, relation = selected.get(index, ("", ""))
        source = candidates[0] if candidates and basis != "UNRESOLVED_ALIGNMENT" else None
        if family:
            support[source.root, family].append((key(row), source.center))
        elif source and source.root in CONTROL_ROOTS:
            relation = row["Status"]
        counts[basis, relation] += 1
        matrix.append({**{field: row[field] for field in (*PACKET_KEY, "NpcIdentity", "NpcName", "Playfield", "NpcLevel", *ITEM_FIELDS, "ItemName")},
                       "Basis": basis, "AssignedHash": source.root if source else "",
                       "FamilyHash": family, "FamilyRelation": relation,
                       "CandidateSources": json_cell([{"hash": s.root, "center": s.center, "equipmentIds": s.equipment,
                                                       "position": s.position} for s in candidates])})

    overlay = dict(helpers)
    weights = []
    children = defaultdict(dict)
    for (root, family), observations in sorted(support.items()):
        bands = leaves[family]
        minimum, maximum = qualities[bands[0]], qualities[bands[-1]]
        min_center, max_center = center_bounds(minimum, maximum, variance, [center for _, center in observations])
        smoothed = root in smoothed_roots
        weight = len(observations) + (1 if smoothed else 0)
        policy = {"Weight": weight, "MinCenter": min_center, "MaxCenter": max_center}
        children[root][family] = policy
        eligible = [(identity, center) for identity, center in opportunities[root] if min_center <= center <= max_center]
        weights.append({"Hash": root, "FamilyHash": family, "TemplateIds": json_cell(bands),
                        "MinQl": minimum, "MaxQl": maximum, **policy,
                        "SupportingSlots": len(observations), "SupportingCorpses": len({identity for identity, _ in observations}),
                        "CandidateRollsInCenterRange": len(eligible),
                        "CandidateCorpsesInCenterRange": len({identity for identity, _ in eligible}),
                        "WeightConfidence": "PROVISIONAL_ADD_ONE_SLOT_COUNT" if smoothed else "PROVISIONAL_OBSERVED_SLOT_COUNT"})
    for root in sorted(REPLACEMENT_ROOTS):
        installed = records.get(root, {}).get("EquipmentLoot")
        if not installed:
            continue
        for family, previous in installed.items():
            proposed = children[root].get(family)
            if (proposed is None or proposed["MinCenter"] > previous["MinCenter"]
                    or proposed["MaxCenter"] < previous["MaxCenter"]):
                raise ValueError(f"Proposal would lose installed evidence in {root}/{family}. Include all supporting batches.")
    for root, policy in sorted(children.items()):
        if root in records and not records[root].get("EquipmentLoot"):
            raise ValueError(f"Proposal would replace an existing non-reconstructed pool: {root}")
        overlay[root] = {"Children": list(policy), "EquipmentLoot": policy}
    return overlay, weights, matrix, counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reconciliation", type=Path, required=True)
    parser.add_argument("--item-metadata", type=Path, required=True)
    parser.add_argument("--game-data", type=Path, default=Path("GameData"))
    parser.add_argument("--item-templates", type=Path, help="Explicit ItemTemplates.json source for deriving the overlay")
    parser.add_argument("--gameplay", type=Path, default=Path("AORebirth/Config/Gameplay.json"))
    parser.add_argument("--smooth-root", action="append", choices=sorted(REPLACEMENT_ROOTS), default=[],
                        help="Add one to observed support weights in this reviewed root. Repeatable.")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    output = args.output.resolve()
    if output.is_relative_to(args.game_data.resolve()) or output.is_relative_to(Path("GameData").resolve()):
        raise ValueError("Reconstruction output must be outside canonical GameData")
    boss_path = args.reconciliation / "bosses.csv"
    slot_path = args.reconciliation / "items.csv"
    catalog = args.item_templates or args.game_data / "ItemTemplates.json"
    items_dat = args.game_data / "items.dat"
    variance = read_json(args.gameplay)["loot"]["qualityVariancePercent"]
    if type(variance) is not int or not 0 <= variance <= 100:
        raise ValueError("Invalid configured equipment loot variance")

    records = read_json(catalog)
    qualities, _, _, _ = load_item_metadata(args.item_metadata, items_dat, set())
    bosses, grouped = collect_rows(boss_path, slot_path, qualities)
    assignments, opportunities = assign_slots(bosses, grouped)
    selected, leaves, helpers = resolve_families(assignments, records, qualities)
    overlay, weights, matrix, counts = build_outputs(assignments, opportunities, selected, leaves, helpers,
                                                      records, qualities, variance, args.smooth_root)
    summary = {
        "confirmedCorpses": len(bosses), "observedSlots": len(assignments),
        "replacementRoots": {root: len(overlay.get(root, {}).get("Children", [])) for root in sorted(REPLACEMENT_ROOTS)},
        "equipmentOnlyLeaves": len(helpers), "configuredVariancePercent": variance,
        "smoothedWeightRoots": sorted(set(args.smooth_root)),
        "slotOutcomes": [{"basis": basis, "relation": relation, "slots": count}
                         for (basis, relation), count in sorted(counts.items())],
        "inputs": [provenance(path) for path in (boss_path, slot_path, catalog, args.gameplay,
                   args.item_metadata / "item-metadata-source.json")],
    }
    output.mkdir(parents=True, exist_ok=True)
    for name in ("reconstructed-item-templates.json", "reconstruction-summary.json", "membership-evidence.csv",
                 "ambiguous-memberships.csv", "provisional-weights.csv"):
        path = output / name
        if path.resolve() != path:
            raise ValueError(f"Refusing redirected reconstruction output: {path}")
    for name, data in (("reconstructed-item-templates.json", overlay), ("reconstruction-summary.json", summary)):
        (output / name).write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    if matrix:
        write_csv(output / "membership-evidence.csv", tuple(matrix[0]), matrix)
        write_csv(output / "ambiguous-memberships.csv", tuple(matrix[0]),
                  [row for row in matrix if row["Basis"] == "UNRESOLVED_ALIGNMENT"])
    if weights:
        write_csv(output / "provisional-weights.csv", tuple(weights[0]), weights)
    print(json_cell({key: value for key, value in summary.items() if key != "inputs"}))


if __name__ == "__main__":
    main()
