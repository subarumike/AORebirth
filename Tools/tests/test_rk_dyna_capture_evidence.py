import csv
import json
from pathlib import Path
import sys
import tempfile
import unittest


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import reconcile_rk_dyna_loot as loot
import rk_dyna_capture_evidence as evidence


class CaptureEvidenceTests(unittest.TestCase):
    def test_project_identity_and_sparse_wire_slot_without_promoting_unlinked_corpse(self):
        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp)
            (folder / "capture_info.json").write_text('{"sessionPlayfieldId":585}', encoding="utf-8")
            rows = {
                "corpse-loot-observations.csv": [
                    {"Direction": "IN", "InitialSnapshot": "true", "Sequence": "30", "CapturedUtc": "T1",
                     "CorpseIdentity": "Corpse:0010", "DeadNpcIdentity": "(SimpleChar:20)", "PlayfieldId": "585",
                     "EnemyName": "NPC", "MonsterData": "3", "EnemyLevel": "25", "Items": "100:200:26:1", "ItemCount": "1"},
                    {"Direction": "IN", "InitialSnapshot": "true", "Sequence": "40", "CapturedUtc": "T2",
                     "CorpseIdentity": "Corpse:0011", "DeadNpcIdentity": "", "PlayfieldId": "", "EnemyName": "",
                     "MonsterData": "", "EnemyLevel": "", "Items": "", "ItemCount": "0"},
                ],
                "corpse-full-updates.csv": [
                    {"Direction": "IN", "Sequence": "20", "CorpseIdentity": "(Corpse:00000010)",
                     "DeadNpcIdentity": "(SimpleChar:00000020)", "PlayfieldId": "585"},
                ],
                "scfu-appearance.csv": [
                    {"Direction": "IN", "Sequence": "10", "DecodeStatus": "decoded_complete", "DecodeFullyConsumed": "true",
                     "Identity": "(SimpleChar:00000020)", "FlagsNumeric": str(evidence.RK_DYNA_WIRE_BIT),
                     "ActiveNanos": "(53019:A):0:10000:10000", "Name": "NPC", "MonsterData": "3", "Level": "25"},
                ],
                "inventory-updates.csv": [
                    {"Direction": "IN", "Sequence": "30", "InventoryIdentity": "Corpse:10", "Slot": "4",
                     "LowId": "100", "HighId": "200", "Quality": "26", "Count": "1"},
                ],
            }
            for name, entries in rows.items():
                loot.write_csv(folder / name, entries[0], entries)
            classifier = lambda auras: ({"fixture"} if auras == {10} else set(), "AURA")
            corpses, items, _ = evidence.project_session(folder, classifier)
            self.assertEqual([row["Cohort"] for row in corpses], [loot.BOSS_COHORT, "UNLINKED"])
            self.assertEqual(corpses[1]["Playfield"], "585")
            self.assertEqual((items[0]["Slot"], items[0]["LowId"], items[0]["HighId"]), ("4", "100", "200"))
            unverified, unverified_items, _ = evidence.project_session(folder, lambda _: (set(), "NONE"))
            self.assertEqual(unverified[0]["Cohort"], "UNVERIFIED_MODIFIER_AURA")
            self.assertEqual(unverified_items, [])

    def test_frequency_separates_slots_from_corpses_and_conditional_weight(self):
        pools = loot.HashPools({}, {11: 1, 12: 50}, {})
        sources = [{"hash": "ABCD", "mechanism": "equipment", "equipmentIds": [2, 3],
                    "functionPosition": 1, "window": [1, 40]}]
        first = {"Session": "A", "CapturedUtc": "T1", "InventorySequence": "2", "CorpseIdentity": "Corpse:1"}
        second = {**first, "CapturedUtc": "T2", "InventorySequence": "3", "CorpseIdentity": "Corpse:2"}
        bosses = [{**key, "LootSources": json.dumps(sources)} for key in (first, second)]
        item = {**first, "NpcIdentity": "NPC:1", "NpcLevel": "22", "Playfield": "585", "LowId": "11", "HighId": "12",
                "Ql": "21", "RuntimeEligibleSources": "[]", "KnownMembershipOnly": "[]"}
        catalog = {(11, 12): {"FamilyKey": "wire:11:12", "FamilyEvidence": "RDB_WIRE_PAIR_ONLY",
                             "Endpoints": '[{"quality":1},{"quality":50}]'}}
        occurrences, frequencies, membership = evidence.occurrence_evidence(bosses, [{**item, "Slot": slot} for slot in ("0", "1")], catalog, pools)
        self.assertEqual(len(occurrences), 2)
        self.assertEqual(frequencies[0]["ObservedSlots"], 2)
        self.assertEqual(frequencies[0]["ObservedCorpses"], 1)
        self.assertEqual(frequencies[0]["ObservedCorpseFrequency"], "0.50000000")
        self.assertEqual(frequencies[0]["SlotShare"], "1.00000000")
        self.assertEqual(frequencies[0]["ConditionalWeight"], "")
        self.assertEqual(membership[0]["EvidenceClass"], "UNIQUE_COMMON_CANDIDATE")
        self.assertEqual(membership[0]["AssignmentStatus"], "CANDIDATE_ONLY")

    def test_endpoint_equivalence_preserves_original_wire_pair(self):
        pools = loot.HashPools({"ROOT": {"Children": ["LEAF"]}, "LEAF": {"Templates": [11, 12]}},
                               {11: 1, 12: 50}, {})
        item = {"Session": "A", "CapturedUtc": "T1", "InventorySequence": "2", "CorpseIdentity": "Corpse:1",
                "Slot": "0", "LowId": "12", "HighId": "12", "Ql": "50", "Count": "1", "ItemFamilies": "LEAF",
                "KnownMembershipOnly": '[{"hash":"ROOT","path":["ROOT","LEAF"],"qlStatus":"UPPER_ENDPOINT_WIRE_ALTERNATIVE"}]'}
        catalog, _, discrepancies = evidence.corpus_item_catalog([item], pools, {12: {"Id": 12, "Quality": 50}}, {})
        self.assertEqual(catalog[0]["FamilyKey"], "wire:12:12")
        self.assertEqual(catalog[0]["FamilyEvidence"], "CANONICAL_UPPER_ENDPOINT_ALTERNATIVE")
        self.assertEqual((discrepancies[0]["WireLowId"], discrepancies[0]["SelectorLowId"]), (12, 11))
        self.assertTrue(discrepancies[0]["EndpointTemplateVerified"])

    def test_reconstructed_rule_is_scoped_and_preserves_fixed_ql_and_scaling_clamp(self):
        records = {"ROOT": {"Children": ["FIXD", "SCAL"], "EquipmentLoot": {
            "FIXD": {"Weight": 1, "MinCenter": 20, "MaxCenter": 30},
            "SCAL": {"Weight": 2, "MinCenter": 20, "MaxCenter": 30}}},
            "FIXD": {"EquipmentLootOnly": True, "Templates": [11]}, "SCAL": {"Templates": [12, 13]}}
        pools = loot.HashPools(records, {11: 80, 12: 10, 13: 29}, {})
        source = {"hash": "ROOT", "mechanism": "equipment", "center": 25, "window": (20, 32)}
        self.assertEqual(pools.pool("ROOT"), ())
        self.assertEqual(pools.pool("FIXD"), ())
        self.assertEqual(loot.slot_explanations(pools, [source], 11, 11, 80)[0][0]["qlStatus"], "ELIGIBLE_RECONSTRUCTED_FIXED")
        self.assertEqual(loot.slot_explanations(pools, [source], 12, 13, 29)[0][0]["desiredQl"], [29, 30, 31, 32])
        self.assertEqual(loot.slot_explanations(pools, [{**source, "center": 19}], 11, 11, 80)[0], [])
        self.assertEqual(loot.slot_explanations(pools, [{**source, "mechanism": "item-table"}], 11, 11, 80)[0], [])

    def test_reconstructed_endpoint_equivalence_requires_same_id_at_ql_and_reachable_policy(self):
        pools = loot.HashPools({
            "ROOT": {"Children": ["SCAL"], "EquipmentLoot": {"SCAL": {"Weight": 1, "MinCenter": 20, "MaxCenter": 30}}},
            "SCAL": {"Templates": [11, 12, 13], "EquipmentLootOnly": True},
        }, {11: 10, 12: 20, 13: 30}, {})
        source = {"hash": "ROOT", "mechanism": "equipment", "center": 25, "window": (20, 31)}
        exact, members, _ = loot.slot_explanations(pools, [source], 12, 12, 20)
        self.assertEqual(exact, [])
        self.assertEqual(members[0]["qlStatus"], "RECONSTRUCTED_FAMILY_QL_EQUIVALENCE")
        self.assertEqual(members[0]["desiredQl"], [20])
        for candidate, template, ql, status in (
            (source, 12, 21, "ENDPOINT_PAIR_NOT_SELECTED_AT_QL"),
            (source, 13, 20, "ENDPOINT_PAIR_NOT_SELECTED_AT_QL"),
            ({**source, "center": 19}, 12, 20, "OUTSIDE_RECONSTRUCTED_CENTER_WINDOW"),
            ({**source, "window": (21, 31)}, 12, 20, "RECONSTRUCTED_SCALING_QL_CONFLICT"),
        ):
            exact, members, _ = loot.slot_explanations(pools, [candidate], template, template, ql)
            self.assertEqual(exact, [])
            self.assertEqual(members[0]["qlStatus"], status)


if __name__ == "__main__":
    unittest.main()
