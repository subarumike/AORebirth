"""Focused checks for capture-based loot pool proposals."""

import json
from pathlib import Path
import sys
import unittest


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from reconstruct_rk_dyna_loot import assign_slots, center_bounds, resolve_families  # noqa: E402


def boss(session):
    def source(name, equipment, root, position):
        return {"mechanism": "equipment", "equipmentName": name, "equipmentIds": equipment,
                "hash": root, "functionPosition": position, "center": 100, "npcHash": "EXAMPLE"}

    return {"Session": session, "CapturedUtc": "capture-time", "InventorySequence": "5", "CorpseIdentity": "7",
            "LootSources": json.dumps([source("Boss Modifier Item", [4, 5], "ARMO", 1),
                                       source("Boss Modifier Item", [4, 5], "DIST", 2),
                                       source("Lootgiver", [1, 2], "OUDY", 1)])}


def slots(boss_row, pairs):
    return [{**boss_row, "Slot": str(index), "LowId": str(low), "HighId": str(high),
             "Ql": str(quality), "RuntimeEligibleSources": "[]", "KnownMembershipOnly": "[]"}
            for index, (low, high, quality) in enumerate(pairs)]


class ReconstructionTests(unittest.TestCase):
    def test_full_inventory_uses_lootgiver_then_modifier_function_order(self):
        record = boss("complete")
        rows = slots(record, [(10, 11, 100), (20, 21, 100), (30, 31, 100)])
        assignments, _ = assign_slots({("complete",): record}, {("complete",): rows})
        self.assertEqual([sources[0].root for _, sources, _ in assignments], ["OUDY", "ARMO", "DIST"])

    def test_short_inventory_needs_independent_full_profile_support(self):
        complete = boss("complete")
        partial = boss("partial")
        rows = {("complete",): slots(complete, [(10, 11, 100), (20, 21, 100), (30, 31, 100)]),
                ("partial",): slots(partial, [(10, 11, 100), (40, 41, 100)])}
        assignments, _ = assign_slots({("complete",): complete, ("partial",): partial}, rows)
        _, first_candidates, first_basis = assignments[3]
        _, second_candidates, second_basis = assignments[4]
        self.assertEqual((first_candidates[0].root, first_basis), ("OUDY", "UNIQUE_SHORT_ALIGNMENT"))
        self.assertEqual({candidate.root for candidate in second_candidates}, {"ARMO", "DIST"})
        self.assertEqual(second_basis, "UNRESOLVED_ALIGNMENT")

    def test_capped_existing_family_and_contiguous_captured_pairs_each_count_once(self):
        record = boss("complete")
        rows = slots(record, [(10, 11, 50), (11, 12, 150), (20, 21, 100), (21, 21, 200)])
        source_assignments, _ = assign_slots({("complete",): record}, {("complete",): rows[:3]})
        lootgiver = source_assignments[0][1]
        modifier_armor = source_assignments[1][1]
        assignments = [(rows[0], lootgiver, "FULL_ORDERED_PROFILE"), (rows[1], lootgiver, "FULL_ORDERED_PROFILE"),
                       (rows[2], modifier_armor, "FULL_ORDERED_PROFILE"),
                       (rows[3], modifier_armor, "FULL_ORDERED_PROFILE")]
        qualities = {10: 1, 11: 100, 12: 200, 20: 1, 21: 200}
        selected, _, helpers = resolve_families(assignments, {"EXST": {"Templates": [20, 21]}}, qualities)
        self.assertEqual(helpers, {"RKDY_10_12": {"Templates": [10, 11, 12], "EquipmentLootOnly": True}})
        self.assertEqual(selected[0], selected[1])
        self.assertEqual(selected[3], ("EXST", "FAMILY_QL_EQUIVALENCE"))

    def test_center_range_keeps_rounded_bounds_and_observed_fixed_exceptions(self):
        self.assertEqual(center_bounds(150, 200, 25, [195]), (120, 267))
        self.assertEqual(center_bounds(20, 20, 25, [8, 183]), (8, 183))


if __name__ == "__main__":
    unittest.main()
