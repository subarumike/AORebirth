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


ISLAND_REET_NPC_IDS = {
    1007852,
    1007853,
    1007854,
    1007855,
    1007856,
    1007857,
    1007858,
    1007859,
    1007860,
    1007861,
    1007987,
}

EXPECTED_GENERATED_PROFILE_GROUPS = {
    "CIMA": ("Cliff Malle", "A035", 13),
    "TPSA": ("Tropical Stalker", "A033", 16),
    "ZIXI": ("Alien Spider - Zix", "A026", 26),
    "ACFJ": ("Scout - Jaax'Sinuh", "A002", 23),
    "LPAK": ("Shuttle Saboteur", "A014", 2),
    "GISK": ("Giant Snake", "A030", 10),
    "SRSK": ("Shore Snake", "A003", 3),
    "SORL": ("Stowaway Rollerrat", "A012", 5),
    "RFSL": ("Reef Salamander", "A034", 7),
    "CBSN": ("Climbing Salamander", "A013", 7),
    "WTCO": ("Waste collector", "A029", 10),
    "FDQO": ("Beach Leet", "A004", 9),
    "CADR": ("Cargo Droid", "A027", 10),
    "LLER": ("Rollerrat", "A012", 5),
    "CRJU": ("Cross-Wired Junkbot", "A009", 8),
    "CRDY": ("Specialist - Cha'Heru", "A016", 1),
    "SRLZ": ("Surf Lizard", "A000", 9),
}

BLOCKED_TEMPLATE_TAGS = {"BSMG", "BDML", "BEML", "BDMO", "BTMO", "BJMR", "BLMM"}


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

    def test_only_governed_placements_are_runtime_eligible(self) -> None:
        eligible = [record for record in self.records if record["RuntimeEligible"]]
        blocked = [record for record in self.records if not record["RuntimeEligible"]]
        self.assertEqual(199, len(eligible))
        self.assertEqual(7, len(blocked))
        self.assertTrue(
            all(record["TemplateMapped"] and record["BehaviorProven"] for record in eligible)
        )
        self.assertTrue(all(record["RuntimeActive"] for record in eligible))
        self.assertEqual(BLOCKED_TEMPLATE_TAGS, {record["TemplateTag"] for record in blocked})

    def test_mapped_and_unresolved_template_hash_counts_are_explicit(self) -> None:
        mapped = {record["TemplateHash"] for record in self.records if record["TemplateMapped"]}
        unresolved = {
            record["TemplateHash"] for record in self.records if not record["TemplateMapped"]
        }
        self.assertEqual(31, len(mapped))
        self.assertEqual(7, len(unresolved))
        self.assertTrue(mapped.isdisjoint(unresolved))

    def test_existing_runtime_reconciliation_is_complete_and_bounded(self) -> None:
        self.assertEqual(199, len(self.model["existingMatches"]))
        self.assertEqual(
            199,
            len({match["npcId"] for match in self.model["existingMatches"]}),
        )
        self.assertLessEqual(
            max(match["positionDelta"] for match in self.model["existingMatches"]),
            5.0,
        )

    def test_all_official_island_reets_reuse_the_proven_runtime_profile(self) -> None:
        runtime_text = generator.DEFAULT_RUNTIME_SOURCE.read_text(encoding="utf-8")
        reet_records = {
            record["NpcId"]: record
            for record in self.records
            if record["RuntimeProfile"] == "IccShuttleportSpawn:Island Reet"
        }
        self.assertEqual(ISLAND_REET_NPC_IDS, set(reet_records))
        self.assertTrue(
            all(
                record["TemplateTag"] == "ISRE"
                and record["BehaviorProven"]
                and record["RuntimeEligible"]
                and record["RuntimeActive"]
                for record in reet_records.values()
            )
        )
        for npc_id in ISLAND_REET_NPC_IDS:
            self.assertIn(f"SourceNpcId = {npc_id}", runtime_text)
        self.assertEqual(
            11,
            runtime_text.count(
                "CombatContractFactory = IccShuttleportBasicCombatCatalog.IslandReet"
            ),
        )
        self.assertIn("CapturedEnemyCombatRuntime.PrepareAndRequireCombatReady(", runtime_text)

    def test_generated_profile_groups_are_exact_and_have_no_combat_contracts(self) -> None:
        generated = self.model["generatedMappings"]
        self.assertEqual(164, len(generated))
        actual_groups = {}
        for template_hash, group in {
            mapping["templateHash"]: [
                candidate
                for candidate in generated
                if candidate["templateHash"] == mapping["templateHash"]
            ]
            for mapping in self.model["templateProfiles"]
        }.items():
            first = group[0]
            tag = self.records_by_id[first["npcId"]]["TemplateTag"]
            actual_groups[tag] = (
                first["sourceName"],
                first["mobTemplateHash"],
                len(group),
            )
            self.assertTrue(
                all(
                    candidate["sourceName"] == first["sourceName"]
                    and candidate["runtimeProfile"]
                    == f"IccShuttleportSpawn:{first['sourceName']}"
                    and candidate["mobTemplateHash"] == first["mobTemplateHash"]
                    and candidate["minimumLevel"]
                    == self.records_by_id[candidate["npcId"]]["MinLevel"]
                    and candidate["maximumLevel"]
                    == self.records_by_id[candidate["npcId"]]["MaxLevel"]
                    for candidate in group
                )
            )
        self.assertEqual(EXPECTED_GENERATED_PROFILE_GROUPS, actual_groups)
        population = generator.render_population_catalog(self.model)
        self.assertEqual(164, population.count("npcs.Add(CreateGeneratedProfileNpc("))
        self.assertNotIn("CombatContractFactory", population)

    def test_generation_is_byte_deterministic_and_artifacts_are_current(self) -> None:
        first_catalog = generator.render_catalog(self.model)
        second_catalog = generator.render_catalog(generator.build_model())
        first_population = generator.render_population_catalog(self.model)
        second_population = generator.render_population_catalog(generator.build_model())
        first_report = generator.render_report(self.model)
        second_report = generator.render_report(generator.build_model())
        self.assertEqual(first_catalog, second_catalog)
        self.assertEqual(first_population, second_population)
        self.assertEqual(first_report, second_report)
        self.assertEqual(first_catalog, generator.DEFAULT_OUTPUT.read_text(encoding="utf-8"))
        self.assertEqual(
            first_population,
            generator.DEFAULT_POPULATION_OUTPUT.read_text(encoding="utf-8"),
        )
        self.assertEqual(first_report, generator.DEFAULT_REPORT.read_text(encoding="utf-8"))

    def test_generated_population_catalog_is_compiled_by_zone_engine(self) -> None:
        project = (
            generator.REPOSITORY_ROOT / "AORebirth/Server/ZoneEngine/ZoneEngine.csproj"
        ).read_text(encoding="utf-8")
        compile_item = (
            '<Compile Include="Core\\Playfields\\'
            'IccShuttleportProfilePopulationCatalog.g.cs" />'
        )
        self.assertEqual(1, project.count(compile_item))
        self.assertTrue(generator.DEFAULT_POPULATION_OUTPUT.is_file())

    def test_report_preserves_required_invariants_and_exact_metrics(self) -> None:
        report = json.loads(generator.render_report(self.model))
        self.assertEqual(206, report["PF4582_SOURCE_PLACEMENTS"])
        self.assertEqual(206, report["PF4582_UNIQUE_NPC_IDS"])
        self.assertEqual(0, report["PF4582_DUPLICATE_NPC_IDS"])
        self.assertEqual(38, report["PF4582_UNIQUE_TEMPLATE_HASHES"])
        self.assertEqual(199, report["PF4582_EXISTING_MATCHED"])
        self.assertEqual(7, report["PF4582_NEW_PLACEMENTS"])
        self.assertEqual(14, report["PF4582_DUPLICATE_POSITION_RECORDS"])
        self.assertEqual(31, report["PF4582_TEMPLATE_HASHES_MAPPED"])
        self.assertEqual(7, report["PF4582_TEMPLATE_HASHES_UNRESOLVED"])
        self.assertEqual(199, report["PF4582_RUNTIME_ELIGIBLE"])
        self.assertEqual(7, report["PF4582_RUNTIME_BLOCKED"])
        self.assertEqual(35, report["PF4582_EXPLICIT_RUNTIME_ACTIVE"])
        self.assertEqual(164, report["PF4582_GENERATED_PROFILE_ACTIVE"])
        self.assertEqual(206, report["PF4582_PLACEMENT_KNOWN"])
        self.assertEqual(199, report["PF4582_BEHAVIOR_PROVEN"])
        self.assertEqual(199, report["PF4582_RUNTIME_ACTIVE"])
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
                json.loads(
                    generator.DEFAULT_TEMPLATE_PROFILE_AUTHORITY.read_text(
                        encoding="utf-8"
                    )
                ),
                generator.load_mobtemplate_profiles(generator.DEFAULT_MOBTEMPLATE_SOURCE),
            )


if __name__ == "__main__":
    unittest.main()
