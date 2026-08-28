import json
from pathlib import Path
import struct
import tempfile
import unittest

import numpy as np

from Tools import acg_monsterdata_resource_audit as audit


def resource_record(resource_type: int, resource_instance: int, payload: bytes) -> bytes:
    outer = 24 + len(payload)
    length = 10 + outer
    header = bytearray(audit.RECORD_HEADER_SIZE)
    header[0:2] = b"\xfa\xfa"
    struct.pack_into("<I", header, 2, length)
    struct.pack_into("<I", header, 6, outer)
    struct.pack_into("<I", header, 10, resource_type)
    struct.pack_into("<I", header, 14, resource_instance)
    struct.pack_into("<I", header, 18, outer - 12)
    struct.pack_into("<I", header, 22, resource_type)
    struct.pack_into("<I", header, 26, resource_instance)
    return bytes(header) + payload


class AcgMonsterDataResourceAuditTests(unittest.TestCase):
    def test_index_parser_reads_big_endian_key_and_little_endian_offset(self):
        data = bytearray(audit.INDEX_PAGE_SIZE)
        struct.pack_into("<I", data, 12, audit.INDEX_PAGE_SIZE)
        struct.pack_into("<I", data, 16, audit.INDEX_LEAF_MARKER)
        struct.pack_into("<I", data, 24, 1)
        struct.pack_into("<H", data, 8, 1)
        struct.pack_into("<H", data, 10, audit.INDEX_ENTRY_SIZE)
        offset = audit.INDEX_PAGE_HEADER
        data[offset : offset + 4] = (1234).to_bytes(4, "little")
        data[offset + 4 : offset + 8] = audit.RESOURCE_TYPE_ACG.to_bytes(4, "big")
        data[offset + 8 : offset + 12] = (4582).to_bytes(4, "big")
        data[offset + 12 : offset + 16] = (9).to_bytes(4, "little")
        entries, pages = audit.parse_index(bytes(data))
        self.assertEqual(pages, [0])
        self.assertEqual(entries[0].key, (audit.RESOURCE_TYPE_ACG, 4582))
        self.assertEqual(entries[0].global_offset, 1234)
        self.assertEqual(entries[0].unknown_u32, 9)

    def test_effective_index_is_active_leaf_chain_only(self):
        data = bytearray(audit.INDEX_PAGE_SIZE * 2)
        struct.pack_into("<I", data, 12, audit.INDEX_PAGE_SIZE)
        struct.pack_into("<I", data, 16, audit.INDEX_LEAF_MARKER)
        struct.pack_into("<I", data, 24, 0)
        entries, pages = audit.parse_index(bytes(data))
        self.assertEqual(entries, [])
        self.assertEqual(pages, [0])

    def test_raw_scan_binary_preserves_effective_record_identity(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / "scan.bin"
            path.write_bytes(
                b"AOMDREF2"
                + struct.pack("<ii", 2, 1)
                + struct.pack("<iii", 10, 20, 4)
                + bytes.fromhex("11" * 32)
                + struct.pack("<i", 1)
                + struct.pack("<iIII", 0, 17655, 359, 0xFFFFFFFF)
            )
            rows = audit.read_raw_reference_scan(path)
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["resourceType"], 10)
        self.assertEqual(
            rows[0]["hits"],
            [{"offset": 0, "value": 17655, "previousValue": 359, "nextValue": None}],
        )

    def test_raw_target_scan_covers_unaligned_values(self):
        value = 17655
        data = b"x" + value.to_bytes(4, "little") + b"y"
        hits = audit.scan_targets(data, np.array([value], dtype=np.uint32))
        self.assertEqual(hits, [(1, value)])

    def test_raw_byte_match_is_not_a_semantic_relationship(self):
        result = audit.acg_field_reference_audit(
            [{"AcgHashNativeUInt32": 17655}],
            {"spawnInfo": []},
            {17655: {audit.RESOURCE_TYPE_MONSTER_DATA}},
            {17655},
        )
        field = result["fields"][0]
        self.assertEqual(field["monsterDataNumericCollisions"], 1)
        self.assertIn("no client consumer", field["structuralEvidence"])

    def test_coordinates_are_never_used_for_static_bridge(self):
        result = audit.acg_field_reference_audit([], {"spawnInfo": []}, {}, set())
        self.assertFalse(result["coordinatesUsedToInferStaticBridge"])

    def test_appearance_is_never_used_for_static_bridge(self):
        result = audit.acg_field_reference_audit([], {"spawnInfo": []}, {}, set())
        self.assertFalse(result["appearanceUsedToInferStaticBridge"])

    def test_runtime_id_is_never_used_for_static_bridge(self):
        result = audit.acg_field_reference_audit([], {"spawnInfo": []}, {}, set())
        self.assertFalse(result["runtimeIdUsedToInferStaticBridge"])

    def test_acghash_remains_packed_placement_identity(self):
        raw = bytearray(40)
        struct.pack_into("<I", raw, 20, 0x4644514F)
        raw[29] = 1
        layout = audit.raw_acg_field_layout(bytes(raw), 7)
        field = next(row for row in layout["rawIntegerFields"] if row["field"] == "AcgHashNativeUInt32")
        self.assertEqual(field["value"], 0x4644514F)
        self.assertEqual(layout["apparentResourceReferences"], [])

    def test_monsterdata_stays_model_identity(self):
        self.assertEqual(audit.RESOURCE_TYPE_MONSTER_DATA, 1040023)
        self.assertNotEqual(audit.RESOURCE_TYPE_MONSTER_DATA, audit.RESOURCE_TYPE_ACG)

    def test_runtime_identity_is_not_a_resource_type(self):
        self.assertNotIn("runtimeIdentity", audit.raw_acg_field_layout(bytes(40), 7))

    def test_server_runtime_path_is_distinct_from_static_linkage(self):
        trace = audit.verify_runtime_trace()
        self.assertTrue(trace["serverSuppliesRuntimeMonsterData"])
        self.assertFalse(trace["clientRuntimeJoinFound"])
        self.assertFalse(trace["acgConsumerFound"])

    def test_spawn_info_numeric_match_remains_rejected(self):
        result = audit.acg_field_reference_audit(
            [],
            {"spawnInfo": [{"UnknownU16": 17655}]},
            {},
            {17655},
        )
        self.assertEqual(result["spawnInfo"]["monsterDataNumericCollisions"], [17655])
        self.assertIn("rejected", result["spawnInfo"]["disposition"])

    def test_big_endian_raw_candidate_is_labeled_separately(self):
        value = 0x01020304
        swapped = audit.byte_swap_u32(value)
        self.assertEqual(swapped, 0x04030201)

    def test_canonical_json_is_deterministic(self):
        first = audit.compact_json_bytes({"b": 2, "a": 1})
        second = audit.compact_json_bytes({"a": 1, "b": 2})
        self.assertEqual(first, second)

    def test_raw_layout_preserves_unknown_optional_byte(self):
        raw = bytearray(40)
        raw[29] = 1
        raw[39] = 15
        layout = audit.raw_acg_field_layout(bytes(raw), 7)
        self.assertEqual(layout["opaqueBytes"][0]["hex"], "0F")


if __name__ == "__main__":
    unittest.main()
