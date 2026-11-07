from collections import Counter, defaultdict
import json
from pathlib import Path
import struct
import sys
import unittest


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import audit_rk_dyna_residual_loot as audit
from reconstruct_rk_dyna_loot import Source


def inventory(encoded=2 * 0x3F1):
    return (bytes(16) + struct.pack(">IIIBIII", audit.INVENTORY_UPDATE, 50000, 4, 1, 21, 2, encoded)
            + struct.pack(">IHHIIIIII", 0, 33, 1, 0, 0, 11, 12, 30, 0)
            + struct.pack(">IIII", audit.CORPSE_TYPE, 7, 5, 1))


def raw_row(direction, sequence, ordinal, n3_type, packet):
    return {"Direction": direction, "Sequence": str(sequence), "GlobalOrdinal": str(ordinal), "N3TypeValue": str(n3_type),
            "PacketLength": str(len(packet)), "RawHex": packet.hex().upper(), "PreservationStatus": "raw_complete"}


class ResidualLootAuditTests(unittest.TestCase):
    def test_inventory_count_and_packet_tail_must_agree(self):
        self.assertEqual(audit.decode_inventory(inventory()), (21, (audit.CORPSE_TYPE, 7), [(0, 11, 12, 30, 1)]))
        for packet in (inventory()[:-1], inventory(3 * 0x3F1), inventory(2 * 0x3F1 + 1)):
            with self.assertRaisesRegex(ValueError, "count disagrees"):
                audit.decode_inventory(packet)

    def test_first_corpse_snapshot_requires_contiguous_matching_raw_sinks(self):
        identity = struct.pack(">II", audit.CORPSE_TYPE, 7)
        corpse = bytes(16) + struct.pack(">I", audit.CORPSE_FULL_UPDATE) + identity
        use = bytes(37) + struct.pack(">I", 3) + bytes(12) + identity
        raw = [raw_row("IN", 1, 1, audit.CORPSE_FULL_UPDATE, corpse), raw_row("OUT", 1, 2, audit.GENERIC_CMD, use),
               raw_row("IN", 2, 3, audit.INVENTORY_UPDATE, inventory())]
        hex_rows = {(row["Direction"], int(row["Sequence"])): (int(row["PacketLength"]), row["RawHex"]) for row in raw}
        capture = {"finalized": True, "rawMissingPackets": 0, "rawPacketWriteErrors": 0}
        boss = {"CorpseIdentity": "Corpse:00000007", "CorpseFullUpdateSequence": "1", "InventorySequence": "2"}
        items = [{"Slot": "0", "LowId": "11", "HighId": "12", "Ql": "30", "Count": "1"}]
        result = audit.audit_packet_window(raw, hex_rows, capture, boss, items)
        self.assertTrue(result["CompleteFirstSnapshotInCapture"])
        self.assertEqual(result["GenericCmdUseSequences"], [1])
        hex_rows.pop(("OUT", 1))
        result = audit.audit_packet_window(raw, hex_rows, capture, boss, items)
        self.assertFalse(result["CompleteFirstSnapshotInCapture"])

    def test_fingerprint_support_ranks_without_discarding_valid_alignments(self):
        sources = (Source(0, (1, 1), 1, "OUDY", 30), Source(1, (2, 2), 1, "ARMO", 30),
                   Source(1, (2, 2), 2, "DIST", 30), Source(1, (2, 2), 3, "MELE", 30))
        rows = [{"Slot": str(i), "LowId": str(i * 2 + 1), "HighId": str(i * 2 + 2), "Ql": "30"} for i in range(3)]
        catalogs = {}
        for row, item_class, attack in zip(rows, ("Armor", "Weapon", "Weapon"), ({}, {"114": 100}, {"103": 100}), strict=True):
            catalogs[audit.pair(row)] = {"Endpoints": json.dumps([
                {"quality": quality, "name": "Test", "itemClassName": item_class, "attack": attack, "nanoUploadIds": []}
                for quality in (1, 100)])}
        profiles = defaultdict(Counter)
        for row, source in zip(rows, (sources[0], sources[2], sources[3]), strict=True):
            profiles[source.root][audit.fingerprint(catalogs[audit.pair(row)])] += 1
        anchors = {0: {"OUDY"}, 1: set(), 2: set()}
        alignments = audit.enumerate_alignments(sources, rows, anchors, catalogs, profiles, 25)
        self.assertEqual(len(alignments), 3)
        self.assertEqual({row["Order"] for row in alignments}, {"LOOTGIVER_FIRST"})
        preferred = [row for row in alignments if all(row["FingerprintObservationCounts"])]
        self.assertEqual(preferred[0]["OmittedSources"][0].root, "ARMO")
        self.assertEqual(preferred[0]["CommonDesiredQl"], {30: [30]})
        rows[2]["Ql"] = "31"
        self.assertEqual(audit.enumerate_alignments(sources, rows, anchors, catalogs, profiles, 25), [])

    def test_control_family_requires_existing_exact_endpoint_bridge(self):
        records = {"CLOH": {"Children": ["SPRC", "PERL"]}, "SPRC": {"Templates": [11]}, "ELSE": {"Children": ["SPRC"]}}
        catalog = {"Endpoints": json.dumps([{"id": 11, "quality": 1, "rdbPresent": True, "rdbReadComplete": True},
                                             {"id": 12, "quality": 50, "rdbPresent": True, "rdbReadComplete": True}])}
        source = Source(1, (2, 2), 1, "CLOH", 30)
        status, details = audit.control_family_gap(records, catalog, source, 30)
        self.assertEqual(status, "PROVEN_EXISTING_FAMILY_ENDPOINT_GAP")
        self.assertEqual(details["canonicalLeaf"], "SPRC")
        self.assertEqual(details["otherCanonicalParents"], ["ELSE"])
        self.assertEqual(details["danglingChildren"], ["PERL"])
        records["SPRC"]["Templates"] = [13]
        self.assertEqual(audit.control_family_gap(records, catalog, source, 30)[0], "ORDERED_ROOT_SUPPORTED_CHILD_UNRESOLVED")


if __name__ == "__main__":
    unittest.main()
