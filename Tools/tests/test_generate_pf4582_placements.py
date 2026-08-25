from __future__ import annotations

import copy
import json
import sys
import unittest
from collections import Counter
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import generate_pf4582_placements as generator


class Pf4582PlacementImportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.raw_source = json.loads(
            generator.DEFAULT_SOURCE.read_text(encoding="utf-8")
        )
        cls.model = generator.build_model()
        cls.records = cls.model["records"]
        cls.records_by_id = {record["NpcId"]: record for record in cls.records}

    def test_imports_exact_authoritative_count_and_unique_npc_ids(self) -> None:
        self.assertEqual(206, len(self.records))
        self.assertEqual(206, len(self.records_by_id))
        self.assertEqual(
            sorted(record["NpcId"] for record in self.records),
            [record["NpcId"] for record in self.records],
        )

    def test_retains_every_template_hash(self) -> None:
        source_hashes = Counter(
            record["TemplateHash"]
            for record in self.raw_source[str(generator.PLAYFIELD_ID)]["Spawns"]
        )
        normalized_hashes = Counter(record["TemplateHash"] for record in self.records)
        self.assertEqual(source_hashes, normalized_hashes)
        self.assertEqual(38, len(normalized_hashes))

    def test_duplicate_positions_are_preserved_as_distinct_records(self) -> None:
        positions: dict[tuple[object, object, object], list[int]] = {}
        for record in self.records:
            position = record["Position"]
            key = (position["X"], position["Y"], position["Z"])
            positions.setdefault(key, []).append(record["NpcId"])
        duplicate_groups = [ids for ids in positions.values() if len(ids) > 1]
        self.assertEqual(5, len(duplicate_groups))
        self.assertEqual(14, sum(len(ids) for ids in duplicate_groups))
        self.assertEqual(9, sum(len(ids) - 1 for ids in duplicate_groups))
        self.assertEqual(206, sum(len(ids) for ids in positions.values()))

    def test_all_unknown_and_candidate_metadata_survives_normalization(self) -> None:
        preserved_fields = {
            "Name",
            "NpcId",
            "TemplateHash",
            "BossMods",
            "SpawnHash",
            "Position",
            "SpawnRadius",
            "SpawnAngle",
            "SpawnAngleW",
            "MinLevel",
            "MaxLevel",
            "SpawnChance",
            "ExtraData",
            "ExFlags",
            "SpawnTime",
            "SpawnUnknowns",
            "SpawnPointFlags",
        }
        raw_by_id = {
            record["NpcId"]: record
            for record in self.raw_source[str(generator.PLAYFIELD_ID)]["Spawns"]
        }
        for npc_id, normalized in self.records_by_id.items():
            raw = raw_by_id[npc_id]
            for field in preserved_fields:
                self.assertEqual(raw[field], normalized[field], (npc_id, field))

    def test_dynamic_source_names_remain_explicitly_unresolved(self) -> None:
        expected = {1007961, 1007963, 1007964, 1007965, 1007966, 1007967, 1008051}
        actual = {
            record["NpcId"]
            for record in self.records
            if record["SourceNameInterpretation"] == "UnresolvedDynamic"
        }
        self.assertEqual(expected, actual)
        self.assertTrue(all(not self.records_by_id[npc_id]["RuntimeActive"] for npc_id in expected))

    def test_only_explicit_existing_placements_are_runtime_eligible(self) -> None:
        eligible = [record for record in self.records if record["RuntimeEligible"]]
        blocked = [record for record in self.records if not record["RuntimeEligible"]]
        self.assertEqual(25, len(eligible))
        self.assertEqual(181, len(blocked))
        self.assertTrue(
            all(record["TemplateMapped"] and record["BehaviorProven"] for record in eligible)
        )
        self.assertTrue(all(record["RuntimeActive"] for record in eligible))

    def test_mapped_and_unresolved_template_hash_counts_are_explicit(self) -> None:
        mapped = {record["TemplateHash"] for record in self.records if record["TemplateMapped"]}
        unresolved = {
            record["TemplateHash"] for record in self.records if not record["TemplateMapped"]
        }
        self.assertEqual(14, len(mapped))
        self.assertEqual(24, len(unresolved))
        self.assertTrue(mapped.isdisjoint(unresolved))

    def test_existing_runtime_reconciliation_is_complete_and_bounded(self) -> None:
        self.assertEqual(25, len(self.model["existingMatches"]))
        self.assertEqual(
            25,
            len({match["npcId"] for match in self.model["existingMatches"]}),
        )
        self.assertLessEqual(
            max(match["positionDelta"] for match in self.model["existingMatches"]),
            5.0,
        )

    def test_island_reet_existing_combat_binding_is_unchanged(self) -> None:
        runtime_text = generator.DEFAULT_RUNTIME_SOURCE.read_text(encoding="utf-8")
        self.assertIn("SourceNpcId = 1007858", runtime_text)
        self.assertIn(
            "CombatContractFactory = IccShuttleportBasicCombatCatalog.IslandReet",
            runtime_text,
        )
        self.assertIn("CapturedEnemyCombatRuntime.PrepareAndRequireCombatReady(", runtime_text)

    def test_generation_is_byte_deterministic_and_artifacts_are_current(self) -> None:
        first_catalog = generator.render_catalog(self.model)
        second_catalog = generator.render_catalog(generator.build_model())
        first_report = generator.render_report(self.model)
        second_report = generator.render_report(generator.build_model())
        self.assertEqual(first_catalog, second_catalog)
        self.assertEqual(first_report, second_report)
        self.assertEqual(first_catalog, generator.DEFAULT_OUTPUT.read_text(encoding="utf-8"))
        self.assertEqual(first_report, generator.DEFAULT_REPORT.read_text(encoding="utf-8"))

    def test_report_preserves_required_invariants_and_exact_metrics(self) -> None:
        report = json.loads(generator.render_report(self.model))
        self.assertEqual(206, report["PF4582_SOURCE_PLACEMENTS"])
        self.assertEqual(206, report["PF4582_UNIQUE_NPC_IDS"])
        self.assertEqual(0, report["PF4582_DUPLICATE_NPC_IDS"])
        self.assertEqual(38, report["PF4582_UNIQUE_TEMPLATE_HASHES"])
        self.assertEqual(25, report["PF4582_EXISTING_MATCHED"])
        self.assertEqual(181, report["PF4582_NEW_PLACEMENTS"])
        self.assertEqual(14, report["PF4582_DUPLICATE_POSITION_RECORDS"])
        self.assertEqual(14, report["PF4582_TEMPLATE_HASHES_MAPPED"])
        self.assertEqual(24, report["PF4582_TEMPLATE_HASHES_UNRESOLVED"])
        self.assertEqual(25, report["PF4582_RUNTIME_ELIGIBLE"])
        self.assertEqual(181, report["PF4582_RUNTIME_BLOCKED"])
        self.assertEqual(206, report["PF4582_PLACEMENT_KNOWN"])
        self.assertEqual(25, report["PF4582_BEHAVIOR_PROVEN"])
        self.assertEqual(25, report["PF4582_RUNTIME_ACTIVE"])
        self.assertEqual(
            {
                "NO_HAND_TRANSCRIPTION": "YES",
                "DUPLICATE_POSITIONS_PRESERVED": "YES",
                "UNKNOWN_METADATA_PRESERVED": "YES",
                "UNPROVEN_BEHAVIOR_INVENTED": "NO",
                "UNPROVEN_SPAWNS_ACTIVATED": "NO",
            },
            report["invariants"],
        )

    def test_duplicate_npc_id_fails_closed(self) -> None:
        mutated = copy.deepcopy(self.raw_source)
        spawns = mutated[str(generator.PLAYFIELD_ID)]["Spawns"]
        spawns[1]["NpcId"] = spawns[0]["NpcId"]
        with self.assertRaisesRegex(generator.PlacementValidationError, "duplicate NpcId"):
            generator.validate_source(mutated)

    def test_missing_source_field_fails_closed(self) -> None:
        mutated = copy.deepcopy(self.raw_source)
        del mutated[str(generator.PLAYFIELD_ID)]["Spawns"][0]["SpawnUnknowns"]
        with self.assertRaisesRegex(generator.PlacementValidationError, "fields differ"):
            generator.validate_source(mutated)

    def test_malformed_unknown_metadata_fails_closed(self) -> None:
        mutated = copy.deepcopy(self.raw_source)
        mutated[str(generator.PLAYFIELD_ID)]["Spawns"][0]["SpawnUnknowns"] = [0, 0, 0]
        with self.assertRaisesRegex(
            generator.PlacementValidationError, "exactly four values"
        ):
            generator.validate_source(mutated)

    def test_source_digest_drift_fails_closed(self) -> None:
        evidence = json.loads(generator.DEFAULT_EVIDENCE_MAP.read_text(encoding="utf-8"))
        evidence["sourceSha256"] = "0" * 64
        with self.assertRaisesRegex(
            generator.PlacementValidationError, "source SHA-256 differs"
        ):
            generator.validate_evidence_map(
                evidence,
                self.model["sourceSha256"],
                self.records_by_id,
            )


if __name__ == "__main__":
    unittest.main()
