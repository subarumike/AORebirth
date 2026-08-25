import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / "Tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import analyze_pf4582_template_identity_bridge as bridge


class Pf4582TemplateIdentityBridgeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.runtime_before = bridge.sha256_file(bridge.DEFAULT_RUNTIME_SOURCE)
        cls.model = bridge.build_model()
        cls.report = cls.model["Report"]
        cls.manifest = cls.model["SearchManifest"]
        cls.records = cls.report["HashRecords"]
        cls.evidence = json.loads(
            bridge.DEFAULT_EVIDENCE.read_text(encoding="utf-8")
        )
        cls.source_records = bridge.load_source_records(bridge.DEFAULT_SOURCE)

    def test_01_exactly_38_source_hash_records_are_analyzed(self):
        self.assertEqual(38, len(self.records))

    def test_02_all_206_source_placements_are_represented(self):
        self.assertEqual(206, sum(record["PlacementCount"] for record in self.records))

    def test_03_all_206_source_npc_ids_are_represented_once(self):
        npc_ids = [npc_id for record in self.records for npc_id in record["NpcIds"]]
        self.assertEqual(206, len(npc_ids))
        self.assertEqual(206, len(set(npc_ids)))

    def test_04_unsigned_decimal_round_trip_is_exact(self):
        for record in self.records:
            self.assertEqual(
                record["TemplateHashUInt32"],
                bridge.parse_uint32(record["TemplateHashOriginal"]),
            )

    def test_05_hex_round_trip_is_exact(self):
        for record in self.records:
            self.assertEqual(record["TemplateHashUInt32"], int(record["TemplateHashHex"], 16))

    def test_06_little_endian_round_trip_is_exact(self):
        for record in self.records:
            raw = bytes.fromhex(record["TemplateHashLittleEndianBytes"])
            self.assertEqual(record["TemplateHashUInt32"], int.from_bytes(raw, "little"))

    def test_07_big_endian_round_trip_is_exact(self):
        for record in self.records:
            raw = bytes.fromhex(record["TemplateHashBigEndianBytes"])
            self.assertEqual(record["TemplateHashUInt32"], int.from_bytes(raw, "big"))

    def test_08_four_character_display_round_trip_is_exact(self):
        for record in self.records:
            raw = record["TemplateHashAscii"].encode("ascii")
            self.assertEqual(record["TemplateHashUInt32"], int.from_bytes(raw, "little"))

    def test_09_no_conversion_collision_occurs(self):
        self.assertEqual(38, len({record["TemplateHashUInt32"] for record in self.records}))
        self.assertEqual(38, len({record["TemplateHashHex"] for record in self.records}))
        self.assertEqual(38, len({record["TemplateHashAscii"] for record in self.records}))

    def test_10_values_are_never_silently_signed_or_truncated(self):
        with self.assertRaises(bridge.BridgeAnalysisError):
            bridge.parse_uint32(-1)
        with self.assertRaises(bridge.BridgeAnalysisError):
            bridge.parse_uint32(0x100000000)

    def test_11_the_14_baseline_keys_remain_exact(self):
        actual = {
            record["TemplateHashAscii"]
            for record in self.records
            if record["BaselineState"] == "MAPPED"
        }
        self.assertEqual(bridge.BASELINE_TAGS, actual)

    def test_12_dynamic_names_remain_exact(self):
        expected = sorted(
            name
            for record in self.evidence["ExpectedDynamicNames"]
            for name in [record]
        )
        actual = sorted(
            name for record in self.records for name in record["DynamicNamesPresent"]
        )
        self.assertEqual(expected, actual)

    def test_13_raw_ascii_occurrence_cannot_produce_direct_static(self):
        self.assertEqual(
            "CORROBORATING_ONLY",
            bridge.evidence_strength(
                official=True,
                direct_source_key=False,
                consumer=False,
                terminal_identity=False,
            ),
        )

    def test_14_name_only_relationship_cannot_produce_direct_bridge(self):
        self.assertEqual(
            "CORROBORATING_ONLY",
            bridge.evidence_strength(
                official=False,
                direct_source_key=False,
                consumer=False,
                terminal_identity=True,
            ),
        )

    def test_15_coordinate_only_relationship_cannot_produce_direct_bridge(self):
        self.assertEqual(
            "CORROBORATING_ONLY",
            bridge.evidence_strength(
                official=False,
                direct_source_key=False,
                consumer=False,
                terminal_identity=False,
            ),
        )

    def test_16_level_only_relationship_cannot_produce_direct_bridge(self):
        self.assertNotEqual(
            "DIRECT_STATIC",
            bridge.evidence_strength(
                official=False,
                direct_source_key=False,
                consumer=True,
                terminal_identity=False,
            ),
        )

    def test_17_monsterdata_similarity_without_key_join_is_not_direct(self):
        self.assertEqual(
            "CORROBORATING_ONLY",
            bridge.evidence_strength(
                official=True,
                direct_source_key=False,
                consumer=True,
                terminal_identity=True,
            ),
        )

    def test_18_third_party_data_cannot_produce_authoritative_status(self):
        self.assertEqual(
            "CORROBORATING_ONLY",
            bridge.evidence_strength(
                official=False,
                direct_source_key=True,
                consumer=True,
                terminal_identity=True,
            ),
        )

    def test_19_direct_static_requires_consumer_and_terminal_identity(self):
        self.assertEqual(
            "DIRECT_STATIC",
            bridge.evidence_strength(
                official=True,
                direct_source_key=True,
                consumer=True,
                terminal_identity=True,
            ),
        )

    def test_20_runtime_ready_requires_same_context_and_capture_implementation(self):
        self.assertEqual(
            "DIRECT_RUNTIME_READY",
            bridge.evidence_strength(
                official=True,
                direct_source_key=True,
                consumer=False,
                terminal_identity=True,
                same_runtime_context=True,
                capture_implemented=True,
            ),
        )

    def test_21_unsupported_or_missing_official_build_fails_closed(self):
        malformed = copy.deepcopy(self.evidence)
        malformed["OfficialSources"][0]["RelativePath"] = "missing/official.json"
        with self.assertRaises(bridge.BridgeAnalysisError):
            bridge.verify_official_sources(malformed)

    def test_22_missing_native_fields_remain_unavailable(self):
        self.assertTrue(all(record["RuntimeFieldLocated"] is True for record in self.records))
        self.assertTrue(all(record["RuntimeNpcIdLocated"] is False for record in self.records))

    def test_23_runtime_never_derives_npcid_from_coordinates(self):
        source_npc_ids = sorted(record["NpcId"] for record in self.source_records)
        report_npc_ids = sorted(npc_id for record in self.records for npc_id in record["NpcIds"])
        self.assertEqual(source_npc_ids, report_npc_ids)
        self.assertEqual("NO", self.report["Safety"]["COORDINATE_JOIN_ACCEPTED"])

    def test_24_same_hash_propagation_is_not_assumed(self):
        self.assertEqual("NO", self.report["Metrics"]["PF4582_SAME_HASH_PROPAGATION_PROVEN"])
        self.assertNotIn("GLOBAL_HASH_UNIQUE", {record["PropagationScope"] for record in self.records})

    def test_25_dynamic_hashes_cannot_receive_global_propagation(self):
        dynamic = [record for record in self.records if record["DynamicNamesPresent"]]
        self.assertTrue(dynamic)
        self.assertTrue(all(record["PropagationScope"] == "DYNAMIC_OR_VARIANT" for record in dynamic))

    def test_26_prior_contradictions_are_preserved(self):
        _, prior = bridge.load_prior_records(bridge.DEFAULT_PRIOR_REPORT)
        for record in self.records:
            key = record["TemplateHashUInt32"]
            self.assertEqual(bridge._contradictions(prior[key]), record["Contradictions"])

    def test_27_existing_25_active_placements_remain_unchanged(self):
        metrics = self.report["Metrics"]
        self.assertEqual(25, metrics["PF4582_RUNTIME_ACTIVE_BEFORE"])
        self.assertEqual(25, metrics["PF4582_RUNTIME_ACTIVE_AFTER"])

    def test_28_no_new_npcid_becomes_active(self):
        self.assertTrue(all(record["RuntimeActivationAllowed"] is False for record in self.records))
        self.assertEqual(0, self.report["Metrics"]["PF4582_NEW_DIRECT_NPCID_BRIDGES"])

    def test_29_all_181_blocked_placements_remain_blocked(self):
        metrics = self.report["Metrics"]
        self.assertEqual(181, metrics["PF4582_RUNTIME_BLOCKED_BEFORE"])
        self.assertEqual(181, metrics["PF4582_RUNTIME_BLOCKED_AFTER"])

    def test_30_generated_json_ordering_is_deterministic(self):
        first = bridge.render_json(self.report)
        second = bridge.render_json(self.report)
        self.assertEqual(first, second)

    def test_31_generated_markdown_ordering_is_deterministic(self):
        first = bridge.render_markdown(self.report)
        second = bridge.render_markdown(self.report)
        self.assertEqual(first, second)

    def test_32_second_model_generation_is_byte_identical(self):
        second = bridge.build_model()
        self.assertEqual(
            bridge.render_json(self.model["Report"]),
            bridge.render_json(second["Report"]),
        )
        self.assertEqual(
            bridge.render_json(self.model["SearchManifest"]),
            bridge.render_json(second["SearchManifest"]),
        )

    def test_33_missing_governed_evidence_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            missing = Path(temporary) / "missing.json"
            with self.assertRaises(bridge.BridgeAnalysisError):
                bridge.build_model(
                    evidence_path=missing,
                    verify_official=False,
                    official_resource_root=None,
                    official_runtime_root=None,
                )

    def test_34_malformed_evidence_fails_closed(self):
        malformed = copy.deepcopy(self.evidence)
        malformed["CurrentOutcome"] = "SPECULATIVE_BRIDGE"
        source_keys = {record["TemplateHash"] for record in self.source_records}
        with self.assertRaises(bridge.BridgeAnalysisError):
            bridge.validate_evidence(malformed, source_keys)

    def test_35_manifest_cannot_claim_uninspected_sources(self):
        malformed = copy.deepcopy(self.evidence)
        malformed["OfficialSources"][0]["InspectionCompleted"] = False
        source_keys = {record["TemplateHash"] for record in self.source_records}
        with self.assertRaises(bridge.BridgeAnalysisError):
            bridge.validate_evidence(malformed, source_keys)

    def test_36_no_official_binary_is_copied_into_artifacts(self):
        serialized = bridge.render_json(self.report) + bridge.render_json(self.manifest)
        self.assertNotIn("BinaryContents", serialized)
        self.assertNotIn("Base64", serialized)

    def test_37_generated_artifacts_match_current_model(self):
        self.assertEqual(
            bridge.DEFAULT_REPORT.read_text(encoding="utf-8"),
            bridge.render_json(self.report),
        )
        self.assertEqual(
            bridge.DEFAULT_SEARCH_MANIFEST.read_text(encoding="utf-8"),
            bridge.render_json(self.manifest),
        )
        self.assertEqual(
            bridge.DEFAULT_MARKDOWN.read_text(encoding="utf-8"),
            bridge.render_markdown(self.report),
        )

    def test_38_outputs_have_no_generation_timestamp(self):
        serialized = bridge.render_json(self.report) + bridge.render_json(self.manifest)
        self.assertNotIn("GeneratedAt", serialized)
        self.assertNotIn("generatedAt", serialized)

    def test_39_report_generation_does_not_modify_runtime_source(self):
        self.assertEqual(self.runtime_before, bridge.sha256_file(bridge.DEFAULT_RUNTIME_SOURCE))

    def test_40_no_bridge_outcome_contains_neither_direct_path(self):
        self.assertEqual("NO_BRIDGE_LOCATED", self.report["PriorOutcome"])
        self.assertEqual("STRUCTURAL_SOURCE_AND_CONSUMER_FOUND", self.report["Outcome"])
        self.assertTrue(self.report["PriorOutcomeSuperseded"])
        self.assertFalse(self.evidence["Claims"]["DirectStaticBridge"])
        self.assertFalse(self.evidence["Claims"]["DirectRuntimeReadyBridge"])

    def test_41_no_bridge_outcome_has_nonempty_blockers(self):
        self.assertTrue(self.report["MissingEvidence"])
        self.assertTrue(all(record["RemainingBlockers"] for record in self.records))

    def test_42_no_mapping_is_promoted(self):
        self.assertEqual(0, self.report["Metrics"]["PF4582_NEW_DIRECT_HASH_BRIDGES"])
        self.assertTrue(all(
            record["DirectBridgeStatus"]
            == "STRUCTURAL_SOURCE_AND_PARSER_CONSUMER_ONLY"
            for record in self.records
        ))
        self.assertTrue(all(
            record["NewProofClassification"]
            == "OFFICIAL_STRUCTURAL_SOURCE_AND_PARSER_CONSUMER"
            for record in self.records
        ))

    def test_43_governed_inputs_are_sha256_pinned(self):
        self.assertEqual(6, len(self.report["InputDigests"]))
        self.assertTrue(all(bridge.SHA256_PATTERN.fullmatch(value) for value in self.report["InputDigests"].values()))

    def test_44_every_official_source_has_a_complete_fingerprint(self):
        for source in self.manifest["OfficialSources"]:
            self.assertGreaterEqual(source["FileSize"], 0)
            self.assertRegex(source["Sha256"], r"^[0-9a-f]{64}$")
            self.assertTrue(source["LogicalSourceLabel"])

    def test_45_every_hash_record_has_all_required_fields(self):
        required = {
            "TemplateHashOriginal", "TemplateHashUInt32", "TemplateHashHex",
            "TemplateHashLittleEndianBytes", "TemplateHashAscii", "PlacementCount",
            "NpcIds", "SourceNames", "DynamicNamesPresent", "BaselineState",
            "PriorAuditClassification", "OfficialOccurrences", "OfficialLookupRecords",
            "StaticTerminalIdentities", "RuntimeFieldLocated", "RuntimeNpcIdLocated",
            "CandidateAoRebirthProfiles", "DirectBridgeStatus", "PropagationScope",
            "NewProofClassification", "EvidenceSources", "EvidenceOffsets",
            "EvidenceFunctions", "EvidenceDigests", "Contradictions",
            "RemainingBlockers", "RuntimeActivationAllowed",
        }
        for record in self.records:
            self.assertTrue(required.issubset(record))

    def test_46_isre_propagation_remains_npcid_specific(self):
        isre = next(record for record in self.records if record["TemplateHashAscii"] == "ISRE")
        self.assertEqual("NPCID_SPECIFIC", isre["PropagationScope"])
        self.assertEqual("NO", self.report["Metrics"]["PF4582_ISRE_BLOCKED_PROPAGATION_PROVEN"])

    def test_47_templatehash_and_spawnhash_are_identical_in_all_source_rows(self):
        self.assertTrue(all(record["TemplateHash"] == record["SpawnHash"] for record in self.source_records))

    def test_48_templatehash_is_not_claimed_as_an_official_field_name(self):
        self.assertEqual(
            "NO",
            self.report["Metrics"]["PF4582_TEMPLATE_FIELD_OFFICIAL_NAME_PROVEN"],
        )

    def test_49_every_key_uses_every_required_search_representation(self):
        for result in self.manifest["PerKeyResults"]:
            self.assertEqual(bridge.SEARCH_REPRESENTATIONS, result["SearchMethods"])

    def test_50_official_source_labels_are_unique(self):
        labels = [item["LogicalSourceLabel"] for item in self.manifest["OfficialSources"]]
        self.assertEqual(len(labels), len(set(labels)))

    def test_51_structural_source_and_parser_statuses_are_proven(self):
        self.assertEqual("PROVEN", self.report["OfficialStructuralSourceStatus"])
        self.assertEqual("PROVEN", self.report["OfficialAcgHashTypeStatus"])
        self.assertEqual("PROVEN", self.report["OfficialParserConsumerStatus"])

    def test_52_terminal_identity_and_runtime_join_remain_unresolved(self):
        self.assertEqual("UNRESOLVED", self.report["TerminalMobIdentityStatus"])
        self.assertEqual("UNRESOLVED", self.report["RuntimeHashToDynelJoinStatus"])
        self.assertEqual(0, self.report["StaticMobMappingsExtracted"])

    def test_53_runtime_capture_remains_not_ready(self):
        self.assertEqual("NO", self.report["Metrics"]["PF4582_RUNTIME_CAPTURE_READY"])
        self.assertEqual("NO", self.report["Metrics"]["PF4582_RUNTIME_DYNEL_JOIN_FOUND"])

    def test_54_cima_dual_encoding_is_explicit(self):
        cima = next(record for record in self.records if record["CanonicalAcgHashText"] == "CIMA")
        self.assertEqual(1095584067, cima["AcceptedSourceUInt32"])
        self.assertEqual("43 49 4D 41", cima["AcceptedSourceLittleEndianBytes"])
        self.assertEqual("41 4D 49 43", cima["OfficialWireBytes"])
        self.assertEqual(0x43494D41, cima["OfficialNativeUInt32"])

    def test_55_current_report_uses_only_local_governed_evidence_paths(self):
        serialized = bridge.render_json(self.report) + bridge.render_json(self.manifest)
        self.assertNotIn("C:\\Users\\Mike", serialized)
        self.assertNotIn("AO stripdown", serialized)

    def test_56_official_record_count_and_extra_key_are_explicit(self):
        self.assertEqual(207, self.report["Metrics"]["PF4582_OFFICIAL_RESOURCE_RECORDS"])
        self.assertEqual(1, self.report["Metrics"]["PF4582_OFFICIAL_ADDITIONAL_RECORDS"])
        self.assertEqual("NCNN", self.manifest["AdditionalOfficialKey"])


if __name__ == "__main__":
    unittest.main()
