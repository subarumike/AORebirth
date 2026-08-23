#!/usr/bin/env python3

from __future__ import annotations

import csv
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

    def inventory_row(
        self,
        capture_id: str,
        capture_path: str,
        **overrides: object,
    ) -> dict[str, object]:
        row: dict[str, object] = {
            column: "" for column in inventory.INVENTORY_COLUMNS
        }
        row.update(
            {
                "capture_id": capture_id,
                "capture_path": capture_path,
                "classification": "ELSEWHERE",
                "confidence": "confirmed",
                "raw_packet_evidence": "none",
                "repository_reference_count": 0,
                "implementation_reference_count": 0,
                "reason": "test record",
            }
        )
        row.update(overrides)
        return row

    def test_retention_artifacts_do_not_create_usage_references(self) -> None:
        capture_id = "20260714-171439"
        retention_csv = self.repo_root / "docs" / "evidence" / "aosharp_capture_retention.csv"
        retention_md = self.repo_root / "docs" / "generated" / "aosharp_capture_retention.md"
        retained_evidence = self.repo_root / "docs" / "generated" / "accepted-evidence.md"
        retention_csv.parent.mkdir(parents=True)
        retention_md.parent.mkdir(parents=True)
        retention_csv.write_text(capture_id + "\n", encoding="utf-8")
        retention_md.write_text(capture_id + "\n", encoding="utf-8")
        retained_evidence.write_text(capture_id + "\n", encoding="utf-8")

        documented, indexed = inventory.collect_repository_references(self.repo_root)

        self.assertEqual(
            {"docs/generated/accepted-evidence.md"},
            documented[capture_id],
        )
        self.assertEqual(
            {"docs/generated/accepted-evidence.md"},
            indexed[capture_id],
        )

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

    def test_human_readable_capture_folder_retains_machine_capture_id(self) -> None:
        human_capture = (
            self.repo_root
            / "Captures"
            / "ICC Shuttleport [PF 4582] - 20260818-143201"
        )
        human_capture.mkdir(parents=True)
        (human_capture / "capture_info.json").write_text(
            json.dumps({"playfieldId": 4582}) + "\n",
            encoding="utf-8",
        )

        captures = inventory.discover_capture_directories(self.repo_root)
        row = inventory.inspect_capture(self.repo_root, human_capture, {}, {})

        self.assertIn(human_capture, captures)
        self.assertEqual("20260818-143201", row["capture_id"])

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

    def test_merge_preserves_historical_row_when_folder_is_absent(self) -> None:
        historical = self.inventory_row(
            "20260101-000001",
            "legacy/20260101-000001",
        )

        merged, counts = inventory.merge_inventory([historical], [])

        self.assertEqual([historical], merged)
        self.assertEqual(1, counts["preserved"])
        self.assertEqual(0, counts["removed"])

    def test_merge_appends_new_local_capture(self) -> None:
        historical = self.inventory_row(
            "20260101-000001",
            "legacy/20260101-000001",
        )
        current = inventory.inspect_capture(self.repo_root, self.capture, {}, {})

        merged, counts = inventory.merge_inventory([historical], [current])

        self.assertEqual(
            ["20260101-000001", "20260714-171439"],
            [row["capture_id"] for row in merged],
        )
        self.assertEqual(1, counts["appended"])
        self.assertEqual(0, counts["removed"])

    def test_merge_refreshes_existing_local_capture_without_duplication(self) -> None:
        current = inventory.inspect_capture(self.repo_root, self.capture, {}, {})
        accepted = self.inventory_row(
            str(current["capture_id"]),
            str(current["capture_path"]),
            classification="UNRESOLVED",
        )

        merged, counts = inventory.merge_inventory([accepted], [current])

        self.assertEqual(1, len(merged))
        self.assertEqual("SUBWAY", merged[0]["classification"])
        self.assertEqual(current["evidence_digest"], merged[0]["evidence_digest"])
        self.assertEqual(1, counts["refreshed"])
        self.assertEqual(0, counts["appended"])

    def test_merge_fails_closed_on_conflicting_identity(self) -> None:
        current = inventory.inspect_capture(self.repo_root, self.capture, {}, {})
        accepted = self.inventory_row(
            str(current["capture_id"]),
            "legacy/same-id-different-capture",
            evidence_digest="a" * 64,
        )

        with self.assertRaisesRegex(SystemExit, "identity conflict") as raised:
            inventory.merge_inventory([accepted], [current])

        message = str(raised.exception)
        self.assertIn("legacy/same-id-different-capture", message)
        self.assertIn(str(current["capture_path"]), message)

    def test_merge_fails_closed_on_conflicting_digest(self) -> None:
        current = inventory.inspect_capture(self.repo_root, self.capture, {}, {})
        accepted = self.inventory_row(
            "20260101-000001",
            "legacy/different-id-same-capture",
            evidence_digest=str(current["evidence_digest"]),
        )

        with self.assertRaisesRegex(SystemExit, "digest conflict") as raised:
            inventory.merge_inventory([accepted], [current])

        message = str(raised.exception)
        self.assertIn("20260101-000001", message)
        self.assertIn(str(current["capture_id"]), message)

    def test_normal_merge_never_implicitly_prunes(self) -> None:
        historical = [
            self.inventory_row("20260101-000001", "legacy/20260101-000001"),
            self.inventory_row("20260101-000002", "legacy/20260101-000002"),
        ]
        current = inventory.inspect_capture(self.repo_root, self.capture, {}, {})

        merged, counts = inventory.merge_inventory(historical, [current])

        self.assertEqual(3, len(merged))
        self.assertEqual(2, counts["preserved"])
        self.assertEqual(0, counts["removed"])

    def test_csv_and_markdown_outputs_contain_the_same_inventory_entries(self) -> None:
        rows = [
            self.inventory_row("20260101-000001", "legacy/20260101-000001"),
            self.inventory_row("20260101-000002", "legacy/20260101-000002"),
        ]
        csv_path = self.repo_root / "inventory.csv"
        markdown_path = self.repo_root / "inventory.md"

        inventory.write_csv(csv_path, rows)
        inventory.write_markdown(markdown_path, rows, current_capture_count=0)
        inventory.validate_csv_markdown_sync(rows, markdown_path)
        with csv_path.open("r", encoding="utf-8", newline="") as stream:
            csv_ids = [row["capture_id"] for row in csv.DictReader(stream)]

        self.assertEqual(
            set(csv_ids),
            set(inventory.markdown_inventory_ids(markdown_path)),
        )

    def test_retention_merge_defaults_new_capture_to_retain(self) -> None:
        row = self.inventory_row("20260101-000001", "legacy/20260101-000001")

        merged, counts = inventory.merge_retention_ledger([], [row], self.repo_root)

        self.assertEqual(1, counts["appended"])
        self.assertEqual("retain", merged[0]["retention_state"])
        self.assertEqual("unreviewed", merged[0]["analysis_state"])
        self.assertEqual("unknown", merged[0]["evidence_coverage"])

    def test_retention_merge_preserves_review_and_refreshes_blank_digest(self) -> None:
        row = self.inventory_row(
            "20260101-000001",
            "legacy/20260101-000001",
            evidence_digest="a" * 64,
        )
        retention = inventory.default_retention_row(row)
        retention["evidence_digest"] = ""
        retention["analysis_state"] = "partial"
        retention["evidence_coverage"] = "partial"

        merged, counts = inventory.merge_retention_ledger(
            [retention],
            [row],
            self.repo_root,
        )

        self.assertEqual("a" * 64, merged[0]["evidence_digest"])
        self.assertEqual("partial", merged[0]["analysis_state"])
        self.assertEqual(1, counts["digest_refreshed"])

    def test_retention_fails_closed_on_digest_conflict(self) -> None:
        row = self.inventory_row(
            "20260101-000001",
            "legacy/20260101-000001",
            evidence_digest="a" * 64,
        )
        retention = inventory.default_retention_row(row)
        retention["evidence_digest"] = "b" * 64

        with self.assertRaisesRegex(SystemExit, "retention digest conflict"):
            inventory.merge_retention_ledger([retention], [row], self.repo_root)

    def test_discard_approval_fails_without_complete_evidence(self) -> None:
        row = self.inventory_row(
            "20260101-000001",
            "legacy/20260101-000001",
            evidence_digest="a" * 64,
        )
        retention = inventory.default_retention_row(row)
        retention["retention_state"] = "discard_approved"

        with self.assertRaisesRegex(SystemExit, "discard approval is incomplete"):
            inventory.merge_retention_ledger([retention], [row], self.repo_root)

    def test_retention_csv_and_markdown_are_synchronized(self) -> None:
        rows = [
            inventory.default_retention_row(
                self.inventory_row("20260101-000001", "legacy/20260101-000001")
            ),
            inventory.default_retention_row(
                self.inventory_row("20260101-000002", "legacy/20260101-000002")
            ),
        ]
        csv_path = self.repo_root / "retention.csv"
        markdown_path = self.repo_root / "retention.md"

        inventory.write_retention_csv(csv_path, rows)
        inventory.write_retention_markdown(markdown_path, rows)
        inventory.validate_retention_markdown_sync(rows, markdown_path)

        with csv_path.open("r", encoding="utf-8", newline="") as stream:
            csv_ids = [row["capture_id"] for row in csv.DictReader(stream)]
        self.assertEqual(csv_ids, inventory.retention_markdown_ids(markdown_path))

    def test_legacy_inventory_without_digest_column_is_upgraded(self) -> None:
        path = self.repo_root / "legacy.csv"
        legacy_columns = [
            column
            for column in inventory.INVENTORY_COLUMNS
            if column != "evidence_digest"
        ]
        with path.open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=legacy_columns)
            writer.writeheader()
            writer.writerow(
                {
                    column: value
                    for column, value in self.inventory_row(
                        "20260101-000001",
                        "legacy/20260101-000001",
                    ).items()
                    if column != "evidence_digest"
                }
            )

        rows = inventory.load_inventory(path)

        self.assertEqual(1, len(rows))
        self.assertEqual("", rows[0]["evidence_digest"])

    def test_explicit_exclusion_omits_only_new_current_capture(self) -> None:
        selected = inventory.select_current_capture_paths(
            [self.capture],
            {"20260714-171439"},
            [],
        )

        self.assertEqual([], selected)

    def test_explicit_exclusion_cannot_prune_accepted_capture(self) -> None:
        accepted = self.inventory_row(
            "20260714-171439",
            "captures/20260714-171439",
        )

        with self.assertRaisesRegex(SystemExit, "cannot prune"):
            inventory.select_current_capture_paths(
                [self.capture],
                {"20260714-171439"},
                [accepted],
            )

    def test_capture_id_cutoff_excludes_newer_concurrent_capture(self) -> None:
        newer = self.repo_root / "captures" / "20260823-030524"
        newer.mkdir(parents=True)
        (newer / "capture_info.json").write_text("{}\n", encoding="utf-8")

        selected = inventory.select_current_capture_paths(
            [self.capture, newer],
            set(),
            [],
            "20260714-171439",
        )

        self.assertEqual([self.capture], selected)

    def test_capture_source_signature_detects_concurrent_write(self) -> None:
        before = inventory.capture_source_signature(self.capture)

        (self.capture / "capture_info.json").write_text(
            json.dumps({"playfieldId": 127, "captureEndUtc": "later"}) + "\n",
            encoding="utf-8",
        )
        after = inventory.capture_source_signature(self.capture)

        self.assertNotEqual(before, after)


if __name__ == "__main__":
    unittest.main()
