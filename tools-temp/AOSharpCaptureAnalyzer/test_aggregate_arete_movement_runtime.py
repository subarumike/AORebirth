#!/usr/bin/env python3
"""Focused tests for deterministic Arete movement runtime aggregation."""

from __future__ import annotations

import csv
import io
import json
import tempfile
import unittest
from pathlib import Path

import aggregate_arete_movement_runtime as aggregate


def runtime_row(observation_id: str, *, end_x: str = "5.25") -> dict[str, str]:
    return {
        "ObservationId": observation_id,
        "EquivalentObservationCount": "1",
        "CapturedUtc": "2026-07-22T10:48:09.123456Z",
        "Sequence": "42",
        "Behavior": "patrol",
        "NpcFamily": "25",
        "MonsterData": "17657",
        "Level": "5",
        "CapturedPlayfieldId": "1044525",
        "RuntimePlayfieldId": "6553",
        "Name": "Test NPC",
        "SourceIdentity": "SimpleChar:00000001",
        "SourceGeneration": "3",
        "RouteSignature": "0123456789abcdef",
        "StartX": "1.125",
        "StartY": "2.25",
        "StartZ": "3.5",
        "EndX": end_x,
        "EndY": "6.75",
        "EndZ": "7.875",
        "DelayAfterSeconds": "1.234567",
        "PathCount": "2",
    }


def write_csv(path: Path, columns: tuple[str, ...], rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=columns, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def write_source(
    root: Path,
    directory_name: str,
    capture_id: str,
    patrol_rows: list[dict[str, str]],
    *,
    scripted_runtime_rows: int = 0,
) -> Path:
    analysis_dir = root / directory_name
    runtime_dir = analysis_dir / "runtime"
    runtime_dir.mkdir(parents=True)

    runtime_behaviors: dict[str, dict[str, object]] = {}
    analysis_behaviors: dict[str, dict[str, object]] = {}
    total_promotable = 0
    total_runtime = 0
    for behavior in aggregate.BEHAVIORS:
        rows = patrol_rows if behavior == "patrol" else []
        write_csv(runtime_dir / f"{behavior}.csv", aggregate.SOURCE_COLUMNS, rows)
        source_count = sum(int(row["EquivalentObservationCount"]) for row in rows)
        runtime_behaviors[behavior] = {
            "path": str(runtime_dir / f"{behavior}.csv"),
            "sourceObservations": source_count,
            "runtimeRows": len(rows),
        }
        analysis_rows = [
            {
                "Behavior": behavior,
                "Disposition": "Promotable",
                "DecisionReasons": "complete_decoded_path",
            }
            for _ in range(source_count)
        ]
        write_csv(
            analysis_dir / f"{behavior}.csv",
            ("Behavior", "Disposition", "DecisionReasons"),
            analysis_rows,
        )
        analysis_behaviors[behavior] = {
            "path": str(analysis_dir / f"{behavior}.csv"),
            "observations": source_count,
            "promotable": source_count,
            "ambiguous": 0,
            "rejected": 0,
        }
        total_promotable += source_count
        total_runtime += len(rows)

    write_csv(
        analysis_dir / "scripted.csv",
        ("Behavior", "Disposition", "DecisionReasons"),
        [],
    )
    analysis_behaviors["scripted"] = {
        "path": str(analysis_dir / "scripted.csv"),
        "observations": 0,
        "promotable": 0,
        "ambiguous": 0,
        "rejected": 0,
    }
    runtime_manifest = {
        "schemaVersion": 3,
        "captureId": capture_id,
        "capturedPlayfieldId": 1044525,
        "runtimePlayfieldId": 6553,
        "sourcePromotableObservations": total_promotable,
        "deduplicatedRuntimeRows": total_runtime,
        "scriptedRuntimeRows": scripted_runtime_rows,
        "behaviors": runtime_behaviors,
    }
    analysis_manifest = {
        "schemaVersion": 3,
        "captureId": capture_id,
        "expectedObservations": total_promotable,
        "reconciledObservations": total_promotable,
        "routeGroups": total_promotable,
        "totals": {
            "promotable": total_promotable,
            "ambiguous": 0,
            "rejected": 0,
        },
        "behaviors": analysis_behaviors,
        "inputs": [],
    }
    (runtime_dir / "manifest.json").write_text(
        json.dumps(runtime_manifest), encoding="utf-8"
    )
    (analysis_dir / "manifest.json").write_text(
        json.dumps(analysis_manifest), encoding="utf-8"
    )
    return runtime_dir


def build_for_test(
    sources: list[Path], output_dir: Path, report: Path, expected_rows: int
) -> tuple[dict[Path, bytes], dict[str, int]]:
    return aggregate.build_artifacts(
        sources,
        output_dir,
        report,
        expected_source_promotable=expected_rows,
        expected_runtime_rows=expected_rows,
        expected_runtime_behaviors={
            "patrol": expected_rows,
            "spawn": 0,
            "chase": 0,
            "flee": 0,
            "leash": 0,
        },
    )


class AggregateAreteMovementRuntimeTests(unittest.TestCase):
    def test_capture_scope_eliminates_regenerated_identity_collisions(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first = write_source(root, "first", "20260722-104809", [runtime_row("m00001")])
            second = write_source(root, "second", "20260722-152454", [runtime_row("m00001")])
            output = root / "output"
            artifacts, summary = build_for_test(
                [second, first], output, root / "report.md", 2
            )

            payload = artifacts[(output / "patrol.csv").resolve()].decode("utf-8")
            rows = list(csv.DictReader(io.StringIO(payload)))
            self.assertEqual(
                [row["ObservationId"] for row in rows],
                ["20260722-104809:m00001", "20260722-152454:m00001"],
            )
            self.assertEqual(
                [row["CaptureId"] for row in rows],
                ["20260722-104809", "20260722-152454"],
            )
            self.assertEqual(
                {row["SourceIdentity"] for row in rows}, {"SimpleChar:00000001"}
            )
            self.assertEqual(summary["runtimeRows"], 2)

    def test_exact_geometry_timing_order_and_metadata_are_preserved(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = runtime_row("m00077", end_x="123.456789")
            source = write_source(root, "source", "20260722-104809", [original])
            output = root / "output"
            artifacts, _ = build_for_test([source], output, root / "report.md", 1)
            payload = artifacts[(output / "patrol.csv").resolve()].decode("utf-8")
            result = next(csv.DictReader(io.StringIO(payload)))

            self.assertEqual(result["CaptureId"], "20260722-104809")
            self.assertEqual(result["ObservationId"], "20260722-104809:m00077")
            for column in aggregate.SOURCE_COLUMNS[1:]:
                self.assertEqual(result[column], original[column], column)

    def test_artifact_bytes_are_deterministic_for_reversed_source_order(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first = write_source(root, "first", "20260722-104809", [runtime_row("m1")])
            second = write_source(root, "second", "20260722-152454", [runtime_row("m2")])
            output = root / "output"
            report = root / "report.md"
            forward, _ = build_for_test([first, second], output, report, 2)
            reverse, _ = build_for_test([second, first], output, report, 2)
            self.assertEqual(forward, reverse)

    def test_duplicate_capture_ids_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first = write_source(root, "first", "duplicate", [runtime_row("m1")])
            second = write_source(root, "second", "duplicate", [runtime_row("m2")])
            with self.assertRaisesRegex(RuntimeError, "duplicate capture id"):
                build_for_test([first, second], root / "output", root / "report.md", 2)

    def test_scripted_runtime_rows_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = write_source(
                root,
                "source",
                "20260722-104809",
                [runtime_row("m1")],
                scripted_runtime_rows=1,
            )
            with self.assertRaisesRegex(RuntimeError, "scripted runtime rows are forbidden"):
                build_for_test([source], root / "output", root / "report.md", 1)


if __name__ == "__main__":
    unittest.main()
