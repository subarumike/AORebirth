import csv
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest


TOOL = Path(__file__).resolve().parents[1] / "reconcile_rk_dyna_loot.py"
sys.path.insert(0, str(TOOL.parent))
SPEC = importlib.util.spec_from_file_location("reconcile_rk_dyna_loot", TOOL)
loot = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = loot
SPEC.loader.exec_module(loot)


def write_json(path, data):
    path.write_text(json.dumps(data), encoding="utf-8")


class ReconcileLootTests(unittest.TestCase):
    def test_nested_roots_keep_known_leaves_and_report_missing_or_cyclic_paths(self):
        pools = loot.HashPools({
            "ROOT": {"Children": ["SUB1", "SUB2", "LOST"]},
            "SUB1": {"Children": ["ITEM", "SUB1"]},
            "SUB2": {"Children": ["ITEM"]},
            "ITEM": {"Templates": [11, 12]},
        }, {11: 1, 12: 50}, {})
        leaves, issues = pools.inspect("ROOT")
        self.assertEqual([leaf.path for leaf in leaves], [("ROOT", "SUB1", "ITEM"), ("ROOT", "SUB2", "ITEM")])
        self.assertEqual(issues, ("cycle:ROOT/SUB1/SUB1", "missing:ROOT/LOST"))
        self.assertEqual(pools.highest("ROOT", 40), ((leaves[0], 40),))

    def test_highest_up_to_considers_all_branches_and_preserves_fixed_tier(self):
        pools = loot.HashPools({
            "ROOT": {"Children": ["GEAR", "FIXD", "SAME", "HIGH"]},
            "GEAR": {"Templates": [11, 12]}, "FIXD": {"Templates": [13]},
            "SAME": {"Templates": [14]}, "HIGH": {"Templates": [15]},
        }, {11: 10, 12: 30, 13: 40, 14: 40, 15: 60}, {})
        self.assertEqual(pools.highest("ROOT", 5), ())
        self.assertEqual([(leaf.ids_at(q), q) for leaf, q in pools.highest("ROOT", 39)], [((11, 12), 30)])
        self.assertEqual([(leaf.ids_at(q), q) for leaf, q in pools.highest("ROOT", 50)], [((13, 13), 40), ((14, 14), 40)])

    def test_selector_pair_is_distinct_from_upper_endpoint_wire_alternative(self):
        pools = loot.HashPools({"ROOT": {"Children": ["ITEM", "LOST"]}, "ITEM": {"Templates": [11, 12]}},
                               {11: 1, 12: 50}, {})
        source = {"hash": "ROOT", "mechanism": "equipment", "window": (45, 55)}
        eligible, member, missing = loot.slot_explanations(pools, [source], 11, 12, 50)
        self.assertEqual(eligible[0]["desiredQl"], [50, 51, 52, 53, 54, 55])
        self.assertEqual(member, [])
        self.assertEqual(missing, ["ROOT"])
        eligible, member, _ = loot.slot_explanations(pools, [source], 12, 12, 50)
        self.assertEqual(eligible, [])
        self.assertEqual(member[0]["qlStatus"], "UPPER_ENDPOINT_WIRE_ALTERNATIVE")

    def test_ordinary_item_table_keeps_special_and_ladder_window_rules(self):
        records = {"LADR": {"Children": ["A001", "A002", "A003"]}, "FIXD": {"Templates": [4]}}
        records.update({f"A00{i}": {"Templates": [i]} for i in range(1, 4)})
        pools = loot.HashPools(records, {1: 10, 2: 20, 3: 30, 4: 50}, {i: loot.SPECIAL_ITEM for i in range(1, 5)})
        a, b, c = pools.pool("LADR")
        self.assertFalse(pools.table_reaches(a, (21, 25), 10))
        self.assertTrue(pools.table_reaches(b, (21, 25), 20))
        self.assertFalse(pools.table_reaches(c, (21, 25), 30))
        self.assertTrue(pools.table_reaches(pools.pool("FIXD")[0], (21, 25), 50))

    def test_definition_proposal_requires_exact_source_record(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "source.json"
            manifest = root / "reviewed.json"
            definition = {"Templates": [11]}
            write_json(source, {"A/B": {"ROOT": definition}})
            row = {"hash": "ROOT", "definition": definition, "sourceFile": source.name,
                   "sourceSha256": loot.sha256(source), "sourceLocator": "/A~1B/ROOT"}
            write_json(manifest, [row])
            self.assertEqual(loot.apply_reviewed_definitions({}, manifest, {11: 1}, {})["ROOT"]["definition"], definition)
            write_json(manifest, [{**row, "definition": {"Templates": [12]}}])
            with self.assertRaisesRegex(ValueError, "exact evidence record"):
                loot.apply_reviewed_definitions({}, manifest, {11: 1, 12: 1}, {})

    def test_replay_deduplicates_exact_packet_slots_and_excludes_unclassified(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            batch, output, game_data = (root / p for p in ("capture", "output", "GameData"))
            metadata = batch / "item-metadata"
            metadata.mkdir(parents=True)
            game_data.mkdir()
            items_dat = game_data / "items.dat"
            items_dat.write_bytes(b"test fixture")
            write_json(game_data / "ItemTemplates.json", {"ROOT": {"Templates": [11, 12]}, "MODF": {"Templates": [100, 200]}})
            write_json(game_data / "NpcTemplates.json", {"NPC1": {"Templates": [
                {"Level": level, "Name": "Boss Monster", "Stats": {"359": 123}, "Equipment": [[100, 200]]}
                for level in (1, 100)
            ]}})
            rows = [{"Id": id_, "Quality": ql, "Name": "Example" if id_ == 11 else None, "Flags": 0}
                    for id_, ql in ((11, 1), (12, 100))]
            for id_, ql in ((100, 1), (200, 100)):
                rows.append({"Id": id_, "Quality": ql, "Name": "Boss Modifier Item (fixture)", "Flags": 0,
                             "FunctionDetails": [
                                 {"Event": "OnWear", "Function": loot.CAST_NANO, "IntegerArguments": [9]},
                                 {"Event": "OnWear", "Function": loot.SPAWN_ITEM, "IntegerArguments": [None, ql],
                                  "StringArguments": ["ROOT", None]},
                             ]})
            item_metadata = metadata / "item-metadata.jsonl"
            item_metadata.write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
            write_json(metadata / "item-metadata-source.json", {"SourceSha256": loot.sha256(items_dat),
                       "RecordCount": len(rows), "OutputSha256": loot.sha256(item_metadata)})
            corpse = dict(zip(loot.PACKET_KEY + loot.BOSS_FIELDS,
                             ("test", "2026-01-01T00:00:00Z", "32", "Corpse:00000001", "1", "25",
                              "SimpleChar:00000002", "Sample", "123", "50", "15", "0x01000000", "9", loot.BOSS_COHORT)))
            other = {**corpse, "InventorySequence": "33", "CorpseIdentity": "Corpse:00000003", "Cohort": "UNLINKED"}
            loot.write_csv(batch / "rk-corpse-inventories.csv", loot.PACKET_KEY + loot.BOSS_FIELDS + ("ItemSlots", "Descriptors"),
                           [{**corpse, "ItemSlots": "1", "Descriptors": "11:12:50:1"}, {**other, "ItemSlots": "0", "Descriptors": ""}])
            loot.write_csv(batch / "rk-dyna-item-observations.csv", loot.PACKET_KEY + loot.BOSS_FIELDS + loot.ITEM_FIELDS,
                           [{**corpse, "Slot": "3", "LowId": "11", "HighId": "12", "Ql": "50", "Count": "1"}])
            config = root / "Gameplay.json"
            write_json(config, {"loot": {"qualityVariancePercent": 25}})
            loot.main(["--capture-dir", str(batch), "--capture-dir", str(batch), "--game-data", str(game_data),
                       "--output-dir", str(output), "--gameplay-config", str(config)])
            summary = loot.read_json(output / "summary.json")
            self.assertEqual((summary["confirmedCorpses"], summary["confirmedItemSlots"], summary["unclassifiedInventories"]), (1, 1, 1))
            self.assertEqual(summary["slotStatuses"], {"COMPATIBLE_CANDIDATE_SOURCE": 1})
            with (output / "items.csv").open() as stream:
                self.assertEqual(next(csv.DictReader(stream))["Slot"], "3")


if __name__ == "__main__":
    unittest.main()
