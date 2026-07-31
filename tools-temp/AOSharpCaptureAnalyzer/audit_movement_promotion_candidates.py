#!/usr/bin/env python3
"""Fail-closed audit of captured NPC movement promotion candidates.

The tool consumes completed AOSharpLiveCapture projections, removes runtime
identity/generation from the promotion key, classifies movement influence, and
collapses geometrically identical routes.  It produces analysis only and never
modifies AORebirth runtime content.
"""

from __future__ import annotations

import argparse
import bisect
import csv
import hashlib
import json
import math
import sys
import tempfile
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Iterable


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CAPTURE = (
    REPO_ROOT
    / "tools-temp"
    / "AOSharpLiveCapture"
    / "bin"
    / "Debug"
    / "captures"
    / "20260722-152454"
)
DEFAULT_REPORT = (
    REPO_ROOT
    / "docs"
    / "generated"
    / "arete_20260722_152454_movement_promotion_audit.md"
)

SCHEMA_VERSION = 1
ROUTE_QUANTIZATION_METERS = 0.5
MICRO_MOVEMENT_METERS = 0.25
ROUTE_CLOSURE_METERS = 2.0
CONTINUITY_METERS = 6.0
TELEPORT_DISCONTINUITY_METERS = 25.0
MAX_PLAUSIBLE_METERS_PER_SECOND = 15.0
MAX_ROUTE_GAP_SECONDS = 120.0
CONTROL_INFLUENCE_SECONDS = 2.5
COMBAT_CLUSTER_GAP_SECONDS = 15.0
COMBAT_START_PADDING_SECONDS = 2.0
COMBAT_END_PADDING_SECONDS = 5.0
LEASH_WINDOW_SECONDS = 30.0
SPAWN_WINDOW_SECONDS = 5.0
CAPTURE_BOUNDARY_SECONDS = 3.0
POSITION_MAX_AGE_SECONDS = 8.0

REQUIRED_INPUTS = (
    "capture_info.json",
    "movement-summary.json",
    "npc-lifecycle-summary.json",
    "movement-packets.csv",
    "scfu-appearance.csv",
    "enemy-combat.csv",
    "enemy-state.csv",
    "npc-lifecycle.csv",
)

HARD_REJECTION_REASONS = {
    "combat_influence",
    "player_influence",
    "external_target_influence",
    "path_interruption",
    "teleport_or_position_discontinuity",
    "incomplete_capture",
    "metadata_missing",
}

CLASSIFICATION_REJECTION_REASONS = {
    "idle": "idle_or_micro_movement_not_a_route",
    "combat chase": "combat_chase",
    "flee": "combat_flee",
    "leash": "leash_after_combat",
    "spawn": "spawn_transient_not_patrol",
}

CLASSIFICATION_PRECEDENCE = (
    "flee",
    "combat chase",
    "leash",
    "spawn",
    "idle",
    "scripted",
    "patrol",
)

COMBAT_MESSAGE_TYPES = {
    "Attack",
    "AttackInfo",
    "CastNanoSpell",
    "CharSecSpecAttack",
    "HealthDamage",
    "MissedAttackInfo",
    "SpecialAttackInfo",
    "SpecialAttackWeapon",
}

TERMINAL_COMBAT_MESSAGE_TYPES = {"StopFight"}
LIFECYCLE_END_PHASES = {
    "character-gone",
    "death-action",
    "enemy-despawn",
}


@dataclass(frozen=True)
class Point:
    x: float
    y: float
    z: float


@dataclass(frozen=True)
class Metadata:
    captured_utc: datetime
    identity: str
    name: str
    playfield: int | None
    family: int | None
    template: int | None
    level: int | None
    character_info_type: str
    position: Point | None
    decode_complete: bool

    @property
    def is_npc(self) -> bool:
        return self.character_info_type.lower() == "npcinfo"

    @property
    def exact_grouping_available(self) -> bool:
        return (
            self.decode_complete
            and self.is_npc
            and self.playfield is not None
            and self.family is not None
            and self.template is not None
            and self.level is not None
        )


@dataclass(frozen=True)
class MovementRow:
    captured_utc: datetime
    sequence: int
    message_type: str
    source_identity: str
    source_name: str
    target_identity: str | None
    follow_kind: str
    start: Point | None
    end: Point | None
    path_count: int


@dataclass(frozen=True)
class TimedControl:
    captured_utc: datetime
    kind: str
    target_identity: str | None


@dataclass
class CombatInterval:
    start: datetime
    end: datetime
    opponents: set[str] = field(default_factory=set)
    player_influence: bool = False


@dataclass
class AnnotatedPath:
    row: MovementRow
    metadata: Metadata | None
    generation: int
    classification: str
    reasons: set[str]


@dataclass
class RouteTrace:
    source_identity: str
    source_name: str
    generation: int
    metadata: Metadata | None
    classification: str
    reasons: set[str]
    rows: list[MovementRow]
    route_signature: str = ""
    canonical_edges: tuple[tuple[tuple[int, int, int], tuple[int, int, int]], ...] = ()
    representative_points: tuple[tuple[int, int, int], ...] = ()
    closed: bool = False
    branched: bool = False
    edge_repeat_count: int = 0


@dataclass
class CanonicalRoute:
    family: int | None
    template: int | None
    level: int | None
    playfield: int | None
    signature: str
    traces: list[RouteTrace]
    names: set[str]
    identities: set[str]
    generations: set[tuple[str, int]]
    classifications: Counter[str]
    reasons: set[str]
    path_rows: int
    unique_edges: int
    closed: bool
    branched: bool
    edge_repeat_count: int
    score: int = 0
    disposition: str = ""
    decision_reasons: list[str] = field(default_factory=list)


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true", help="write the deterministic Markdown report")
    mode.add_argument("--check", action="store_true", help="verify the generated report is current")
    mode.add_argument("--self-test", action="store_true", help="run deterministic unit checks")
    parser.add_argument(
        "--capture-folder",
        type=Path,
        default=DEFAULT_CAPTURE,
        help="completed AOSharpLiveCapture folder",
    )
    parser.add_argument(
        "--report",
        type=Path,
        default=DEFAULT_REPORT,
        help="generated Markdown report path",
    )
    return parser.parse_args(argv)


def parse_time(value: str) -> datetime:
    text = (value or "").strip()
    if not text:
        raise ValueError("missing timestamp")
    parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def parse_int(value: Any) -> int | None:
    text = str(value or "").strip()
    if not text:
        return None
    try:
        return int(text, 10)
    except ValueError:
        return None


def parse_float(value: Any) -> float | None:
    text = str(value or "").strip()
    if not text:
        return None
    try:
        result = float(text)
    except ValueError:
        return None
    return result if math.isfinite(result) else None


def normalize_identity(value: Any) -> str | None:
    text = str(value or "").strip()
    if not text or text.lower() in {"none:00000000", "(none:00000000)"}:
        return None
    if text.startswith("(") and text.endswith(")"):
        text = text[1:-1]
    if ":" not in text:
        return None
    identity_type, instance = text.split(":", 1)
    try:
        normalized_instance = int(instance, 16)
    except ValueError:
        return None
    return f"{identity_type}:{normalized_instance:08X}"


def point_from(row: dict[str, str], prefix: str) -> Point | None:
    x = parse_float(row.get(prefix + "X"))
    y = parse_float(row.get(prefix + "Y"))
    z = parse_float(row.get(prefix + "Z"))
    if x is None or y is None or z is None:
        return None
    return Point(x, y, z)


def horizontal_distance(first: Point, second: Point) -> float:
    return math.hypot(second.x - first.x, second.z - first.z)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while True:
            block = stream.read(1024 * 1024)
            if not block:
                break
            digest.update(block)
    return digest.hexdigest()


def relative_path(path: Path) -> str:
    return path.resolve().relative_to(REPO_ROOT.resolve()).as_posix()


def load_json(path: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict):
        raise RuntimeError(f"JSON root is not an object: {path}")
    return document


def read_csv(path: Path) -> Iterable[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        yield from csv.DictReader(stream)


def validate_capture(capture: Path) -> dict[str, Any]:
    missing = [name for name in REQUIRED_INPUTS if not (capture / name).is_file()]
    if missing:
        raise RuntimeError("capture is missing required inputs: " + ", ".join(missing))

    capture_info = load_json(capture / "capture_info.json")
    movement_summary = load_json(capture / "movement-summary.json")
    lifecycle_summary = load_json(capture / "npc-lifecycle-summary.json")
    movement_counts = movement_summary.get("counts", {})

    failures: list[str] = []
    if not lifecycle_summary.get("captureComplete"):
        failures.append("lifecycle captureComplete is false")
    if not lifecycle_summary.get("processingAllowed"):
        failures.append("lifecycle processingAllowed is false")
    if lifecycle_summary.get("pendingSimpleCharFullUpdateRows") != 0:
        failures.append("pending SCFU rows are non-zero")
    if lifecycle_summary.get("simpleCharFullUpdateDecodeErrors") != 0:
        failures.append("SCFU decode errors are non-zero")
    if movement_counts.get("decodeErrors") != 0:
        failures.append("movement decode errors are non-zero")
    if not movement_summary.get("followTargetDecodedWithUsablePath"):
        failures.append("usable FollowTarget path capability is false")
    if failures:
        raise RuntimeError("capture is not promotion-audit complete: " + "; ".join(failures))

    return {
        "captureInfo": capture_info,
        "movementSummary": movement_summary,
        "lifecycleSummary": lifecycle_summary,
    }


def load_metadata(capture: Path) -> tuple[dict[str, list[Metadata]], set[str]]:
    by_identity: dict[str, list[Metadata]] = defaultdict(list)
    player_identities: set[str] = set()
    for row in read_csv(capture / "scfu-appearance.csv"):
        identity = normalize_identity(row.get("Identity"))
        if identity is None:
            continue
        metadata = Metadata(
            captured_utc=parse_time(row["CapturedUtc"]),
            identity=identity,
            name=(row.get("Name") or "").strip(),
            playfield=parse_int(row.get("PlayfieldId")),
            family=parse_int(row.get("NpcFamily")),
            template=parse_int(row.get("MonsterData")),
            level=parse_int(row.get("Level")),
            character_info_type=(row.get("CharacterInfoType") or "").strip(),
            position=point_from(row, "Position"),
            decode_complete=(row.get("DecodeFullyConsumed") or "").strip().lower() == "true",
        )
        by_identity[identity].append(metadata)
        if metadata.character_info_type and not metadata.is_npc:
            player_identities.add(identity)
    for values in by_identity.values():
        values.sort(key=lambda value: value.captured_utc)
    return dict(by_identity), player_identities


def metadata_at(
    metadata_index: dict[str, list[Metadata]],
    identity: str,
    captured_utc: datetime,
) -> Metadata | None:
    values = metadata_index.get(identity)
    if not values:
        return None
    timestamps = [value.captured_utc for value in values]
    index = bisect.bisect_right(timestamps, captured_utc) - 1
    return values[index] if index >= 0 else None


def metadata_group_key(
    metadata: Metadata | None,
) -> tuple[int | None, int | None, int | None, int | None] | None:
    if metadata is None:
        return None
    return (
        metadata.family,
        metadata.template,
        metadata.level,
        metadata.playfield,
    )


def load_movements(capture: Path) -> tuple[list[MovementRow], dict[str, list[TimedControl]]]:
    paths: list[MovementRow] = []
    controls: dict[str, list[TimedControl]] = defaultdict(list)
    for row in read_csv(capture / "movement-packets.csv"):
        source = normalize_identity(row.get("SourceIdentity"))
        if source is None:
            continue
        movement = MovementRow(
            captured_utc=parse_time(row["CapturedUtc"]),
            sequence=parse_int(row.get("Sequence")) or 0,
            message_type=(row.get("MessageType") or "").strip(),
            source_identity=source,
            source_name=(row.get("SourceName") or "").strip(),
            target_identity=normalize_identity(row.get("TargetIdentity")),
            follow_kind=(row.get("FollowKind") or "").strip(),
            start=point_from(row, "Current"),
            end=point_from(row, "Destination"),
            path_count=parse_int(row.get("PathCount")) or 0,
        )
        if (
            movement.message_type == "FollowTarget"
            and movement.follow_kind == "NpcPath"
            and movement.start is not None
            and movement.end is not None
        ):
            paths.append(movement)
            continue

        if movement.message_type == "SetPos":
            controls[source].append(TimedControl(movement.captured_utc, "setpos", None))
        elif movement.message_type == "StopMovingCmd":
            controls[source].append(TimedControl(movement.captured_utc, "stop", None))
        elif movement.message_type == "FollowTarget" and movement.follow_kind == "Target":
            target = movement.target_identity
            if target is not None and target != source:
                controls[source].append(
                    TimedControl(movement.captured_utc, "external-target", target)
                )
    paths.sort(key=lambda value: (value.source_identity, value.captured_utc, value.sequence))
    for values in controls.values():
        values.sort(key=lambda value: value.captured_utc)
    return paths, dict(controls)


def load_lifecycle(
    capture: Path,
) -> tuple[dict[str, list[tuple[datetime, str]]], dict[str, list[datetime]]]:
    events: dict[str, list[tuple[datetime, str]]] = defaultdict(list)
    spawn_times: dict[str, list[datetime]] = defaultdict(list)
    for row in read_csv(capture / "npc-lifecycle.csv"):
        identity = normalize_identity(row.get("PrimaryIdentity"))
        if identity is None:
            continue
        phase = (row.get("Phase") or "").strip()
        captured_utc = parse_time(row["CapturedUtc"])
        events[identity].append((captured_utc, phase))
        if phase == "character-seen":
            spawn_times[identity].append(captured_utc)
    for values in events.values():
        values.sort()
    for values in spawn_times.values():
        values.sort()
    return dict(events), dict(spawn_times)


def build_generation_index(
    lifecycle: dict[str, list[tuple[datetime, str]]],
) -> dict[str, tuple[list[datetime], list[int]]]:
    result: dict[str, tuple[list[datetime], list[int]]] = {}
    for identity, events in lifecycle.items():
        generation = 0
        alive = False
        times: list[datetime] = []
        generations: list[int] = []
        for captured_utc, phase in events:
            if phase == "character-seen" and not alive:
                generation += 1
                alive = True
            elif phase in LIFECYCLE_END_PHASES:
                alive = False
            times.append(captured_utc)
            generations.append(generation)
        result[identity] = (times, generations)
    return result


def generation_at(
    generation_index: dict[str, tuple[list[datetime], list[int]]],
    identity: str,
    captured_utc: datetime,
) -> int:
    values = generation_index.get(identity)
    if values is None:
        return 0
    times, generations = values
    index = bisect.bisect_right(times, captured_utc) - 1
    return generations[index] if index >= 0 else 0


def lifecycle_boundary_between(
    lifecycle: dict[str, list[tuple[datetime, str]]],
    identity: str,
    start: datetime,
    end: datetime,
) -> bool:
    for captured_utc, phase in lifecycle.get(identity, []):
        if captured_utc <= start:
            continue
        if captured_utc > end:
            break
        if phase in LIFECYCLE_END_PHASES or phase == "character-seen":
            return True
    return False


def load_combat_events(
    capture: Path,
    metadata_index: dict[str, list[Metadata]],
    player_identities: set[str],
) -> dict[str, list[CombatInterval]]:
    raw_events: dict[str, list[tuple[datetime, str, set[str], bool]]] = defaultdict(list)
    for row in read_csv(capture / "enemy-combat.csv"):
        message_type = (row.get("MessageType") or "").strip()
        action = (row.get("Action") or "").strip()
        if (
            message_type not in COMBAT_MESSAGE_TYPES
            and message_type not in TERMINAL_COMBAT_MESSAGE_TYPES
            and action not in {"Death", "Die"}
        ):
            continue
        captured_utc = parse_time(row["CapturedUtc"])
        participants = [
            normalize_identity(row.get("SourceIdentity")),
            normalize_identity(row.get("TargetIdentity")),
            normalize_identity(row.get("AuxIdentity1")),
            normalize_identity(row.get("AuxIdentity2")),
        ]
        identities = [identity for identity in participants if identity is not None]
        for identity in identities:
            metadata = metadata_at(metadata_index, identity, captured_utc)
            if metadata is None or not metadata.is_npc:
                continue
            opponents = {value for value in identities if value != identity}
            has_player = any(
                value in player_identities
                or (
                    (opponent_metadata := metadata_at(metadata_index, value, captured_utc))
                    is not None
                    and not opponent_metadata.is_npc
                )
                for value in opponents
            )
            raw_events[identity].append(
                (captured_utc, message_type, opponents, has_player)
            )

    result: dict[str, list[CombatInterval]] = {}
    for identity, values in raw_events.items():
        values.sort(key=lambda value: value[0])
        intervals: list[CombatInterval] = []
        current: CombatInterval | None = None
        last_event: datetime | None = None
        for captured_utc, message_type, opponents, has_player in values:
            gap = (
                None
                if last_event is None
                else (captured_utc - last_event).total_seconds()
            )
            if current is None or (gap is not None and gap > COMBAT_CLUSTER_GAP_SECONDS):
                current = CombatInterval(
                    start=captured_utc
                    - timedelta(seconds=COMBAT_START_PADDING_SECONDS),
                    end=captured_utc + timedelta(seconds=COMBAT_END_PADDING_SECONDS),
                )
                intervals.append(current)
            current.end = max(
                current.end,
                captured_utc + timedelta(seconds=COMBAT_END_PADDING_SECONDS),
            )
            current.opponents.update(opponents)
            current.player_influence |= has_player
            last_event = captured_utc
            if message_type in TERMINAL_COMBAT_MESSAGE_TYPES:
                current.end = captured_utc + timedelta(seconds=1)
                current = None
                last_event = None
        result[identity] = intervals
    return result


def combat_interval_at(
    intervals: dict[str, list[CombatInterval]],
    identity: str,
    captured_utc: datetime,
) -> CombatInterval | None:
    values = intervals.get(identity, [])
    for interval in values:
        if interval.start <= captured_utc <= interval.end:
            return interval
        if interval.start > captured_utc:
            break
    return None


def recent_combat_interval(
    intervals: dict[str, list[CombatInterval]],
    identity: str,
    captured_utc: datetime,
) -> CombatInterval | None:
    for interval in reversed(intervals.get(identity, [])):
        if interval.end < captured_utc:
            if (captured_utc - interval.end).total_seconds() <= LEASH_WINDOW_SECONDS:
                return interval
            break
    return None


def load_positions(capture: Path) -> dict[str, tuple[list[datetime], list[Point]]]:
    positions: dict[str, list[tuple[datetime, Point]]] = defaultdict(list)
    for row in read_csv(capture / "enemy-state.csv"):
        identity = normalize_identity(row.get("entityId"))
        if identity is None:
            continue
        x = parse_float(row.get("x"))
        y = parse_float(row.get("y"))
        z = parse_float(row.get("z"))
        if x is None or y is None or z is None:
            continue
        positions[identity].append(
            (parse_time(row["timestamp"]), Point(x, y, z))
        )
    result: dict[str, tuple[list[datetime], list[Point]]] = {}
    for identity, values in positions.items():
        values.sort(key=lambda value: value[0])
        result[identity] = (
            [value[0] for value in values],
            [value[1] for value in values],
        )
    return result


def position_at(
    positions: dict[str, tuple[list[datetime], list[Point]]],
    identity: str,
    captured_utc: datetime,
) -> Point | None:
    values = positions.get(identity)
    if values is None:
        return None
    times, points = values
    index = bisect.bisect_right(times, captured_utc) - 1
    if index < 0:
        return None
    if (captured_utc - times[index]).total_seconds() > POSITION_MAX_AGE_SECONDS:
        return None
    return points[index]


def nearest_control(
    controls: dict[str, list[TimedControl]],
    identity: str,
    captured_utc: datetime,
) -> list[TimedControl]:
    values = controls.get(identity, [])
    if not values:
        return []
    times = [value.captured_utc for value in values]
    index = bisect.bisect_left(times, captured_utc)
    candidates = values[max(0, index - 2) : min(len(values), index + 2)]
    return [
        value
        for value in candidates
        if abs((value.captured_utc - captured_utc).total_seconds())
        <= CONTROL_INFLUENCE_SECONDS
    ]


def latest_spawn_before(
    spawn_times: dict[str, list[datetime]],
    identity: str,
    captured_utc: datetime,
) -> datetime | None:
    values = spawn_times.get(identity, [])
    index = bisect.bisect_right(values, captured_utc) - 1
    return values[index] if index >= 0 else None


def classify_path(
    row: MovementRow,
    metadata: Metadata | None,
    controls: dict[str, list[TimedControl]],
    combat_intervals: dict[str, list[CombatInterval]],
    positions: dict[str, tuple[list[datetime], list[Point]]],
    player_identities: set[str],
    spawn_times: dict[str, list[datetime]],
) -> tuple[str, set[str]]:
    if row.start is None or row.end is None:
        return "idle", {"incomplete_capture"}

    reasons: set[str] = set()
    if metadata is None or not metadata.exact_grouping_available:
        reasons.add("metadata_missing")

    nearby_controls = nearest_control(controls, row.source_identity, row.captured_utc)
    for control in nearby_controls:
        if control.kind == "setpos":
            reasons.add("teleport_or_position_discontinuity")
        elif control.kind == "stop":
            reasons.add("path_interruption")
        elif control.kind == "external-target":
            reasons.add("external_target_influence")
            if control.target_identity in player_identities:
                reasons.add("player_influence")

    distance = horizontal_distance(row.start, row.end)
    if distance <= MICRO_MOVEMENT_METERS:
        return "idle", reasons

    interval = combat_interval_at(
        combat_intervals,
        row.source_identity,
        row.captured_utc,
    )
    if interval is not None:
        reasons.add("combat_influence")
        if interval.player_influence:
            reasons.add("player_influence")
        deltas: list[float] = []
        for opponent in sorted(interval.opponents):
            target = position_at(positions, opponent, row.captured_utc)
            if target is None:
                continue
            deltas.append(
                horizontal_distance(row.end, target)
                - horizontal_distance(row.start, target)
            )
        if deltas and min(deltas) > 1.0:
            return "flee", reasons
        return "combat chase", reasons

    recent = recent_combat_interval(
        combat_intervals,
        row.source_identity,
        row.captured_utc,
    )
    if recent is not None:
        reasons.add("combat_influence")
        if recent.player_influence:
            reasons.add("player_influence")
        if metadata is not None and metadata.position is not None:
            start_distance = horizontal_distance(row.start, metadata.position)
            end_distance = horizontal_distance(row.end, metadata.position)
            if end_distance + 1.0 < start_distance:
                return "leash", reasons

    spawn = latest_spawn_before(spawn_times, row.source_identity, row.captured_utc)
    if (
        spawn is not None
        and 0 <= (row.captured_utc - spawn).total_seconds() <= SPAWN_WINDOW_SECONDS
    ):
        return "spawn", reasons

    if metadata is not None and metadata.family in {103, 137}:
        return "scripted", reasons
    return "patrol", reasons


def annotate_paths(
    paths: list[MovementRow],
    metadata_index: dict[str, list[Metadata]],
    generation_index: dict[str, tuple[list[datetime], list[int]]],
    controls: dict[str, list[TimedControl]],
    combat_intervals: dict[str, list[CombatInterval]],
    positions: dict[str, tuple[list[datetime], list[Point]]],
    player_identities: set[str],
    spawn_times: dict[str, list[datetime]],
) -> list[AnnotatedPath]:
    annotated: list[AnnotatedPath] = []
    for row in paths:
        metadata = metadata_at(metadata_index, row.source_identity, row.captured_utc)
        classification, reasons = classify_path(
            row,
            metadata,
            controls,
            combat_intervals,
            positions,
            player_identities,
            spawn_times,
        )
        annotated.append(
            AnnotatedPath(
                row=row,
                metadata=metadata,
                generation=generation_at(
                    generation_index,
                    row.source_identity,
                    row.captured_utc,
                ),
                classification=classification,
                reasons=reasons,
            )
        )
    return annotated


def close_trace(
    traces: list[RouteTrace],
    current: RouteTrace | None,
    extra_reasons: Iterable[str] = (),
) -> None:
    if current is None or not current.rows:
        return
    current.reasons.update(extra_reasons)
    populate_route_geometry(current)
    traces.append(current)


def segment_paths(
    annotated: list[AnnotatedPath],
    lifecycle: dict[str, list[tuple[datetime, str]]],
    capture_start: datetime,
    capture_end: datetime,
) -> list[RouteTrace]:
    by_identity: dict[str, list[AnnotatedPath]] = defaultdict(list)
    for value in annotated:
        by_identity[value.row.source_identity].append(value)

    traces: list[RouteTrace] = []
    for identity in sorted(by_identity):
        values = sorted(
            by_identity[identity],
            key=lambda value: (value.row.captured_utc, value.row.sequence),
        )
        current: RouteTrace | None = None
        previous: AnnotatedPath | None = None
        for value in values:
            split_reasons: set[str] = set()
            current_reasons = set(value.reasons)
            if previous is not None:
                gap = (value.row.captured_utc - previous.row.captured_utc).total_seconds()
                if gap > MAX_ROUTE_GAP_SECONDS:
                    split_reasons.add("incomplete_capture")
                    current_reasons.add("incomplete_capture")
                if lifecycle_boundary_between(
                    lifecycle,
                    identity,
                    previous.row.captured_utc,
                    value.row.captured_utc,
                ):
                    split_reasons.add("incomplete_capture")
                    current_reasons.add("incomplete_capture")
                if previous.row.end is not None and value.row.start is not None:
                    discontinuity = horizontal_distance(previous.row.end, value.row.start)
                    implied_speed = discontinuity / max(gap, 0.001)
                    if (
                        discontinuity > TELEPORT_DISCONTINUITY_METERS
                        or implied_speed > MAX_PLAUSIBLE_METERS_PER_SECOND
                    ):
                        split_reasons.add("teleport_or_position_discontinuity")
                        current_reasons.add("teleport_or_position_discontinuity")
                    elif discontinuity > CONTINUITY_METERS:
                        split_reasons.add("path_interruption")
                        current_reasons.add("path_interruption")

            must_split = (
                current is None
                or previous is None
                or value.generation != previous.generation
                or metadata_group_key(value.metadata)
                != metadata_group_key(previous.metadata)
                or value.classification != previous.classification
                or current_reasons != current.reasons
                or bool(split_reasons)
            )
            if must_split:
                close_trace(traces, current, split_reasons)
                current = RouteTrace(
                    source_identity=identity,
                    source_name=value.row.source_name,
                    generation=value.generation,
                    metadata=value.metadata,
                    classification=value.classification,
                    reasons=current_reasons,
                    rows=[],
                )
            current.rows.append(value.row)
            previous = value

        close_trace(traces, current)

    for trace in traces:
        if (
            trace.rows[0].captured_utc - capture_start
        ).total_seconds() <= CAPTURE_BOUNDARY_SECONDS:
            trace.reasons.add("incomplete_capture")
        if (
            capture_end - trace.rows[-1].captured_utc
        ).total_seconds() <= CAPTURE_BOUNDARY_SECONDS:
            trace.reasons.add("incomplete_capture")
    return traces


def quantize(point: Point) -> tuple[int, int, int]:
    return (
        round(point.x / ROUTE_QUANTIZATION_METERS),
        round(point.y / ROUTE_QUANTIZATION_METERS),
        round(point.z / ROUTE_QUANTIZATION_METERS),
    )


def canonical_edge(
    first: tuple[int, int, int],
    second: tuple[int, int, int],
) -> tuple[tuple[int, int, int], tuple[int, int, int]]:
    return (first, second) if first <= second else (second, first)


def populate_route_geometry(trace: RouteTrace) -> None:
    points = [
        quantize(row.end)
        for row in trace.rows
        if row.end is not None
    ]
    if trace.rows and trace.rows[0].start is not None:
        points.insert(0, quantize(trace.rows[0].start))
    compact: list[tuple[int, int, int]] = []
    for point in points:
        if not compact or compact[-1] != point:
            compact.append(point)

    edges: Counter[
        tuple[tuple[int, int, int], tuple[int, int, int]]
    ] = Counter()
    if len(compact) >= 2:
        for index in range(1, len(compact)):
            if compact[index - 1] != compact[index]:
                edges[canonical_edge(compact[index - 1], compact[index])] += 1
    elif trace.rows and trace.rows[0].start is not None and trace.rows[0].end is not None:
        first = quantize(trace.rows[0].start)
        second = quantize(trace.rows[0].end)
        if first != second:
            edges[canonical_edge(first, second)] += 1

    canonical_edges = tuple(sorted(edges))
    graph: dict[tuple[int, int, int], set[tuple[int, int, int]]] = defaultdict(set)
    for first, second in canonical_edges:
        graph[first].add(second)
        graph[second].add(first)
    degrees = [len(neighbors) for neighbors in graph.values()]
    branched = any(value > 2 for value in degrees)
    graph_loop = bool(degrees) and len(degrees) >= 3 and all(value == 2 for value in degrees)

    first_point = (
        trace.rows[0].start
        if trace.rows and trace.rows[0].start is not None
        else None
    )
    last_point = (
        trace.rows[-1].end
        if trace.rows and trace.rows[-1].end is not None
        else None
    )
    spatially_closed = (
        first_point is not None
        and last_point is not None
        and horizontal_distance(first_point, last_point) <= ROUTE_CLOSURE_METERS
    )
    payload = {
        "quantization": ROUTE_QUANTIZATION_METERS,
        "edges": canonical_edges,
    }
    trace.route_signature = hashlib.sha256(
        json.dumps(payload, separators=(",", ":")).encode("ascii")
    ).hexdigest()[:16]
    trace.canonical_edges = canonical_edges
    trace.representative_points = tuple(compact)
    trace.closed = graph_loop or spatially_closed
    trace.branched = branched
    trace.edge_repeat_count = min(edges.values()) if edges else 0


def choose_classification(counter: Counter[str]) -> str:
    for classification in CLASSIFICATION_PRECEDENCE:
        if counter.get(classification, 0):
            return classification
    return "scripted"


def group_canonical_routes(traces: list[RouteTrace]) -> list[CanonicalRoute]:
    grouped: dict[
        tuple[int | None, int | None, int | None, int | None, str],
        list[RouteTrace],
    ] = defaultdict(list)
    for trace in traces:
        metadata = trace.metadata
        key = (
            metadata.family if metadata is not None else None,
            metadata.template if metadata is not None else None,
            metadata.level if metadata is not None else None,
            metadata.playfield if metadata is not None else None,
            trace.route_signature,
        )
        grouped[key].append(trace)

    routes: list[CanonicalRoute] = []
    for key, values in grouped.items():
        family, template, level, playfield, signature = key
        classifications: Counter[str] = Counter()
        reasons: set[str] = set()
        names: set[str] = set()
        identities: set[str] = set()
        generations: set[tuple[str, int]] = set()
        for trace in values:
            classifications[trace.classification] += len(trace.rows)
            reasons.update(trace.reasons)
            if trace.source_name:
                names.add(trace.source_name)
            identities.add(trace.source_identity)
            generations.add((trace.source_identity, trace.generation))

        route = CanonicalRoute(
            family=family,
            template=template,
            level=level,
            playfield=playfield,
            signature=signature,
            traces=values,
            names=names,
            identities=identities,
            generations=generations,
            classifications=classifications,
            reasons=reasons,
            path_rows=sum(len(trace.rows) for trace in values),
            unique_edges=max((len(trace.canonical_edges) for trace in values), default=0),
            closed=bool(values) and all(trace.closed for trace in values),
            branched=any(trace.branched for trace in values),
            edge_repeat_count=max((trace.edge_repeat_count for trace in values), default=0),
        )
        score_route(route)
        routes.append(route)

    routes.sort(
        key=lambda route: (
            {"Safe for immediate promotion": 0, "Requires live verification": 1, "Reject": 2}[
                route.disposition
            ],
            -route.score,
            route.playfield if route.playfield is not None else -1,
            route.family if route.family is not None else -1,
            route.template if route.template is not None else -1,
            route.level if route.level is not None else -1,
            route.signature,
        )
    )
    return routes


def score_route(route: CanonicalRoute) -> None:
    classification = choose_classification(route.classifications)
    rejection_reasons = set(route.reasons)
    if classification in CLASSIFICATION_REJECTION_REASONS:
        rejection_reasons.add(CLASSIFICATION_REJECTION_REASONS[classification])

    score = 0
    if None not in {route.family, route.template, route.level, route.playfield}:
        score += 20
    if "incomplete_capture" not in rejection_reasons:
        score += 20
    if not rejection_reasons.intersection(HARD_REJECTION_REASONS):
        score += 20
    if route.closed:
        score += 15
    if route.edge_repeat_count >= 2:
        score += 10
    if len(route.identities) >= 2:
        score += 10
    if route.unique_edges >= 3:
        score += 5
    if len(route.generations) >= 2:
        score += 5
    if route.branched:
        score -= 15
    if classification == "scripted":
        score -= 15
    if len(route.traces) == 1 and route.edge_repeat_count < 2:
        score -= 10
    score = max(0, min(100, score))

    if rejection_reasons:
        route.disposition = "Reject"
        route.score = min(score, 49)
        route.decision_reasons = sorted(rejection_reasons)
        return

    safe = (
        classification == "patrol"
        and route.closed
        and not route.branched
        and route.edge_repeat_count >= 2
        and route.unique_edges >= 3
        and (
            len(route.identities) >= 2
            or len(route.generations) >= 2
            or route.edge_repeat_count >= 3
        )
        and score >= 85
    )
    if safe:
        route.disposition = "Safe for immediate promotion"
        route.score = score
        route.decision_reasons = [
            "exact_metadata",
            "closed_repeated_route",
            "no_combat_player_interruption_teleport_or_boundary_influence",
        ]
        return

    route.disposition = "Requires live verification"
    route.score = score
    reasons: list[str] = []
    if classification == "scripted":
        reasons.append("scripted_semantics_require_live_confirmation")
    if not route.closed:
        reasons.append("open_route_not_closed")
    if route.branched:
        reasons.append("branched_route_requires_live_confirmation")
    if route.edge_repeat_count < 2:
        reasons.append("route_not_repeated_end_to_end")
    if len(route.identities) < 2 and len(route.generations) < 2:
        reasons.append("single_identity_generation_support")
    if route.unique_edges < 3:
        reasons.append("insufficient_route_geometry")
    route.decision_reasons = reasons or ["confidence_below_immediate_promotion_threshold"]


def input_records(capture: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for name in REQUIRED_INPUTS:
        path = capture / name
        records.append(
            {
                "path": relative_path(path),
                "size": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )
    return records


def display_number(value: int | None) -> str:
    return "unresolved" if value is None else str(value)


def format_point(point: tuple[int, int, int]) -> str:
    return (
        f"({point[0] * ROUTE_QUANTIZATION_METERS:.1f},"
        f"{point[1] * ROUTE_QUANTIZATION_METERS:.1f},"
        f"{point[2] * ROUTE_QUANTIZATION_METERS:.1f})"
    )


def route_path_preview(route: CanonicalRoute, limit: int = 8) -> str:
    representative = max(
        route.traces,
        key=lambda trace: (len(trace.representative_points), len(trace.rows)),
    )
    points = list(representative.representative_points)
    preview = " -> ".join(format_point(point) for point in points[:limit])
    if len(points) > limit:
        preview += f" -> … (+{len(points) - limit})"
    return preview or "no canonical points"


def table_escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


def render_route_table(routes: list[CanonicalRoute], include_path: bool) -> list[str]:
    if not routes:
        return ["None.", ""]
    header = (
        "| Score | Classification | Family | Template | Level | PF | Names | Signature | "
        "Paths | IDs | Generations | Edges | Closed | Decision |"
    )
    separator = (
        "| ---: | --- | ---: | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: | --- | --- |"
    )
    lines = [header, separator]
    for route in routes:
        classification = choose_classification(route.classifications)
        decision = ", ".join(route.decision_reasons)
        if include_path:
            decision += "; path=" + route_path_preview(route)
        lines.append(
            "| "
            + " | ".join(
                [
                    str(route.score),
                    classification,
                    display_number(route.family),
                    display_number(route.template),
                    display_number(route.level),
                    display_number(route.playfield),
                    table_escape(", ".join(sorted(route.names)) or "unresolved"),
                    route.signature,
                    str(route.path_rows),
                    str(len(route.identities)),
                    str(len(route.generations)),
                    str(route.unique_edges),
                    "yes" if route.closed else "no",
                    table_escape(decision),
                ]
            )
            + " |"
        )
    lines.append("")
    return lines


def render_report(
    capture: Path,
    validation: dict[str, Any],
    paths: list[MovementRow],
    traces: list[RouteTrace],
    routes: list[CanonicalRoute],
    inputs: list[dict[str, Any]],
) -> bytes:
    movement_counts = validation["movementSummary"]["counts"]
    usable_expected = int(movement_counts["usableFollowTargetPackets"])
    if len(paths) != usable_expected:
        raise RuntimeError(
            f"usable NpcPath reconciliation failed: parsed={len(paths)} expected={usable_expected}"
        )
    if sum(len(trace.rows) for trace in traces) != len(paths):
        raise RuntimeError("trace path accounting does not reconcile")
    if sum(route.path_rows for route in routes) != len(paths):
        raise RuntimeError("canonical route path accounting does not reconcile")

    classification_rows: Counter[str] = Counter()
    for trace in traces:
        classification_rows[trace.classification] += len(trace.rows)
    disposition_rows: Counter[str] = Counter()
    disposition_routes: Counter[str] = Counter()
    reason_rows: Counter[str] = Counter()
    for route in routes:
        disposition_routes[route.disposition] += 1
        disposition_rows[route.disposition] += route.path_rows
        for reason in route.decision_reasons:
            reason_rows[reason] += route.path_rows

    capture_id = capture.name
    lines = [
        f"# Movement Promotion Audit — {capture_id}",
        "",
        "Generated deterministically by "
        f"`{relative_path(Path(__file__))}`. This report is analysis-only; it does not modify runtime data.",
        "",
        "## Verdict",
        "",
        f"- Captured usable `FollowTarget/NpcPath` rows audited: **{len(paths):,} / {usable_expected:,}**.",
        f"- Canonical route groups after removing runtime identity/generation from the key: **{len(routes):,}**.",
        f"- Safe for immediate promotion: **{disposition_routes['Safe for immediate promotion']:,} routes / {disposition_rows['Safe for immediate promotion']:,} paths**.",
        f"- Requires live verification: **{disposition_routes['Requires live verification']:,} routes / {disposition_rows['Requires live verification']:,} paths**.",
        f"- Reject: **{disposition_routes['Reject']:,} routes / {disposition_rows['Reject']:,} paths**.",
        "",
        "Every captured path is accounted for exactly once. Runtime identities and respawn generations remain evidence fields only; the canonical key is `(NPC family, MonsterData template, level, playfield, route signature)`.",
        "",
        "## Movement classification",
        "",
        "| Classification | Path rows |",
        "| --- | ---: |",
    ]
    for classification in (
        "idle",
        "patrol",
        "combat chase",
        "flee",
        "leash",
        "spawn",
        "scripted",
    ):
        lines.append(f"| {classification} | {classification_rows[classification]:,} |")
    lines.extend(
        [
            "",
            "## Safe for immediate promotion",
            "",
        ]
    )
    lines.extend(
        render_route_table(
            [route for route in routes if route.disposition == "Safe for immediate promotion"],
            include_path=True,
        )
    )
    lines.extend(["## Requires live verification", ""])
    lines.extend(
        render_route_table(
            [route for route in routes if route.disposition == "Requires live verification"],
            include_path=True,
        )
    )
    lines.extend(["## Reject with exact reason", ""])
    lines.extend(
        render_route_table(
            [route for route in routes if route.disposition == "Reject"],
            include_path=False,
        )
    )

    lines.extend(
        [
            "## Decision reason accounting",
            "",
            "| Exact reason | Affected path rows |",
            "| --- | ---: |",
        ]
    )
    for reason, count in sorted(reason_rows.items(), key=lambda value: (-value[1], value[0])):
        lines.append(f"| `{reason}` | {count:,} |")

    lines.extend(
        [
            "",
            "## Deterministic method",
            "",
            f"- Route coordinates are quantized to **{ROUTE_QUANTIZATION_METERS:.1f} m** and represented by a direction-independent set of canonical edges.",
            "- Identical edge sets are collapsed even when observed under different runtime identities or respawn generations.",
            "- Metadata comes only from exact identity-linked, fully consumed SCFU rows.",
            f"- Combat rows are clustered with {COMBAT_START_PADDING_SECONDS:.0f}s pre-roll and {COMBAT_END_PADDING_SECONDS:.0f}s post-roll; affected movement is rejected.",
            f"- Stop commands and external-target controls within {CONTROL_INFLUENCE_SECONDS:.1f}s reject the route.",
            f"- Position discontinuities above {TELEPORT_DISCONTINUITY_METERS:.0f}m or {MAX_PLAUSIBLE_METERS_PER_SECOND:.0f}m/s reject the route as teleport/discontinuity.",
            f"- Visibility/lifecycle gaps above {MAX_ROUTE_GAP_SECONDS:.0f}s and capture-boundary traces reject the route as incomplete.",
            "- `Safe` requires exact metadata, a clean closed unbranched patrol, repeated complete traversal, at least three canonical edges, and confidence >= 85.",
            "- Clean but open, scripted, branched, or weakly repeated routes require live verification.",
            "- Idle, spawn, combat chase, flee, and leash traces are not patrol candidates.",
            "",
            "## Confidence score",
            "",
            "The 0–100 score awards exact metadata, complete capture, clean influence history, closure, repetition, independent identities/generations, and sufficient geometry. It penalizes branching, scripted ambiguity, and single observations. Any hard rejection caps confidence below 50 and forces `Reject` regardless of score.",
            "",
            "## Inputs",
            "",
            "| Path | Bytes | SHA-256 |",
            "| --- | ---: | --- |",
        ]
    )
    for record in inputs:
        lines.append(
            f"| `{record['path']}` | {record['size']:,} | `{record['sha256']}` |"
        )
    lines.extend(
        [
            "",
            "## Capture validation",
            "",
            f"- Lifecycle processing allowed: `{str(validation['lifecycleSummary']['processingAllowed']).lower()}`",
            f"- SCFU decoded/pending/errors: `{validation['lifecycleSummary']['decodedSimpleCharFullUpdateRows']}/{validation['lifecycleSummary']['pendingSimpleCharFullUpdateRows']}/{validation['lifecycleSummary']['simpleCharFullUpdateDecodeErrors']}`",
            f"- Movement decode errors: `{movement_counts['decodeErrors']}`",
            f"- Movement rows / usable paths: `{movement_counts['movementPacketRows']}/{movement_counts['usableFollowTargetPackets']}`",
            f"- Report schema: `{SCHEMA_VERSION}`",
            "",
        ]
    )
    return ("\n".join(lines)).encode("utf-8")


def build_report(capture: Path) -> tuple[bytes, dict[str, int]]:
    capture = capture.resolve()
    validation = validate_capture(capture)
    metadata_index, player_identities = load_metadata(capture)
    paths, controls = load_movements(capture)
    lifecycle, spawn_times = load_lifecycle(capture)
    generation_index = build_generation_index(lifecycle)
    combat_intervals = load_combat_events(
        capture,
        metadata_index,
        player_identities,
    )
    positions = load_positions(capture)
    annotated = annotate_paths(
        paths,
        metadata_index,
        generation_index,
        controls,
        combat_intervals,
        positions,
        player_identities,
        spawn_times,
    )
    capture_info = validation["captureInfo"]
    capture_start = parse_time(capture_info["captureStartUtc"])
    capture_end = parse_time(capture_info["captureEndUtc"])
    traces = segment_paths(
        annotated,
        lifecycle,
        capture_start,
        capture_end,
    )
    routes = group_canonical_routes(traces)
    payload = render_report(
        capture,
        validation,
        paths,
        traces,
        routes,
        input_records(capture),
    )
    return payload, {
        "paths": len(paths),
        "traces": len(traces),
        "routes": len(routes),
        "safe": sum(route.disposition == "Safe for immediate promotion" for route in routes),
        "live": sum(route.disposition == "Requires live verification" for route in routes),
        "reject": sum(route.disposition == "Reject" for route in routes),
    }


def run_self_test() -> None:
    assert normalize_identity("(SimpleChar:000000AA)") == "SimpleChar:000000AA"
    assert normalize_identity("SimpleChar:aa") == "SimpleChar:000000AA"
    assert normalize_identity("None:00000000") is None

    trace = RouteTrace(
        source_identity="SimpleChar:00000001",
        source_name="Test",
        generation=1,
        metadata=Metadata(
            captured_utc=datetime(2026, 1, 1, tzinfo=timezone.utc),
            identity="SimpleChar:00000001",
            name="Test",
            playfield=1,
            family=2,
            template=3,
            level=4,
            character_info_type="NPCInfo",
            position=Point(0, 0, 0),
            decode_complete=True,
        ),
        classification="patrol",
        reasons=set(),
        rows=[
            MovementRow(
                datetime(2026, 1, 1, tzinfo=timezone.utc),
                1,
                "FollowTarget",
                "SimpleChar:00000001",
                "Test",
                None,
                "NpcPath",
                Point(0, 0, 0),
                Point(5, 0, 0),
                2,
            ),
            MovementRow(
                datetime(2026, 1, 1, 0, 0, 1, tzinfo=timezone.utc),
                2,
                "FollowTarget",
                "SimpleChar:00000001",
                "Test",
                None,
                "NpcPath",
                Point(5, 0, 0),
                Point(5, 0, 5),
                2,
            ),
            MovementRow(
                datetime(2026, 1, 1, 0, 0, 2, tzinfo=timezone.utc),
                3,
                "FollowTarget",
                "SimpleChar:00000001",
                "Test",
                None,
                "NpcPath",
                Point(5, 0, 5),
                Point(0, 0, 0),
                2,
            ),
        ],
    )
    trace.rows.extend(
        MovementRow(
            row.captured_utc + timedelta(seconds=3),
            row.sequence + 3,
            row.message_type,
            row.source_identity,
            row.source_name,
            row.target_identity,
            row.follow_kind,
            row.start,
            row.end,
            row.path_count,
        )
        for row in list(trace.rows)
    )
    populate_route_geometry(trace)
    assert trace.closed, "repeated triangle must be closed"
    assert len(trace.canonical_edges) == 3, "triangle must have three canonical edges"
    reverse = RouteTrace(
        source_identity=trace.source_identity,
        source_name=trace.source_name,
        generation=2,
        metadata=trace.metadata,
        classification="patrol",
        reasons=set(),
        rows=list(reversed(trace.rows)),
    )
    reverse.rows = [
        MovementRow(
            row.captured_utc,
            row.sequence,
            row.message_type,
            row.source_identity,
            row.source_name,
            row.target_identity,
            row.follow_kind,
            row.end,
            row.start,
            row.path_count,
        )
        for row in reverse.rows
    ]
    populate_route_geometry(reverse)
    assert reverse.route_signature == trace.route_signature, "reverse traversal must collapse"

    routes = group_canonical_routes([trace, reverse])
    assert len(routes) == 1, "identical routes must collapse"
    assert routes[0].disposition == "Safe for immediate promotion", (
        routes[0].score,
        routes[0].decision_reasons,
        routes[0].edge_repeat_count,
    )

    influenced = RouteTrace(
        source_identity=trace.source_identity,
        source_name=trace.source_name,
        generation=1,
        metadata=trace.metadata,
        classification="combat chase",
        reasons={"combat_influence", "player_influence"},
        rows=trace.rows,
    )
    populate_route_geometry(influenced)
    rejected = group_canonical_routes([influenced])[0]
    assert rejected.disposition == "Reject"
    assert "combat_influence" in rejected.decision_reasons

    with tempfile.TemporaryDirectory(dir=REPO_ROOT / "tools-temp") as temporary:
        path = Path(temporary) / "sample.bin"
        path.write_bytes(b"movement-audit")
        assert len(sha256_file(path)) == 64
    print("PASS movement promotion audit self-test")


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    try:
        if args.self_test:
            run_self_test()
            return 0
        payload, summary = build_report(args.capture_folder)
        report = args.report.resolve()
        if args.write:
            report.parent.mkdir(parents=True, exist_ok=True)
            temporary = report.with_suffix(report.suffix + ".tmp")
            temporary.write_bytes(payload)
            temporary.replace(report)
            print(
                f"WROTE {relative_path(report)} "
                f"paths={summary['paths']} traces={summary['traces']} routes={summary['routes']} "
                f"safe={summary['safe']} live={summary['live']} reject={summary['reject']}"
            )
        else:
            if not report.is_file():
                raise RuntimeError(f"missing report: {relative_path(report)}")
            if report.read_bytes() != payload:
                raise RuntimeError(
                    f"stale report: run {relative_path(Path(__file__))} --write"
                )
            print(f"PASS {relative_path(report)} is current")
        return 0
    except Exception as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
