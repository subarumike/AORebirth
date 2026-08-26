from __future__ import annotations

import hashlib
import io
import json
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import aorebirth_playfield_reconciliation as reconciliation


class AoRebirthPlayfieldReconciliationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.official_index_path = self.root / "official-placement-index.json"
        self.manifest_path = self.root / "representation-manifest.json"
        self.output_path = self.root / "official-playfield-reconciliation.json"
        self._write_fixture_sources()
        self._write_json(self.official_index_path, self._official_index())
        self._write_json(self.manifest_path, self._manifest())

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    @staticmethod
    def _write_json(path: Path, value: object) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")

    def _write_fixture_sources(self) -> None:
        files = {
            "Playfields.xml": (
                '<Playfields><Playfield id="200" name="Metadata Only" />'
                '<Playfield id="4582" name="ICC Shuttleport" /></Playfields>'
            ),
            "ZoneEngine.csproj": (
                "Core/Playfields/Content/IccShuttleportContentModule.cs\n"
                "Core/Playfields/Content/FakeContentModule.cs\n"
                "Core/Playfields/Content/MissionInstanceContentModule.cs\n"
            ),
            "LinuxBuild/ZoneEngine.CompileItems.props": (
                "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
                "IccShuttleportContentModule.cs\n"
                "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
                "FakeContentModule.cs\n"
                "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
                "MissionInstanceContentModule.cs\n"
            ),
            "registration.cs": (
                "new IccShuttleportContentModule();\n"
                "new FakeContentModule();\n"
                "new MissionInstanceContentModule();\n"
            ),
            "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
            "IccShuttleportContentModule.cs": "sealed class IccShuttleportContentModule {}\n",
            "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
            "FakeContentModule.cs": "sealed class FakeContentModule {}\n",
            "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
            "MissionInstanceContentModule.cs": "sealed class MissionInstanceContentModule {}\n",
            "MissionInstanceService.cs": "bool Supports(int id) => id == 1500000;\n",
            "test/pf4582-exact-identity-bridge.json": "{}\n",
            "docs/generated/playfields/placements/pf_103.json": "{\"pf\":103}\n",
            "docs/generated/playfields/placements/pf_300.json": "{\"pf\":300}\n",
            "docs/generated/playfields/placements/pf_4582.json": "{\"pf\":4582}\n",
        }
        for relative_path, content in files.items():
            path = self.root / relative_path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8")

    def _official_index(self) -> dict[str, object]:
        def shard_sha(playfield_id: int) -> str:
            path = (
                self.root
                / "docs"
                / "generated"
                / "playfields"
                / "placements"
                / f"pf_{playfield_id}.json"
            )
            return hashlib.sha256(path.read_bytes()).hexdigest()

        return {
            "SchemaVersion": 1,
            "SourceClientVariant": "TEST_CLIENT",
            "SourceClientBuild": "TEST_BUILD",
            "ResourceType": 1000014,
            "SourceManifestSha256": "0" * 64,
            "Playfields": [
                {
                    "PlayfieldId": 103,
                    "ResourceInstance": 103,
                    "FormatVersion": None,
                    "ParseStatus": "MALFORMED_FOR_CURRENT_EXTRACTOR",
                    "DistrictCount": None,
                    "OfficialSpawnCount": None,
                    "Path": "docs/generated/playfields/placements/pf_103.json",
                    "Sha256": shard_sha(103),
                },
                {
                    "PlayfieldId": 300,
                    "ResourceInstance": 300,
                    "FormatVersion": 1,
                    "ParseStatus": "PARSED",
                    "DistrictCount": 1,
                    "OfficialSpawnCount": 5,
                    "Path": "docs/generated/playfields/placements/pf_300.json",
                    "Sha256": shard_sha(300),
                },
                {
                    "PlayfieldId": 4582,
                    "ResourceInstance": 4582,
                    "FormatVersion": 1,
                    "ParseStatus": "PARSED",
                    "DistrictCount": 2,
                    "OfficialSpawnCount": 207,
                    "Path": "docs/generated/playfields/placements/pf_4582.json",
                    "Sha256": shard_sha(4582),
                },
            ],
        }

    @staticmethod
    def _manifest() -> dict[str, object]:
        return {
            "SchemaVersion": 1,
            "Description": "Test-only governed representation evidence.",
            "OfficialIndexExpectations": {
                "SchemaVersion": 1,
                "SourceClientVariant": "TEST_CLIENT",
                "SourceClientBuild": "TEST_BUILD",
                "ResourceType": 1000014,
                "ResourceCount": 3,
                "ParsedResourceCount": 2,
                "MalformedResourceCount": 1,
                "DistrictCount": 3,
                "OfficialSpawnCount": 212,
                "MalformedPlayfieldIds": [103],
            },
            "PlayfieldsXml": {
                "Path": "Playfields.xml",
                "ExpectedPlayfieldCount": 2,
                "Authority": "METADATA_REPRESENTATION_ONLY",
            },
            "CompileEvidence": {
                "WindowsProjectPath": "ZoneEngine.csproj",
                "LinuxCompileInventoryPath": "LinuxBuild/ZoneEngine.CompileItems.props",
                "RuntimeRegistrationPath": "registration.cs",
            },
            "FixedPlayfields": [
                {
                    "PlayfieldId": 4582,
                    "Modules": [
                        {
                            "Name": "IccShuttleportContentModule",
                            "SourcePath": (
                                "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
                                "IccShuttleportContentModule.cs"
                            ),
                        }
                    ],
                    "ExistingSpawnCountStatus": "GOVERNED_PLACEMENT_CATALOG_ENUMERATED",
                },
                {
                    "PlayfieldId": 9999,
                    "Modules": [
                        {
                            "Name": "FakeContentModule",
                            "SourcePath": (
                                "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
                                "FakeContentModule.cs"
                            ),
                        }
                    ],
                    "ExistingSpawnCountStatus": "NOT_ENUMERATED_OFFLINE",
                },
            ],
            "DynamicScopes": [
                {
                    "ScopeId": "TEST_MISSION_INSTANCES",
                    "Module": "MissionInstanceContentModule",
                    "ModuleSourcePath": (
                        "AORebirth/Server/ZoneEngine/Core/Playfields/Content/"
                        "MissionInstanceContentModule.cs"
                    ),
                    "PredicateSourcePath": "MissionInstanceService.cs",
                    "Description": "Test dynamic scope.",
                    "RangeMinimum": 1376256,
                    "RangeMaximum": 1507327,
                    "ExplicitPlayfieldIds": [1500000],
                    "ExpandRangeIntoInventory": False,
                    "IncludeExplicitPlayfieldIdsInInventory": True,
                    "EnumerationStatus": "EXPLICIT_IDENTITIES_ENUMERATED_RANGE_DESCRIPTOR_ONLY",
                }
            ],
            "GovernedPlacementReconciliations": [
                {
                    "PlayfieldId": 4582,
                    "ExistingAoRebirthSpawnCount": 206,
                    "ExistingSpawnsReconciled": 206,
                    "ExistingSpawnsUnmatched": 0,
                    "CurrentActiveOfficialSpawnCount": 25,
                    "OfficialSpawnsWithoutAoRebirthRuntimeEntry": 182,
                    "OfficialSpawnsWithoutAoRebirthPlacement": 1,
                    "Evidence": ["test/pf4582-exact-identity-bridge.json"],
                }
            ],
            "Safety": {
                "LiveDatabaseQueriesAllowed": False,
                "RuntimeConsumptionAllowed": False,
                "ProximityReconciliationAllowed": False,
                "FilenameInferredImplementationAllowed": False,
            },
        }

    def _model(self) -> dict[str, object]:
        return reconciliation.build_model(
            official_index_path=self.official_index_path,
            representation_manifest_path=self.manifest_path,
            repository_root=self.root,
        )

    def test_union_contains_only_evidence_backed_concrete_ids(self) -> None:
        model = self._model()
        self.assertEqual(
            [103, 200, 300, 4582, 9999, 1500000],
            [row["PlayfieldId"] for row in model["Playfields"]],
        )
        self.assertEqual(6, model["Summary"]["InventoryPlayfieldCount"])
        self.assertEqual(1, model["Summary"]["DynamicExplicitPlayfieldCount"])
        self.assertEqual(1507327, model["DynamicScopes"][0]["RangeMaximum"])
        self.assertNotIn(1376256, [row["PlayfieldId"] for row in model["Playfields"]])

    def test_required_per_playfield_fields_and_pf4582_exact_counts(self) -> None:
        model = self._model()
        by_id = {row["PlayfieldId"]: row for row in model["Playfields"]}
        required = {
            "PlayfieldId",
            "AoRebirthImplementationPresent",
            "OfficialPlacementResourcePresent",
            "OfficialPlacementParseStatus",
            "OfficialDistrictCount",
            "OfficialSpawnCount",
            "ExistingAoRebirthSpawnCount",
            "ExistingSpawnsReconciled",
            "ExistingSpawnsUnmatched",
            "OfficialSpawnsWithoutAoRebirthRuntimeEntry",
        }
        self.assertTrue(required.issubset(by_id[4582]))
        self.assertEqual(207, by_id[4582]["OfficialSpawnCount"])
        self.assertEqual(206, by_id[4582]["ExistingAoRebirthSpawnCount"])
        self.assertEqual(206, by_id[4582]["ExistingSpawnsReconciled"])
        self.assertEqual(0, by_id[4582]["ExistingSpawnsUnmatched"])
        self.assertEqual(25, by_id[4582]["CurrentActiveOfficialSpawnCount"])
        self.assertEqual(182, by_id[4582]["OfficialSpawnsWithoutAoRebirthRuntimeEntry"])
        self.assertEqual(1, by_id[4582]["OfficialSpawnsWithoutAoRebirthPlacement"])
        self.assertEqual("EXACT_GOVERNED_RECONCILIATION", by_id[4582]["ReconciliationStatus"])

    def test_unenumerated_counts_are_null_with_explicit_statuses(self) -> None:
        model = self._model()
        by_id = {row["PlayfieldId"]: row for row in model["Playfields"]}
        self.assertTrue(by_id[9999]["AoRebirthImplementationPresent"])
        self.assertIsNone(by_id[9999]["ExistingAoRebirthSpawnCount"])
        self.assertIsNone(by_id[9999]["ExistingSpawnsReconciled"])
        self.assertIsNone(by_id[9999]["ExistingSpawnsUnmatched"])
        self.assertIsNone(by_id[9999]["OfficialSpawnsWithoutAoRebirthRuntimeEntry"])
        self.assertEqual("NOT_ENUMERATED_OFFLINE", by_id[9999]["ExistingAoRebirthSpawnCountStatus"])
        self.assertEqual(
            "UNAVAILABLE_WITHOUT_EXACT_RUNTIME_INVENTORY",
            by_id[9999]["OfficialSpawnsWithoutAoRebirthRuntimeEntryStatus"],
        )
        self.assertFalse(by_id[300]["AoRebirthImplementationPresent"])

    def test_malformed_official_resource_preserves_unavailable_counts_as_null(self) -> None:
        model = self._model()
        row = next(row for row in model["Playfields"] if row["PlayfieldId"] == 103)
        self.assertEqual(
            "MALFORMED_FOR_CURRENT_EXTRACTOR",
            row["OfficialPlacementParseStatus"],
        )
        self.assertIsNone(row["OfficialDistrictCount"])
        self.assertIsNone(row["OfficialSpawnCount"])

        invalid = self._official_index()
        invalid["Playfields"][0]["OfficialSpawnCount"] = 0
        self._write_json(self.official_index_path, invalid)
        with self.assertRaisesRegex(reconciliation.ReconciliationError, "must retain.*null"):
            self._model()

    def test_safety_boundary_is_reported_and_no_runtime_change_is_claimed(self) -> None:
        model = self._model()
        self.assertFalse(model["Summary"]["LiveDatabaseQueried"])
        self.assertFalse(model["Summary"]["ExistingRuntimeBehaviorChanged"])
        self.assertEqual(0, model["Summary"]["NewRuntimeSpawnsActivated"])
        source = Path(reconciliation.__file__).read_text(encoding="utf-8").lower()
        self.assertNotIn("mysql", source)
        self.assertNotIn("sqlconnection", source)
        self.assertNotIn("distance(", source)

    def test_write_check_and_stale_detection_are_repeatable(self) -> None:
        arguments = [
            "--official-index",
            str(self.official_index_path),
            "--representation-manifest",
            str(self.manifest_path),
            "--repository-root",
            str(self.root),
            "--output",
            str(self.output_path),
        ]
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            self.assertEqual(0, reconciliation.main([*arguments, "--write"]))
            first = self.output_path.read_bytes()
            self.assertEqual(0, reconciliation.main([*arguments, "--write"]))
            self.assertEqual(first, self.output_path.read_bytes())
            self.assertEqual(0, reconciliation.main([*arguments, "--check"]))
            self.output_path.write_text("{}\n", encoding="utf-8")
            self.assertEqual(1, reconciliation.main([*arguments, "--check"]))

    def test_compile_and_registration_drift_fails_closed(self) -> None:
        registration = self.root / "registration.cs"
        registration.write_text("new IccShuttleportContentModule();\n", encoding="utf-8")
        with self.assertRaisesRegex(
            reconciliation.ReconciliationError,
            "does not register FakeContentModule",
        ):
            self._model()

    def test_official_shard_sha_drift_fails_closed(self) -> None:
        shard = (
            self.root
            / "docs"
            / "generated"
            / "playfields"
            / "placements"
            / "pf_300.json"
        )
        shard.write_text("{}\n", encoding="utf-8")
        with self.assertRaisesRegex(
            reconciliation.ReconciliationError,
            "shard SHA-256 drifted",
        ):
            self._model()

    def test_repository_manifest_is_valid_and_matches_current_offline_sources(self) -> None:
        manifest = reconciliation.validate_manifest(
            json.loads(
                reconciliation.DEFAULT_REPRESENTATION_MANIFEST.read_text(encoding="utf-8")
            )
        )
        self.assertEqual(
            602,
            manifest["PlayfieldsXml"]["ExpectedPlayfieldCount"],
        )
        self.assertEqual(
            16,
            len(manifest["FixedPlayfields"]),
        )
        self.assertEqual(
            [1048576, 1245183],
            [
                manifest["DynamicScopes"][1]["RangeMinimum"],
                manifest["DynamicScopes"][1]["RangeMaximum"],
            ],
        )
        reconciliation.load_playfields_xml(manifest, reconciliation.REPOSITORY_ROOT)
        reconciliation.validate_compile_and_registration_evidence(
            manifest,
            reconciliation.REPOSITORY_ROOT,
        )


if __name__ == "__main__":
    unittest.main()
