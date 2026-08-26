from __future__ import annotations

import copy
import json
import os
import sys
import unittest
from collections import Counter
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
TOOLS_ROOT = REPOSITORY_ROOT / "Tools"
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import import_official_playfield_placements as importer  # noqa: E402


SOURCE_ROOT = Path(
    os.environ.get("AO_STRIPDOWN_ROOT", str(importer.DEFAULT_SOURCE_ROOT))
)
SOURCE_AVAILABLE = (
    SOURCE_ROOT
    / "Docs"
    / "generated"
    / "playfield_district_info"
    / "playfield_district_import_index.json"
).is_file()


class OfficialPlacementImporterUnitTests(unittest.TestCase):
    def test_parse_error_is_normalized_without_losing_source_object(self) -> None:
        source = {
            "Code": "UNSUPPORTED_HASH_SPAWN_EXTENSION",
            "Detail": "spell extension key 20 at 0x97B",
        }
        self.assertEqual(
            importer._source_parse_error_text(source),
            "UNSUPPORTED_HASH_SPAWN_EXTENSION: spell extension key 20 at 0x97B",
        )
        shard = {
            "DatabaseGlobalOffset": 1,
            "DatabaseSha256": "a" * 64,
            "Districts": [],
            "DuplicateResourceKeyStatus": "UNIQUE_RESOURCE_KEY",
            "IndexRecordIdentity": "index-page-1:slot-1",
            "OfficialResourceId": "18.8.62_EP1:1000014:4805",
            "ParseWarnings": [],
            "ParserVersion": "parser-v1",
            "ResourceFile": "ResourceDatabase.dat",
            "ResourceLength": 10239,
            "ResourceOffset": 208526086,
            "ResourceSha256": "b" * 64,
            "ParseError": source,
            "UnknownFields": {},
        }
        self.assertEqual(importer._resource_unknown_fields(shard)["SourceParseError"], source)
        self.assertNotIn("Districts", importer._resource_unknown_fields(shard))

    def test_compact_json_is_sorted_utf8_with_trailing_newline(self) -> None:
        payload = importer._json_bytes({"z": "\u009f", "a": None}, compact=True)
        self.assertEqual(payload, b'{"a":null,"z":"\xc2\x9f"}\n')
        self.assertEqual(json.loads(payload.decode("utf-8")), {"a": None, "z": "\u009f"})


@unittest.skipUnless(SOURCE_AVAILABLE, "governed AO Stripdown corpus is not available")
class OfficialPlacementImporterSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.model = importer.build_import_model(SOURCE_ROOT)
        cls.outputs = importer.build_candidate_outputs(cls.model)
        cls.records = [
            record
            for shard in cls.model.placement_shards.values()
            for record in shard["Records"]
        ]

    def test_exact_source_counts_hashes_and_index_contract(self) -> None:
        self.assertEqual(len(self.model.placement_shards), 630)
        self.assertEqual(len(self.records), 32805)
        self.assertEqual(self.model.source_shard_bytes, 86019468)
        self.assertEqual(len(self.model.acghash_inventory["Tags"]), 4016)
        self.assertEqual(
            set(self.model.index),
            {
                "SchemaVersion",
                "SourceClientVariant",
                "SourceClientBuild",
                "ResourceType",
                "SourceManifestSha256",
                "Playfields",
            },
        )
        self.assertEqual(len(self.model.index["Playfields"]), 630)
        statuses = Counter(row["ParseStatus"] for row in self.model.index["Playfields"])
        self.assertEqual(statuses, Counter({"PARSED": 627, "MALFORMED_FOR_CURRENT_EXTRACTOR": 3}))
        expected_global_hashes = {
            role: expected
            for role, (_, expected) in importer.EXPECTED_GLOBAL_ARTIFACTS.items()
        }
        source_artifacts = {
            row["Role"]: row["Sha256"]
            for row in self.model.source_manifest["SourceArtifacts"]
        }
        for role, expected in expected_global_hashes.items():
            self.assertEqual(source_artifacts[role], expected)
        self.assertNotIn("DefaultSourceRoot", self.model.source_manifest)
        self.assertNotIn("C:\\", json.dumps(self.model.source_manifest))

    def test_shard_v2_has_one_typed_district_projection(self) -> None:
        self.assertTrue(
            all(shard["SchemaVersion"] == 2 for shard in self.model.placement_shards.values())
        )
        self.assertTrue(
            all("Districts" not in shard["UnknownFields"] for shard in self.model.placement_shards.values())
        )
        self.assertEqual(
            sum(len(shard["Districts"]) for shard in self.model.placement_shards.values()),
            4146,
        )
        district = self.model.placement_shards[4582]["Districts"][0]
        self.assertEqual(
            set(district),
            {
                "DistrictIndex",
                "DistrictName",
                "DistrictRecordOffset",
                "DistrictSerializedSize",
                "HashSpawnRecordCount",
                "OfficialDistrictId",
                "OfficialResourceId",
                "OtherCollectionCountsWhereDecoded",
                "RecordSha256",
                "UnknownFields",
            },
        )
        for shard in self.model.placement_shards.values():
            if shard["ParseStatus"] != "PARSED":
                continue
            for typed_district in shard["Districts"]:
                self.assertGreaterEqual(typed_district["DistrictRecordOffset"], 0)
                self.assertGreater(typed_district["DistrictSerializedSize"], 0)
                self.assertGreaterEqual(typed_district["HashSpawnRecordCount"], 0)
                self.assertRegex(typed_district["RecordSha256"], r"^[0-9a-f]{64}$")
                collection_counts = typed_district["OtherCollectionCountsWhereDecoded"]
                self.assertIsInstance(collection_counts, dict)
                self.assertTrue(all(value >= 0 for value in collection_counts.values()))

    def test_all_records_are_independent_and_no_duplicates_are_dropped(self) -> None:
        identities = [record["OfficialSpawnRecordId"] for record in self.records]
        self.assertEqual(len(identities), 32805)
        self.assertEqual(len(set(identities)), 32805)
        metrics = self.model.summary["Metrics"]
        self.assertEqual(metrics["OfficialRecordsDroppedByDeduplication"], 0)
        self.assertEqual(metrics["OfficialDuplicatePositionRecords"], 7395)
        self.assertEqual(metrics["OfficialDuplicatePositionGroups"], 2869)
        self.assertEqual(metrics["OfficialExactDuplicateRecords"], 2552)
        self.assertEqual(metrics["OfficialExactDuplicateGroups"], 1095)
        self.assertEqual(metrics["OfficialCrossDistrictDuplicateGroups"], 1085)

    def test_malformed_resources_remain_explicit_null_unavailable_entries(self) -> None:
        expected = {
            103: (
                "MALFORMED_RESOURCE: index-page-1111:slot-91 lacks FA FA envelope",
                "ResourceDatabase.dat.002",
                133203574,
                2280687222,
            ),
            615: (
                "MALFORMED_RESOURCE: index-page-1111:slot-137 lacks FA FA envelope",
                "ResourceDatabase.dat.002",
                141220969,
                2288704617,
            ),
            4805: (
                "UNSUPPORTED_HASH_SPAWN_EXTENSION: spell extension key 20 at 0x97B",
                "ResourceDatabase.dat",
                208526086,
                208526086,
            ),
        }
        for playfield_id, (error, resource_file, local_offset, global_offset) in expected.items():
            shard = self.model.placement_shards[playfield_id]
            self.assertEqual(shard["ParseStatus"], "MALFORMED_FOR_CURRENT_EXTRACTOR")
            self.assertIsNone(shard["DistrictCount"])
            self.assertIsNone(shard["OfficialSpawnCount"])
            self.assertEqual(shard["Records"], [])
            self.assertEqual(shard["Districts"], [])
            self.assertEqual(shard["ParseError"], error)
            self.assertEqual(shard["UnknownFields"]["ResourceFile"], resource_file)
            self.assertEqual(shard["UnknownFields"]["ResourceOffset"], local_offset)
            self.assertEqual(shard["UnknownFields"]["DatabaseGlobalOffset"], global_offset)

    def test_pf4582_reconciles_by_stable_id_and_ncnn_stays_blocked(self) -> None:
        records = self.model.placement_shards[4582]["Records"]
        self.assertEqual(len(records), 207)
        self.assertEqual(sum(record["SourceNpcId"] is not None for record in records), 206)
        self.assertEqual(
            sum(record["CurrentRuntimeActive"] is True for record in records),
            importer.PF4582_RUNTIME_ACTIVE_COUNT,
        )
        self.assertEqual(
            sum(record["ExistingAoRebirthProfile"] is not None for record in records),
            importer.PF4582_RUNTIME_ACTIVE_COUNT,
        )
        ncnn = next(
            record
            for record in records
            if record["OfficialSpawnRecordId"]
            == "18.8.62_EP1:1000014:4582:district-1:record-50"
        )
        self.assertEqual(ncnn["CanonicalAcgHashText"], "NCNN")
        self.assertEqual(ncnn["OfficialAcgHashWireBytes"], "4E 4E 43 4E")
        self.assertEqual(ncnn["OfficialAcgHashNativeUInt32"], 0x4E434E4E)
        self.assertIsNone(ncnn["SourceNpcId"])
        self.assertIsNone(ncnn["ExistingAoRebirthProfile"])
        self.assertIs(ncnn["CurrentRuntimeActive"], False)
        self.assertIs(ncnn["RuntimeActivationAuthorized"], False)

    def test_pf4582_full_record_mismatch_fails_closed(self) -> None:
        source_shard = importer._load_json(
            SOURCE_ROOT
            / "Docs"
            / "generated"
            / "playfield_district_info"
            / "18.8.62_EP1"
            / "resource_4582.json"
        )
        source_record = copy.deepcopy(source_shard["Districts"][0]["HashSpawnRecords"][0])
        overlay = importer._load_json(
            REPOSITORY_ROOT / "docs" / "generated" / "pf4582_official_placement_overlay.json"
        )
        overlay_record = next(
            row
            for row in overlay["Records"]
            if row["OfficialRecordIdentity"] == source_record["OfficialSpawnRecordId"]
        )
        source_record["PositionX"] += 1.0
        with self.assertRaisesRegex(importer.PlacementImportError, "PF4582 position mismatch"):
            importer._compare_pf4582_record(source_record, overlay_record)

    def test_runtime_and_future_identity_fields_fail_closed(self) -> None:
        outside_pf4582 = [record for record in self.records if record["PlayfieldId"] != 4582]
        self.assertTrue(all(record["CurrentRuntimeActive"] is None for record in outside_pf4582))
        self.assertTrue(all(record["RuntimeActivationAuthorized"] is False for record in outside_pf4582))
        self.assertTrue(all(record["ResolvedMobTemplateHash"] is None for record in self.records))
        self.assertTrue(all(record["ResolvedMobTemplateId"] is None for record in self.records))
        self.assertTrue(all(record["ResolvedMobTemplateName"] is None for record in self.records))
        self.assertTrue(all(record["ResolvedMonsterData"] is None for record in self.records))
        self.assertTrue(all(record["MobTemplateResolutionStatus"] == "UNRESOLVED" for record in self.records))
        self.assertTrue(all(record["PlacementKnown"] is True for record in self.records))
        for record in self.records:
            self.assertGreater(record["SerializedSize"], 0)
            self.assertIsInstance(record["PositionX"], (int, float))
            self.assertIsInstance(record["PositionY"], (int, float))
            self.assertIsInstance(record["PositionZ"], (int, float))
            active_and_profile_backed = (
                record["CurrentRuntimeActive"] is True
                and record["ExistingAoRebirthProfile"] is not None
            )
            self.assertIs(record["IdentityResolved"], active_and_profile_backed)
            self.assertIs(record["BehaviorReady"], active_and_profile_backed)
            self.assertIs(record["RuntimeActivationAuthorized"], active_and_profile_backed)
        self.assertEqual(
            sum(record["IdentityResolved"] is True for record in self.records),
            importer.PF4582_RUNTIME_ACTIVE_COUNT,
        )
        self.assertEqual(
            sum(record["BehaviorReady"] is True for record in self.records),
            importer.PF4582_RUNTIME_ACTIVE_COUNT,
        )
        self.assertEqual(
            sum(record["RuntimeActivationAuthorized"] is True for record in self.records),
            importer.PF4582_RUNTIME_ACTIVE_COUNT,
        )
        self.assertEqual(
            self.model.summary["Metrics"]["NewRuntimeSpawnsActivated"],
            importer.PF4582_NEWLY_AUTHORIZED_COUNT,
        )
        self.assertIs(self.model.summary["Metrics"]["ExistingRuntimeBehaviorChanged"], False)

    def test_general_catalog_is_packaged_but_not_wired_to_runtime_materialization(self) -> None:
        runtime_entrypoints = [
            REPOSITORY_ROOT
            / "AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldRuntimeSystems.cs",
            REPOSITORY_ROOT
            / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs",
            REPOSITORY_ROOT
            / "AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldObjectMaterializationRuntimeService.cs",
        ]
        for path in runtime_entrypoints:
            self.assertNotIn(
                "OfficialPlayfieldPlacementCatalog",
                path.read_text(encoding="utf-8"),
                str(path),
            )
        windows_project = (
            REPOSITORY_ROOT / "AORebirth/Server/ZoneEngine/ZoneEngine.csproj"
        ).read_text(encoding="utf-8").replace("\\", "/")
        self.assertIn("../../../docs/generated/playfields/official-placement-corpus-manifest.json", windows_project)
        self.assertIn("../../../docs/generated/playfields/official-placement-index.json", windows_project)
        self.assertIn("../../../docs/generated/playfields/official-placement-summary.json", windows_project)
        self.assertIn("../../../docs/generated/playfields/official-acghash-inventory.json", windows_project)
        self.assertNotIn("../../../docs/generated/playfields/placements/*.json", windows_project)
        self.assertEqual(
            windows_project.count("../../../docs/generated/playfields/placements/pf_"),
            630,
        )
        packaged_playfield_ids = [
            int(line.split("/pf_", 1)[1].split(".json", 1)[0])
            for line in windows_project.splitlines()
            if "../../../docs/generated/playfields/placements/pf_" in line
        ]
        self.assertEqual(
            packaged_playfield_ids,
            sorted(self.model.placement_shards),
        )
        self.assertEqual(
            windows_project.count("Content/Official/PlayfieldPlacements/placements/pf_"),
            630,
        )
        self.assertNotIn("official-playfield-reconciliation.json", windows_project)

    def test_corpus_manifest_is_exact_deterministic_runtime_authority(self) -> None:
        manifest = self.model.corpus_manifest
        self.assertEqual(
            set(manifest),
            {
                "SchemaVersion",
                "CorpusVersion",
                "SourceClientVariant",
                "SourceClientBuild",
                "ResourceType",
                "SourceManifestSha256",
                "IndexSha256",
                "SummarySha256",
                "AcgHashInventorySha256",
                "Metrics",
                "ParserLimitedPlayfieldIds",
                "Policy",
                "Playfields",
            },
        )
        self.assertEqual(manifest["SchemaVersion"], 1)
        self.assertEqual(manifest["CorpusVersion"], importer.CORPUS_VERSION)
        self.assertEqual(
            manifest["Metrics"],
            {
                "ResourceCount": 630,
                "ParsedResourceCount": 627,
                "ParserLimitedResourceCount": 3,
                "DistrictCount": 4146,
                "PlacementCount": 32805,
                "UniqueAcgHashCount": 4016,
                "RuntimeActivationAuthorizedCount": importer.PF4582_RUNTIME_ACTIVE_COUNT,
            },
        )
        self.assertEqual(manifest["ParserLimitedPlayfieldIds"], [103, 615, 4805])
        self.assertEqual(
            manifest["Policy"],
            {
                "MassPlacementActivation": False,
                "UnresolvedAcgHashActivated": False,
                "ExistingRuntimeBehaviorChanged": False,
            },
        )
        self.assertEqual(
            [row["PlayfieldId"] for row in manifest["Playfields"]],
            sorted(row["PlayfieldId"] for row in manifest["Playfields"]),
        )
        self.assertEqual(len(manifest["Playfields"]), 630)
        self.assertTrue(
            all(
                set(row)
                == {
                    "PlayfieldId",
                    "Path",
                    "ParseStatus",
                    "DistrictCount",
                    "PlacementCount",
                    "SourceResourceSha256",
                    "ShardSha256",
                    "RuntimeActivationAuthorizedCount",
                }
                for row in manifest["Playfields"]
            )
        )
        self.assertEqual(
            sum(row["DistrictCount"] or 0 for row in manifest["Playfields"]),
            4146,
        )
        self.assertEqual(
            sum(row["PlacementCount"] or 0 for row in manifest["Playfields"]),
            32805,
        )
        self.assertEqual(
            sum(
                row["RuntimeActivationAuthorizedCount"]
                for row in manifest["Playfields"]
            ),
            importer.PF4582_RUNTIME_ACTIVE_COUNT,
        )
        expected_hashes = {
            "SourceManifestSha256": importer._sha256_bytes(
                self.outputs[REPOSITORY_ROOT.joinpath(*PurePathParts(importer.OUTPUT_SOURCE_MANIFEST.as_posix()))]
            ),
            "IndexSha256": importer._sha256_bytes(
                self.outputs[REPOSITORY_ROOT.joinpath(*PurePathParts(importer.OUTPUT_INDEX.as_posix()))]
            ),
            "SummarySha256": importer._sha256_bytes(
                self.outputs[REPOSITORY_ROOT.joinpath(*PurePathParts(importer.OUTPUT_SUMMARY.as_posix()))]
            ),
            "AcgHashInventorySha256": importer._sha256_bytes(
                self.outputs[REPOSITORY_ROOT.joinpath(*PurePathParts(importer.OUTPUT_ACGHASH.as_posix()))]
            ),
        }
        for key, expected in expected_hashes.items():
            self.assertEqual(manifest[key], expected)
        for row in manifest["Playfields"]:
            shard_path = REPOSITORY_ROOT.joinpath(
                *PurePathParts(f"docs/generated/playfields/{row['Path']}")
            )
            self.assertEqual(
                row["ShardSha256"], importer._sha256_bytes(self.outputs[shard_path])
            )

    def test_rendering_is_deterministic_compact_and_index_hashes_match(self) -> None:
        second = importer.build_candidate_outputs(self.model)
        self.assertEqual(self.outputs, second)
        self.assertLess(self.model.normalized_shard_bytes, self.model.source_shard_bytes)
        for row in self.model.index["Playfields"]:
            payload = self.outputs[REPOSITORY_ROOT.joinpath(*PurePathParts(row["Path"]))]
            self.assertEqual(importer._sha256_bytes(payload), row["Sha256"])


def PurePathParts(value: str) -> tuple[str, ...]:
    return tuple(value.split("/"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
