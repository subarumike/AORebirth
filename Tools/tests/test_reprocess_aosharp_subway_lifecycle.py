#!/usr/bin/env python3

from __future__ import annotations

import csv
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import reprocess_aosharp_subway_lifecycle as lifecycle


class SubwayLifecycleReprocessTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.repo_root = Path(self.temporary.name)
        self.manifest = self.repo_root / "capture-inventory.csv"
        self.output = self.repo_root / "lifecycle-report.csv"
        self.config = lifecycle.ReprocessConfig(
            repo_root=self.repo_root,
            manifest_path=self.manifest,
            output_path=self.output,
            analyzer_path=self.repo_root / "fake-analyzer.exe",
            decoder_path=self.repo_root / "fake-decoder.py",
            python_executable=sys.executable,
        )
        self.rows = self._create_reviewed_manifest()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def _create_reviewed_manifest(self) -> list[dict[str, str]]:
        rows: list[dict[str, str]] = []
        for ordinal in range(lifecycle.EXPECTED_RAW_CAPTURE_COUNT):
            capture_id = f"20260101-{ordinal:06d}"
            capture_path = f"captures/{capture_id}"
            folder = self.repo_root / capture_path
            folder.mkdir(parents=True)
            (folder / "packets.hex.log").write_text("packet\n", encoding="utf-8")
            rows.append(
                {
                    "capture_id": capture_id,
                    "capture_path": capture_path,
                    "classification": "SUBWAY" if ordinal % 2 == 0 else "MIXED",
                    "raw_packet_evidence": "packets.hex.log",
                }
            )
        for capture_id in sorted(lifecycle.NO_RAW_CAPTURE_IDS):
            capture_path = f"captures/{capture_id}"
            folder = self.repo_root / capture_path
            folder.mkdir(parents=True)
            if capture_id == "20260714-171439":
                (folder / "packets.hex.log").write_bytes(b"\xef\xbb\xbf")
                (folder / "raw-packets.csv").write_text(
                    "CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,"
                    "Sequence,PacketLength,N3TypeValue,N3TypeName,IdentityType,"
                    "IdentityInstance,PreservationStatus,RawHex\n",
                    encoding="utf-8",
                )
            rows.append(
                {
                    "capture_id": capture_id,
                    "capture_path": capture_path,
                    "classification": "SUBWAY",
                    "raw_packet_evidence": "none",
                }
            )
        self._write_manifest(rows)
        return rows

    def _write_manifest(self, rows: list[dict[str, str]]) -> None:
        with self.manifest.open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(
                stream,
                fieldnames=(
                    "capture_id",
                    "capture_path",
                    "classification",
                    "raw_packet_evidence",
                ),
                lineterminator="\n",
            )
            writer.writeheader()
            writer.writerows(rows)

    @staticmethod
    def _summary(
        *,
        processing: bool,
        recapture: bool = False,
        offline: bool = False,
        raw_cfu: int = 3,
        decoded_cfu: int = 3,
        cfu_errors: int = 0,
        corpse_status: str = "corpse_full_update_decode_complete",
        local_corpse_evidence: bool = True,
    ) -> dict[str, object]:
        return {
            "capabilityStatus": (
                "raw_source_recapture_required"
                if recapture
                else "offline_scfu_decode_required"
                if offline or not processing
                else "scfu_decode_complete"
            ),
            "processingAllowed": processing,
            "outputsPromoted": processing,
            "recaptureRequired": recapture,
            "offlineDecodeRequired": offline,
            "corpseCapabilityStatus": corpse_status,
            "localCorpseEvidenceObserved": local_corpse_evidence,
            "rawCorpseFullUpdatePackets": raw_cfu,
            "corpseFullUpdateRows": decoded_cfu,
            "corpseFullUpdateDecodeErrorCount": cfu_errors,
            "enemyRespawnCompleteRows": 1,
            "enemyRespawnAmbiguousRows": 0,
            "enemyRespawnIncompleteRows": 2,
            "rawSimpleCharFullUpdatePackets": 4,
            "decodedSimpleCharFullUpdateRows": 4 if processing else 3,
            "simpleCharFullUpdateDecodeErrors": 0 if processing else 1,
        }

    def test_snapshot_only_corpse_evidence_is_not_decoder_debt(self) -> None:
        summary = self._summary(
            processing=True,
            raw_cfu=0,
            decoded_cfu=0,
            corpse_status="no_raw_corpse_full_update_observed",
        )
        self.assertEqual(
            lifecycle.RESULT_PASS,
            lifecycle._classify_result(
                lifecycle.ToolResult(0),
                lifecycle.ToolResult(0),
                "promoted",
                summary,
            ),
        )

    def test_raw_corpse_decode_error_remains_offline_debt(self) -> None:
        summary = self._summary(
            processing=False,
            offline=True,
            raw_cfu=1,
            decoded_cfu=0,
            cfu_errors=1,
            corpse_status="offline_corpse_decode_required",
        )
        self.assertEqual(
            lifecycle.RESULT_OFFLINE_REPAIR_REQUIRED,
            lifecycle._classify_result(
                lifecycle.ToolResult(1),
                lifecycle.ToolResult(1),
                "pending",
                summary,
            ),
        )

    def test_raw_corpse_contradictions_are_tool_errors(self) -> None:
        summary = self._summary(
            processing=True,
            raw_cfu=1,
            decoded_cfu=0,
            corpse_status="no_raw_corpse_full_update_observed",
        )
        self.assertEqual(
            lifecycle.RESULT_TOOL_ERROR,
            lifecycle._classify_result(
                lifecycle.ToolResult(0),
                lifecycle.ToolResult(0),
                "promoted",
                summary,
            ),
        )

    @staticmethod
    def _write_summary(
        capture_path: Path, kind: str, summary: dict[str, object]
    ) -> None:
        suffix = "" if kind == "promoted" else ".pending"
        path = capture_path / f"npc-lifecycle-summary{suffix}.json"
        path.write_text(json.dumps(summary) + "\n", encoding="utf-8")

    def test_manifest_selects_65_raw_captures_and_skips_all_no_raw(self) -> None:
        selected = lifecycle.load_manifest(self.config)

        self.assertEqual(65, len(selected))
        self.assertEqual(
            sorted(entry.capture_id for entry in selected),
            [entry.capture_id for entry in selected],
        )
        self.assertTrue(
            lifecycle.NO_RAW_CAPTURE_IDS.isdisjoint(
                entry.capture_id for entry in selected
            )
        )
        self.assertTrue(
            all(entry.raw_packet_evidence != "none" for entry in selected)
        )

    def test_bom_only_log_and_header_only_csv_are_not_raw_evidence(self) -> None:
        entry = next(
            row for row in self.rows if row["capture_id"] == "20260714-171439"
        )
        capture_path = self.repo_root / entry["capture_path"]
        self.assertEqual(
            0,
            lifecycle.packets_hex_row_count(capture_path / "packets.hex.log"),
        )
        self.assertEqual(
            0,
            lifecycle.raw_packets_csv_row_count(capture_path / "raw-packets.csv"),
        )

        entry["raw_packet_evidence"] = "both"
        self._write_manifest(self.rows)
        with self.assertRaises(lifecycle.ManifestDriftError):
            lifecycle.load_manifest(self.config)

    def test_manifest_drift_stops_before_processing_and_preserves_report(self) -> None:
        self.rows[0]["raw_packet_evidence"] = "both"
        self._write_manifest(self.rows)
        self.output.write_text("existing-report\n", encoding="utf-8")
        calls: list[tuple[str, ...]] = []

        with self.assertRaises(lifecycle.ManifestDriftError):
            lifecycle.execute(
                self.config,
                lambda command, _cwd: (
                    calls.append(tuple(command)) or lifecycle.ToolResult(0)
                ),
            )

        self.assertEqual([], calls)
        self.assertEqual(
            "existing-report\n", self.output.read_text(encoding="utf-8")
        )

    def test_analyzer_precedes_decoder_and_repair_results_are_distinct(self) -> None:
        entry = lifecycle.load_manifest(self.config)[0]
        calls: list[str] = []

        def runner(command: list[str], _cwd: Path) -> lifecycle.ToolResult:
            if len(command) == 2:
                calls.append("analyzer")
                return lifecycle.ToolResult(1)
            calls.append("decoder")
            self._write_summary(
                entry.resolved_path,
                "pending",
                self._summary(processing=False, offline=True),
            )
            return lifecycle.ToolResult(1)

        row = lifecycle.process_capture(self.config, entry, runner)

        self.assertEqual(["analyzer", "decoder"], calls)
        self.assertEqual("pending", row["summary_kind"])
        self.assertEqual(lifecycle.RESULT_OFFLINE_REPAIR_REQUIRED, row["result"])

        recapture_summary = self._summary(
            processing=False, recapture=True, offline=False
        )
        self.assertEqual(
            lifecycle.RESULT_RAW_RECAPTURE_REQUIRED,
            lifecycle._classify_result(
                lifecycle.ToolResult(1),
                lifecycle.ToolResult(1),
                "pending",
                recapture_summary,
            ),
        )

    def test_stale_summary_and_exit_contradictions_are_tool_errors(self) -> None:
        entry = lifecycle.load_manifest(self.config)[0]
        stale = self._summary(processing=True)
        self._write_summary(entry.resolved_path, "promoted", stale)
        stale_path = entry.resolved_path / "npc-lifecycle-summary.json"
        os.utime(stale_path, (1, 1))

        row = lifecycle.process_capture(
            self.config,
            entry,
            lambda _command, _cwd: lifecycle.ToolResult(0),
        )

        self.assertEqual("", row["summary_kind"])
        self.assertEqual("", row["capability_status"])
        self.assertEqual(lifecycle.RESULT_TOOL_ERROR, row["result"])
        self.assertEqual(
            lifecycle.RESULT_TOOL_ERROR,
            lifecycle._classify_result(
                lifecycle.ToolResult(1),
                lifecycle.ToolResult(0),
                "promoted",
                stale,
            ),
        )
        salvaged = self._summary(processing=True)
        salvaged["capabilityStatus"] = (
            "raw_source_legacy_terminal_tail_salvaged"
        )
        self.assertEqual(
            lifecycle.RESULT_PASS,
            lifecycle._classify_result(
                lifecycle.ToolResult(1),
                lifecycle.ToolResult(0),
                "promoted",
                salvaged,
            ),
        )

    def test_batch_continues_and_writes_complete_stable_atomic_report(self) -> None:
        decoder_calls = 0

        def runner(command: list[str], _cwd: Path) -> lifecycle.ToolResult:
            nonlocal decoder_calls
            if len(command) == 2:
                return lifecycle.ToolResult(0)
            decoder_calls += 1
            capture = Path(command[-1])
            if capture.name == "20260101-000000":
                self._write_summary(
                    capture,
                    "pending",
                    self._summary(processing=False, offline=True),
                )
                return lifecycle.ToolResult(1)
            self._write_summary(
                capture,
                "promoted",
                self._summary(processing=True),
            )
            return lifecycle.ToolResult(0)

        exit_code, rows = lifecycle.execute(self.config, runner)
        first_bytes = self.output.read_bytes()
        with self.output.open("r", encoding="utf-8", newline="") as stream:
            report_rows = list(csv.DictReader(stream))

        self.assertEqual(1, exit_code)
        self.assertEqual(65, decoder_calls)
        self.assertEqual(65, len(rows))
        self.assertEqual(65, len(report_rows))
        self.assertEqual(list(lifecycle.REPORT_COLUMNS), list(report_rows[0]))
        self.assertEqual(
            lifecycle.RESULT_OFFLINE_REPAIR_REQUIRED,
            report_rows[0]["result"],
        )
        self.assertEqual(
            64,
            sum(row["result"] == lifecycle.RESULT_PASS for row in report_rows),
        )

        lifecycle.write_report_atomic(self.output, reversed(rows))
        self.assertEqual(first_bytes, self.output.read_bytes())
        self.assertEqual([], list(self.output.parent.glob(self.output.name + ".*.tmp")))


if __name__ == "__main__":
    unittest.main()
