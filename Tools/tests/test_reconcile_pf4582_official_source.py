from __future__ import annotations

import hashlib
import json
import sys
import unittest
from collections import Counter
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import reconcile_pf4582_official_source as reconcile


class Pf4582OfficialSourceReconciliationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.model = reconcile.build_model()
        cls.report = cls.model["Report"]
        cls.metrics = cls.report["Metrics"]
        cls.overlay = cls.model["Overlay"]
        cls.rows = cls.report["ReconciliationRecords"]
        cls.ncnn = cls.report["NcnnAudit"]
        cls.accepted = json.loads(
            reconcile.DEFAULT_ACCEPTED_SOURCE.read_text(encoding="utf-8")
        )["4582"]["Spawns"]

    def test_01_imported_record_snapshot_sha256(self):
        self.assertEqual(reconcile.EXPECTED_ARTIFACT_SHA256["records"], reconcile.sha256_file(reconcile.DEFAULT_OFFICIAL_RECORDS))

    def test_02_imported_search_report_sha256(self):
        self.assertEqual(reconcile.EXPECTED_ARTIFACT_SHA256["search_report"], reconcile.sha256_file(reconcile.DEFAULT_OFFICIAL_SEARCH_REPORT))

    def test_03_imported_occurrence_manifest_sha256(self):
        self.assertEqual(reconcile.EXPECTED_ARTIFACT_SHA256["occurrence_manifest"], reconcile.sha256_file(reconcile.DEFAULT_OFFICIAL_OCCURRENCE_MANIFEST))

    def test_04_accepted_json_sha256_is_unchanged(self):
        self.assertEqual(reconcile.EXPECTED_ACCEPTED_SHA256, reconcile.sha256_file(reconcile.DEFAULT_ACCEPTED_SOURCE))

    def test_05_official_build_is_exact(self):
        self.assertEqual("18.8.62_EP1", self.metrics["PF4582_OFFICIAL_BUILD"])

    def test_06_official_resource_type_and_instance_are_exact(self):
        self.assertEqual(1000014, self.metrics["PF4582_OFFICIAL_RESOURCE_TYPE"])
        self.assertEqual(4582, self.metrics["PF4582_OFFICIAL_RESOURCE_INSTANCE"])

    def test_07_official_record_count_is_207(self):
        self.assertEqual(207, self.metrics["PF4582_OFFICIAL_RESOURCE_RECORDS"])
        self.assertEqual(207, len(self.model["OfficialRecords"]))

    def test_08_district_counts_are_142_and_65(self):
        counts = Counter(record["district_index"] for record in self.model["OfficialRecords"])
        self.assertEqual({0: 142, 1: 65}, dict(counts))
        self.assertEqual(207, sum(counts.values()))

    def test_09_all_206_accepted_records_reconcile(self):
        self.assertEqual(206, self.metrics["PF4582_ACCEPTED_RECORDS_RECONCILED"])
        self.assertEqual(0, self.metrics["PF4582_ACCEPTED_RECORDS_UNMATCHED"])

    def test_10_exactly_one_official_record_is_unmatched(self):
        self.assertEqual(1, self.metrics["PF4582_OFFICIAL_RECORDS_UNMATCHED"])
        self.assertEqual(1, len(self.report["OfficialRecordsNotPresentInAcceptedSource"]))

    def test_11_additional_key_is_ncnn(self):
        self.assertEqual("NCNN", self.metrics["PF4582_OFFICIAL_EXTRA_KEY"])
        self.assertEqual("NCNN", self.ncnn["CanonicalAcgHashText"])

    def test_12_ncnn_wire_bytes_are_exact(self):
        self.assertEqual("4E 4E 43 4E", self.ncnn["OfficialWireBytes"])

    def test_13_ncnn_native_scalar_is_exact(self):
        self.assertEqual(0x4E434E4E, self.ncnn["OfficialNativeUInt32"])
        self.assertEqual("0x4E434E4E", self.ncnn["OfficialNativeUInt32Hex"])

    def test_14_ncnn_has_no_fabricated_source_npcid(self):
        self.assertIsNone(self.ncnn["SourceNpcId"])
        self.assertFalse(self.ncnn["AcceptedSourceRecordPresent"])

    def test_15_all_accepted_source_npcids_are_retained(self):
        expected = sorted(record["NpcId"] for record in self.accepted)
        actual = sorted(record["SourceNpcId"] for record in self.rows)
        self.assertEqual(expected, actual)
        self.assertEqual(206, len(set(actual)))

    def test_16_reconciliation_is_one_to_one(self):
        self.assertEqual(206, len({record["SourceNpcId"] for record in self.rows}))
        self.assertEqual(206, len({record["OfficialRecordIdentity"] for record in self.rows}))

    def test_17_duplicate_position_records_remain_separate(self):
        identities = [record["official_record_identity"] for record in self.model["OfficialRecords"]]
        self.assertEqual(207, len(identities))
        self.assertEqual(207, len(set(identities)))

    def test_18_duplicate_field_groups_remain_explicit(self):
        groups = self.report["DuplicateEquivalenceGroups"]
        self.assertEqual(5, len(groups))
        self.assertEqual(14, sum(len(group["AcceptedSourceNpcIds"]) for group in groups))
        self.assertTrue(all(len(group["AcceptedSourceNpcIds"]) == len(group["OfficialRecordIdentities"]) for group in groups))

    def test_19_ambiguous_order_fails_closed(self):
        with self.assertRaises(reconcile.ReconciliationError):
            reconcile.require_monotonic_source_order([(1, 20), (2, 10)])

    def test_20_all_38_accepted_keys_are_structurally_present(self):
        accepted_keys = {reconcile.accepted_uint32_to_canonical_text(record["TemplateHash"]) for record in self.accepted}
        official_keys = {record["acghash_get_hash_as_text"] for record in self.model["OfficialRecords"]}
        self.assertEqual(38, len(accepted_keys))
        self.assertTrue(accepted_keys.issubset(official_keys))

    def test_21_every_row_reconciles_by_canonical_text(self):
        self.assertTrue(all(len(record["CanonicalAcgHashText"]) == 4 for record in self.rows))
        self.assertTrue(all("CanonicalAcgHashText" in record["MatchBasis"] for record in self.rows))

    def test_22_accepted_and_official_scalars_are_not_compared_directly(self):
        self.assertFalse(self.report["EncodingModel"]["AcceptedAndOfficialNativeScalarsComparedDirectly"])
        cima = next(record for record in self.rows if record["CanonicalAcgHashText"] == "CIMA")
        self.assertNotEqual(cima["AcceptedTemplateHashUInt32"], cima["OfficialNativeUInt32"])

    def test_23_cima_dual_encoding_is_exact(self):
        cima = self.report["EncodingModel"]["CimaExample"]
        self.assertEqual(1095584067, cima["AcceptedSourceUInt32"])
        self.assertEqual("0x414D4943", cima["AcceptedSourceHex"])
        self.assertEqual("43 49 4D 41", cima["AcceptedSourceLittleEndianBytes"])
        self.assertEqual("41 4D 49 43", cima["OfficialWireBytes"])
        self.assertEqual(0x43494D41, cima["OfficialNativeUInt32"])
        self.assertEqual("CIMA", cima["OfficialGetHashAsText"])

    def test_24_all_38_dual_encodings_roundtrip(self):
        values = {record["TemplateHash"] for record in self.accepted}
        self.assertEqual(38, len(values))
        self.assertTrue(all(reconcile.roundtrip_dual_encoding(value) for value in values))
        self.assertEqual(38, self.metrics["PF4582_DUAL_ENCODING_KEYS_ROUNDTRIPPED"])

    def test_25_no_key_conversion_collision_occurs(self):
        values = {record["TemplateHash"] for record in self.accepted}
        texts = {reconcile.accepted_uint32_to_canonical_text(value) for value in values}
        native = {reconcile.canonical_text_to_official_native_uint32(text) for text in texts}
        self.assertEqual(38, len(texts))
        self.assertEqual(38, len(native))

    def test_26_acghash_is_described_as_packed_scalar_tag(self):
        self.assertEqual("packed four-byte ACGHash_t scalar/tag", self.report["EncodingModel"]["OfficialType"])

    def test_27_acghash_is_not_claimed_as_cryptographic_or_terminal_identity(self):
        manifest = self.report["OfficialSource"]
        self.assertFalse(manifest["AcgHash"]["CryptographicHash"])
        self.assertFalse(manifest["AcgHash"]["MobTemplateIdentityProven"])
        self.assertFalse(manifest["AcgHash"]["TerminalRuntimeIdentityProven"])

    def test_28_npcid_is_stable_aorebirth_key_not_native_claim(self):
        self.assertEqual("YES", self.metrics["PF4582_SOURCE_NPCID_STABLE_FOR_AOREBIRTH"])
        self.assertEqual("NO", self.metrics["PF4582_SOURCE_NPCID_PROVEN_NATIVE_FUNCOM_FIELD"])

    def test_29_official_overlay_has_207_records(self):
        self.assertEqual(207, self.overlay["OfficialSourceRecords"])
        self.assertEqual(207, len(self.overlay["Records"]))

    def test_30_overlay_links_206_source_npcids(self):
        linked = [record for record in self.overlay["Records"] if record["SourceNpcId"] is not None]
        self.assertEqual(206, len(linked))
        self.assertEqual(206, self.overlay["ReconciledToSourceNpcId"])

    def test_31_overlay_has_one_null_source_npcid(self):
        unlinked = [record for record in self.overlay["Records"] if record["SourceNpcId"] is None]
        self.assertEqual(1, len(unlinked))
        self.assertEqual("NCNN", unlinked[0]["CanonicalAcgHashText"])

    def test_32_overlay_is_not_runtime_consumed(self):
        self.assertEqual("NOT_CONSUMED", self.overlay["RuntimeConsumptionStatus"])
        runtime = (reconcile.REPOSITORY_ROOT / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs").read_text(encoding="utf-8")
        self.assertNotIn("IccShuttleportOfficialPlacementCatalog", runtime)

    def test_33_existing_active_source_npcid_set_is_exact(self):
        expected = {
            1007858, 1007985, 1008027, 1008028, 1008029, 1008030, 1008031,
            1008032, 1008033, 1008034, 1008035, 1008036, 1008037, 1008039,
            1008040, 1008041, 1008042, 1008043, 1008044, 1008045, 1008046,
            1008047, 1008048, 1008049, 1008050,
        }
        normalized = json.loads(reconcile.DEFAULT_NORMALIZED_REPORT.read_text(encoding="utf-8"))
        self.assertEqual(expected, set(normalized["runtimeEligibleNpcIds"]))

    def test_34_current_runtime_counts_are_unchanged(self):
        self.assertEqual(25, self.metrics["PF4582_RUNTIME_ACTIVE_BEFORE"])
        self.assertEqual(25, self.metrics["PF4582_RUNTIME_ACTIVE_AFTER"])
        self.assertEqual(181, self.metrics["PF4582_CURRENT_RUNTIME_BLOCKED_BEFORE"])
        self.assertEqual(181, self.metrics["PF4582_CURRENT_RUNTIME_BLOCKED_AFTER"])

    def test_35_ncnn_is_inactive_and_has_no_profile(self):
        self.assertFalse(self.ncnn["ProfileSelected"])
        self.assertFalse(self.ncnn["RuntimeActivationAuthorized"])
        self.assertEqual("NO", self.metrics["PF4582_NCNN_RUNTIME_ACTIVE"])

    def test_36_ncnn_has_exactly_one_allowed_disposition(self):
        allowed = {
            "INCLUDE_AS_OFFICIAL_BLOCKED_PLACEMENT",
            "EXCLUDE_WITH_PROVEN_OFFICIAL_RULE",
            "OFFICIAL_RECORD_PENDING_CLASSIFICATION",
        }
        self.assertIn(self.ncnn["Disposition"], allowed)
        self.assertEqual("INCLUDE_AS_OFFICIAL_BLOCKED_PLACEMENT", self.ncnn["Disposition"])

    def test_37_exclusion_requires_direct_official_consumer_evidence(self):
        with self.assertRaises(reconcile.ReconciliationError):
            reconcile.classify_ncnn(structurally_ordinary=True, official_exclusion_rule={"Rule": "unusual zero"})
        self.assertEqual(
            "EXCLUDE_WITH_PROVEN_OFFICIAL_RULE",
            reconcile.classify_ncnn(
                structurally_ordinary=True,
                official_exclusion_rule={"Rule": "proven rule", "DirectOfficialConsumerEvidence": True},
            ),
        )

    def test_38_unusual_or_zero_fields_do_not_exclude_ncnn(self):
        self.assertEqual("INCLUDE_AS_OFFICIAL_BLOCKED_PLACEMENT", reconcile.classify_ncnn(structurally_ordinary=True))
        self.assertFalse(self.ncnn["OfficialExclusionRuleFound"])

    def test_39_pending_classification_is_fail_closed(self):
        self.assertEqual("OFFICIAL_RECORD_PENDING_CLASSIFICATION", reconcile.classify_ncnn(structurally_ordinary=False))

    def test_40_include_disposition_does_not_authorize_activation(self):
        self.assertEqual("INCLUDE_AS_OFFICIAL_BLOCKED_PLACEMENT", self.ncnn["Disposition"])
        self.assertFalse(self.ncnn["RuntimeActivationAuthorized"])

    def test_41_no_candidate_mapping_or_isre_propagation_occurs(self):
        self.assertEqual("NO", self.report["Safety"]["ISRE_PROPAGATION_PERFORMED"])
        self.assertEqual("NO", self.report["Safety"]["RUNTIME_ACTIVATION_CHANGED"])

    def test_42_every_official_field_is_preserved_in_overlay(self):
        source_by_identity = {record["official_record_identity"]: record for record in self.model["OfficialRecords"]}
        for overlay in self.overlay["Records"]:
            original = {
                key: value
                for key, value in source_by_identity[overlay["OfficialRecordIdentity"]].items()
                if key not in {"official_record_index", "official_record_identity"}
            }
            self.assertEqual(original, overlay["OfficialFields"])

    def test_43_legacy_angle_variances_are_exposed_not_hidden(self):
        differences = [record for record in self.rows if record["FieldDifferences"]]
        self.assertEqual({1008043, 1008044}, {record["SourceNpcId"] for record in differences})
        self.assertTrue(all(record["FieldMatchStatus"] == "DETERMINISTIC_RECORD_CORRESPONDENCE_WITH_LEGACY_ANGLE_VARIANCE" for record in differences))

    def test_44_ncnn_preserves_every_available_official_field(self):
        required = {
            "accepted_manifest_match", "acghash_field_database_offset",
            "acghash_get_hash_as_text", "acghash_raw_bytes_hex", "assistance_radius",
            "database_offset", "district_index", "district_name", "max_level",
            "min_level", "more_flags", "native_flags", "official_scalar_uint32",
            "record_relative_offset", "respawn_chance", "respawn_time",
            "rotation_spawn_point", "serialized_optional_flags", "serialized_size",
            "spawn_index", "unknown_optional_u8",
        }
        self.assertTrue(required.issubset(self.ncnn["OfficialFields"]))

    def test_45_generated_outputs_are_current_and_deterministic(self):
        second = reconcile.build_model()
        self.assertEqual(reconcile.render_json(self.report), reconcile.render_json(second["Report"]))
        self.assertEqual(reconcile.render_json(self.overlay), reconcile.render_json(second["Overlay"]))
        self.assertEqual(reconcile.render_csharp(self.model), reconcile.render_csharp(second))
        self.assertEqual(reconcile.render_markdown(self.report), reconcile.render_markdown(second["Report"]))
        self.assertEqual(reconcile.DEFAULT_REPORT.read_text(encoding="utf-8"), reconcile.render_json(self.report))
        self.assertEqual(reconcile.DEFAULT_OVERLAY.read_text(encoding="utf-8"), reconcile.render_json(self.overlay))
        self.assertEqual(reconcile.DEFAULT_CSHARP.read_text(encoding="utf-8"), reconcile.render_csharp(self.model))
        self.assertEqual(reconcile.DEFAULT_MARKDOWN.read_text(encoding="utf-8"), reconcile.render_markdown(self.report))

    def test_46_no_generation_timestamp_or_machine_path_exists(self):
        serialized = reconcile.render_json(self.report) + reconcile.render_json(self.overlay)
        self.assertNotIn("GeneratedAt", serialized)
        self.assertNotIn("C:\\Users\\Mike", serialized)
        self.assertNotIn("AO stripdown", serialized)

    def test_47_no_official_binary_was_imported(self):
        allowed = {".json", ".md"}
        files = [path for path in reconcile.DEFAULT_OFFICIAL_RECORDS.parent.iterdir() if path.is_file()]
        self.assertTrue(files)
        self.assertTrue(all(path.suffix.lower() in allowed for path in files))

    def test_48_official_catalog_has_no_activation_field(self):
        text = (reconcile.REPOSITORY_ROOT / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportOfficialPlacementCatalog.cs").read_text(encoding="utf-8")
        self.assertNotIn("RuntimeActive", text)
        self.assertNotIn("RuntimeEligible", text)

    def test_49_input_digests_are_sha256_pinned(self):
        self.assertEqual(6, len(self.report["InputDigests"]))
        self.assertTrue(all(len(value) == 64 and int(value, 16) >= 0 for value in self.report["InputDigests"].values()))

    def test_50_generated_artifact_hashes_are_stable(self):
        paths = [reconcile.DEFAULT_REPORT, reconcile.DEFAULT_OVERLAY, reconcile.DEFAULT_CSHARP, reconcile.DEFAULT_MARKDOWN]
        first = [hashlib.sha256(path.read_bytes()).hexdigest() for path in paths]
        second = [hashlib.sha256(path.read_bytes()).hexdigest() for path in paths]
        self.assertEqual(first, second)


if __name__ == "__main__":
    unittest.main()
