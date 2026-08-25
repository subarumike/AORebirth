import copy
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / "Tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import audit_pf4582_template_hashes as audit
import generate_pf4582_placements as placement_generator


class Pf4582TemplateHashAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model = audit.build_audit_model()
        cls.records = cls.model["HashRecords"]
        cls.mapped = [
            record for record in cls.records
            if record["BaselineMappingState"] == "MAPPED"
        ]
        cls.unresolved = [
            record for record in cls.records
            if record["BaselineMappingState"] == "UNRESOLVED"
        ]

    def test_01_exactly_38_hash_records(self):
        self.assertEqual(38, len(self.records))

    def test_02_exactly_14_baseline_mapped_hashes(self):
        self.assertEqual(14, len(self.mapped))

    def test_03_exactly_24_baseline_unresolved_hashes(self):
        self.assertEqual(24, len(self.unresolved))

    def test_04_each_unresolved_hash_has_one_allowed_classification(self):
        self.assertTrue(all(
            record["Classification"] in audit.ALLOWED_CLASSIFICATIONS
            for record in self.unresolved
        ))

    def test_05_classification_totals_sum_to_24(self):
        totals = self.model["Metrics"]
        self.assertEqual(24, sum([
            totals["PF4582_AUDIT_PROVEN"],
            totals["PF4582_AUDIT_CANDIDATE"],
            totals["PF4582_AUDIT_AMBIGUOUS"],
            totals["PF4582_AUDIT_NO_EVIDENCE"],
        ]))

    def test_06_baseline_hashes_have_one_verification_state(self):
        self.assertTrue(all(
            record["BaselineVerificationState"] in audit.ALLOWED_BASELINE_STATES
            for record in self.mapped
        ))

    def test_07_all_206_placements_are_grouped(self):
        self.assertEqual(206, sum(record["PlacementCount"] for record in self.records))

    def test_08_all_206_npc_ids_are_represented_once(self):
        npc_ids = [npc_id for record in self.records for npc_id in record["NpcIds"]]
        self.assertEqual(206, len(npc_ids))
        self.assertEqual(206, len(set(npc_ids)))

    def test_09_placement_counts_sum_to_206(self):
        self.assertEqual(206, sum(record["PlacementCount"] for record in self.records))

    def test_10_blocked_accounting_preserves_the_real_hash_boundary(self):
        unresolved_blocked = sum(record["BlockedPlacementCount"] for record in self.unresolved)
        mapped_blocked = sum(record["BlockedPlacementCount"] for record in self.mapped)
        self.assertEqual(171, unresolved_blocked)
        self.assertEqual(10, mapped_blocked)
        self.assertEqual(181, unresolved_blocked + mapped_blocked)

    def test_11_existing_active_placement_count_remains_25(self):
        self.assertEqual(25, sum(
            record["ExistingRuntimeActivePlacementCount"] for record in self.records
        ))

    def test_12_no_audit_record_authorizes_runtime_activation(self):
        self.assertTrue(all(
            record["RuntimeActivationAllowed"] is False for record in self.records
        ))

    def test_13_source_hash_values_round_trip_exactly(self):
        tokens = audit.parse_source_hash_tokens(audit.DEFAULT_SOURCE)
        originals = {
            record["TemplateHashOriginal"] for record in self.records
        }
        self.assertEqual(originals, set(tokens.values()))
        for original in originals:
            self.assertEqual(int(original), int(audit.canonical_template_hash(original), 16))

    def test_14_signed_unsigned_formatting_does_not_collide(self):
        canonicals = [record["TemplateHashCanonical"] for record in self.records]
        self.assertEqual(len(canonicals), len(set(canonicals)))
        self.assertEqual("0xFFFFFFFF", audit.canonical_template_hash(-1))
        self.assertEqual("0xFFFFFFFF", audit.canonical_template_hash(4294967295))

    def test_15_dynamic_names_are_preserved_exactly(self):
        actual = {
            name for record in self.records for name in record["DynamicNamesPresent"]
        }
        self.assertEqual({
            "Dreadknot the Toxictwister",
            "Burntooth the Inferno Muddevil",
            "Sparkletail the Jolly Wrecker",
            "Malicespine the Wasteland Roller",
            "Oozefoot the Noxious Malle",
            "Chipmind the Overclocked182-T1",
            "Sparky the Stabber",
        }, actual)

    def test_16_name_only_evidence_cannot_produce_proven(self):
        self.assertEqual("CANDIDATE", audit.decide_classification(["profile:A"]))

    def test_17_coordinate_only_evidence_cannot_produce_proven(self):
        self.assertEqual("NO_EVIDENCE", audit.decide_classification([]))

    def test_18_level_only_evidence_cannot_produce_proven(self):
        self.assertEqual("NO_EVIDENCE", audit.decide_classification([]))

    def test_19_contradictory_candidates_produce_ambiguous(self):
        self.assertEqual(
            "AMBIGUOUS",
            audit.decide_classification(["profile:A"], contradictory_evidence=["conflict"]),
        )

    def test_20_missing_direct_evidence_prevents_proven(self):
        self.assertEqual(
            "CANDIDATE",
            audit.decide_classification(["profile:A"], resolved_profile="profile:A"),
        )

    def test_21_proven_requires_direct_evidence(self):
        synthetic = {
            "Classification": "PROVEN",
            "BaselineVerificationState": "NOT_APPLICABLE",
            "CandidateAoRebirthProfiles": ["profile:A"],
            "ResolvedAoRebirthProfile": "profile:A",
            "DirectEvidence": [{"RecordId": "stable:1"}],
            "ContradictoryEvidence": [],
            "Rationale": "Unique stable identifier.",
            "RemainingBlockers": [],
            "RuntimeActivationAllowed": False,
        }
        audit.validate_audit_record(synthetic)
        synthetic["DirectEvidence"] = []
        with self.assertRaises(audit.AuditError):
            audit.validate_audit_record(synthetic)

    def test_22_every_classification_has_a_rationale(self):
        self.assertTrue(all(record["Rationale"] for record in self.records))

    def test_23_every_non_proven_unresolved_result_has_blockers(self):
        self.assertTrue(all(
            record["RemainingBlockers"]
            for record in self.unresolved
            if record["Classification"] != "PROVEN"
        ))

    def test_24_output_ordering_is_deterministic(self):
        canonicals = [record["TemplateHashCanonical"] for record in self.records]
        self.assertEqual(sorted(canonicals), canonicals)
        ranking = self.model["UnresolvedImpactRanking"]
        rank_keys = [(-item["PlacementCount"], item["TemplateHashCanonical"]) for item in ranking]
        self.assertEqual(sorted(rank_keys), rank_keys)

    def test_25_second_generation_is_byte_identical(self):
        first = audit.build_audit_model()
        second = audit.build_audit_model()
        self.assertEqual(audit.render_json(first), audit.render_json(second))
        self.assertEqual(audit.render_markdown(first), audit.render_markdown(second))

    def test_26_malformed_evidence_fails_closed(self):
        ledger = json.loads(audit.DEFAULT_EVIDENCE_LEDGER.read_text(encoding="utf-8"))
        ledger["unresolvedAssessments"][0]["assessmentBasis"] = "PROVEN_BY_NAME"
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "malformed-ledger.json"
            path.write_text(json.dumps(ledger), encoding="utf-8")
            with self.assertRaises(audit.AuditError):
                audit.build_audit_model(ledger_path=path)

    def test_27_missing_governed_input_fails_closed(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            missing = Path(temp_dir) / "missing-source.json"
            with self.assertRaises(audit.AuditError):
                audit.build_audit_model(source_path=missing)

    def test_28_existing_placement_generator_validation_passes(self):
        model = placement_generator.build_model()
        self.assertEqual(206, len(model["records"]))
        self.assertEqual(25, sum(record["RuntimeEligible"] for record in model["records"]))

    def test_29_duplicate_position_records_remain_preserved(self):
        placement_model = placement_generator.build_model()
        duplicate_ids = audit._duplicate_positions(placement_model["records"])
        self.assertEqual(14, len(duplicate_ids))
        represented = {
            npc_id
            for record in self.records
            if record["DuplicatePositionParticipation"]
            for npc_id in record["NpcIds"]
            if npc_id in duplicate_ids
        }
        self.assertEqual(duplicate_ids, represented)

    def test_30_report_generation_does_not_change_runtime_source(self):
        before = hashlib.sha256(audit.DEFAULT_RUNTIME_SOURCE.read_bytes()).hexdigest()
        audit.build_audit_model()
        after = hashlib.sha256(audit.DEFAULT_RUNTIME_SOURCE.read_bytes()).hexdigest()
        self.assertEqual(before, after)

    def test_31_expected_classification_totals_are_locked(self):
        metrics = self.model["Metrics"]
        self.assertEqual(0, metrics["PF4582_AUDIT_PROVEN"])
        self.assertEqual(17, metrics["PF4582_AUDIT_CANDIDATE"])
        self.assertEqual(1, metrics["PF4582_AUDIT_AMBIGUOUS"])
        self.assertEqual(6, metrics["PF4582_AUDIT_NO_EVIDENCE"])

    def test_32_all_baseline_mappings_are_governance_proven(self):
        self.assertTrue(all(
            record["BaselineVerificationState"] == "BASELINE_PROVEN"
            and record["DirectEvidence"]
            for record in self.mapped
        ))

    def test_33_required_record_fields_are_present(self):
        required = {
            "TemplateHashOriginal", "TemplateHashCanonical", "BaselineMappingState",
            "BaselineAoRebirthProfile", "PlacementCount", "BlockedPlacementCount",
            "NpcIds", "SourceNames", "DynamicNamesPresent", "SourceLevelMinimum",
            "SourceLevelMaximum", "DuplicatePositionParticipation",
            "CandidateAoRebirthProfiles", "Classification", "ResolvedAoRebirthProfile",
            "DirectEvidence", "CorroboratingEvidence", "ContradictoryEvidence",
            "EvidencePaths", "EvidenceRecordIds", "CaptureIds",
            "EvidenceDigestsWhereAvailable", "Rationale", "RemainingBlockers",
            "UnlockPotential", "RuntimeActivationAllowed",
        }
        self.assertTrue(all(required <= set(record) for record in self.records))

    def test_34_evidence_paths_are_repository_relative(self):
        for record in self.records:
            for path in record["EvidencePaths"]:
                self.assertFalse(Path(path).is_absolute(), path)
                self.assertNotIn("\\", path)

    def test_35_generated_outputs_match_the_current_model(self):
        self.assertEqual(
            audit.render_json(self.model),
            audit.DEFAULT_JSON_OUTPUT.read_text(encoding="utf-8"),
        )
        self.assertEqual(
            audit.render_markdown(self.model),
            audit.DEFAULT_MARKDOWN_OUTPUT.read_text(encoding="utf-8"),
        )

    def test_36_generated_outputs_have_no_unstable_timestamp(self):
        self.assertNotIn("generatedUtc", audit.render_json(self.model))
        self.assertNotIn("generatedUtc", audit.render_markdown(self.model))

    def test_37_unknown_candidate_profile_fails_closed(self):
        ledger = json.loads(audit.DEFAULT_EVIDENCE_LEDGER.read_text(encoding="utf-8"))
        ledger["unresolvedAssessments"][0]["candidateProfileKeys"] = ["mobtemplate:UNKNOWN"]
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "unknown-profile-ledger.json"
            path.write_text(json.dumps(ledger), encoding="utf-8")
            with self.assertRaises(audit.AuditError):
                audit.build_audit_model(ledger_path=path)

    def test_38_all_accepted_pf4582_capture_ids_are_recorded_once(self):
        scope = self.model["AcceptedPf4582CaptureSearchScope"]
        capture_ids = [record["CaptureId"] for record in scope]
        self.assertEqual(25, len(capture_ids))
        self.assertEqual(25, len(set(capture_ids)))
        self.assertEqual(sorted(capture_ids), capture_ids)

    def test_39_text_digest_is_stable_across_checkout_line_endings(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            lf_path = Path(temp_dir) / "inventory-lf.csv"
            crlf_path = Path(temp_dir) / "inventory-crlf.csv"
            lf_path.write_bytes(b"CaptureId,Status\n4582,accepted\n")
            crlf_path.write_bytes(b"CaptureId,Status\r\n4582,accepted\r\n")

            self.assertNotEqual(audit.sha256_file(lf_path), audit.sha256_file(crlf_path))
            self.assertEqual(
                audit.sha256_governed_input(lf_path, "text-lf"),
                audit.sha256_governed_input(crlf_path, "text-lf"),
            )

    def test_40_capture_dossier_fixture_is_stable_and_minimal(self):
        source = {
            "generatedUtc": "ignored",
            "enemies": [
                {
                    "identity": "(SimpleChar:2)",
                    "name": "Second",
                    "monsterData": "22",
                    "monsterScale": "90",
                    "npcFamily": "3",
                    "level": 2,
                    "currentHealth": 1,
                },
                {
                    "identity": "(SimpleChar:1)",
                    "name": "First",
                    "monsterData": "11",
                    "monsterScale": "80",
                    "npcFamily": "4",
                    "level": 1,
                    "currentHealth": 99,
                },
            ],
        }
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "enemy-dossier.json"
            path.write_text(json.dumps(source), encoding="utf-8")
            fixture = audit.build_capture_dossier_fixture(path)

        self.assertEqual(2, fixture["enemyCount"])
        self.assertEqual(
            ["(SimpleChar:1)", "(SimpleChar:2)"],
            [enemy["identity"] for enemy in fixture["enemies"]],
        )
        self.assertNotIn("generatedUtc", fixture)
        self.assertNotIn("currentHealth", fixture["enemies"][0])


if __name__ == "__main__":
    unittest.main()
