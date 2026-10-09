"""Offline catalog contracts, independently reconciled against retained evidence."""
import copy
import gzip
import hashlib
import json
import pathlib
import struct
import tempfile
import unittest
from collections import defaultdict

import mission_destination_catalog as catalog


ROOT = pathlib.Path(__file__).resolve().parents[1]
CONTENT = ROOT / "AORebirth/GameData/Missions/Destinations"
PLACEMENTS = "MissionEntrancePlacements.json"
OBSERVED = "ObservedMissionDestinations.json"
MANIFEST = "MissionDestinationCatalogManifest.json"
NOT_OBSERVED = "CLIENT_ACGENTRANCE_NOT_YET_OBSERVED_IN_RANDOM_MISSION_CAPTURE"


def jsonl(path):
    opener = gzip.open if path.suffix == ".gz" else open
    decoder = json.JSONDecoder()
    with opener(path, "rt", encoding="utf-8") as stream:
        for line in stream:
            yield decoder.decode(line)


def identity(row):
    return row["IdentityType"], row["IdentityInstance"]


def evidence_identity(row):
    value = row["destination_identity"]
    return value["type"], value.get("instance_uint32", value.get("instance"))


def offer_key(row):
    return tuple(row[key] for key in ("session_id", "source_line", "request_id", "cohort_id", "offer_index"))


class CatalogEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.docs = {name: json.loads((CONTENT / name).read_text(encoding="utf-8"))
                    for name in (PLACEMENTS, OBSERVED, MANIFEST)}
        cls.placements = cls.docs[PLACEMENTS]["Placements"]
        cls.observed = cls.docs[OBSERVED]["Destinations"]
        cls.by_id = {identity(row): row for row in cls.placements}
        cls.original = list(jsonl(ROOT / "docs/generated/missions/acgentrance-reconstruction/acgentrance-records.jsonl"))
        cls.raw_assignments = {}
        cls.missing_raw = []
        for row in jsonl(ROOT / "docs/generated/missions/destination-duplicate-audit/offer-audit-inventory.jsonl.gz"):
            key = offer_key(row)
            if key in cls.raw_assignments:
                raise AssertionError("Duplicate audited provenance key")
            if not row["raw_byte_range_verified"]:
                if row["destination_identity"] is not None:
                    raise AssertionError("Missing raw offer has a promoted identity")
                cls.missing_raw.append(key)
                continue
            cls.raw_assignments[key] = evidence_identity(row)
        cls.aggregates = defaultdict(lambda: {
            "count": 0, "requests": set(), "cohorts": set(), "sessions": set(),
            "qls": set(), "levels": set(), "types": set(), "terminals": set(),
            "sides": set(), "side_values": set()})
        joined = set()
        for row in jsonl(ROOT / "docs/generated/missions/destination-eligibility-analysis/mission-offer-analysis-inventory.jsonl.gz"):
            key = offer_key(row)
            if key not in cls.raw_assignments:
                if row["destination_identity"] is not None:
                    raise AssertionError("Eligibility promotes a non-raw destination")
                continue
            did = evidence_identity(row)
            if did != cls.raw_assignments[key] or key in joined:
                raise AssertionError("Contradictory or duplicate evidence join")
            joined.add(key)
            aggregate = cls.aggregates[did]
            aggregate["count"] += 1
            aggregate["requests"].add((row["session_id"], row["request_id"]))
            aggregate["cohorts"].add(key[:4])
            aggregate["sessions"].add(row["session_id"])
            for target, field in (("qls", "static_expected_mission_ql"), ("levels", "character_level"),
                                  ("types", "mission_type"), ("sides", "faction_side"),
                                  ("side_values", "faction_side_raw")):
                if row[field] is not None:
                    aggregate[target].add(row[field])
            aggregate["terminals"].add(row["terminal_playfield"]["instance_uint32"])
        if joined != set(cls.raw_assignments):
            raise AssertionError("Missing evidence metadata join")

    def test_counts_and_unique_complete_identities(self):
        self.assertEqual(len(self.placements), 2242)
        self.assertEqual(len(self.by_id), 2242)
        self.assertEqual(len(self.observed), 812)
        self.assertEqual(len({identity(row) for row in self.observed}), 812)
        self.assertEqual(len({self.by_id[identity(row)]["PlayfieldId"] for row in self.observed}), 22)

    def test_all_original_fields_preserved_by_identity(self):
        self.assertEqual(len(self.original), len(self.placements))
        for source in self.original:
            row = self.by_id[(source["identity_type"], source["identity_instance_uint32"])]
            bits = struct.unpack("<7I", bytes.fromhex(source["raw_transform_bytes"]))
            self.assertEqual(row["PlayfieldId"], source["explicit_playfield_id"])
            self.assertEqual(tuple(row["Local" + axis + "Bits"] for axis in "XYZ"), bits[:3])
            self.assertEqual([row["Local" + axis] for axis in "XYZ"], source["raw_position_components"])
            self.assertEqual([row["RotationComponent" + str(index)] for index in range(4)], source["raw_rotation_components"])
            self.assertEqual(row["DisplayName"], source["display_name_exact"])
            self.assertEqual(row["RawNameHex"], source["display_name_raw_bytes"])
            self.assertEqual(row["NameProvenance"], {
                "Encoding": source["text_encoding"], "ResolutionPath": source["name_resolution_path"],
                "ResourceType": source["string_resource_type"], "ResourceInstance": source["string_resource_instance"],
                "SourceOffset": source["string_source_offset"]})
            self.assertEqual(row["TemplateInstance"], source["template_identity_instance"])
            self.assertEqual(row["SourcePlacementContainer"], {
                "ResourceType": source["parent_resource_type"], "ResourceInstance": source["parent_resource_instance"]})
            self.assertEqual(row["SourceRecordOffset"], source["source_record_offset"])
            self.assertEqual(row["SourceRecordLength"], source["source_record_length"])
            self.assertEqual(row["SourceRecordSha256"], source["source_record_sha256"])
            self.assertEqual(row["SourceDatabaseSha256"], source["source_database_sha256"])

    def test_exact_coordinate_index_has_zero_ambiguity(self):
        keys = [(row["PlayfieldId"], row["LocalXBits"], row["LocalYBits"], row["LocalZBits"])
                for row in self.placements]
        self.assertEqual(len(set(keys)), 2242)

    def test_cross_playfield_identical_coordinates_remain_distinct(self):
        groups = defaultdict(list)
        for row in self.placements:
            key = tuple(row["Local" + axis + "Bits"] for axis in "XYZ")
            key += tuple(struct.unpack("<I", struct.pack("<f", row["RotationComponent" + str(index)]))[0]
                         for index in range(4))
            groups[key].append(row)
        cross_playfield = [rows for rows in groups.values() if len({r["PlayfieldId"] for r in rows}) > 1]
        self.assertTrue(cross_playfield)
        for rows in cross_playfield:
            self.assertEqual(len(rows), len({identity(row) for row in rows}))

    def test_pf505_anchors_and_uint32_identities(self):
        for instance, name in ((0xC00001F9, "Central Desert Den"), (0xC00101F9, "South Desert Den"),
                               (0xC01201F9, "Mantis Hive")):
            row = self.by_id[(56006, instance)]
            self.assertEqual(row["PlayfieldId"], 505)
            self.assertEqual(row["DisplayName"], name)
            self.assertGreater(row["IdentityInstance"], 0x7FFFFFFF)

    def test_raw_latin1_name_remains_authoritative(self):
        row = self.by_id[(56006, 0xC0000280)]
        self.assertEqual(row["RawNameHex"], "c66e696d61204851")
        self.assertEqual(row["DisplayName"], "\u00c6nima HQ")
        self.assertIn("Latin-1", row["NameProvenance"]["Encoding"])
        self.assertEqual(bytes.fromhex(row["RawNameHex"]).decode("latin-1"), row["DisplayName"])

    def test_large_repeated_name_family_is_not_collapsed(self):
        rows = [row for row in self.placements if row["DisplayName"] == "a building"]
        self.assertEqual(len(rows), 188)
        self.assertEqual(len({identity(row) for row in rows}), 188)
        observed_ids = {identity(row) for row in self.observed}
        self.assertEqual(sum(identity(row) in observed_ids for row in rows), 85)

    def test_all_1430_unobserved_placements_retained_without_ineligibility(self):
        observed_ids = {identity(row) for row in self.observed}
        self.assertEqual(observed_ids, set(self.aggregates))
        self.assertLessEqual(observed_ids, set(self.by_id))
        unobserved = [row for row in self.placements if identity(row) not in observed_ids]
        self.assertEqual(len(unobserved), 1430)
        for row in self.placements:
            self.assertEqual(row["Classification"], "CLIENT_ACGENTRANCE_PLACEMENT")
            expected = "OBSERVED_RANDOM_MISSION_DESTINATION" if identity(row) in observed_ids else NOT_OBSERVED
            self.assertEqual(row["ObservationClassification"], expected)

    def test_355_missing_raw_offers_promote_zero_identities(self):
        self.assertEqual(len(self.missing_raw), 355)
        self.assertEqual(len(self.raw_assignments), 92830)
        self.assertEqual(len(self.raw_assignments) + len(self.missing_raw), 93185)
        self.assertEqual({identity(row) for row in self.observed}, set(self.raw_assignments.values()))
        self.assertEqual(self.docs[MANIFEST]["MissingRawOffersPromoted"], 0)

    def test_every_observed_aggregate_recomputed_from_independent_offer_provenance(self):
        for row in self.observed:
            aggregate = self.aggregates[identity(row)]
            self.assertEqual(row["ObservationCount"], aggregate["count"])
            self.assertEqual(row["RequestCount"], len(aggregate["requests"]))
            self.assertEqual(row["CohortCount"], len(aggregate["cohorts"]))
            self.assertEqual(row["SessionCount"], len(aggregate["sessions"]))
            for property_name, key in (("ObservedExpectedMissionQls", "qls"), ("ObservedCharacterLevels", "levels"),
                                       ("ObservedMissionTypes", "types"), ("ObservedTerminalPlayfields", "terminals"),
                                       ("ObservedFactionSides", "sides"), ("ObservedFactionSideValues", "side_values")):
                self.assertEqual(row[property_name], sorted(aggregate[key]))
            self.assertEqual(row["FactionEvidenceClassification"], "OBSERVED_WITH_OMNI")
        self.assertEqual(sum(row["ObservationCount"] for row in self.observed), 92830)

    def test_unresolved_operational_keys_remain_explicit_nulls(self):
        for row in self.placements:
            self.assertIsNone(row["OperationalEntranceKey"])
            self.assertIsNone(row["EffectiveStatBd"])
        self.assertIs(self.docs[MANIFEST]["DestinationUniquenessWithinCohortRequired"], False)
        self.assertEqual(self.docs[MANIFEST]["OperationalEntranceKeysResolved"], 0)

    def test_manifest_hashes_and_repository_relative_provenance(self):
        manifest = self.docs[MANIFEST]
        self.assertEqual(manifest["SourceEvidenceCommit"], "a259df4b118ee6074dd17c1da91d650d95fb5ea1")
        for item in manifest["Files"]:
            path = pathlib.PurePosixPath(item["Path"])
            self.assertEqual(len(path.parts), 1)
            self.assertEqual(hashlib.sha256((CONTENT / path).read_bytes()).hexdigest(), item["Sha256"])
        for item in manifest["Sources"] + [manifest["Generator"]]:
            path = pathlib.PurePosixPath(item["Path"])
            self.assertFalse(path.is_absolute())
            self.assertNotIn("..", path.parts)
            self.assertNotIn(":", item["Path"])
            self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), item["Sha256"])

    def test_deterministic_generation_reproduces_committed_content(self):
        first = catalog.build_catalog(ROOT)
        second = catalog.build_catalog(ROOT)
        self.assertEqual(set(first), {PLACEMENTS, OBSERVED, MANIFEST})
        for name in first:
            self.assertEqual(catalog.serialize(first[name]), catalog.serialize(second[name]))
            self.assertEqual(catalog.serialize(first[name]), (CONTENT / name).read_bytes())


class CatalogMalformedFixtureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.placements = json.loads((CONTENT / PLACEMENTS).read_text(encoding="utf-8"))
        cls.observed = json.loads((CONTENT / OBSERVED).read_text(encoding="utf-8"))

    def reject(self, mutate):
        placements, observed = copy.deepcopy(self.placements), copy.deepcopy(self.observed)
        mutate(placements, observed)
        with self.assertRaises(ValueError):
            catalog.validate_catalog(placements, observed)

    def test_duplicate_complete_identity_rejected_without_discarding_row(self):
        def mutate(p, _):
            p["Placements"][1]["IdentityType"] = p["Placements"][0]["IdentityType"]
            p["Placements"][1]["IdentityInstance"] = p["Placements"][0]["IdentityInstance"]
        self.reject(mutate)

    def test_coordinate_collision_rejected_without_first_row_fallback(self):
        def mutate(p, _):
            source, target = p["Placements"][:2]
            for key in ("PlayfieldId", "LocalX", "LocalY", "LocalZ", "LocalXBits", "LocalYBits", "LocalZBits"):
                target[key] = source[key]
        self.reject(mutate)

    def test_wrong_schema_rejected(self):
        self.reject(lambda p, _: p.update(SchemaVersion=999))

    def test_wrong_row_count_rejected(self):
        self.reject(lambda p, _: p["Placements"].pop())

    def test_nonfinite_coordinate_rejected(self):
        self.reject(lambda p, _: p["Placements"][0].update(LocalX=float("nan"), LocalXBits=0x7FC00000))

    def test_nonfinite_rotation_rejected(self):
        self.reject(lambda p, _: p["Placements"][0].update(RotationComponent0=float("inf")))

    def test_coordinate_number_bit_disagreement_rejected(self):
        self.reject(lambda p, _: p["Placements"][0].update(LocalXBits=p["Placements"][0]["LocalXBits"] ^ 1))

    def test_coordinate_sub_ulp_rounding_is_not_accepted(self):
        self.reject(lambda p, _: p["Placements"][0].update(LocalX=p["Placements"][0]["LocalX"] + 1e-10))

    def test_rotation_sub_ulp_rounding_is_not_accepted(self):
        self.reject(lambda p, _: p["Placements"][0].update(RotationComponent0=1.0000000001))

    def test_invalid_playfield_rejected(self):
        self.reject(lambda p, _: p["Placements"][0].update(PlayfieldId=0))

    def test_unknown_observed_identity_rejected(self):
        self.reject(lambda _, o: o["Destinations"][0].update(IdentityInstance=1))

    def test_duplicate_observed_identity_rejected(self):
        def mutate(_, o):
            o["Destinations"][1]["IdentityType"] = o["Destinations"][0]["IdentityType"]
            o["Destinations"][1]["IdentityInstance"] = o["Destinations"][0]["IdentityInstance"]
        self.reject(mutate)

    def test_fabricated_operational_key_rejected(self):
        self.reject(lambda p, _: p["Placements"][0].update(OperationalEntranceKey=0))

    def test_fabricated_effective_stat_bd_rejected(self):
        self.reject(lambda p, _: p["Placements"][0].update(EffectiveStatBd=0))

    def test_ineligible_classification_rejected(self):
        self.reject(lambda p, _: p["Placements"][0].update(ObservationClassification="INELIGIBLE"))

    def test_stale_detection_never_rewrites_artifact(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = pathlib.Path(temporary) / "catalog.json"
            path.write_bytes(b"original\n")
            with self.assertRaisesRegex(ValueError, "STALE_ARTIFACT"):
                catalog.write_or_check(path, b"replacement\n", True)
            self.assertEqual(path.read_bytes(), b"original\n")
            catalog.write_or_check(path, b"original\n", True)


if __name__ == "__main__":
    unittest.main()
