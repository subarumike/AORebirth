"""Offline integrity regression tests; never mutate the retained evidence corpus."""
import base64
import copy
import gzip
import hashlib
import json
import pathlib
import struct
import tempfile
import unittest
from collections import Counter, defaultdict
from unittest import mock

import mission_destination_duplicate_audit as audit
import mission_duplicate_catalog as catalog
import mission_duplicate_sources as sources
from acgentrance_mission_decoder import decode_cohort


ROOT = pathlib.Path(__file__).resolve().parents[1]
GENERATED = ROOT / "docs/generated/missions/destination-duplicate-audit"


def jsonl(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as stream:
        for line in stream:
            yield json.loads(line)


def offer_fixture(**overrides):
    row = {
        "session_id": "session-a", "source_line": 3,
        "request_id": "request-a", "cohort_id": "cohort-a", "offer_index": 0,
        "raw_offer_hash": "offer-bytes-a", "decoded_offer_hash": "decoded-a",
        "raw_response_sha256": "packet-a", "offer_start": 51, "offer_end": 100,
    }
    row.update(overrides)
    return row


class IndependentCorpusTests(unittest.TestCase):
    """Recount journals directly, rather than trusting generated summary totals."""

    @classmethod
    def setUpClass(cls):
        manifest_path = ROOT / "docs/generated/missions/location-reconciliation/source-manifest.json"
        cls.manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        cls.placements = list(jsonl(ROOT / "docs/generated/missions/acgentrance-reconstruction/acgentrance-records.jsonl"))
        position_index = defaultdict(list)
        for placement in cls.placements:
            bits = struct.pack(">3f", *placement["raw_position_components"]).hex()
            position_index[(placement["explicit_playfield_id"], bits)].append(
                (placement["identity_type"], placement["identity_instance_uint32"]))
        cls.counts = Counter()
        cls.offer_keys = set()
        cls.verified_offers = {}
        cls.destinations = set()
        cls.playfields = set()
        cls.range_errors = []
        cls.repeated_destination_cohorts = []
        cls.empty_raw_cohorts = []
        cls.destination_bins = Counter()
        cls.session_ids = set()
        cls.manifest_hash_errors = []
        retained = ROOT / "docs/reference/missions/modern-capture/level2-slider-discovery/raw"
        cls.example_packet = None
        for source in cls.manifest:
            sid = source["session_id"]
            cls.session_ids.add(sid)
            path = retained / sid / "events.jsonl"
            if not path.exists():
                path = pathlib.Path(source["path"])
            raw_journal = path.read_bytes()
            if hashlib.sha256(raw_journal).hexdigest() != source["sha256"]:
                cls.manifest_hash_errors.append(sid)
            for source_line, line in enumerate(raw_journal.splitlines(), 1):
                event = json.loads(line)
                cls.counts["events"] += 1
                if event["event_type"] == "request_started":
                    cls.counts["requests"] += 1
                if event["event_type"] != "cohort_received":
                    continue
                cls.counts["cohorts"] += 1
                payload = event["payload"]
                offers = payload["offers"]
                rid = event.get("request_id")
                cid = payload.get("cohort_id") or f"{rid}/line/{source_line}"
                for offer in offers:
                    key = (sid, source_line, rid, cid, offer["offer_index"])
                    cls.counts["offers"] += 1
                    if key in cls.offer_keys:
                        cls.counts["duplicate_offer_keys"] += 1
                    cls.offer_keys.add(key)
                packet = payload.get("raw_response_packet") or {}
                encoded = packet.get("base64")
                if not encoded:
                    cls.counts["missing_raw_offers"] += len(offers)
                    continue
                raw = base64.b64decode(encoded, validate=True)
                cls.counts["raw_cohorts"] += 1
                cls.counts["raw_offers"] += len(offers)
                if packet.get("sha256") != hashlib.sha256(raw).hexdigest():
                    cls.range_errors.append((sid, source_line, "RAW_HASH_MISMATCH"))
                decoded = decode_cohort(raw, offers)["offers"]
                if not offers:
                    cls.empty_raw_cohorts.append({"session_id": sid, "source_line": source_line,
                                                  "sha256": hashlib.sha256(raw).hexdigest(),
                                                  "packet_length": len(raw), "decoded_offers": decoded})
                if cls.example_packet is None:
                    cls.example_packet = (raw, decoded)
                # Independent ownership checks do not rely on the new auditor.
                indexes, byte_ranges = set(), set()
                next_start = 51
                selected = []
                for row in decoded:
                    current = (row["offer_start"], row["offer_end"])
                    if row["offer_index"] in indexes or current in byte_ranges:
                        cls.range_errors.append((sid, source_line, "REPEATED_BYTE_OWNER"))
                    if current[0] != next_start or not current[0] < current[1] <= len(raw):
                        cls.range_errors.append((sid, source_line, "NONCONTIGUOUS_BYTE_RANGE"))
                    indexes.add(row["offer_index"])
                    byte_ranges.add(current)
                    next_start = current[1]
                    world = row["worldpos"]
                    pf = world["playfield_identity"]["instance"]
                    candidates = position_index[(pf, world["local_float32_hex"])]
                    if len(candidates) != 1:
                        cls.range_errors.append((sid, source_line, "NONUNIQUE_EXACT_DESTINATION"))
                    else:
                        selected.append(candidates[0])
                        cls.destinations.add(candidates[0])
                        cls.playfields.add(pf)
                        cls.counts["exact_observations"] += 1
                        key = (sid, source_line, rid, cid, row["offer_index"])
                        cls.verified_offers[key] = {
                            "range": current, "raw_offer_hash": hashlib.sha256(raw[current[0]:current[1]]).hexdigest(),
                            "packet_hash": hashlib.sha256(raw).hexdigest(), "destination": candidates[0]}
                if next_start != len(raw) or len(decoded) != len(offers):
                    cls.range_errors.append((sid, source_line, "INCOMPLETE_PACKET_OWNERSHIP"))
                if len(offers) == 5:
                    unique = len(set(selected))
                    cls.destination_bins[unique] += 1
                    if unique < 5:
                        cls.repeated_destination_cohorts.append((sid, source_line, 5 - unique))

    def test_manifest_reconstructs_77_logical_sessions_without_copy_inflation(self):
        self.assertEqual(len(self.manifest), 77)
        self.assertEqual(len(self.session_ids), 77)
        self.assertEqual(self.manifest_hash_errors, [])

    def test_all_offer_provenance_keys_are_unique(self):
        self.assertEqual(self.counts["offers"], 93185)
        self.assertEqual(len(self.offer_keys), 93185)
        self.assertEqual(self.counts["duplicate_offer_keys"], 0)

    def test_raw_offers_own_exactly_one_nonoverlapping_byte_range(self):
        self.assertEqual(self.range_errors, [])
        self.assertEqual(self.counts["raw_cohorts"], 18567)
        self.assertEqual(sum(self.destination_bins.values()), 18566)
        self.assertEqual(self.counts["raw_offers"], 92830)

    def test_empty_raw_response_remains_a_cohort_without_invented_offers(self):
        self.assertEqual(len(self.empty_raw_cohorts), 1)
        empty = self.empty_raw_cohorts[0]
        self.assertEqual(empty["session_id"], "mission-20260903T030938856Z-32279304-2c007c1c")
        self.assertEqual(empty["sha256"], "50276c5a1eafc50460bc91e3649d18750ac65faa2241bbfe0914b60ee0d9f9fe")
        self.assertEqual(empty["packet_length"], 51)
        self.assertEqual(empty["decoded_offers"], [])
        self.assertEqual(self.counts["cohorts"], 18638)

    def test_exact_observation_and_missing_raw_populations_remain_separate(self):
        self.assertEqual(self.counts["exact_observations"], 92830)
        self.assertEqual(self.counts["missing_raw_offers"], 355)
        self.assertEqual(self.counts["raw_offers"] + self.counts["missing_raw_offers"], self.counts["offers"])

    def test_observed_identity_set_is_independently_recomputed(self):
        self.assertEqual(len(self.destinations), 812)
        self.assertEqual(len(self.playfields), 22)

    def test_complete_client_placement_identities_are_unique(self):
        keys = [(row["identity_type"], row["identity_instance_uint32"]) for row in self.placements]
        self.assertEqual(len(keys), 2242)
        self.assertEqual(len(set(keys)), 2242)

    def test_exact_same_playfield_bit_coordinates_are_unique(self):
        groups = defaultdict(list)
        for row in self.placements:
            bits = struct.pack(">3f", *row["raw_position_components"]).hex()
            groups[(row["explicit_playfield_id"], bits)].append((row["identity_type"], row["identity_instance_uint32"]))
        self.assertEqual([values for values in groups.values() if len(set(values)) > 1], [])

    def test_repeated_names_preserve_all_identities(self):
        names = defaultdict(set)
        for row in self.placements:
            names[row["display_name_exact"]].add((row["identity_type"], row["identity_instance_uint32"]))
        self.assertEqual(len(names), 371)
        self.assertEqual(sum(len(values) > 1 for values in names.values()), 120)
        self.assertEqual(sum(map(len, names.values())), 2242)

    def test_server_cohort_destination_repeats_are_preserved(self):
        self.assertEqual(dict(self.destination_bins), {5: 15888, 4: 2481, 3: 185, 2: 12})
        self.assertEqual(len(self.repeated_destination_cohorts), 2678)
        self.assertEqual(sum(value[2] for value in self.repeated_destination_cohorts), 2887)
        self.assertEqual(sum(self.destination_bins.values()) * 5, 92830)

    def test_real_packet_passes_new_range_validator(self):
        raw, decoded = self.example_packet
        self.assertEqual(audit.audit_offer_ranges(raw, decoded), [])

    def test_generated_offer_ledger_matches_independent_journal_byte_ownership(self):
        keys, exact, unresolved = set(), 0, 0
        for row in jsonl(GENERATED / "offer-audit-inventory.jsonl.gz"):
            key = (row["session_id"], row["source_line"], row["request_id"], row["cohort_id"], row["offer_index"])
            self.assertNotIn(key, keys)
            keys.add(key)
            expected = self.verified_offers.get(key)
            if expected is None:
                unresolved += 1
                self.assertIsNone(row["destination_identity"])
                self.assertIsNone(row["raw_offer_hash"])
                self.assertFalse(row["raw_byte_range_verified"])
            else:
                exact += 1
                self.assertEqual((row["offer_start"], row["offer_end"]), expected["range"])
                self.assertEqual(row["raw_offer_hash"], expected["raw_offer_hash"])
                self.assertEqual(row["raw_response_sha256"], expected["packet_hash"])
                self.assertTrue(row["raw_byte_range_verified"])
                self.assertEqual((row["destination_identity"]["type"], row["destination_identity"]["instance"]), expected["destination"])
        self.assertEqual(keys, self.offer_keys)
        self.assertEqual((exact, unresolved), (92830, 355))

    def test_generated_destination_counts_retain_all_observations(self):
        rows = list(jsonl(GENERATED / "destination-observation-counts.jsonl.gz"))
        identities = {(row["destination_identity"]["type"], row["destination_identity"]["instance"]) for row in rows}
        self.assertEqual(identities, self.destinations)
        self.assertEqual(sum(row["observation_count"] for row in rows), 92830)
        self.assertEqual(len(rows), 812)
        self.assertTrue(all(row["observation_count"] >= row["cohort_count"] >= row["session_count"] for row in rows))

    def test_generated_repeat_report_points_to_distinct_actual_offer_ranges(self):
        count, positions = 0, 0
        for row in jsonl(GENERATED / "same-cohort-destination-repeats.jsonl.gz"):
            count += 1
            positions += row["repeated_offer_positions"]
            self.assertEqual(row["classification"], "LEGITIMATE_SERVER_COHORT_DESTINATION_REPEAT")
            self.assertTrue(row["disjoint_byte_ranges_verified"])
            for repeated in row["repeated_destinations"]:
                indexes, byte_ranges = set(), set()
                for offer in repeated["offers"]:
                    key = (row["session_id"], row["source_line"], row["request_id"], row["cohort_id"], offer["offer_index"])
                    expected = self.verified_offers[key]
                    indexes.add(offer["offer_index"])
                    byte_ranges.add((offer["offer_start"], offer["offer_end"]))
                    self.assertEqual(offer["raw_offer_hash"], expected["raw_offer_hash"])
                    self.assertEqual((offer["offer_start"], offer["offer_end"]), expected["range"])
                self.assertEqual(len(indexes), len(repeated["offers"]))
                self.assertEqual(len(byte_ranges), len(repeated["offers"]))
                ordered = sorted(byte_ranges)
                self.assertTrue(all(previous[1] <= following[0] for previous, following in zip(ordered, ordered[1:])))
        self.assertEqual((count, positions), (2678, 2887))

    def test_generated_summary_counts_match_independent_corpus(self):
        summary = json.loads((GENERATED / "duplicate-audit-summary.json").read_text(encoding="utf-8"))
        expected = {"LOGICAL_UNIQUE_SESSIONS": len(self.session_ids), "TOTAL_OFFERS": self.counts["offers"],
                    "TOTAL_COHORTS": self.counts["cohorts"], "RAW_BACKED_OFFERS": self.counts["raw_offers"],
                    "MISSING_RAW_OFFERS": self.counts["missing_raw_offers"],
                    "EXACT_DESTINATION_OBSERVATIONS": self.counts["exact_observations"],
                    "UNIQUE_OBSERVED_DESTINATION_IDENTITIES": len(self.destinations),
                    "UNIQUE_OBSERVED_DESTINATION_PLAYFIELDS": len(self.playfields),
                    "CLIENT_PLACEMENTS": len(self.placements), "PARSER_DUPLICATION_BUGS": len(self.range_errors),
                    "COHORTS_WITH_LEGITIMATE_DESTINATION_REPEATS": len(self.repeated_destination_cohorts)}
        for field, value in expected.items():
            self.assertEqual(summary[field], value, field)
        for field in ("HISTORICAL_FILES_MODIFIED", "RUNTIME_MISSION_LOGIC_CHANGED", "DESTINATION_DATA_DEDUPLICATED"):
            self.assertEqual(summary[field], "NO")
        self.assertEqual(summary["LOGICAL_OFFERS_AFTER_PROVEN_DEDUPLICATION"], 93185)
        self.assertEqual(summary["EXACT_OBSERVATIONS_AFTER_PROVEN_DEDUPLICATION"], 92830)
        self.assertEqual(summary["UNIQUE_DESTINATIONS_AFTER_PROVEN_DEDUPLICATION"], 812)

    def test_generated_artifact_composite_keys_are_unique(self):
        report = json.loads((GENERATED / "artifact-primary-key-audit.json").read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(report["artifacts"]), 14)
        for artifact in report["artifacts"]:
            self.assertEqual(artifact["duplicate_primary_keys"], 0, artifact["path"])
            self.assertEqual(artifact["row_count"], artifact["unique_primary_keys"], artifact["path"])
            self.assertEqual(artifact["issues"], [], artifact["path"])

    def test_generated_manifest_hashes_and_gzip_headers_match_written_bytes(self):
        manifest = json.loads((GENERATED / "duplicate-audit-manifest.json").read_text(encoding="utf-8"))
        self.assertTrue(manifest["audit_only"])
        self.assertFalse(manifest["historical_inputs_rewritten"])
        for relative, expected in manifest["outputs"].items():
            self.assertFalse(pathlib.Path(relative).is_absolute(), relative)
            data = (ROOT / relative).read_bytes()
            self.assertEqual(hashlib.sha256(data).hexdigest(), expected["sha256"], relative)
            self.assertEqual(len(data), expected["bytes"], relative)
            if relative.endswith(".gz"):
                self.assertEqual(data[4:8], bytes(4), relative)


class DuplicateFixtureTests(unittest.TestCase):
    def test_logical_key_retains_all_provenance_dimensions(self):
        row = offer_fixture()
        self.assertEqual(audit.offer_key(row), ("session-a", 3, "request-a", "cohort-a", 0))
        for field in ("session_id", "source_line", "request_id", "cohort_id", "offer_index"):
            other = dict(row)
            other[field] = "changed"
            self.assertNotEqual(audit.offer_key(row), audit.offer_key(other))

    def test_deliberate_duplicate_offer_record_is_reported(self):
        row = offer_fixture()
        findings = audit.classify_offer_duplicates([row, dict(row)])
        self.assertTrue(any(value["classification"] == "EXACT_RAW_DUPLICATE_SAME_LOGICAL_KEY" for value in findings))
        self.assertTrue(any(value["severity"] == "ACCIDENTAL_DOUBLE_INGESTION" for value in findings))

    def test_independent_roll_identical_result_is_not_count_reducing(self):
        first = offer_fixture()
        second = offer_fixture(source_line=7, request_id="request-b", cohort_id="cohort-b")
        findings = audit.classify_offer_duplicates([first, second])
        self.assertTrue(findings)
        self.assertFalse(any(value["severity"] in {"ACCIDENTAL_DOUBLE_INGESTION", "PARSER_DUPLICATION_BUG"} for value in findings))

    def test_missing_raw_duplicate_cannot_claim_exact_raw_equality(self):
        row = offer_fixture(raw_offer_hash=None, raw_response_sha256=None, offer_start=None, offer_end=None)
        findings = audit.classify_offer_duplicates([row, dict(row)])
        self.assertFalse(any(value["classification"] == "EXACT_RAW_DUPLICATE_SAME_LOGICAL_KEY" for value in findings))
        self.assertTrue(any(value["classification"] == "MISSING_RAW_UNRESOLVED" for value in findings))

    def test_same_cohort_occurrence_double_emission_is_count_reducing(self):
        row = {"session_id": "session-a", "source_line": 3, "request_id": "request-a", "cohort_id": "cohort-a",
               "raw_response_sha256": "packet-a", "decoded_cohort_hash": "decoded-a"}
        findings = audit.classify_cohort_duplicates([row, dict(row)])
        self.assertTrue(any(value["severity"] == "ACCIDENTAL_DOUBLE_INGESTION" for value in findings))

    def test_exact_cohort_event_copied_to_another_line_is_proven_duplication(self):
        row = {"session_id": "session-a", "source_line": 3, "request_id": "request-a", "cohort_id": "cohort-a",
               "source_row_sha256": "exact-source-event", "raw_response_sha256": "packet-a", "decoded_cohort_hash": "decoded-a"}
        copied = dict(row, source_line=7)
        findings = audit.classify_cohort_duplicates([row, copied])
        self.assertTrue(any(value["severity"] == "ACCIDENTAL_DOUBLE_INGESTION" for value in findings))
        offers = [offer_fixture(source_row_sha256="exact-source-event"),
                  offer_fixture(source_line=7, source_row_sha256="exact-source-event")]
        self.assertEqual(audit.proposed_duplicate_positions(offers), {1})

    def test_duplicate_removal_proposal_unions_overlapping_proof_classes(self):
        first = offer_fixture(source_row_sha256="exact-source-event")
        rows = [first, dict(first), dict(first, source_line=7), dict(first, offer_index=1),
                offer_fixture(source_line=11, request_id="request-b", cohort_id="cohort-b",
                              source_row_sha256="independent-source-event")]
        original = copy.deepcopy(rows)
        self.assertEqual(audit.proposed_duplicate_positions(rows), {1, 2, 3})
        self.assertEqual(rows, original)

    def test_missing_raw_observations_with_distinct_provenance_are_not_proposed_for_removal(self):
        first = offer_fixture(raw_offer_hash=None, raw_response_sha256=None, offer_start=None,
                              offer_end=None, source_row_sha256="event-a")
        second = dict(first, source_line=7, request_id="request-b", cohort_id="cohort-b", source_row_sha256="event-b")
        self.assertEqual(audit.proposed_duplicate_positions([first, second]), set())
        findings = audit.classify_offer_duplicates([first, second])
        self.assertTrue(any(value["classification"] == "MISSING_RAW_UNRESOLVED" and value["record_count"] == 2 for value in findings))

    def test_distinct_requests_identical_raw_cohorts_are_not_automatically_removed(self):
        first = {"session_id": "session-a", "source_line": 3, "request_id": "request-a", "cohort_id": "cohort-a",
                 "raw_response_sha256": "packet-a", "decoded_cohort_hash": "decoded-a"}
        second = dict(first, source_line=7, request_id="request-b", cohort_id="cohort-b")
        findings = audit.classify_cohort_duplicates([first, second])
        self.assertTrue(any(value["classification"] == "SAME_RAW_DIFFERENT_COHORT_ID" for value in findings))
        self.assertFalse(any(value["severity"] in {"ACCIDENTAL_DOUBLE_INGESTION", "PARSER_DUPLICATION_BUG"} for value in findings))

    def test_same_cohort_id_conflicting_raw_payloads_remain_conflicts(self):
        first = {"session_id": "session-a", "source_line": 3, "request_id": "request-a", "cohort_id": "cohort-a",
                 "raw_response_sha256": "packet-a", "decoded_cohort_hash": "decoded-a"}
        second = dict(first, source_line=7, raw_response_sha256="packet-b", decoded_cohort_hash="decoded-b")
        findings = audit.classify_cohort_duplicates([first, second])
        conflicts = [value for value in findings if value["classification"] == "SAME_COHORT_ID_DIFFERENT_RAW"]
        self.assertEqual(len(conflicts), 1)
        self.assertEqual(conflicts[0]["severity"], "CONFLICTING_SOURCE_DATA")
        self.assertFalse(any(value["severity"] == "ACCIDENTAL_DOUBLE_INGESTION" for value in findings))

    def test_deliberate_parser_double_emission_is_detected(self):
        first = {"offer_index": 0, "offer_start": 51, "offer_end": 76}
        second = {"offer_index": 1, "offer_start": 51, "offer_end": 76}
        findings = audit.audit_offer_ranges(bytes(76), [first, second])
        self.assertTrue(findings)
        self.assertTrue(any(value["severity"] == "PARSER_DUPLICATION_BUG" for value in findings))

    def test_same_byte_owner_is_not_also_labeled_legitimate_repeat(self):
        first = offer_fixture()
        second = offer_fixture(offer_index=1)
        findings = audit.classify_offer_duplicates([first, second])
        self.assertTrue(any(value["severity"] == "PARSER_DUPLICATION_BUG" for value in findings))
        self.assertFalse(any(value["severity"] == "LEGITIMATE_REPEATED_OBSERVATION" for value in findings))

    def test_overlapping_ranges_are_detected_without_deduplicating(self):
        rows = [{"offer_index": 0, "offer_start": 51, "offer_end": 76},
                {"offer_index": 1, "offer_start": 70, "offer_end": 100}]
        original = copy.deepcopy(rows)
        self.assertTrue(audit.audit_offer_ranges(bytes(100), rows))
        self.assertEqual(rows, original)

    def test_distinct_ranges_are_valid_even_if_bytes_are_equal(self):
        rows = [{"offer_index": 0, "offer_start": 51, "offer_end": 76},
                {"offer_index": 1, "offer_start": 76, "offer_end": 101}]
        self.assertEqual(audit.audit_offer_ranges(bytes(101), rows), [])

    def test_gzip_generation_is_byte_deterministic_and_has_no_timestamp(self):
        rows = [{"name": "Ænima HQ", "position": [0.0, -0.0, 1.5]}, {"id": 2}]
        first = audit.deterministic_jsonl_gzip(rows)
        second = audit.deterministic_jsonl_gzip(copy.deepcopy(rows))
        self.assertEqual(first, second)
        self.assertEqual(first[4:8], bytes(4))
        self.assertEqual([json.loads(line) for line in gzip.decompress(first).splitlines()], rows)

    def test_check_mode_rejects_stale_artifact_without_writing(self):
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "artifact.json"
            path.write_bytes(b"old bytes\n")
            with self.assertRaisesRegex(ValueError, "STALE_ARTIFACT"):
                audit.write_or_check(path, b"new bytes\n", check=True)
            self.assertEqual(path.read_bytes(), b"old bytes\n")
            audit.write_or_check(path, b"old bytes\n", check=True)
            missing = pathlib.Path(directory) / "missing.json"
            with self.assertRaisesRegex(ValueError, "STALE_ARTIFACT"):
                audit.write_or_check(missing, b"new bytes\n", check=True)
            self.assertFalse(missing.exists())

    def test_repository_paths_do_not_embed_active_checkout(self):
        path = ROOT / "docs/generated/missions/destination-duplicate-audit/example.json"
        self.assertEqual(audit.stable_path(path, root=ROOT), "docs/generated/missions/destination-duplicate-audit/example.json")


class SourceFixtureTests(unittest.TestCase):
    @staticmethod
    def event(event_type, request_id=None, payload=None, timestamp="2026-09-02T00:00:00Z"):
        return {"session_id": "session-a", "event_type": event_type,
                "request_id": request_id, "timestamp_utc": timestamp, "payload": payload or {}}

    @staticmethod
    def analyze(events):
        rows = [(index, sources.digest(event), event, sources.digest(event))
                for index, event in enumerate(events, 1)]
        return sources.analyze_event_rows(rows, "session-a", "fixture/events.jsonl")

    def test_identical_request_inputs_with_distinct_ids_are_distinct_rolls(self):
        inputs = {"sliders": {"difficulty": 1}, "character_level": 2}
        result = self.analyze([
            self.event("session_started"),
            self.event("request_started", "request-a", inputs),
            self.event("request_started", "request-b", inputs),
        ])
        self.assertEqual(result["metadata"]["request_count"], 2)
        self.assertEqual(result["request_findings"], [])

    def test_request_lifecycle_phases_are_not_reused_id_conflicts(self):
        result = self.analyze([
            self.event("session_started"),
            self.event("request_started", "request-a", {"phase": "start"}),
            self.event("request_transmitted", "request-a", {"phase": "transmitted"}),
        ])
        self.assertEqual(result["metadata"]["request_count"], 1)
        self.assertEqual(result["request_findings"], [])

    def test_duplicate_request_row_is_detected(self):
        request = self.event("request_started", "request-a", {"sliders": {"difficulty": 1}})
        result = self.analyze([self.event("session_started"), request, copy.deepcopy(request)])
        kinds = {value["classification"] for value in result["request_findings"]}
        self.assertIn("EXACT_DUPLICATE_REQUEST_RECORD", kinds)
        self.assertIn("REUSED_REQUEST_ID_IDENTICAL", kinds)

    def test_conflicting_request_payloads_are_retained(self):
        result = self.analyze([
            self.event("session_started"),
            self.event("request_started", "request-a", {"sliders": {"difficulty": 1}}),
            self.event("request_started", "request-a", {"sliders": {"difficulty": 2}}),
        ])
        findings = [value for value in result["request_findings"] if value["classification"] == "REUSED_REQUEST_ID_CONFLICT"]
        self.assertEqual(len(findings), 1)
        self.assertEqual(findings[0]["severity"], "CONFLICTING_SOURCE_DATA")
        self.assertEqual(len(findings[0]["records"]), 2)
        self.assertEqual(len(result["requests"][0]["contexts"]), 2)

    def test_conflicting_cohort_and_multiple_responses_are_reported(self):
        first = {"cohort_id": "cohort-a", "offers": [{"offer_index": 0, "value": 1}]}
        second = {"cohort_id": "cohort-a", "offers": [{"offer_index": 0, "value": 2}]}
        result = self.analyze([
            self.event("session_started"), self.event("request_started", "request-a"),
            self.event("cohort_received", "request-a", first),
            self.event("cohort_received", "request-a", second),
        ])
        self.assertIn("MULTIPLE_RESPONSES_FOR_ONE_REQUEST", {value["classification"] for value in result["request_findings"]})
        self.assertEqual(result["cohort_findings"][0]["classification"], "CONFLICTING_COHORT_CONTENTS")
        self.assertEqual(result["metadata"]["offer_count"], 2)

    def test_orphan_response_is_not_silently_dropped(self):
        result = self.analyze([
            self.event("session_started"),
            self.event("cohort_received", "request-a", {"cohort_id": "cohort-a", "offers": [{"offer_index": 0}]}),
        ])
        kinds = {value["classification"] for value in result["request_findings"]}
        self.assertIn("MISSING_REQUEST_HEADER", kinds)
        self.assertIn("ORPHAN_RESPONSE", kinds)
        self.assertEqual(result["metadata"]["offer_count"], 1)

    def test_partial_source_overlap_uses_event_fingerprints(self):
        prefix = sources.compare_event_sequences(["a", "b"], ["a", "b", "c"])
        suffix = sources.compare_event_sequences(["b", "c"], ["a", "b", "c"])
        interior = sources.compare_event_sequences(["a", "b", "c"], ["x", "b", "y"])
        self.assertIn("PARTIAL_PREFIX_DUPLICATE", {value["classification"] for value in prefix})
        self.assertIn("PARTIAL_SUFFIX_DUPLICATE", {value["classification"] for value in suffix})
        self.assertEqual({value["classification"] for value in interior}, {"OVERLAPPING_EVENT_RANGE"})
        self.assertEqual(sources.compare_event_sequences(["same-time-payload-a"], ["same-time-payload-b"]), [])

    def test_archival_copy_is_not_an_additional_logical_session(self):
        events = [self.event("session_started"), self.event("request_started", "request-a"),
                  self.event("cohort_received", "request-a", {
                      "cohort_id": "cohort-a", "offers": [{"offer_index": index} for index in range(5)]})]
        data = ("\n".join(json.dumps(event, sort_keys=True) for event in events) + "\n").encode("utf-8")
        expected_hash = hashlib.sha256(data).hexdigest()
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            original = root / "originals/session-a/events.jsonl"
            retained = root / sources.RETAINED / "session-a/events.jsonl"
            for path in (original, retained):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            manifest = [{"session_id": "session-a", "path": "originals/session-a/events.jsonl",
                         "sha256": expected_hash, "bytes": len(data),
                         "events": {"session_started": 1, "request_started": 1, "cohort_received": 1}}]
            coverage = [{"session_id": "session-a", "path_read": "originals/session-a/events.jsonl",
                         "prior_source_sha256": expected_hash, "counts": {"offers": 5, "cohorts": 1}}]
            for relative, value in ((sources.SOURCE_MANIFEST, manifest), (sources.COVERAGE_MANIFEST, coverage)):
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(json.dumps(value), encoding="utf-8")
            with mock.patch.object(sources, "ARCHIVE", root / "archive"), mock.patch.object(sources, "ORIGINALS", root / "originals"):
                result = sources.audit_sources(root)
            self.assertEqual(result["counts"]["PHYSICAL_SOURCE_FILES_FOUND"], 2)
            self.assertEqual(result["counts"]["LOGICAL_UNIQUE_SESSIONS"], 1)
            self.assertEqual(result["counts"]["TOTAL_OFFERS"], 5)
            self.assertEqual(result["issues"], [])
            groups = result["source_file_report"]["duplicate_groups"]
            self.assertTrue(any(value.get("severity") == "BENIGN_ARCHIVAL_COPY" for value in groups))
            self.assertEqual(original.read_bytes(), data)
            self.assertEqual(retained.read_bytes(), data)
            # A deliberately repeated manifest pointer is detected separately
            # from the harmless second physical file, without deleting either.
            (root / sources.SOURCE_MANIFEST).write_text(json.dumps(manifest * 2), encoding="utf-8")
            with mock.patch.object(sources, "ARCHIVE", root / "archive"), mock.patch.object(sources, "ORIGINALS", root / "originals"):
                repeated = sources.audit_sources(root)
            self.assertEqual(repeated["counts"]["PROVEN_ACCIDENTAL_DUPLICATE_SESSIONS"], 1)
            self.assertEqual(repeated["counts"]["LOGICAL_UNIQUE_SESSIONS"], 1)
            self.assertEqual(repeated["counts"]["TOTAL_OFFERS"], 5)

    def test_conflicting_session_hashes_are_not_proven_accidental_duplicates(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            manifest, coverage = [], []
            for variant in ("a", "b"):
                events = [self.event("session_started", payload={"variant": variant}),
                          self.event("request_started", "request-" + variant),
                          self.event("cohort_received", "request-" + variant,
                                     {"cohort_id": "cohort-" + variant, "offers": [{"offer_index": 0}]})]
                raw = ("\n".join(json.dumps(event) for event in events) + "\n").encode("utf-8")
                path = root / ("original-" + variant) / "session-a/events.jsonl"
                path.parent.mkdir(parents=True)
                path.write_bytes(raw)
                digest = hashlib.sha256(raw).hexdigest()
                relative = path.relative_to(root).as_posix()
                manifest.append({"session_id": "session-a", "path": relative, "sha256": digest,
                                 "bytes": len(raw), "events": {"session_started": 1, "request_started": 1, "cohort_received": 1}})
                coverage.append({"session_id": "session-a", "path_read": relative,
                                 "prior_source_sha256": digest, "counts": {"offers": 1, "cohorts": 1}})
            for relative, value in ((sources.SOURCE_MANIFEST, manifest), (sources.COVERAGE_MANIFEST, coverage)):
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(json.dumps(value), encoding="utf-8")
            with mock.patch.object(sources, "ARCHIVE", root / "archive"), mock.patch.object(sources, "ORIGINALS", root / "originals"):
                result = sources.audit_sources(root)
            self.assertEqual(result["counts"]["PROVEN_ACCIDENTAL_DUPLICATE_SESSIONS"], 0)
            self.assertEqual(result["counts"]["LOGICAL_SOURCE_VARIANTS"], 2)
            self.assertEqual(result["counts"]["TOTAL_OFFERS"], 2)
            self.assertTrue(any(value["classification"] == "CONFLICTING_SOURCE_DATA" for value in result["issues"]))


class CatalogFixtureTests(unittest.TestCase):
    @staticmethod
    def placement(instance, x=1.0, name="a building", playfield=505):
        position = [x, 2.0, 3.0]
        rotation = [1.0, 0.0, 0.0, 0.0]
        return {"identity_type": 56006, "identity_instance_uint32": instance,
                "explicit_playfield_id": playfield, "display_name_exact": name,
                "raw_position_components": position, "raw_rotation_components": rotation,
                "raw_transform_bytes": struct.pack("<7f", *position, *rotation).hex(),
                "template_resource_type": 1000020, "template_identity_instance": 1234}

    def test_same_name_different_placements_remain_distinct(self):
        rows = [self.placement(100, x=1.0), self.placement(101, x=2.0)]
        original = copy.deepcopy(rows)
        result = catalog.placement_collision_audit(rows)
        self.assertEqual(result["counts"]["CLIENT_PLACEMENTS"], 2)
        self.assertEqual(result["counts"]["DUPLICATE_COMPLETE_PLACEMENT_IDENTITIES"], 0)
        self.assertEqual(result["counts"]["MULTI_IDENTITY_NAME_GROUPS"], 1)
        self.assertEqual(result["placement_name"]["multi_identity_name_groups"][0]["classification"], "DESCRIPTIVE_METADATA_COLLISION")
        self.assertEqual(rows, original)

    def test_exact_bit_collision_is_distinct_from_name_collision(self):
        rows = [self.placement(100, name="First"), self.placement(101, name="Second")]
        result = catalog.placement_collision_audit(rows)
        self.assertEqual(result["counts"]["EXACT_SAME_PLAYFIELD_XYZ_COLLISIONS"], 1)
        self.assertEqual(result["counts"]["MULTI_IDENTITY_NAME_GROUPS"], 0)
        self.assertEqual(result["counts"]["DUPLICATE_COMPLETE_PLACEMENT_IDENTITIES"], 0)

    def test_numeric_signed_zero_collision_does_not_become_exact_bit_match(self):
        result = catalog.placement_collision_audit([self.placement(100, x=0.0), self.placement(101, x=-0.0)])
        self.assertEqual(result["counts"]["EXACT_SAME_PLAYFIELD_XYZ_COLLISIONS"], 0)
        self.assertEqual(result["counts"]["NUMERIC_XYZ_DIFFERENT_BITS_COLLISIONS"], 1)
        self.assertEqual(len(result["placement_coordinate"]["numeric_xyz_groups"]), 1)

    def test_close_coordinates_are_not_rounded_into_collisions(self):
        next_float = struct.unpack("<f", struct.pack("<I", 0x3F800001))[0]
        result = catalog.placement_collision_audit([self.placement(100, x=1.0), self.placement(101, x=next_float)])
        self.assertEqual(result["counts"]["EXACT_SAME_PLAYFIELD_XYZ_COLLISIONS"], 0)
        self.assertEqual(result["placement_coordinate"]["numeric_xyz_groups"], [])

    def test_duplicate_complete_identity_fixture_is_reported(self):
        row = self.placement(100)
        result = catalog.placement_collision_audit([row, dict(row)])
        self.assertEqual(result["counts"]["DUPLICATE_COMPLETE_PLACEMENT_IDENTITIES"], 1)
        self.assertEqual(result["counts"]["DUPLICATE_PLACEMENT_IDENTITY_ROWS"], 1)
        self.assertEqual(result["counts"]["CLIENT_PLACEMENTS"], 2)

    def test_artifact_condition_dimensions_preserve_repeated_target_identity(self):
        rows = [{"identity": {"type": 56006, "instance": 100}, "ql": 1},
                {"identity": {"type": 56006, "instance": 100}, "ql": 2}]
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "conditions.jsonl"
            path.write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
            result = catalog.primary_key_audit(path, ("identity.type", "identity.instance", "ql"))
            self.assertEqual(result["row_count"], 2)
            self.assertEqual(result["unique_primary_keys"], 2)
            self.assertEqual(result["duplicate_primary_keys"], 0)

    def test_artifact_duplicate_primary_key_fixture_is_detected(self):
        row = {"identity": {"type": 56006, "instance": 100}, "ql": 1}
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "duplicate.jsonl"
            original = ((json.dumps(row) + "\n") * 2).encode("utf-8")
            path.write_bytes(original)
            result = catalog.primary_key_audit(path, ("identity.type", "identity.instance", "ql"))
            self.assertEqual(result["row_count"], 2)
            self.assertEqual(result["unique_primary_keys"], 1)
            self.assertEqual(result["duplicate_primary_keys"], 1)
            self.assertEqual(result["duplicate_rows"], 1)
            self.assertEqual(path.read_bytes(), original)


def run_tests():
    suite = unittest.TestSuite([
        unittest.defaultTestLoader.loadTestsFromTestCase(IndependentCorpusTests),
        unittest.defaultTestLoader.loadTestsFromTestCase(DuplicateFixtureTests),
        unittest.defaultTestLoader.loadTestsFromTestCase(SourceFixtureTests),
        unittest.defaultTestLoader.loadTestsFromTestCase(CatalogFixtureTests),
    ])
    result = unittest.TextTestRunner().run(suite)
    if not result.wasSuccessful():
        raise SystemExit(1)
    print(f"MISSION_DESTINATION_DUPLICATE_AUDIT_TESTS=PASS ({result.testsRun} tests)")


if __name__ == "__main__":
    run_tests()
