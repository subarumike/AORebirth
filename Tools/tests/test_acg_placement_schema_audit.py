import struct
import unittest

from Tools import acg_placement_schema_audit as audit


def base_record(*, version=7, optional_flags=0):
    raw = bytearray(36 if version >= 7 else 32)
    struct.pack_into("<ffff", raw, 0, 1.25, 2.5, 3.75, 4.0)
    struct.pack_into("<HHIHHBBH", raw, 16, 90, 15, 0x4644514F, 5, 8, 80, optional_flags, 30)
    return raw


class AcgPlacementSchemaNegativeTests(unittest.TestCase):
    def test_runtime_identity_cannot_affect_placement_validity(self):
        first = audit.placement_readiness(parser_status="PARSED_SUPPORTED", raw_round_trip=True, runtime_identity=None)
        second = audit.placement_readiness(parser_status="PARSED_SUPPORTED", raw_round_trip=True, runtime_identity="SimpleChar:1")
        self.assertEqual(first, second)

    def test_monsterdata_cannot_affect_placement_validity(self):
        first = audit.placement_readiness(parser_status="PARSED_SUPPORTED", raw_round_trip=True, monster_data=None)
        second = audit.placement_readiness(parser_status="PARSED_SUPPORTED", raw_round_trip=True, monster_data=17655)
        self.assertEqual(first, second)

    def test_missing_runtime_capture_cannot_block_placement(self):
        self.assertEqual(
            audit.placement_readiness(parser_status="PARSED_SUPPORTED", raw_round_trip=True, runtime_capture_present=False),
            "placement_ready",
        )

    def test_spatial_clustering_is_not_official_grouping(self):
        serialized_names = {row["fieldId"] for row in audit.FIELD_SCHEMA}
        self.assertNotIn("spatial_cluster_25m", serialized_names)

    def test_unknown_float_is_not_automatically_radius(self):
        radius = next(row for row in audit.FIELD_SCHEMA if row["fieldId"] == "radius")
        self.assertEqual(radius["offset"], 12)
        self.assertIn("does not prove", radius["notes"])

    def test_unknown_integer_is_not_automatically_count(self):
        self.assertFalse(any(row["semanticName"] == "spawn_count" for row in audit.FIELD_SCHEMA))

    def test_unknown_numeric_is_not_automatically_respawn_time(self):
        unknown = next(row for row in audit.FIELD_SCHEMA if row["fieldId"] == "unknown_optional_u8")
        self.assertIsNone(unknown["semanticName"])

    def test_numeric_resource_collision_does_not_prove_reference(self):
        self.assertFalse(audit.STATIC_ACG_MONSTERDATA_SEARCH_REOPENED)
        self.assertNotEqual(audit.resource_audit.RESOURCE_TYPE_ACG, audit.resource_audit.RESOURCE_TYPE_MONSTER_DATA)

    def test_unknown_flag_bit_remains_unknown(self):
        rows = audit.bit_analysis([8], 16, {8: {4582}})
        self.assertIsNone(rows[0]["provenMeaning"])
        self.assertEqual(rows[0]["evidenceClass"], "unknown")

    def test_opaque_bytes_survive_round_trip_exactly(self):
        raw = bytes([0, 1, 1, 0])
        decoded = audit.zone_to_district_index(raw, 4, 2)
        self.assertEqual(bytes(decoded["decoded"]), raw)

    def test_parser_limited_is_distinct_from_invalid(self):
        self.assertEqual(
            audit.placement_readiness(parser_status="MALFORMED_RESOURCE", raw_round_trip=False),
            "parser_limited",
        )
        self.assertEqual(audit.placement_readiness(parser_status="UNSUPPORTED", raw_round_trip=False), "invalid")

    def test_acghash_is_not_monsterdata(self):
        field = next(row for row in audit.FIELD_SCHEMA if row["fieldId"] == "acg_hash")
        self.assertEqual(field["semanticName"], "authoritative_placement_identity")
        self.assertIn("not MonsterData", field["notes"])

    def test_server_monsterdata_role_stays_separate(self):
        self.assertEqual(audit.resource_audit.RESOURCE_TYPE_MONSTER_DATA, 1040023)
        self.assertFalse(audit.MONSTERDATA_REQUIRED_FOR_PLACEMENT)

    def test_population_readiness_cannot_gate_placement(self):
        first = audit.placement_readiness(parser_status="PARSED_SUPPORTED", raw_round_trip=True, population_identity_ready=False)
        second = audit.placement_readiness(parser_status="PARSED_SUPPORTED", raw_round_trip=True, population_identity_ready=True)
        self.assertEqual(first, second)

    def test_static_acg_monsterdata_search_is_not_reopened(self):
        self.assertFalse(audit.STATIC_ACG_MONSTERDATA_SEARCH_REOPENED)

    def test_zone_index_rejects_out_of_range_district(self):
        with self.assertRaises(audit.SchemaAuditError):
            audit.zone_to_district_index(bytes([0, 2]), 2, 2)

    def test_unclassified_record_bytes_fail_closed(self):
        with self.assertRaises(audit.SchemaAuditError):
            audit.field_layout(bytes(base_record()) + b"\x00", 7)


class AcgPlacementSchemaPositiveTests(unittest.TestCase):
    def test_known_coordinates_decode_exactly(self):
        decoded = audit.decoded_from_raw(bytes(base_record()), 7)
        self.assertEqual((decoded["PositionX"], decoded["PositionY"], decoded["PositionZ"]), (1.25, 2.5, 3.75))

    def test_known_orientation_decodes_exactly_without_unit_claim(self):
        decoded = audit.decoded_from_raw(bytes(base_record()), 7)
        self.assertEqual((decoded["RotationMidEncoded"], decoded["RotationWidthEncoded"]), (90, 15))
        orientation = next(row for row in audit.FIELD_SCHEMA if row["fieldId"] == "rotation_mid")
        self.assertIn("no transform consumer", orientation["notes"])

    def test_raw_record_round_trip_is_lossless(self):
        raw = bytes(base_record())
        self.assertEqual(audit.reconstruct_record(audit.field_layout(raw, 7), len(raw)), raw)

    def test_variable_additional_point_boundaries_are_preserved(self):
        raw = base_record(optional_flags=2)
        raw.extend(b"\x01" + bytes(range(20)))
        layout = audit.field_layout(bytes(raw), 7)
        section = layout["variableSections"]["AdditionalPoints"]
        self.assertEqual((section["offset"], section["size"], section["count"]), (36, 21, 1))

    def test_variable_extension_boundaries_are_preserved(self):
        raw = base_record(optional_flags=4)
        raw.extend(struct.pack("<I", 0))
        layout = audit.field_layout(bytes(raw), 7)
        section = layout["variableSections"]["Extensions"]
        self.assertEqual((section["offset"], section["size"], section["count"]), (36, 4, 0))

    def test_known_field_consumer_maps_correctly(self):
        field = next(row for row in audit.FIELD_SCHEMA if row["fieldId"] == "respawn_time")
        self.assertIn("HashSpawnPoint_t::GetRespawnTime", field["consumer"])

    def test_explicit_child_grouping_is_declared(self):
        section = next(row for row in audit.VARIABLE_SECTIONS if row["sectionId"] == "additional_points")
        self.assertEqual(section["elementType"], "RotationSpawnPoint_t")
        self.assertIn("child", section["notes"])

    def test_proven_serialized_presence_flags_decode(self):
        raw = base_record(optional_flags=1)
        raw.extend(struct.pack("<HBB", 10, 11, 12))
        decoded = audit.decoded_from_raw(bytes(raw), 7)
        self.assertEqual((decoded["NativeFlags"], decoded["AssistanceRadius"], decoded["UnknownOptionalU8"]), (10, 11, 12))

    def test_complete_corpus_count_is_governed(self):
        self.assertEqual(audit.EXPECTED_PLACEMENTS, 32805)

    def test_canonical_export_digest_is_deterministic(self):
        first = audit.canonical_bytes({"b": 2, "a": 1})
        second = audit.canonical_bytes({"a": 1, "b": 2})
        self.assertEqual(audit.sha256_bytes(first), audit.sha256_bytes(second))

    def test_version_six_layout_has_no_more_flags(self):
        layout = audit.field_layout(bytes(base_record(version=6)), 6)
        self.assertIsNone(layout["MoreFlags"])

    def test_zone_vector_header_count_and_bounds_decode(self):
        decoded = audit.zone_to_district_index(bytes([0, 1, 2, 1]), 4, 3)
        self.assertEqual(decoded["distribution"], {"0": 1, "1": 2, "2": 1})


if __name__ == "__main__":
    unittest.main()
