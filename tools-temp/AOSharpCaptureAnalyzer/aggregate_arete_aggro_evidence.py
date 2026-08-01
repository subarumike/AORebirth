#!/usr/bin/env python3
"""Aggregate exact Arete NPC-first attack evidence across capture projections."""

from __future__ import annotations

import argparse
import csv
import io
import json
import math
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, replace
from datetime import datetime, timezone
from pathlib import Path


CAPTURED_PLAYFIELD_ID = 1044525
RUNTIME_PLAYFIELD_ID = 6553
CAPTURE_ID_PATTERN = re.compile(r"^\d{8}-\d{6}$")

EVENT_FIELDS = [
    "CapturedUtc",
    "Sequence",
    "SourceIdentity",
    "SourceGeneration",
    "Name",
    "NpcFamily",
    "MonsterData",
    "Level",
    "PlayfieldId",
    "PlayerAttackedFirstWithin30Seconds",
    "PriorPlayerAttackSeconds",
    "NearestNpcPathDeltaSeconds",
    "NearestNpcPathSpanMeters",
    "AttackDistanceMeters",
]

RUNTIME_FIELDS = [
    "Name",
    "NpcFamily",
    "MonsterData",
    "Level",
    "CapturedPlayfieldId",
    "RuntimePlayfieldId",
    "NpcFirstAttackStarts",
    "AutomaticAggroEligible",
    "ObservedAutomaticAggroRadiusMeters",
    "RadiusEvidenceKind",
    "RadiusEvidenceCaptureId",
    "RadiusEvidenceCapturedUtc",
    "RadiusEvidenceSequence",
    "ContributingCaptureIds",
]


@dataclass(frozen=True)
class AttackEvent:
    capture_id: str
    captured_utc: datetime
    sequence: int
    source_identity: str
    source_generation: int
    name: str
    npc_family: int
    monster_data: int
    level: int
    playfield_id: int
    player_attacked_first: bool
    attack_distance_meters: float | None

    @property
    def observation_key(self) -> tuple[object, ...]:
        return (
            self.capture_id,
            self.captured_utc,
            self.sequence,
            self.source_identity,
            self.source_generation,
        )

    @property
    def constraint_key(self) -> tuple[object, ...]:
        return (
            self.name,
            self.npc_family,
            self.monster_data,
            self.level,
            self.playfield_id,
        )


@dataclass(frozen=True)
class AggregateResult:
    rows: tuple[dict[str, object], ...]
    source_counts: tuple[tuple[str, int, int], ...]
    raw_event_count: int
    unique_event_count: int
    duplicate_event_count: int
    npc_first_event_count: int


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--self-test", action="store_true")
    parser.add_argument(
        "--source",
        action="append",
        default=[],
        metavar="CAPTURE_ID=EVENTS_CSV",
    )
    parser.add_argument("--runtime-dataset", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--manifest", type=Path)
    return parser.parse_args(argv)


def parse_utc(value: str) -> datetime:
    parsed = datetime.fromisoformat(value.strip().replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def format_utc(value: datetime) -> str:
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_int(value: str, field: str, row_number: int) -> int:
    try:
        return int(value.strip())
    except (AttributeError, ValueError) as exception:
        raise RuntimeError(f"invalid {field} at event row {row_number}") from exception


def parse_optional_float(value: str, field: str, row_number: int) -> float | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        parsed = float(text)
    except ValueError as exception:
        raise RuntimeError(f"invalid {field} at event row {row_number}") from exception
    if not math.isfinite(parsed) or parsed <= 0.0:
        raise RuntimeError(f"invalid {field} at event row {row_number}")
    return parsed


def parse_bool(value: str, field: str, row_number: int) -> bool:
    text = (value or "").strip().lower()
    if text == "true":
        return True
    if text == "false":
        return False
    raise RuntimeError(f"invalid {field} at event row {row_number}")


def parse_source(value: str) -> tuple[str, Path]:
    capture_id, separator, path = value.partition("=")
    if not separator or not CAPTURE_ID_PATTERN.fullmatch(capture_id):
        raise RuntimeError(f"invalid --source value: {value}")
    if not path:
        raise RuntimeError(f"invalid --source value: {value}")
    return capture_id, Path(path).resolve()


def load_event_projection(capture_id: str, path: Path) -> list[AttackEvent]:
    if not path.is_file():
        raise RuntimeError(f"event projection missing: {path}")
    events: list[AttackEvent] = []
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames != EVENT_FIELDS:
            raise RuntimeError(f"event projection header mismatch: {path}")
        for row_number, row in enumerate(reader, start=2):
            try:
                captured_utc = parse_utc(row["CapturedUtc"])
            except (TypeError, ValueError) as exception:
                raise RuntimeError(
                    f"invalid CapturedUtc at event row {row_number}"
                ) from exception
            name = (row["Name"] or "").strip()
            source_identity = (row["SourceIdentity"] or "").strip()
            if not name or not source_identity or "," in name:
                raise RuntimeError(f"invalid identity metadata at event row {row_number}")
            event = AttackEvent(
                capture_id=capture_id,
                captured_utc=captured_utc,
                sequence=parse_int(row["Sequence"], "Sequence", row_number),
                source_identity=source_identity,
                source_generation=parse_int(
                    row["SourceGeneration"],
                    "SourceGeneration",
                    row_number,
                ),
                name=name,
                npc_family=parse_int(row["NpcFamily"], "NpcFamily", row_number),
                monster_data=parse_int(
                    row["MonsterData"],
                    "MonsterData",
                    row_number,
                ),
                level=parse_int(row["Level"], "Level", row_number),
                playfield_id=parse_int(
                    row["PlayfieldId"],
                    "PlayfieldId",
                    row_number,
                ),
                player_attacked_first=parse_bool(
                    row["PlayerAttackedFirstWithin30Seconds"],
                    "PlayerAttackedFirstWithin30Seconds",
                    row_number,
                ),
                attack_distance_meters=parse_optional_float(
                    row["AttackDistanceMeters"],
                    "AttackDistanceMeters",
                    row_number,
                ),
            )
            validate_event(event, row_number)
            events.append(event)
    return events


def validate_event(event: AttackEvent, row_number: int) -> None:
    if event.playfield_id != CAPTURED_PLAYFIELD_ID:
        raise RuntimeError(
            "captured playfield namespace mismatch at event row "
            f"{row_number}: {event.playfield_id}"
        )
    if (
        event.sequence < 0
        or event.source_generation < 0
        or event.npc_family <= 0
        or event.monster_data <= 0
        or event.level <= 0
    ):
        raise RuntimeError(f"invalid exact metadata at event row {row_number}")


def aggregate_events(events: list[AttackEvent]) -> AggregateResult:
    raw_event_count = len(events)
    unique: dict[tuple[object, ...], AttackEvent] = {}
    source_raw_counts: dict[str, int] = defaultdict(int)
    for event in events:
        source_raw_counts[event.capture_id] += 1
        existing = unique.get(event.observation_key)
        if existing is None:
            unique[event.observation_key] = event
        elif existing != event:
            raise RuntimeError(
                "contradictory duplicate event: "
                f"{event.capture_id}/{event.sequence}/{event.source_identity}"
            )

    unique_events = sorted(
        unique.values(),
        key=lambda event: (
            event.capture_id,
            event.captured_utc,
            event.sequence,
            event.source_identity,
            event.source_generation,
        ),
    )
    source_unique_counts: dict[str, int] = defaultdict(int)
    for event in unique_events:
        source_unique_counts[event.capture_id] += 1

    npc_first = [event for event in unique_events if not event.player_attacked_first]
    grouped: dict[tuple[object, ...], list[AttackEvent]] = defaultdict(list)
    for event in npc_first:
        grouped[event.constraint_key].append(event)

    rows: list[dict[str, object]] = []
    for key in sorted(
        grouped,
        key=lambda value: (
            str(value[0]).lower(),
            str(value[0]),
            int(value[1]),
            int(value[2]),
            int(value[3]),
            int(value[4]),
        ),
    ):
        values = grouped[key]
        measured = [
            event for event in values if event.attack_distance_meters is not None
        ]
        selected = None
        if measured:
            selected = sorted(
                measured,
                key=lambda event: (
                    -float(event.attack_distance_meters),
                    event.capture_id,
                    event.captured_utc,
                    event.sequence,
                    event.source_identity,
                ),
            )[0]
        contributing = ";".join(sorted({event.capture_id for event in values}))
        rows.append(
            {
                "Name": key[0],
                "NpcFamily": key[1],
                "MonsterData": key[2],
                "Level": key[3],
                "CapturedPlayfieldId": key[4],
                "RuntimePlayfieldId": RUNTIME_PLAYFIELD_ID,
                "NpcFirstAttackStarts": len(values),
                "AutomaticAggroEligible": "true",
                "ObservedAutomaticAggroRadiusMeters": (
                    format(float(selected.attack_distance_meters), ".6f")
                    if selected is not None
                    else ""
                ),
                "RadiusEvidenceKind": (
                    "measured-lower-bound" if selected is not None else "eligibility-only"
                ),
                "RadiusEvidenceCaptureId": (
                    selected.capture_id if selected is not None else ""
                ),
                "RadiusEvidenceCapturedUtc": (
                    format_utc(selected.captured_utc) if selected is not None else ""
                ),
                "RadiusEvidenceSequence": (
                    selected.sequence if selected is not None else ""
                ),
                "ContributingCaptureIds": contributing,
            }
        )

    source_counts = tuple(
        (
            capture_id,
            source_raw_counts[capture_id],
            source_unique_counts[capture_id],
        )
        for capture_id in sorted(source_raw_counts)
    )
    return AggregateResult(
        rows=tuple(rows),
        source_counts=source_counts,
        raw_event_count=raw_event_count,
        unique_event_count=len(unique_events),
        duplicate_event_count=raw_event_count - len(unique_events),
        npc_first_event_count=len(npc_first),
    )


def render_runtime_dataset(result: AggregateResult) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=RUNTIME_FIELDS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(result.rows)
    return stream.getvalue().encode("utf-8")


def render_report(result: AggregateResult) -> bytes:
    measured = sum(
        row["RadiusEvidenceKind"] == "measured-lower-bound" for row in result.rows
    )
    eligibility_only = len(result.rows) - measured
    lines = [
        "# Aggregate Arete NPC-first Aggro Evidence",
        "",
        "This deterministic aggregate consumes the complete per-capture attack-event "
        "projections for the two recovered July 22 Arete captures. Exact NPC metadata "
        "constraints are reconciled before runtime promotion.",
        "",
        "## Reconciliation",
        "",
    ]
    for capture_id, raw_count, unique_count in result.source_counts:
        lines.append(
            f"- `{capture_id}`: **{raw_count:,}** projected enemy-to-player starts; "
            f"**{unique_count:,}** unique."
        )
    lines.extend(
        [
            f"- All projected enemy-to-player starts: **{result.raw_event_count:,}**.",
            f"- Unique projected starts: **{result.unique_event_count:,}**.",
            f"- Exact duplicate projections collapsed: **{result.duplicate_event_count:,}**.",
            f"- NPC-first starts proving automatic-aggro eligibility: **{result.npc_first_event_count:,}**.",
            f"- Exact metadata constraints: **{len(result.rows):,}**.",
            f"- Constraints with a measured direct lower bound: **{measured:,}**.",
            f"- Eligibility-only constraints with no invented radius: **{eligibility_only:,}**.",
            "- Captured playfield namespace: **1044525**; runtime content playfield: **6553**.",
            "",
            "## Promoted constraints",
            "",
            "| Exact NPC constraint | NPC-first starts | Eligibility | Measured direct lower bound (m) | Radius evidence | Contributing captures |",
            "| --- | ---: | --- | ---: | --- | --- |",
        ]
    )
    for row in result.rows:
        constraint = (
            f"{row['Name']} (family={row['NpcFamily']}, template={row['MonsterData']}, "
            f"level={row['Level']}, captured-pf={row['CapturedPlayfieldId']})"
        )
        radius = row["ObservedAutomaticAggroRadiusMeters"] or "none"
        radius_evidence = (
            f"`{row['RadiusEvidenceCaptureId']}` sequence {row['RadiusEvidenceSequence']}"
            if row["RadiusEvidenceCaptureId"]
            else "eligibility only"
        )
        captures = str(row["ContributingCaptureIds"]).replace(";", ", ")
        lines.append(
            f"| {constraint} | {row['NpcFirstAttackStarts']} | proven | {radius} | "
            f"{radius_evidence} | {captures} |"
        )
    lines.extend(
        [
            "",
            "## Runtime boundary",
            "",
            "- Every listed constraint has at least one exact NPC-first attack packet and is therefore eligible for automatic aggro.",
            "- A direct distance is a measured lower bound, not an inferred full sight radius or probability.",
            "- Duplicate exact metadata constraints are collapsed by summing distinct NPC-first starts and selecting the strongest measured lower bound with a deterministic tie-break.",
            "- Player-first attacks remain in the per-capture event projections but do not establish automatic-aggro eligibility.",
            "- Eligibility-only rows are queryable by runtime, while radius lookup fails closed until a direct lower bound exists.",
            "",
        ]
    )
    return "\n".join(lines).encode("utf-8")


def render_manifest(result: AggregateResult) -> bytes:
    measured = sum(
        row["RadiusEvidenceKind"] == "measured-lower-bound" for row in result.rows
    )
    payload = {
        "schemaVersion": 1,
        "capturedPlayfieldId": CAPTURED_PLAYFIELD_ID,
        "runtimePlayfieldId": RUNTIME_PLAYFIELD_ID,
        "sourceCaptures": [
            {
                "captureId": capture_id,
                "projectedAttackStarts": raw_count,
                "uniqueAttackStarts": unique_count,
            }
            for capture_id, raw_count, unique_count in result.source_counts
        ],
        "projectedAttackStarts": result.raw_event_count,
        "uniqueAttackStarts": result.unique_event_count,
        "collapsedExactDuplicates": result.duplicate_event_count,
        "npcFirstAttackStarts": result.npc_first_event_count,
        "exactMetadataConstraints": len(result.rows),
        "measuredLowerBoundConstraints": measured,
        "eligibilityOnlyConstraints": len(result.rows) - measured,
    }
    return (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")


def write_or_check(path: Path, payload: bytes, write: bool) -> None:
    if write:
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(path.suffix + ".tmp")
        temporary.write_bytes(payload)
        temporary.replace(path)
        return
    if not path.is_file() or path.read_bytes() != payload:
        raise RuntimeError(f"stale or missing artifact: {path}")


def run_self_test() -> None:
    instant = datetime(2026, 7, 22, tzinfo=timezone.utc)
    supreme_low = AttackEvent(
        "20260722-104809",
        instant,
        10,
        "SimpleChar:00000001",
        1,
        "Supreme Collector of Waste",
        1019,
        17714,
        4,
        CAPTURED_PLAYFIELD_ID,
        False,
        4.145187,
    )
    supreme_high = replace(
        supreme_low,
        capture_id="20260722-152454",
        sequence=20,
        source_identity="SimpleChar:00000002",
        attack_distance_meters=9.240783,
    )
    flea = AttackEvent(
        "20260722-104809",
        instant,
        30,
        "SimpleChar:00000003",
        1,
        "Garbage Flea",
        25,
        17657,
        1,
        CAPTURED_PLAYFIELD_ID,
        False,
        None,
    )
    result = aggregate_events([supreme_low, supreme_high, flea, supreme_low])
    assert result.raw_event_count == 4
    assert result.unique_event_count == 3
    assert result.duplicate_event_count == 1
    assert result.npc_first_event_count == 3
    assert len(result.rows) == 2
    supreme_row = next(row for row in result.rows if row["Name"].startswith("Supreme"))
    flea_row = next(row for row in result.rows if row["Name"] == "Garbage Flea")
    assert supreme_row["NpcFirstAttackStarts"] == 2
    assert supreme_row["ObservedAutomaticAggroRadiusMeters"] == "9.240783"
    assert supreme_row["RadiusEvidenceCaptureId"] == "20260722-152454"
    assert flea_row["AutomaticAggroEligible"] == "true"
    assert flea_row["ObservedAutomaticAggroRadiusMeters"] == ""
    assert flea_row["RadiusEvidenceKind"] == "eligibility-only"
    assert render_runtime_dataset(result) == render_runtime_dataset(result)
    try:
        aggregate_events([supreme_low, replace(supreme_low, attack_distance_meters=1.0)])
    except RuntimeError as exception:
        assert "contradictory duplicate event" in str(exception)
    else:
        raise AssertionError("contradictory duplicate event was accepted")
    try:
        validate_event(replace(flea, playfield_id=6553), 2)
    except RuntimeError as exception:
        assert "namespace mismatch" in str(exception)
    else:
        raise AssertionError("captured/runtime playfield namespace mix was accepted")
    print("PASS aggregate Arete aggro evidence self-tests")


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    try:
        if args.self_test:
            run_self_test()
            return 0
        if (
            not args.source
            or args.runtime_dataset is None
            or args.report is None
            or args.manifest is None
        ):
            raise RuntimeError(
                "--source, --runtime-dataset, --report, and --manifest are required"
            )
        parsed_sources = [parse_source(value) for value in args.source]
        capture_ids = [capture_id for capture_id, _ in parsed_sources]
        if len(capture_ids) != len(set(capture_ids)):
            raise RuntimeError("duplicate capture id in --source")
        events: list[AttackEvent] = []
        for capture_id, path in sorted(parsed_sources):
            events.extend(load_event_projection(capture_id, path))
        result = aggregate_events(events)
        if not result.rows:
            raise RuntimeError("aggregate contains no NPC-first constraints")
        write_or_check(
            args.runtime_dataset.resolve(),
            render_runtime_dataset(result),
            args.write,
        )
        write_or_check(args.report.resolve(), render_report(result), args.write)
        write_or_check(args.manifest.resolve(), render_manifest(result), args.write)
        mode = "WROTE" if args.write else "PASS"
        measured = sum(
            row["RadiusEvidenceKind"] == "measured-lower-bound"
            for row in result.rows
        )
        print(
            f"{mode} aggregate Arete aggro evidence "
            f"events={result.unique_event_count} npc_first={result.npc_first_event_count} "
            f"constraints={len(result.rows)} measured={measured}"
        )
        return 0
    except (OSError, RuntimeError, ValueError) as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
