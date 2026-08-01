#!/usr/bin/env python3
"""Normalize the capture-scoped 20260721 robot replay into Arete movement schema 3.

The legacy CSV contains only decoded inbound NpcPath observations for captured
Malfunctioning Cleaning Robot identities in the eleven-member spawn cohort.  This tool preserves those
observations as provenance and promotes only that proven patrol behavior.  It
does not synthesize closing edges, terminal delays, spawn movement, or loops.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import math
import re
import sys
from collections import defaultdict
from dataclasses import dataclass
from datetime import date
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any, Iterable


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CAPTURE_ID = "20260721-Rox-robots"
CAPTURED_PLAYFIELD_ID = 1044525
RUNTIME_PLAYFIELD_ID = 6553
NPC_FAMILY = 1019
MONSTER_DATA = 297023
LEVEL = 1
NPC_NAME = "Malfunctioning Cleaning Robot"
SCHEMA_VERSION = 3
ROUTE_QUANTIZATION_METERS = 0.5
BEHAVIORS = ("patrol", "spawn", "chase", "flee", "leash")
ANALYSIS_BEHAVIORS = BEHAVIORS + ("scripted",)

DEFAULT_DATASET_DIR = (
    REPOSITORY_ROOT / "docs" / "generated" / "arete_20260721_rox_robots_movement"
)
DEFAULT_INPUT = DEFAULT_DATASET_DIR / "source" / "cleaning_robot_patrol_replay.csv"
DEFAULT_REPORT = (
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "arete_20260721_rox_robots_movement_promotion_audit.md"
)
DEFAULT_METADATA_EVIDENCE = (
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "arete_20260722_104809_movement"
    / "patrol.csv",
    REPOSITORY_ROOT
    / "docs"
    / "generated"
    / "arete_20260722_152454_movement"
    / "patrol.csv",
)

EXPECTED_IDENTITIES = {
    "SimpleChar:79543CB6",
    "SimpleChar:797D36A5",
    "SimpleChar:79866518",
    "SimpleChar:7986653C",
    "SimpleChar:79866547",
    "SimpleChar:79866553",
    "SimpleChar:7986655D",
    "SimpleChar:7986655E",
    "SimpleChar:79866560",
    "SimpleChar:79866562",
    "SimpleChar:79866565",
}

SOURCE_COLUMNS = (
    "CapturedUtc",
    "Direction",
    "Sequence",
    "MessageType",
    "SourceType",
    "SourceInstance",
    "SourceIdentity",
    "SourceName",
    "TargetType",
    "TargetInstance",
    "TargetIdentity",
    "TargetName",
    "FollowKind",
    "CurrentX",
    "CurrentY",
    "CurrentZ",
    "DestinationX",
    "DestinationY",
    "DestinationZ",
    "Speed",
    "Animation",
    "Flags",
    "PathCount",
    "RawParams",
    "RawTailHex",
)

DATASET_COLUMNS = (
    "ObservationId",
    "CapturedUtc",
    "Sequence",
    "Behavior",
    "Disposition",
    "Confidence",
    "DecisionReasons",
    "Influences",
    "MetadataResolution",
    "NpcFamily",
    "MonsterData",
    "Level",
    "PlayfieldId",
    "Name",
    "RuntimeIdentity",
    "RuntimeGeneration",
    "RouteSignature",
    "CurrentX",
    "CurrentY",
    "CurrentZ",
    "DestinationX",
    "DestinationY",
    "DestinationZ",
    "HorizontalDistance",
    "PathCount",
)

RUNTIME_COLUMNS = (
    "ObservationId",
    "EquivalentObservationCount",
    "CapturedUtc",
    "Sequence",
    "Behavior",
    "NpcFamily",
    "MonsterData",
    "Level",
    "CapturedPlayfieldId",
    "RuntimePlayfieldId",
    "Name",
    "SourceIdentity",
    "SourceGeneration",
    "RouteSignature",
    "StartX",
    "StartY",
    "StartZ",
    "EndX",
    "EndY",
    "EndZ",
    "DelayAfterSeconds",
    "PathCount",
)

TIMESTAMP_PATTERN = re.compile(
    r"^(?P<date>\d{4}-\d{2}-\d{2})T(?P<hour>\d{2}):(?P<minute>\d{2}):"
    r"(?P<second>\d{2})\.(?P<fraction>\d{1,7})Z$"
)


@dataclass(frozen=True)
class Observation:
    observation_id: str
    captured_utc: str
    timestamp_ticks: int
    sequence: int
    identity: str
    route_signature: str
    start_text: tuple[str, str, str]
    end_text: tuple[str, str, str]
    start: tuple[Decimal, Decimal, Decimal]
    end: tuple[Decimal, Decimal, Decimal]
    path_count: int


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    parser.add_argument("--input", type=Path, default=DEFAULT_INPUT)
    parser.add_argument(
        "--metadata-evidence",
        action="append",
        type=Path,
        dest="metadata_evidence",
        help="Corrected analysis CSV used to correlate exact NPC metadata.",
    )
    parser.add_argument("--dataset-dir", type=Path, default=DEFAULT_DATASET_DIR)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    return parser.parse_args(argv)


def relative_path(path: Path) -> str:
    resolved = path.resolve()
    try:
        return resolved.relative_to(REPOSITORY_ROOT).as_posix()
    except ValueError:
        return resolved.as_posix()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def parse_timestamp_ticks(value: str, ordinal: int) -> int:
    match = TIMESTAMP_PATTERN.fullmatch(value)
    if match is None:
        raise RuntimeError(f"invalid CapturedUtc at source row {ordinal}: {value!r}")
    year, month, day = (
        int(part, 10) for part in match.group("date").split("-")
    )
    hour = int(match.group("hour"), 10)
    minute = int(match.group("minute"), 10)
    second = int(match.group("second"), 10)
    try:
        day_ordinal = date(year, month, day).toordinal()
    except ValueError as exception:
        raise RuntimeError(
            f"invalid CapturedUtc at source row {ordinal}: {value!r}"
        ) from exception
    if hour > 23 or minute > 59 or second > 59:
        raise RuntimeError(f"invalid CapturedUtc at source row {ordinal}: {value!r}")
    fraction = match.group("fraction").ljust(7, "0")
    # Seven decimal places retain the capture's 100 ns ticks.
    return (
        (((day_ordinal * 24) + hour) * 60 + minute) * 60 + second
    ) * 10_000_000 + int(fraction, 10)


def parse_decimal(value: str, field: str, ordinal: int) -> Decimal:
    try:
        result = Decimal(value)
    except InvalidOperation as exception:
        raise RuntimeError(
            f"invalid {field} at source row {ordinal}: {value!r}"
        ) from exception
    if not result.is_finite():
        raise RuntimeError(f"non-finite {field} at source row {ordinal}: {value!r}")
    return result


def quantize(value: Decimal) -> int:
    return round(float(value) / ROUTE_QUANTIZATION_METERS)


def route_signature(
    start: tuple[Decimal, Decimal, Decimal],
    end: tuple[Decimal, Decimal, Decimal],
) -> str:
    first = tuple(quantize(value) for value in start)
    second = tuple(quantize(value) for value in end)
    edge = (first, second) if first <= second else (second, first)
    payload = json.dumps(
        {"quantization": ROUTE_QUANTIZATION_METERS, "edge": edge},
        separators=(",", ":"),
    )
    return hashlib.sha256(payload.encode("ascii")).hexdigest()[:16]


def load_observations(path: Path) -> list[Observation]:
    observations: list[Observation] = []
    identities: set[str] = set()
    previous_timestamp: int | None = None
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if tuple(reader.fieldnames or ()) != SOURCE_COLUMNS:
            raise RuntimeError(f"source header mismatch: {relative_path(path)}")
        for index, row in enumerate(reader, start=1):
            ordinal = index + 1
            required = {
                "Direction": "IN",
                "MessageType": "FollowTarget",
                "SourceType": "SimpleChar",
                "SourceName": NPC_NAME,
                "FollowKind": "NpcPath",
                "PathCount": "2",
            }
            for field, expected in required.items():
                if row[field] != expected:
                    raise RuntimeError(
                        f"unsupported {field} at source row {ordinal}: "
                        f"{row[field]!r} != {expected!r}"
                    )
            if any(
                row[field]
                for field in (
                    "TargetType",
                    "TargetInstance",
                    "TargetIdentity",
                    "TargetName",
                    "RawTailHex",
                )
            ):
                raise RuntimeError(f"unexpected target/tail data at source row {ordinal}")
            expected_identity = f"SimpleChar:{row['SourceInstance']}"
            if row["SourceIdentity"] != expected_identity:
                raise RuntimeError(f"source identity mismatch at source row {ordinal}")
            if expected_identity not in EXPECTED_IDENTITIES:
                raise RuntimeError(
                    f"identity outside captured robot cohort at source row {ordinal}: "
                    f"{expected_identity}"
                )
            try:
                sequence = int(row["Sequence"], 10)
                path_count = int(row["PathCount"], 10)
            except ValueError as exception:
                raise RuntimeError(f"invalid integer at source row {ordinal}") from exception
            timestamp_ticks = parse_timestamp_ticks(row["CapturedUtc"], ordinal)
            if previous_timestamp is not None and timestamp_ticks < previous_timestamp:
                raise RuntimeError(f"source ordering regression at source row {ordinal}")
            previous_timestamp = timestamp_ticks
            start_text = (row["CurrentX"], row["CurrentY"], row["CurrentZ"])
            end_text = (
                row["DestinationX"],
                row["DestinationY"],
                row["DestinationZ"],
            )
            start = tuple(
                parse_decimal(value, field, ordinal)
                for value, field in zip(start_text, ("CurrentX", "CurrentY", "CurrentZ"))
            )
            end = tuple(
                parse_decimal(value, field, ordinal)
                for value, field in zip(
                    end_text,
                    ("DestinationX", "DestinationY", "DestinationZ"),
                )
            )
            observations.append(
                Observation(
                    observation_id=f"m{index:05d}",
                    captured_utc=row["CapturedUtc"],
                    timestamp_ticks=timestamp_ticks,
                    sequence=sequence,
                    identity=expected_identity,
                    route_signature=route_signature(start, end),
                    start_text=start_text,
                    end_text=end_text,
                    start=start,
                    end=end,
                    path_count=path_count,
                )
            )
            identities.add(expected_identity)
    if not observations:
        raise RuntimeError("legacy robot movement source is empty")
    return observations


def validate_metadata_evidence(
    paths: Iterable[Path], observations: Iterable[Observation]
) -> tuple[Path, ...]:
    resolved_paths = tuple(path.resolve() for path in paths)
    route_signatures = {observation.route_signature for observation in observations}
    required_columns = {
        "Disposition",
        "NpcFamily",
        "MonsterData",
        "Level",
        "PlayfieldId",
        "Name",
        "RouteSignature",
    }
    for path in resolved_paths:
        matched = False
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            reader = csv.DictReader(stream)
            if not required_columns.issubset(reader.fieldnames or []):
                raise RuntimeError(
                    f"metadata evidence header mismatch: {relative_path(path)}"
                )
            for row in reader:
                if (
                    row["Disposition"] == "Promotable"
                    and row["NpcFamily"] == str(NPC_FAMILY)
                    and row["MonsterData"] == str(MONSTER_DATA)
                    and row["Level"] == str(LEVEL)
                    and row["PlayfieldId"] == str(CAPTURED_PLAYFIELD_ID)
                    and row["Name"] == NPC_NAME
                    and row["RouteSignature"] in route_signatures
                ):
                    matched = True
                    break
        if not matched:
            raise RuntimeError(
                "legacy robot metadata is not correlated by an exact promoted route in "
                + relative_path(path)
            )
    return resolved_paths


def decimal_text(value: Decimal) -> str:
    text = format(value, "f")
    return text.rstrip("0").rstrip(".") if "." in text else text


def analysis_row(observation: Observation) -> dict[str, str]:
    dx = observation.end[0] - observation.start[0]
    dz = observation.end[2] - observation.start[2]
    horizontal_distance = Decimal(str(math.hypot(float(dx), float(dz))))
    return {
        "ObservationId": observation.observation_id,
        "CapturedUtc": observation.captured_utc,
        "Sequence": str(observation.sequence),
        "Behavior": "patrol",
        "Disposition": "Promotable",
        "Confidence": "100",
        "DecisionReasons": (
            "complete_decoded_path;exact_capture_scoped_identity;"
            "captured_patrol_observation"
        ),
        "Influences": "",
        "MetadataResolution": "legacy_capture_scoped_exact_identity",
        "NpcFamily": str(NPC_FAMILY),
        "MonsterData": str(MONSTER_DATA),
        "Level": str(LEVEL),
        "PlayfieldId": str(CAPTURED_PLAYFIELD_ID),
        "Name": NPC_NAME,
        "RuntimeIdentity": observation.identity,
        "RuntimeGeneration": "0",
        "RouteSignature": observation.route_signature,
        "CurrentX": observation.start_text[0],
        "CurrentY": observation.start_text[1],
        "CurrentZ": observation.start_text[2],
        "DestinationX": observation.end_text[0],
        "DestinationY": observation.end_text[1],
        "DestinationZ": observation.end_text[2],
        "HorizontalDistance": decimal_text(horizontal_distance.quantize(Decimal("0.000001"))),
        "PathCount": str(observation.path_count),
    }


def runtime_equivalence_key(observation: Observation) -> tuple[Any, ...]:
    return (
        "patrol",
        NPC_FAMILY,
        MONSTER_DATA,
        LEVEL,
        CAPTURED_PLAYFIELD_ID,
        NPC_NAME,
        observation.identity,
        0,
        observation.captured_utc,
        observation.route_signature,
        observation.start,
        observation.end,
        observation.path_count,
    )


def build_runtime_rows(observations: Iterable[Observation]) -> list[dict[str, str]]:
    grouped: dict[tuple[Any, ...], list[Observation]] = defaultdict(list)
    for observation in observations:
        grouped[runtime_equivalence_key(observation)].append(observation)
    unique = [
        (
            min(values, key=lambda value: (value.sequence, value.observation_id)),
            len(values),
        )
        for values in grouped.values()
    ]
    by_identity: dict[str, list[tuple[Observation, int]]] = defaultdict(list)
    for value in unique:
        by_identity[value[0].identity].append(value)

    rows: list[dict[str, str]] = []
    for identity in sorted(by_identity):
        values = sorted(
            by_identity[identity],
            key=lambda value: (
                value[0].timestamp_ticks,
                value[0].sequence,
                value[0].observation_id,
            ),
        )
        for index, (observation, equivalent_count) in enumerate(values):
            next_observation = values[index + 1][0] if index + 1 < len(values) else None
            delay = (
                Decimal(next_observation.timestamp_ticks - observation.timestamp_ticks)
                / Decimal(10_000_000)
                if next_observation is not None
                else Decimal(0)
            )
            rows.append(
                {
                    "ObservationId": observation.observation_id,
                    "EquivalentObservationCount": str(equivalent_count),
                    "CapturedUtc": observation.captured_utc,
                    "Sequence": str(observation.sequence),
                    "Behavior": "patrol",
                    "NpcFamily": str(NPC_FAMILY),
                    "MonsterData": str(MONSTER_DATA),
                    "Level": str(LEVEL),
                    "CapturedPlayfieldId": str(CAPTURED_PLAYFIELD_ID),
                    "RuntimePlayfieldId": str(RUNTIME_PLAYFIELD_ID),
                    "Name": NPC_NAME,
                    "SourceIdentity": observation.identity,
                    "SourceGeneration": "0",
                    "RouteSignature": observation.route_signature,
                    "StartX": observation.start_text[0],
                    "StartY": observation.start_text[1],
                    "StartZ": observation.start_text[2],
                    "EndX": observation.end_text[0],
                    "EndY": observation.end_text[1],
                    "EndZ": observation.end_text[2],
                    "DelayAfterSeconds": decimal_text(delay),
                    "PathCount": str(observation.path_count),
                }
            )
    return rows


def render_csv(columns: tuple[str, ...], rows: Iterable[dict[str, str]]) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=columns, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return stream.getvalue().encode("utf-8")


def render_json(value: dict[str, Any]) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")


def render_report(
    source: Path,
    metadata_evidence: tuple[Path, ...],
    observations: list[Observation],
    runtime_rows: list[dict[str, str]],
) -> bytes:
    identities = sorted({observation.identity for observation in observations})
    missing_identities = sorted(EXPECTED_IDENTITIES - set(identities))
    terminal_rows = len(identities)
    lines = [
        "# Arete 20260721 robot movement promotion audit",
        "",
        "## Result",
        "",
        (
            f"The capture-scoped legacy projection reconciles to **{len(observations):,}** "
            f"promotable patrol observations and **{len(runtime_rows):,}** "
            "deduplicated schema-3 runtime rows."
        ),
        "",
        "## Evidence used",
        "",
        f"- `{relative_path(source)}`",
        *[
            f"- `{relative_path(path)}` (exact promoted route and NPC metadata correlation)"
            for path in metadata_evidence
        ],
        f"- Capture: `{CAPTURE_ID}`",
        f"- Exact identities: {len(identities)}",
        f"- Source sha256: `{sha256_file(source)}`",
        "",
        "## Exact behavior promoted",
        "",
        (
            "Every complete inbound `FollowTarget/NpcPath` observation for the observed "
            "Malfunctioning Cleaning Robot identities is promoted as patrol with "
            "its original timestamp, sequence, coordinates, identity, family, template, "
            "level, captured playfield, and generation 0."
        ),
        "",
        (
            f"The final row for each identity has an exact terminal delay of zero "
            f"({terminal_rows} terminal rows). Schema-4 completion therefore falls back "
            "normally and cannot wrap into the removed legacy replay loop."
        ),
        "",
        "## Evidence gaps preserved",
        "",
        "- This projection proves patrol only; it does not infer spawn, chase, flee, leash, or scripted behavior.",
        (
            "- No legacy patrol packet exists for spawn-cohort identity "
            + ", ".join(f"`{identity}`" for identity in missing_identities)
            + "; no route is synthesized for it."
            if missing_identities
            else "- Every identity in the capture-scoped spawn cohort has at least one patrol packet."
        ),
        "- It does not invent route closures, return edges, waypoint fallbacks, or repeat timing.",
        "",
        "The source projection remains under generated non-runtime provenance. No available legacy robot movement evidence was discarded.",
        "",
        "## Deterministic reproduction",
        "",
        "- `python tools-temp/AOSharpCaptureAnalyzer/promote_arete_legacy_robot_movement.py --write`",
        "- `python tools-temp/AOSharpCaptureAnalyzer/aggregate_arete_movement_runtime.py --write`",
        "",
    ]
    return "\n".join(lines).encode("utf-8")


def build_artifacts(
    source: Path,
    metadata_evidence: Iterable[Path],
    dataset_dir: Path,
    report: Path,
) -> tuple[dict[Path, bytes], dict[str, int]]:
    source = source.resolve()
    dataset_dir = dataset_dir.resolve()
    report = report.resolve()
    observations = load_observations(source)
    metadata_evidence = validate_metadata_evidence(metadata_evidence, observations)
    runtime_rows = build_runtime_rows(observations)
    analysis_rows = [analysis_row(value) for value in observations]
    route_groups = len(
        {
            (
                "patrol",
                NPC_FAMILY,
                MONSTER_DATA,
                LEVEL,
                CAPTURED_PLAYFIELD_ID,
                value.route_signature,
            )
            for value in observations
        }
    )

    artifacts: dict[Path, bytes] = {}
    for behavior in ANALYSIS_BEHAVIORS:
        rows = analysis_rows if behavior == "patrol" else []
        artifacts[dataset_dir / f"{behavior}.csv"] = render_csv(DATASET_COLUMNS, rows)
    for behavior in BEHAVIORS:
        rows = runtime_rows if behavior == "patrol" else []
        artifacts[dataset_dir / "runtime" / f"{behavior}.csv"] = render_csv(
            RUNTIME_COLUMNS, rows
        )

    behaviors = {
        behavior: {
            "path": relative_path(dataset_dir / f"{behavior}.csv"),
            "observations": len(observations) if behavior == "patrol" else 0,
            "promotable": len(observations) if behavior == "patrol" else 0,
            "ambiguous": 0,
            "rejected": 0,
        }
        for behavior in ANALYSIS_BEHAVIORS
    }
    analysis_manifest = {
        "schemaVersion": SCHEMA_VERSION,
        "captureId": CAPTURE_ID,
        "expectedObservations": len(observations),
        "reconciledObservations": len(observations),
        "routeGroups": route_groups,
        "totals": {
            "promotable": len(observations),
            "ambiguous": 0,
            "rejected": 0,
        },
        "behaviors": behaviors,
        "inputs": [
            {
                "path": relative_path(path),
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
            for path in (source,) + metadata_evidence
        ],
    }
    runtime_behaviors = {
        behavior: {
            "path": relative_path(dataset_dir / "runtime" / f"{behavior}.csv"),
            "sourceObservations": len(observations) if behavior == "patrol" else 0,
            "runtimeRows": len(runtime_rows) if behavior == "patrol" else 0,
        }
        for behavior in BEHAVIORS
    }
    runtime_manifest = {
        "schemaVersion": SCHEMA_VERSION,
        "captureId": CAPTURE_ID,
        "capturedPlayfieldId": CAPTURED_PLAYFIELD_ID,
        "runtimePlayfieldId": RUNTIME_PLAYFIELD_ID,
        "sourcePromotableObservations": len(observations),
        "deduplicatedRuntimeRows": len(runtime_rows),
        "scriptedRuntimeRows": 0,
        "behaviors": runtime_behaviors,
    }
    artifacts[dataset_dir / "manifest.json"] = render_json(analysis_manifest)
    artifacts[dataset_dir / "runtime" / "manifest.json"] = render_json(runtime_manifest)
    artifacts[report] = render_report(
        source, metadata_evidence, observations, runtime_rows
    )
    return artifacts, {
        "sourceObservations": len(observations),
        "runtimeRows": len(runtime_rows),
        "routeGroups": route_groups,
    }


def write_artifacts(artifacts: dict[Path, bytes]) -> None:
    for path, payload in sorted(artifacts.items(), key=lambda item: str(item[0])):
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(path.suffix + ".tmp")
        temporary.write_bytes(payload)
        temporary.replace(path)


def check_artifacts(artifacts: dict[Path, bytes]) -> None:
    stale = [
        relative_path(path)
        for path, payload in artifacts.items()
        if not path.exists() or path.read_bytes() != payload
    ]
    if stale:
        raise RuntimeError("stale legacy robot movement artifacts: " + ", ".join(sorted(stale)))


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    try:
        artifacts, summary = build_artifacts(
            args.input,
            args.metadata_evidence or DEFAULT_METADATA_EVIDENCE,
            args.dataset_dir,
            args.report,
        )
        if args.write:
            write_artifacts(artifacts)
            print(
                "WROTE Arete legacy robot movement "
                f"sourceObservations={summary['sourceObservations']} "
                f"runtimeRows={summary['runtimeRows']} "
                f"routeGroups={summary['routeGroups']}"
            )
        else:
            check_artifacts(artifacts)
            print("PASS Arete legacy robot movement artifacts are current")
        return 0
    except Exception as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
