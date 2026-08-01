#!/usr/bin/env python3
"""Extract exact NPC-first local-player attack evidence from an Arete capture."""

from __future__ import annotations

import argparse
import bisect
import csv
import io
import math
import statistics
import struct
import sys
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import audit_movement_promotion_candidates as movement_audit


REPO_ROOT = Path(__file__).resolve().parents[2]
PRIOR_PLAYER_ATTACK_SECONDS = 30.0
MOVEMENT_EVIDENCE_SECONDS = 5.0


@dataclass(frozen=True)
class AttackEvidence:
    captured_utc: datetime
    sequence: int
    identity: str
    name: str
    family: int
    template: int
    level: int
    playfield: int
    generation: int
    player_attacked_first: bool
    prior_player_attack_seconds: float | None
    movement_delta_seconds: float | None
    movement_span_meters: float | None
    attack_distance_meters: float | None


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--self-test", action="store_true")
    parser.add_argument("--capture-folder", type=Path)
    parser.add_argument("--events", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--runtime-dataset", type=Path)
    return parser.parse_args(argv)


def read_csv(path: Path):
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        yield from csv.DictReader(stream)


def parse_time(value: str) -> datetime:
    result = datetime.fromisoformat(value.strip().replace("Z", "+00:00"))
    if result.tzinfo is None:
        result = result.replace(tzinfo=timezone.utc)
    return result.astimezone(timezone.utc)


def normalize_identity(value: str | None) -> str | None:
    return movement_audit.normalize_identity(value)


def horizontal_span(row: movement_audit.MovementRow) -> float:
    return math.hypot(row.end.x - row.start.x, row.end.z - row.start.z)


def decode_local_player_positions(
    capture: Path,
) -> tuple[list[datetime], list[movement_audit.Point]]:
    values: list[tuple[datetime, movement_audit.Point]] = []
    marker = bytes.fromhex("54111123")
    for row in read_csv(capture / "raw-packets.csv"):
        if (
            (row.get("Direction") or "").strip() != "OUT"
            or (row.get("N3TypeName") or "").strip() != "CharDCMove"
        ):
            continue
        try:
            payload = bytes.fromhex((row.get("RawHex") or "").strip())
        except ValueError:
            continue
        body = payload.find(marker)
        coordinate_offset = body + 30
        if body < 0 or len(payload) < coordinate_offset + 12:
            continue
        x, y, z = struct.unpack(
            ">fff",
            payload[coordinate_offset : coordinate_offset + 12],
        )
        if not all(math.isfinite(value) for value in (x, y, z)):
            continue
        values.append(
            (
                parse_time(row["CapturedUtc"]),
                movement_audit.Point(x, y, z),
            )
        )
    values.sort(key=lambda value: value[0])
    return [value[0] for value in values], [value[1] for value in values]


def latest_point(
    values: tuple[list[datetime], list[movement_audit.Point]],
    captured_utc: datetime,
    maximum_age_seconds: float = 2.0,
) -> movement_audit.Point | None:
    times, points = values
    index = bisect.bisect_right(times, captured_utc) - 1
    if index < 0 or (captured_utc - times[index]).total_seconds() > maximum_age_seconds:
        return None
    return points[index]


def closest_movement(
    movements: dict[str, list[movement_audit.MovementRow]],
    identity: str,
    captured_utc: datetime,
) -> tuple[float | None, float | None]:
    rows = movements.get(identity, [])
    if not rows:
        return None, None
    times = [row.captured_utc for row in rows]
    insertion = bisect.bisect_left(times, captured_utc)
    candidates = rows[max(0, insertion - 3) : min(len(rows), insertion + 3)]
    eligible = [
        row
        for row in candidates
        if abs((row.captured_utc - captured_utc).total_seconds())
        <= MOVEMENT_EVIDENCE_SECONDS
    ]
    if not eligible:
        return None, None
    selected = min(
        eligible,
        key=lambda row: (
            abs((row.captured_utc - captured_utc).total_seconds()),
            row.captured_utc,
            row.sequence,
        ),
    )
    return (
        (selected.captured_utc - captured_utc).total_seconds(),
        horizontal_span(selected),
    )


def build_evidence(capture: Path) -> list[AttackEvidence]:
    lifecycle, _ = movement_audit.load_lifecycle(capture)
    generation_index = movement_audit.build_generation_index(lifecycle)
    metadata_index, _ = movement_audit.load_metadata(capture, generation_index)
    movement_rows, _ = movement_audit.load_movements(capture)
    npc_positions = movement_audit.load_positions(capture)
    local_player_positions = decode_local_player_positions(capture)
    movements: dict[str, list[movement_audit.MovementRow]] = defaultdict(list)
    for row in movement_rows:
        movements[row.source_identity].append(row)

    combat_rows = sorted(
        read_csv(capture / "enemy-combat.csv"),
        key=lambda row: (
            parse_time(row["CapturedUtc"]),
            int((row.get("Sequence") or "0").strip() or "0"),
        ),
    )
    latest_player_attack: dict[str, datetime] = {}
    result: list[AttackEvidence] = []
    for row in combat_rows:
        captured_utc = parse_time(row["CapturedUtc"])
        message_type = (row.get("MessageType") or "").strip()
        source_role = (row.get("SourceRole") or "").strip()
        target_role = (row.get("TargetRole") or "").strip()
        source = normalize_identity(row.get("SourceIdentity"))
        target = normalize_identity(row.get("TargetIdentity"))

        if (
            message_type == "Attack"
            and source_role == "local-player"
            and target_role == "enemy"
            and target is not None
        ):
            latest_player_attack[target] = captured_utc
            continue

        if message_type == "StopFight":
            for identity in (source, target):
                if identity is not None:
                    latest_player_attack.pop(identity, None)
            continue

        if (
            message_type != "Attack"
            or source_role != "enemy"
            or target_role != "local-player"
            or source is None
        ):
            continue

        generation = movement_audit.generation_at(
            generation_index,
            source,
            captured_utc,
        )
        metadata, _, failure = movement_audit.resolve_metadata(
            metadata_index,
            source,
            generation,
            captured_utc,
        )
        if failure is not None or metadata is None or not metadata.exact:
            continue
        previous = latest_player_attack.get(source)
        previous_seconds = (
            None
            if previous is None
            else (captured_utc - previous).total_seconds()
        )
        player_attacked_first = (
            previous_seconds is not None
            and 0.0 <= previous_seconds <= PRIOR_PLAYER_ATTACK_SECONDS
        )
        movement_delta, movement_span = closest_movement(
            movements,
            source,
            captured_utc,
        )
        npc_position = movement_audit.position_at(
            npc_positions,
            source,
            captured_utc,
        )
        local_player_position = latest_point(local_player_positions, captured_utc)
        attack_distance = (
            None
            if npc_position is None or local_player_position is None
            else movement_audit.horizontal_distance(
                npc_position,
                local_player_position,
            )
        )
        result.append(
            AttackEvidence(
                captured_utc=captured_utc,
                sequence=int((row.get("Sequence") or "0").strip() or "0"),
                identity=source,
                name=metadata.name,
                family=metadata.family,
                template=metadata.template,
                level=metadata.level,
                playfield=metadata.playfield,
                generation=generation,
                player_attacked_first=player_attacked_first,
                prior_player_attack_seconds=previous_seconds,
                movement_delta_seconds=movement_delta,
                movement_span_meters=movement_span,
                attack_distance_meters=attack_distance,
            )
        )
    return result


def render_events(events: list[AttackEvidence]) -> bytes:
    stream = io.StringIO(newline="")
    fieldnames = [
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
    writer = csv.DictWriter(stream, fieldnames=fieldnames, lineterminator="\n")
    writer.writeheader()
    for event in events:
        writer.writerow(
            {
                "CapturedUtc": event.captured_utc.isoformat().replace("+00:00", "Z"),
                "Sequence": event.sequence,
                "SourceIdentity": event.identity,
                "SourceGeneration": event.generation,
                "Name": event.name,
                "NpcFamily": event.family,
                "MonsterData": event.template,
                "Level": event.level,
                "PlayfieldId": event.playfield,
                "PlayerAttackedFirstWithin30Seconds": str(
                    event.player_attacked_first
                ).lower(),
                "PriorPlayerAttackSeconds": (
                    ""
                    if event.prior_player_attack_seconds is None
                    else format(event.prior_player_attack_seconds, ".6f")
                ),
                "NearestNpcPathDeltaSeconds": (
                    ""
                    if event.movement_delta_seconds is None
                    else format(event.movement_delta_seconds, ".6f")
                ),
                "NearestNpcPathSpanMeters": (
                    ""
                    if event.movement_span_meters is None
                    else format(event.movement_span_meters, ".6f")
                ),
                "AttackDistanceMeters": (
                    ""
                    if event.attack_distance_meters is None
                    else format(event.attack_distance_meters, ".6f")
                ),
            }
        )
    return stream.getvalue().encode("utf-8")


def render_report(capture: Path, events: list[AttackEvidence]) -> bytes:
    groups: dict[tuple[str, int, int, int, int], list[AttackEvidence]] = defaultdict(list)
    for event in events:
        groups[
            (
                event.name,
                event.family,
                event.template,
                event.level,
                event.playfield,
            )
        ].append(event)
    lines = [
        f"# Arete NPC-first Aggro Evidence — {capture.name}",
        "",
        "This report uses exact `Attack` packets where the captured source role is `enemy` and target role is `local-player`, resolved through complete-capture SCFU metadata.",
        "",
        f"- Exact enemy-to-local-player attack starts: **{len(events):,}**.",
        f"- No local-player attack to the same NPC in the preceding {PRIOR_PLAYER_ATTACK_SECONDS:.0f} seconds: **{sum(not event.player_attacked_first for event in events):,}**.",
        f"- Attack starts with a nearby decoded NPC path: **{sum(event.movement_span_meters is not None for event in events):,}**.",
        "",
        f"- Attack starts with direct NPC/local-player positions: **{sum(event.attack_distance_meters is not None for event in events):,}**.",
        "",
        "| NPC constraint | Starts | NPC first | Direct attack-start distance (m) | Nearby path spans (m) |",
        "| --- | ---: | ---: | --- | --- |",
    ]
    for key, values in sorted(groups.items()):
        spans = [
            event.movement_span_meters
            for event in values
            if event.movement_span_meters is not None
        ]
        distances = [
            event.attack_distance_meters
            for event in values
            if event.attack_distance_meters is not None
        ]
        span_text = (
            "none"
            if not spans
            else f"{min(spans):.3f}..{max(spans):.3f}; median {statistics.median(spans):.3f}"
        )
        lines.append(
            f"| {key[0]} (family={key[1]}, template={key[2]}, level={key[3]}, pf={key[4]}) "
            f"| {len(values)} | {sum(not event.player_attacked_first for event in values)} "
            f"| {'none' if not distances else f'{min(distances):.3f}..{max(distances):.3f}; median {statistics.median(distances):.3f}'} "
            f"| {span_text} |"
        )
    lines.extend(
        [
            "",
            "## Boundary",
            "",
            "- NPC-first attack packets prove automatic hostility for the exact metadata constraint.",
            "- Direct attack-start distance uses the latest preceding decoded outbound local-player `CharDCMove` coordinate and NPC `enemy-state` coordinate.",
            "- NPC-first direct distances are lower-bound observations for automatic aggro; they do not prove a larger unobserved radius.",
            "",
        ]
    )
    return "\n".join(lines).encode("utf-8")


def render_runtime_dataset(events: list[AttackEvidence]) -> bytes:
    grouped: dict[tuple[str, int, int, int, int], list[AttackEvidence]] = defaultdict(list)
    for event in events:
        if not event.player_attacked_first and event.attack_distance_meters is not None:
            grouped[
                (
                    event.name,
                    event.family,
                    event.template,
                    event.level,
                    event.playfield,
                )
            ].append(event)
    stream = io.StringIO(newline="")
    fieldnames = [
        "Name",
        "NpcFamily",
        "MonsterData",
        "Level",
        "CapturedPlayfieldId",
        "RuntimePlayfieldId",
        "NpcFirstAttackStarts",
        "ObservedAutomaticAggroRadiusMeters",
        "EvidenceCapturedUtc",
        "EvidenceSequence",
    ]
    writer = csv.DictWriter(stream, fieldnames=fieldnames, lineterminator="\n")
    writer.writeheader()
    for key, values in sorted(grouped.items()):
        selected = max(
            values,
            key=lambda event: (
                event.attack_distance_meters,
                -event.sequence,
            ),
        )
        writer.writerow(
            {
                "Name": key[0],
                "NpcFamily": key[1],
                "MonsterData": key[2],
                "Level": key[3],
                "CapturedPlayfieldId": key[4],
                "RuntimePlayfieldId": 6553,
                "NpcFirstAttackStarts": len(values),
                "ObservedAutomaticAggroRadiusMeters": format(
                    selected.attack_distance_meters,
                    ".6f",
                ),
                "EvidenceCapturedUtc": selected.captured_utc.isoformat().replace(
                    "+00:00",
                    "Z",
                ),
                "EvidenceSequence": selected.sequence,
            }
        )
    return stream.getvalue().encode("utf-8")


def write_or_check(path: Path, payload: bytes, write: bool) -> None:
    if write:
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(path.suffix + ".tmp")
        temporary.write_bytes(payload)
        temporary.replace(path)
    elif not path.is_file() or path.read_bytes() != payload:
        raise RuntimeError(f"stale or missing artifact: {path}")


def run_self_test() -> None:
    row = movement_audit.MovementRow(
        ordinal=1,
        captured_utc=datetime(2026, 7, 22, tzinfo=timezone.utc),
        sequence=1,
        source_identity="SimpleChar:00000001",
        source_name="Garbage Flea",
        target_identity=None,
        start=movement_audit.Point(0, 0, 0),
        end=movement_audit.Point(3, 100, 4),
        path_count=2,
    )
    assert horizontal_span(row) == 5.0
    delta, span = closest_movement(
        {row.source_identity: [row]},
        row.source_identity,
        row.captured_utc,
    )
    assert delta == 0.0
    assert span == 5.0
    assert closest_movement({}, row.source_identity, row.captured_utc) == (None, None)
    expected_body = bytes.fromhex(
        "541111230000C350010203040107000000003F000000000000003F800000"
        "4124000041A4000041F60000000030393FC00000C0100000"
    )
    body = expected_body.find(bytes.fromhex("54111123"))
    assert struct.unpack(">fff", expected_body[body + 30 : body + 42]) == (
        10.25,
        20.5,
        30.75,
    )
    print("PASS Arete aggro evidence analyzer self-tests")


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    try:
        if args.self_test:
            run_self_test()
            return 0
        if (
            args.capture_folder is None
            or args.events is None
            or args.report is None
            or args.runtime_dataset is None
        ):
            raise RuntimeError(
                "--capture-folder, --events, --report, and --runtime-dataset are required"
            )
        capture = args.capture_folder.resolve()
        events = build_evidence(capture)
        events_payload = render_events(events)
        report_payload = render_report(capture, events)
        runtime_payload = render_runtime_dataset(events)
        write_or_check(args.events.resolve(), events_payload, args.write)
        write_or_check(args.report.resolve(), report_payload, args.write)
        write_or_check(args.runtime_dataset.resolve(), runtime_payload, args.write)
        print(
            f"{'WROTE' if args.write else 'PASS'} Arete aggro evidence "
            f"events={len(events)} "
            f"npc_first={sum(not event.player_attacked_first for event in events)}"
        )
        return 0
    except Exception as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
