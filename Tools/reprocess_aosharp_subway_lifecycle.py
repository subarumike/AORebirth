#!/usr/bin/env python3
"""Deterministically rebuild lifecycle projections for raw Subway captures."""

from __future__ import annotations

import argparse
import csv
import json
import os
import subprocess
import sys
import tempfile
import time
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable, Sequence

from inventory_aosharp_captures import (
    packets_hex_row_count,
    raw_packets_csv_row_count,
)


SUBWAY_CLASSIFICATIONS = frozenset({"SUBWAY", "MIXED"})
GEOMETRY_ONLY_CAPTURE_IDS = frozenset(
    {
        "20260714-185728",
        "20260714-202820",
    }
)
NO_RAW_CAPTURE_IDS = GEOMETRY_ONLY_CAPTURE_IDS | {
    "20260714-171439",
    "20260719-001621",
}
RAW_EVIDENCE_FILES = {
    "none": (False, False),
    "packets.hex.log": (True, False),
    "raw-packets.csv": (False, True),
    "both": (True, True),
}
EXPECTED_SUBWAY_CAPTURE_COUNT = 78
EXPECTED_RAW_CAPTURE_COUNT = 74

REPORT_COLUMNS = (
    "capture_id",
    "capture_path",
    "classification",
    "raw_packet_evidence",
    "analyzer_exit_code",
    "decoder_exit_code",
    "summary_kind",
    "capability_status",
    "processing_allowed",
    "outputs_promoted",
    "recapture_required",
    "offline_decode_required",
    "corpse_capability_status",
    "local_corpse_evidence_observed",
    "raw_corpse_full_update_packets",
    "corpse_full_update_rows",
    "corpse_full_update_decode_errors",
    "enemy_respawn_complete_rows",
    "enemy_respawn_ambiguous_rows",
    "enemy_respawn_incomplete_rows",
    "raw_scfu_packets",
    "decoded_scfu_rows",
    "scfu_decode_errors",
    "result",
)

SUMMARY_FIELDS = {
    "capabilityStatus": str,
    "processingAllowed": bool,
    "outputsPromoted": bool,
    "recaptureRequired": bool,
    "offlineDecodeRequired": bool,
    "corpseCapabilityStatus": str,
    "localCorpseEvidenceObserved": bool,
    "rawCorpseFullUpdatePackets": int,
    "corpseFullUpdateRows": int,
    "corpseFullUpdateDecodeErrorCount": int,
    "enemyRespawnCompleteRows": int,
    "enemyRespawnAmbiguousRows": int,
    "enemyRespawnIncompleteRows": int,
    "rawSimpleCharFullUpdatePackets": int,
    "decodedSimpleCharFullUpdateRows": int,
    "simpleCharFullUpdateDecodeErrors": int,
}

RESULT_PASS = "PASS"
RESULT_OFFLINE_REPAIR_REQUIRED = "OFFLINE_REPAIR_REQUIRED"
RESULT_RAW_RECAPTURE_REQUIRED = "RAW_RECAPTURE_REQUIRED"
RESULT_TOOL_ERROR = "TOOL_ERROR"


class ManifestDriftError(ValueError):
    """Raised before processing when the reviewed capture manifest has drifted."""


@dataclass(frozen=True)
class CaptureEntry:
    capture_id: str
    capture_path: str
    resolved_path: Path
    classification: str
    raw_packet_evidence: str


@dataclass(frozen=True)
class ToolResult:
    exit_code: int | None
    launch_error: str = ""


@dataclass(frozen=True)
class ReprocessConfig:
    repo_root: Path
    manifest_path: Path
    output_path: Path
    analyzer_path: Path
    decoder_path: Path
    python_executable: str


ToolRunner = Callable[[Sequence[str], Path], ToolResult]


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    repo_root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=str(repo_root))
    parser.add_argument(
        "--manifest",
        default="docs/generated/aosharp_capture_inventory.csv",
    )
    parser.add_argument(
        "--output",
        default="docs/generated/aosharp_subway_lifecycle_reprocess.csv",
    )
    parser.add_argument(
        "--analyzer",
        default=(
            "tools-temp/AOSharpCaptureAnalyzer/bin/Debug/"
            "AOSharpCaptureAnalyzer.exe"
        ),
    )
    parser.add_argument(
        "--decoder",
        default="tools-temp/AOSharpLiveCapture/decode_npc_lifecycle_capture.py",
    )
    parser.add_argument("--python-executable", default=sys.executable)
    return parser.parse_args(argv)


def resolve_from_root(repo_root: Path, value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else repo_root / path


def config_from_args(args: argparse.Namespace) -> ReprocessConfig:
    repo_root = Path(args.repo_root).resolve()
    return ReprocessConfig(
        repo_root=repo_root,
        manifest_path=resolve_from_root(repo_root, args.manifest),
        output_path=resolve_from_root(repo_root, args.output),
        analyzer_path=resolve_from_root(repo_root, args.analyzer),
        decoder_path=resolve_from_root(repo_root, args.decoder),
        python_executable=args.python_executable,
    )


def _validate_capture_path(repo_root: Path, capture_id: str, value: str) -> Path:
    if not value:
        raise ManifestDriftError(f"capture {capture_id} has no capture_path")
    unresolved = Path(value)
    resolved = resolve_from_root(repo_root, unresolved).resolve()
    try:
        resolved.relative_to(repo_root)
    except ValueError as error:
        raise ManifestDriftError(
            f"capture {capture_id} path escapes the repository: {value}"
        ) from error
    if resolved.name != capture_id:
        raise ManifestDriftError(
            f"capture {capture_id} path ends in {resolved.name!r}"
        )
    if not resolved.is_dir():
        raise ManifestDriftError(
            f"capture {capture_id} directory is missing: {value}"
        )
    return resolved


def _validate_raw_evidence(entry: CaptureEntry) -> None:
    expected = RAW_EVIDENCE_FILES.get(entry.raw_packet_evidence)
    if expected is None:
        raise ManifestDriftError(
            f"capture {entry.capture_id} has unknown raw_packet_evidence "
            f"{entry.raw_packet_evidence!r}"
        )
    actual = (
        packets_hex_row_count(entry.resolved_path / "packets.hex.log") > 0,
        raw_packets_csv_row_count(entry.resolved_path / "raw-packets.csv") > 0,
    )
    if actual != expected:
        raise ManifestDriftError(
            f"capture {entry.capture_id} raw evidence disagrees with the manifest: "
            f"manifest={entry.raw_packet_evidence} actual="
            f"packets.hex.log:{str(actual[0]).lower()},"
            f"raw-packets.csv:{str(actual[1]).lower()}"
        )


def load_manifest(config: ReprocessConfig) -> list[CaptureEntry]:
    try:
        stream = config.manifest_path.open(
            "r", encoding="utf-8-sig", newline=""
        )
    except OSError as error:
        raise ManifestDriftError(
            f"cannot read capture manifest {config.manifest_path}: {error}"
        ) from error

    with stream:
        reader = csv.DictReader(stream)
        required = {
            "capture_id",
            "capture_path",
            "classification",
            "raw_packet_evidence",
        }
        if reader.fieldnames is None or not required.issubset(reader.fieldnames):
            missing = sorted(required.difference(reader.fieldnames or ()))
            raise ManifestDriftError(
                "capture manifest is missing columns: " + ",".join(missing)
            )

        seen_ids: set[str] = set()
        seen_paths: set[Path] = set()
        subway_entries: list[CaptureEntry] = []
        for row_number, row in enumerate(reader, start=2):
            capture_id = (row.get("capture_id") or "").strip()
            capture_path = (row.get("capture_path") or "").strip()
            classification = (row.get("classification") or "").strip()
            raw_evidence = (row.get("raw_packet_evidence") or "").strip()
            if not capture_id:
                raise ManifestDriftError(
                    f"capture manifest row {row_number} has no capture_id"
                )
            if capture_id in seen_ids:
                raise ManifestDriftError(
                    f"capture manifest repeats capture_id {capture_id}"
                )
            seen_ids.add(capture_id)
            if classification not in SUBWAY_CLASSIFICATIONS:
                continue

            resolved = _validate_capture_path(
                config.repo_root, capture_id, capture_path
            )
            if resolved in seen_paths:
                raise ManifestDriftError(
                    f"capture manifest repeats capture_path {capture_path}"
                )
            seen_paths.add(resolved)
            entry = CaptureEntry(
                capture_id=capture_id,
                capture_path=capture_path,
                resolved_path=resolved,
                classification=classification,
                raw_packet_evidence=raw_evidence,
            )
            _validate_raw_evidence(entry)
            subway_entries.append(entry)

    if len(subway_entries) != EXPECTED_SUBWAY_CAPTURE_COUNT:
        raise ManifestDriftError(
            "expected exactly "
            f"{EXPECTED_SUBWAY_CAPTURE_COUNT} SUBWAY/MIXED captures; "
            f"found {len(subway_entries)}"
        )

    no_raw_entries = {
        entry.capture_id: entry
        for entry in subway_entries
        if entry.raw_packet_evidence == "none"
    }
    if set(no_raw_entries) != NO_RAW_CAPTURE_IDS:
        raise ManifestDriftError(
            "no-raw capture set drifted: expected "
            f"{','.join(sorted(NO_RAW_CAPTURE_IDS))}; found "
            f"{','.join(sorted(no_raw_entries)) or 'none'}"
        )
    for entry in no_raw_entries.values():
        if entry.classification != "SUBWAY":
            raise ManifestDriftError(
                f"no-raw capture {entry.capture_id} is not SUBWAY"
            )

    selected = [
        entry
        for entry in subway_entries
        if entry.raw_packet_evidence != "none"
    ]
    if len(selected) != EXPECTED_RAW_CAPTURE_COUNT:
        raise ManifestDriftError(
            f"expected exactly {EXPECTED_RAW_CAPTURE_COUNT} raw captures; "
            f"found {len(selected)}"
        )
    if any(entry.raw_packet_evidence == "none" for entry in selected):
        raise ManifestDriftError(
            "a selected Subway lifecycle capture has no raw packet evidence"
        )
    return sorted(selected, key=lambda entry: entry.capture_id)


def run_tool(command: Sequence[str], cwd: Path) -> ToolResult:
    try:
        completed = subprocess.run(
            list(command),
            cwd=str(cwd),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            errors="replace",
            check=False,
        )
        return ToolResult(completed.returncode)
    except OSError as error:
        return ToolResult(None, str(error))


def _valid_int(value: object) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _load_fresh_summary(
    capture_path: Path, invocation_started_ns: int
) -> tuple[str, dict[str, object] | None]:
    paths = (
        ("promoted", capture_path / "npc-lifecycle-summary.json"),
        ("pending", capture_path / "npc-lifecycle-summary.pending.json"),
    )
    fresh: list[tuple[int, int, str, Path]] = []
    for preference, (kind, path) in enumerate(paths):
        try:
            modified_ns = path.stat().st_mtime_ns
        except OSError:
            continue
        if modified_ns >= invocation_started_ns:
            fresh.append((modified_ns, -preference, kind, path))
    if not fresh:
        return "", None

    _, _, kind, path = max(fresh)
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            summary = json.load(stream)
    except (OSError, UnicodeError, json.JSONDecodeError):
        return "", None
    if not isinstance(summary, dict):
        return "", None
    for name, expected_type in SUMMARY_FIELDS.items():
        value = summary.get(name)
        if expected_type is int:
            if not _valid_int(value):
                return "", None
        elif not isinstance(value, expected_type):
            return "", None
    if not summary["capabilityStatus"]:
        return "", None
    return kind, summary


def _classify_result(
    analyzer: ToolResult,
    decoder: ToolResult,
    summary_kind: str,
    summary: dict[str, object] | None,
) -> str:
    if (
        analyzer.exit_code is None
        or decoder.exit_code is None
        or analyzer.exit_code not in (0, 1)
        or decoder.exit_code not in (0, 1)
        or summary is None
    ):
        return RESULT_TOOL_ERROR

    processing = summary["processingAllowed"]
    promoted = summary["outputsPromoted"]
    recapture = summary["recaptureRequired"]
    offline = summary["offlineDecodeRequired"]
    if processing != promoted or (recapture and offline):
        return RESULT_TOOL_ERROR

    raw_cfu = summary["rawCorpseFullUpdatePackets"]
    decoded_cfu = summary["corpseFullUpdateRows"]
    cfu_errors = summary["corpseFullUpdateDecodeErrorCount"]
    corpse_status = summary["corpseCapabilityStatus"]
    local_corpse_evidence = summary["localCorpseEvidenceObserved"]
    if raw_cfu == 0:
        corpse_consistent = bool(
            decoded_cfu == 0
            and cfu_errors == 0
            and corpse_status
            == (
                "no_raw_corpse_full_update_observed"
                if local_corpse_evidence
                else "no_corpse_full_update_observed"
            )
        )
    elif cfu_errors:
        corpse_consistent = bool(
            decoded_cfu + cfu_errors == raw_cfu
            and corpse_status == "offline_corpse_decode_required"
            and offline
            and not processing
        )
    else:
        corpse_consistent = bool(
            decoded_cfu == raw_cfu
            and corpse_status == "corpse_full_update_decode_complete"
        )
    if not corpse_consistent:
        return RESULT_TOOL_ERROR

    if summary_kind == "promoted":
        if not processing or recapture or offline:
            return RESULT_TOOL_ERROR
        analyzer_succeeded = analyzer.exit_code == 0
        analyzer_superseded_by_tail_salvage = bool(
            analyzer.exit_code == 1
            and summary["capabilityStatus"]
            == "raw_source_legacy_terminal_tail_salvaged"
        )
        if (
            (analyzer_succeeded or analyzer_superseded_by_tail_salvage)
            and decoder.exit_code == 0
        ):
            return RESULT_PASS
        return RESULT_TOOL_ERROR

    if summary_kind != "pending":
        return RESULT_TOOL_ERROR
    if processing or promoted or decoder.exit_code != 1:
        return RESULT_TOOL_ERROR
    if recapture:
        return RESULT_RAW_RECAPTURE_REQUIRED
    return RESULT_OFFLINE_REPAIR_REQUIRED


def _csv_bool(value: object) -> str:
    return str(bool(value)).lower()


def _csv_exit_code(result: ToolResult) -> str:
    return "" if result.exit_code is None else str(result.exit_code)


def process_capture(
    config: ReprocessConfig,
    entry: CaptureEntry,
    runner: ToolRunner = run_tool,
) -> dict[str, object]:
    invocation_started_ns = time.time_ns()
    analyzer = runner(
        (str(config.analyzer_path), str(entry.resolved_path)),
        config.repo_root,
    )
    decoder = runner(
        (
            config.python_executable,
            str(config.decoder_path),
            str(entry.resolved_path),
        ),
        config.repo_root,
    )
    summary_kind, summary = _load_fresh_summary(
        entry.resolved_path, invocation_started_ns
    )
    result = _classify_result(analyzer, decoder, summary_kind, summary)

    row: dict[str, object] = {
        "capture_id": entry.capture_id,
        "capture_path": entry.capture_path,
        "classification": entry.classification,
        "raw_packet_evidence": entry.raw_packet_evidence,
        "analyzer_exit_code": _csv_exit_code(analyzer),
        "decoder_exit_code": _csv_exit_code(decoder),
        "summary_kind": summary_kind,
        "capability_status": "",
        "processing_allowed": "",
        "outputs_promoted": "",
        "recapture_required": "",
        "offline_decode_required": "",
        "corpse_capability_status": "",
        "local_corpse_evidence_observed": "",
        "raw_corpse_full_update_packets": "",
        "corpse_full_update_rows": "",
        "corpse_full_update_decode_errors": "",
        "enemy_respawn_complete_rows": "",
        "enemy_respawn_ambiguous_rows": "",
        "enemy_respawn_incomplete_rows": "",
        "raw_scfu_packets": "",
        "decoded_scfu_rows": "",
        "scfu_decode_errors": "",
        "result": result,
    }
    if summary is not None:
        row.update(
            {
                "capability_status": summary["capabilityStatus"],
                "processing_allowed": _csv_bool(summary["processingAllowed"]),
                "outputs_promoted": _csv_bool(summary["outputsPromoted"]),
                "recapture_required": _csv_bool(summary["recaptureRequired"]),
                "offline_decode_required": _csv_bool(
                    summary["offlineDecodeRequired"]
                ),
                "corpse_capability_status": summary[
                    "corpseCapabilityStatus"
                ],
                "local_corpse_evidence_observed": _csv_bool(
                    summary["localCorpseEvidenceObserved"]
                ),
                "raw_corpse_full_update_packets": summary[
                    "rawCorpseFullUpdatePackets"
                ],
                "corpse_full_update_rows": summary["corpseFullUpdateRows"],
                "corpse_full_update_decode_errors": summary[
                    "corpseFullUpdateDecodeErrorCount"
                ],
                "enemy_respawn_complete_rows": summary[
                    "enemyRespawnCompleteRows"
                ],
                "enemy_respawn_ambiguous_rows": summary[
                    "enemyRespawnAmbiguousRows"
                ],
                "enemy_respawn_incomplete_rows": summary[
                    "enemyRespawnIncompleteRows"
                ],
                "raw_scfu_packets": summary[
                    "rawSimpleCharFullUpdatePackets"
                ],
                "decoded_scfu_rows": summary[
                    "decodedSimpleCharFullUpdateRows"
                ],
                "scfu_decode_errors": summary[
                    "simpleCharFullUpdateDecodeErrors"
                ],
            }
        )
    return row


def write_report_atomic(
    output_path: Path, rows: Iterable[dict[str, object]]
) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    ordered = sorted(rows, key=lambda row: str(row["capture_id"]))
    temporary_name = ""
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="",
            prefix=output_path.name + ".",
            suffix=".tmp",
            dir=str(output_path.parent),
            delete=False,
        ) as stream:
            temporary_name = stream.name
            writer = csv.DictWriter(
                stream,
                fieldnames=REPORT_COLUMNS,
                extrasaction="raise",
                lineterminator="\n",
            )
            writer.writeheader()
            writer.writerows(ordered)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_name, output_path)
    finally:
        if temporary_name:
            try:
                Path(temporary_name).unlink()
            except FileNotFoundError:
                pass


def execute(
    config: ReprocessConfig, runner: ToolRunner = run_tool
) -> tuple[int, list[dict[str, object]]]:
    entries = load_manifest(config)
    rows = [process_capture(config, entry, runner) for entry in entries]
    write_report_atomic(config.output_path, rows)
    return (
        0 if all(row["result"] == RESULT_PASS for row in rows) else 1,
        rows,
    )


def main(argv: Sequence[str] | None = None) -> int:
    config = config_from_args(parse_args(argv))
    try:
        exit_code, rows = execute(config)
    except ManifestDriftError as error:
        print(f"Subway lifecycle manifest drift: {error}", file=sys.stderr)
        return 2
    except OSError as error:
        print(f"Subway lifecycle report failure: {error}", file=sys.stderr)
        return 1

    counts = Counter(str(row["result"]) for row in rows)
    summary = " ".join(
        f"{name}={counts.get(name, 0)}"
        for name in (
            RESULT_PASS,
            RESULT_OFFLINE_REPAIR_REQUIRED,
            RESULT_RAW_RECAPTURE_REQUIRED,
            RESULT_TOOL_ERROR,
        )
    )
    print(f"Subway lifecycle reprocess rows={len(rows)} {summary}")
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
