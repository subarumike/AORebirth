from pathlib import Path
import sys
import unittest


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from simulate_rk_dyna_equipment_loot import HashPools, equipment_rules, roll_family, simulate, weighted_families


class Draw:
    def __init__(self, draw):
        self.draw = draw

    def randrange(self, total):
        if not 0 <= self.draw < total:
            raise AssertionError("Draw is outside the eligible weight range")
        return self.draw


class SimulateEquipmentLootTests(unittest.TestCase):
    def test_weighted_families_use_center_bounds_and_preserve_native_fixed_ql(self):
        pools = HashPools({
            "ROOT": {"Children": ["GEAR", "FIXD"], "EquipmentLoot": {
                "GEAR": {"Weight": 2, "MinCenter": 30, "MaxCenter": 60},
                "FIXD": {"Weight": 3, "MinCenter": 40, "MaxCenter": 55},
            }},
            "GEAR": {"Templates": [1, 2], "EquipmentLootOnly": True},
            "FIXD": {"Templates": [3], "EquipmentLootOnly": True},
        }, {1: 32, 2: 50, 3: 80}, {})
        rules = equipment_rules(pools, ["ROOT"])["ROOT"]
        self.assertEqual(weighted_families(rules, 61, 50), ())

        fixed = roll_family(weighted_families(rules, 40, 31), Draw(0))
        self.assertEqual((fixed[0].ids_at(fixed[1]), fixed[1]), ((3, 3), 80))

        both = weighted_families(rules, 55, 65)
        gear = roll_family(both, Draw(1))
        fixed = roll_family(both, Draw(2))
        self.assertEqual((gear[0].ids_at(gear[1]), gear[1]), ((1, 2), 50))
        self.assertEqual((fixed[0].ids_at(fixed[1]), fixed[1]), ((3, 3), 80))

    def test_reconstructed_scaling_roots_share_ql_but_legacy_fixed_root_is_unchanged(self):
        record = {"Templates": [1, 2], "EquipmentLootOnly": True}
        rules = {"Weight": 1, "MinCenter": 30, "MaxCenter": 50}
        pools = HashPools({
            "SRC1": {"Children": ["ARM1"], "EquipmentLoot": {"ARM1": rules}},
            "SRC2": {"Children": ["ARM2"], "EquipmentLoot": {"ARM2": rules}},
            "ARM1": record,
            "ARM2": record,
            "FIXD": {"Templates": [3]},
        }, {1: 1, 2: 100, 3: 20}, {})
        cases = [([("SRC1", 40), ("SRC2", 40), ("FIXD", 40)], "Synthetic", 40)]
        model = simulate(cases, pools, 25, 128, 17, shared=True)

        self.assertEqual(model["ItemCountHistogram"], {3: 128})
        self.assertEqual(model["UnclampedScalingQlAgreement"], {"AllScalingItemsAtSameQl": 128})
        self.assertEqual(model["PerHashItemQlHistogram"]["FIXD"], {20: 128})
        self.assertEqual(model["PerEndpointPairSlots"], {"1:2": 256, "3:3": 128})
        self.assertEqual(model["PerEndpointPairCorpses"], {"1:2": 128, "3:3": 128})


if __name__ == "__main__":
    unittest.main()
