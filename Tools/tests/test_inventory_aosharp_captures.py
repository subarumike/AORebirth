#!/usr/bin/env python3

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import inventory_aosharp_captures as inventory


RAW_HEADER = (
    "CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,Sequence,"
    "PacketLength,N3TypeValue,N3TypeName,IdentityType,IdentityInstance,"
    "PreservationStatus,RawHex\n"
)


class AOSharpCaptureInventoryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.repo_root = Path(self.temporary.name)
        self.capture = self.repo_root / "captures" / "20260714-171439"
        self.capture.mkdir(parents=True)
        (self.capture / "capture_info.json").write_text(
            json.dumps({"playfieldId": 127}) + "\n",
            encoding="utf-8",
        )

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_bom_only_log_and_header_only_csv_have_no_raw_rows(self) -> None:
        (self.capture / "packets.hex.log").write_bytes(b"\xef\xbb\xbf")
        (self.capture / "raw-packets.csv").write_text(
            RAW_HEADER,
            encoding="utf-8",
        )

        row = inventory.inspect_capture(self.repo_root, self.capture, {}, {})

        self.assertEqual("SUBWAY", row["classification"])
        self.assertEqual("none", row["raw_packet_evidence"])
        self.assertEqual(0, row["packets_hex_rows"])
        self.assertEqual(0, row["raw_packets_rows"])
        self.assertEqual(3, row["packets_hex_bytes"])
        self.assertGreater(row["raw_packets_bytes"], 0)

    def test_raw_status_uses_data_rows_in_each_format(self) -> None:
        (self.capture / "packets.hex.log").write_text(
            "2026-07-17T00:00:00Z IN #1 len=1 hex=00\n",
            encoding="utf-8",
        )
        (self.capture / "raw-packets.csv").write_text(
            RAW_HEADER
            + '"2026-07-17T00:00:00Z",1,"IN",1,1,1,0,"Unknown",0,0,'
            + '"raw_complete","00"\n',
            encoding="utf-8",
        )

        status, packets_rows, csv_rows = inventory.raw_packet_evidence(
            self.capture
        )

        self.assertEqual("both", status)
        self.assertEqual(1, packets_rows)
        self.assertEqual(1, csv_rows)

    def test_official_pf127_start_only_capture_is_subway_without_raw(self) -> None:
        (self.capture / "capture_info.json").write_text(
            json.dumps(
                {
                    "playfieldId": "(Playfield2:15781E)",
                    "captureEndUtc": None,
                    "validation": {"status": "running"},
                }
            )
            + "\n",
            encoding="utf-8",
        )

        row = inventory.inspect_capture(self.repo_root, self.capture, {}, {})

        self.assertEqual("SUBWAY", row["classification"])
        self.assertEqual("none", row["raw_packet_evidence"])

    def test_reviewed_corpus_contract_includes_latest_four_subway_folders(self) -> None:
        self.assertEqual("20260717-220340", inventory.REVIEWED_CAPTURE_CUTOFF)
        self.assertTrue(
            {
                "20260717-214612",
                "20260717-214751",
                "20260717-215250",
                "20260717-220340",
            }.issubset(inventory.EXPECTED_REVIEWED_SUBWAY_ONLY)
        )
        self.assertEqual(72, inventory.EXPECTED_REVIEWED_SUBWAY_CAPTURE_COUNT)
        self.assertEqual(69, inventory.EXPECTED_REVIEWED_SUBWAY_RAW_CAPTURE_COUNT)
        self.assertTrue(
            {"20260717-214612", "20260717-215250"}.isdisjoint(
                inventory.EXPECTED_REVIEWED_SUBWAY_NO_RAW
            )
        )


if __name__ == "__main__":
    unittest.main()
