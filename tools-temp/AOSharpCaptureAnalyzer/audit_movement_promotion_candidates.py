#!/usr/bin/env python3
"""Build observation-level promotion datasets from a completed movement capture.

Each usable FollowTarget/NpcPath packet is resolved, classified, scored, and
assigned a disposition before route grouping. Runtime identity and generation
remain evidence fields; they are never part of the reusable route key.
"""

from __future__ import annotations

import argparse
import bisect
import csv
import hashlib
import io
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
CAPTURE_ID = "20260722-152454"
DEFAULT_CAPTURE = (
    REPO_ROOT
    / "tools-temp"
    / "AOSharpLiveCapture"
    / "bin"
    / "Debug"
    / "captures"
    / CAPTURE_ID
)
DEFAULT_REPORT = (
    REPO_ROOT
    / "docs"
    / "generated"
    / "arete_20260722_152454_movement_promotion_audit.md"
)
DEFAULT_DATASET_DIR = (
    REPO_ROOT
    / "docs"
    / "generated"
    / "arete_20260722_152454_movement"
)

SCHEMA_VERSION = 2
BEHAVIORS = ("patrol", "spawn", "chase", "flee", "leash", "scripted")
DISPOSITIONS = ("Promotable", "Ambiguous", "Rejected")
ROUTE_QUANTIZATION_METERS = 0.5
MICRO_MOVEMENT_METERS = 0.25
CONTROL_INFLUENCE_SECONDS = 2.5
COMBAT_CLUSTER_GAP_SECONDS = 15.0
COMBAT_START_PADDING_SECONDS = 2.0
COMBAT_END_PADDING_SECONDS = 5.0
LEASH_WINDOW_SECONDS = 30.0
SPAWN_WINDOW_SECONDS = 5.0
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
LIFECYCLE_END_PHASES = {"character-gone", "death-action", "enemy-despawn"}
SCRIPTED_FAMILIES = {103, 137}

HARD_REJECTION_REASONS = {
    "metadata_unresolved",
    "metadata_conflict_within_generation",
    "metadata_conflict_across_reused_identity",
    "movement_endpoint_missing",
    "micro_movement_not_route_evidence",
    "explicit_setpos_teleport",
    "path_interrupted_by_stop_command",
    "capture_decode_incomplete",
}

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


@dataclass(frozen=True)
class Point:
    x: float
    y: float
    z: float


@dataclass(frozen=True)
class Metadata:
    captured_utc: datetime
    identity: str
    generation: int
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
    def exact(self) -> bool:
        return (
            self.decode_complete
            and self.is_npc
            and self.playfield is not None
            and self.family is not None
            and self.template is not None
            and self.level is not None
        )

    @property
    def group_key(self) -> tuple[int | None, int | None, int | None, int | None]:
        return self.family, self.template, self.level, self.playfield


@dataclass(frozen=True)
class MovementRow:
    ordinal: int
    captured_utc: datetime
    sequence: int
    source_identity: str
    source_name: str
    target_identity: str | None
    start: Point
    end: Point
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
class Observation:
    observation_id: str
    row: MovementRow
    generation: int
    metadata: Metadata | None
    metadata_resolution: str
    behavior: str
    disposition: str
    confidence: int
    decision_reasons: tuple[str, ...]
    influences: tuple[str, ...]
    route_signature: str
    distance: float


@dataclass
class RouteGroup:
    behavior: str
    disposition: str
    family: int | None
    template: int | None
    level: int | None
    playfield: int | None
    signature: str
    observations: list[Observation]
    reasons: Counter[str]
    influences: Counter[str]


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--self-test", action="store_true")
    parser.add_argument("--capture-folder", type=Path, default=DEFAULT_CAPTURE)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--dataset-dir", type=Path, default=DEFAULT_DATASET_DIR)
    return parser.parse_args(argv)


def parse_time(value: str) -> datetime:
    parsed = datetime.fromisoformat(value.strip().replace("Z", "+00:00"))
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
    values = (
        parse_float(row.get(prefix + "X")),
        parse_float(row.get(prefix + "Y")),
        parse_float(row.get(prefix + "Z")),
    )
    if any(value is None for value in values):
        return None
    return Point(values[0], values[1], values[2])  # type: ignore[arg-type]


def horizontal_distance(first: Point, second: Point) -> float:
    return math.hypot(second.x - first.x, second.z - first.z)


def read_csv(path: Path) -> Iterable[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        yield from csv.DictReader(stream)


def load_json(path: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict):
        raise RuntimeError(f"JSON root is not an object: {path}")
    return document


def relative_path(path: Path) -> str:
    return path.resolve().relative_to(REPO_ROOT.resolve()).as_posix()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


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
        raise RuntimeError("capture is not audit-complete: " + "; ".join(failures))
    return {
        "captureInfo": capture_info,
        "movementSummary": movement_summary,
        "lifecycleSummary": lifecycle_summary,
    }


def load_lifecycle(
    capture: Path,
) -> tuple[
    dict[str, list[tuple[datetime, str]]],
    dict[str, list[datetime]],
]:
    events: dict[str, list[tuple[datetime, str]]] = defaultdict(list)
    spawn_times: dict[str, list[datetime]] = defaultdict(list)
    for row in read_csv(capture / "npc-lifecycle.csv"):
        identity = normalize_identity(row.get("PrimaryIdentity"))
        if identity is None:
            continue
        captured_utc = parse_time(row["CapturedUtc"])
        phase = (row.get("Phase") or "").strip()
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
        result[identity] = times, generations
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


def load_metadata(
    capture: Path,
    generation_index: dict[str, tuple[list[datetime], list[int]]],
) -> tuple[dict[str, list[Metadata]], set[str]]:
    by_identity: dict[str, list[Metadata]] = defaultdict(list)
    player_identities: set[str] = set()
    for row in read_csv(capture / "scfu-appearance.csv"):
        identity = normalize_identity(row.get("Identity"))
        if identity is None:
            continue
        captured_utc = parse_time(row["CapturedUtc"])
        metadata = Metadata(
            captured_utc=captured_utc,
            identity=identity,
            generation=generation_at(generation_index, identity, captured_utc),
            name=(row.get("Name") or "").strip(),
            playfield=parse_int(row.get("PlayfieldId")),
            family=parse_int(row.get("NpcFamily")),
            template=parse_int(row.get("MonsterData")),
            level=parse_int(row.get("Level")),
            character_info_type=(row.get("CharacterInfoType") or "").strip(),
            position=point_from(row, "Position"),
            decode_complete=(row.get("DecodeFullyConsumed") or "").lower() == "true",
        )
        by_identity[identity].append(metadata)
        if metadata.character_info_type and not metadata.is_npc:
            player_identities.add(identity)
    for values in by_identity.values():
        values.sort(key=lambda value: value.captured_utc)
    return dict(by_identity), player_identities


def choose_consistent_metadata(
    values: list[Metadata],
    captured_utc: datetime,
) -> Metadata:
    preceding = [value for value in values if value.captured_utc <= captured_utc]
    if preceding:
        return preceding[-1]
    return values[0]


def resolve_metadata(
    metadata_index: dict[str, list[Metadata]],
    identity: str,
    generation: int,
    captured_utc: datetime,
) -> tuple[Metadata | None, str, str | None]:
    exact = [value for value in metadata_index.get(identity, []) if value.exact]
    if not exact:
        return None, "unresolved", "metadata_unresolved"

    same_generation = [
        value for value in exact if generation > 0 and value.generation == generation
    ]
    if same_generation:
        keys = {value.group_key for value in same_generation}
        if len(keys) != 1:
            return None, "conflict", "metadata_conflict_within_generation"
        chosen = choose_consistent_metadata(same_generation, captured_utc)
        resolution = (
            "preceding_scfu_same_generation"
            if chosen.captured_utc <= captured_utc
            else "later_scfu_same_generation"
        )
        return chosen, resolution, None

    keys = {value.group_key for value in exact}
    character_types = {value.character_info_type.lower() for value in exact}
    if len(keys) != 1 or character_types != {"npcinfo"}:
        return None, "conflict", "metadata_conflict_across_reused_identity"
    return (
        choose_consistent_metadata(exact, captured_utc),
        "complete_capture_stable_identity",
        None,
    )


def load_movements(
    capture: Path,
) -> tuple[list[MovementRow], dict[str, list[TimedControl]]]:
    paths: list[MovementRow] = []
    controls: dict[str, list[TimedControl]] = defaultdict(list)
    for ordinal, row in enumerate(read_csv(capture / "movement-packets.csv"), start=1):
        source = normalize_identity(row.get("SourceIdentity"))
        if source is None:
            continue
        captured_utc = parse_time(row["CapturedUtc"])
        message_type = (row.get("MessageType") or "").strip()
        follow_kind = (row.get("FollowKind") or "").strip()
        target = normalize_identity(row.get("TargetIdentity"))
        start = point_from(row, "Current")
        end = point_from(row, "Destination")
        if (
            message_type == "FollowTarget"
            and follow_kind == "NpcPath"
            and start is not None
            and end is not None
        ):
            paths.append(
                MovementRow(
                    ordinal=ordinal,
                    captured_utc=captured_utc,
                    sequence=parse_int(row.get("Sequence")) or 0,
                    source_identity=source,
                    source_name=(row.get("SourceName") or "").strip(),
                    target_identity=target,
                    start=start,
                    end=end,
                    path_count=parse_int(row.get("PathCount")) or 0,
                )
            )
        elif message_type == "SetPos":
            controls[source].append(TimedControl(captured_utc, "setpos", target))
        elif message_type == "StopMovingCmd":
            controls[source].append(TimedControl(captured_utc, "stop", target))
        elif message_type == "FollowTarget" and follow_kind == "Target":
            if target is not None and target != source:
                controls[source].append(
                    TimedControl(captured_utc, "external-target", target)
                )
    paths.sort(key=lambda row: (row.captured_utc, row.sequence, row.ordinal))
    for values in controls.values():
        values.sort(key=lambda value: value.captured_utc)
    return paths, dict(controls)


def nearby_controls(
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


def load_positions(capture: Path) -> dict[str, tuple[list[datetime], list[Point]]]:
    positions: dict[str, list[tuple[datetime, Point]]] = defaultdict(list)
    for row in read_csv(capture / "enemy-state.csv"):
        identity = normalize_identity(row.get("entityId"))
        x = parse_float(row.get("x"))
        y = parse_float(row.get("y"))
        z = parse_float(row.get("z"))
        if identity is None or x is None or y is None or z is None:
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


def load_combat_intervals(
    capture: Path,
    metadata_index: dict[str, list[Metadata]],
    generation_index: dict[str, tuple[list[datetime], list[int]]],
    player_identities: set[str],
) -> dict[str, list[CombatInterval]]:
    raw: dict[str, list[tuple[datetime, str, set[str], bool]]] = defaultdict(list)
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
        identities = [
            identity
            for identity in (
                normalize_identity(row.get("SourceIdentity")),
                normalize_identity(row.get("TargetIdentity")),
                normalize_identity(row.get("AuxIdentity1")),
                normalize_identity(row.get("AuxIdentity2")),
            )
            if identity is not None
        ]
        for identity in identities:
            generation = generation_at(generation_index, identity, captured_utc)
            metadata, _, _ = resolve_metadata(
                metadata_index, identity, generation, captured_utc
            )
            if metadata is None or not metadata.is_npc:
                continue
            opponents = {value for value in identities if value != identity}
            has_player = any(
                opponent in player_identities
                or (
                    (
                        opponent_metadata := resolve_metadata(
                            metadata_index,
                            opponent,
                            generation_at(
                                generation_index, opponent, captured_utc
                            ),
                            captured_utc,
                        )[0]
                    )
                    is not None
                    and not opponent_metadata.is_npc
                )
                for opponent in opponents
            )
            raw[identity].append(
                (captured_utc, message_type, opponents, has_player)
            )

    result: dict[str, list[CombatInterval]] = {}
    for identity, values in raw.items():
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
                    captured_utc - timedelta(seconds=COMBAT_START_PADDING_SECONDS),
                    captured_utc + timedelta(seconds=COMBAT_END_PADDING_SECONDS),
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
    for interval in intervals.get(identity, []):
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


def latest_spawn(
    spawn_times: dict[str, list[datetime]],
    identity: str,
    captured_utc: datetime,
) -> datetime | None:
    values = spawn_times.get(identity, [])
    index = bisect.bisect_right(values, captured_utc) - 1
    return values[index] if index >= 0 else None


def quantize(point: Point) -> tuple[int, int, int]:
    return (
        round(point.x / ROUTE_QUANTIZATION_METERS),
        round(point.y / ROUTE_QUANTIZATION_METERS),
        round(point.z / ROUTE_QUANTIZATION_METERS),
    )


def route_signature(start: Point, end: Point) -> str:
    first, second = quantize(start), quantize(end)
    edge = (first, second) if first <= second else (second, first)
    payload = json.dumps(
        {"quantization": ROUTE_QUANTIZATION_METERS, "edge": edge},
        separators=(",", ":"),
    )
    return hashlib.sha256(payload.encode("ascii")).hexdigest()[:16]


def classify_behavior(
    row: MovementRow,
    metadata: Metadata | None,
    controls: dict[str, list[TimedControl]],
    combat_intervals: dict[str, list[CombatInterval]],
    positions: dict[str, tuple[list[datetime], list[Point]]],
    player_identities: set[str],
    spawn_times: dict[str, list[datetime]],
) -> tuple[str, set[str], set[str]]:
    hard_reasons: set[str] = set()
    ambiguous_reasons: set[str] = set()
    influences: set[str] = set()

    distance = horizontal_distance(row.start, row.end)
    if distance <= MICRO_MOVEMENT_METERS:
        hard_reasons.add("micro_movement_not_route_evidence")

    controls_nearby = nearby_controls(
        controls, row.source_identity, row.captured_utc
    )
    external_target: str | None = None
    for control in controls_nearby:
        if control.kind == "setpos":
            hard_reasons.add("explicit_setpos_teleport")
        elif control.kind == "stop":
            hard_reasons.add("path_interrupted_by_stop_command")
        elif control.kind == "external-target":
            external_target = control.target_identity
            influences.add("external_target")
            if external_target in player_identities:
                influences.add("player")

    active = combat_interval_at(
        combat_intervals, row.source_identity, row.captured_utc
    )
    if active is not None:
        influences.add("combat")
        if active.player_influence:
            influences.add("player")
        deltas: list[float] = []
        for opponent in sorted(active.opponents):
            target = position_at(positions, opponent, row.captured_utc)
            if target is None:
                continue
            deltas.append(
                horizontal_distance(row.end, target)
                - horizontal_distance(row.start, target)
            )
        if deltas and min(deltas) > 1.0:
            return "flee", hard_reasons, influences
        if not deltas:
            ambiguous_reasons.add("combat_target_position_unavailable")
        elif min(deltas) >= -1.0:
            ambiguous_reasons.add("combat_direction_ambiguous")
        return "chase", hard_reasons | ambiguous_reasons, influences

    if external_target is not None:
        ambiguous_reasons.add("target_follow_without_combat_event")
        return "chase", hard_reasons | ambiguous_reasons, influences

    recent = recent_combat_interval(
        combat_intervals, row.source_identity, row.captured_utc
    )
    if recent is not None:
        influences.add("combat")
        if recent.player_influence:
            influences.add("player")
        if metadata is not None and metadata.position is not None:
            start_distance = horizontal_distance(row.start, metadata.position)
            end_distance = horizontal_distance(row.end, metadata.position)
            if end_distance + 1.0 < start_distance:
                return "leash", hard_reasons, influences
        ambiguous_reasons.add("post_combat_direction_not_leash")
        return "chase", hard_reasons | ambiguous_reasons, influences

    spawn = latest_spawn(spawn_times, row.source_identity, row.captured_utc)
    if (
        spawn is not None
        and 0 <= (row.captured_utc - spawn).total_seconds() <= SPAWN_WINDOW_SECONDS
    ):
        return "spawn", hard_reasons, influences

    if metadata is not None and metadata.family in SCRIPTED_FAMILIES:
        ambiguous_reasons.add("scripted_family_heuristic_only")
        return "scripted", hard_reasons | ambiguous_reasons, influences
    return "patrol", hard_reasons, influences


def score_observation(
    metadata: Metadata | None,
    behavior: str,
    reasons: set[str],
    influences: set[str],
    path_count: int,
) -> tuple[str, int, tuple[str, ...]]:
    hard = sorted(reason for reason in reasons if reason in HARD_REJECTION_REASONS)
    ambiguous = sorted(reason for reason in reasons if reason not in HARD_REJECTION_REASONS)
    score = 25
    if metadata is not None and metadata.exact:
        score += 30
    score += 20
    if not hard:
        score += 15
    if not ambiguous:
        score += 5
    if path_count >= 2:
        score += 5
    score = max(0, min(100, score))

    if hard:
        return "Rejected", min(score, 49), tuple(hard + ambiguous)
    if ambiguous:
        return "Ambiguous", min(score, 84), tuple(ambiguous)

    exact_reasons = [
        "complete_decoded_path",
        "exact_identity_metadata",
        f"captured_{behavior}_observation",
    ]
    if "combat" in influences:
        exact_reasons.append("combat_influence_preserved_for_behavior")
    if "player" in influences:
        exact_reasons.append("player_influence_preserved_for_behavior")
    return "Promotable", max(score, 85), tuple(exact_reasons)


def build_observation(
    index: int,
    row: MovementRow,
    generation_index: dict[str, tuple[list[datetime], list[int]]],
    metadata_index: dict[str, list[Metadata]],
    controls: dict[str, list[TimedControl]],
    combat_intervals: dict[str, list[CombatInterval]],
    positions: dict[str, tuple[list[datetime], list[Point]]],
    player_identities: set[str],
    spawn_times: dict[str, list[datetime]],
) -> Observation:
    generation = generation_at(
        generation_index, row.source_identity, row.captured_utc
    )
    metadata, resolution, metadata_reason = resolve_metadata(
        metadata_index,
        row.source_identity,
        generation,
        row.captured_utc,
    )
    behavior, reasons, influences = classify_behavior(
        row,
        metadata,
        controls,
        combat_intervals,
        positions,
        player_identities,
        spawn_times,
    )
    if metadata_reason is not None:
        reasons.add(metadata_reason)
    disposition, confidence, decision_reasons = score_observation(
        metadata, behavior, reasons, influences, row.path_count
    )
    return Observation(
        observation_id=f"m{index:05d}",
        row=row,
        generation=generation,
        metadata=metadata,
        metadata_resolution=resolution,
        behavior=behavior,
        disposition=disposition,
        confidence=confidence,
        decision_reasons=decision_reasons,
        influences=tuple(sorted(influences)),
        route_signature=route_signature(row.start, row.end),
        distance=horizontal_distance(row.start, row.end),
    )


def group_routes(observations: list[Observation]) -> list[RouteGroup]:
    grouped: dict[
        tuple[
            str,
            str,
            int | None,
            int | None,
            int | None,
            int | None,
            str,
        ],
        list[Observation],
    ] = defaultdict(list)
    for observation in observations:
        metadata = observation.metadata
        key = (
            observation.behavior,
            observation.disposition,
            metadata.family if metadata else None,
            metadata.template if metadata else None,
            metadata.level if metadata else None,
            metadata.playfield if metadata else None,
            observation.route_signature,
        )
        grouped[key].append(observation)

    result: list[RouteGroup] = []
    for key, values in grouped.items():
        behavior, disposition, family, template, level, playfield, signature = key
        reasons: Counter[str] = Counter()
        influences: Counter[str] = Counter()
        for value in values:
            reasons.update(value.decision_reasons)
            influences.update(value.influences)
        result.append(
            RouteGroup(
                behavior,
                disposition,
                family,
                template,
                level,
                playfield,
                signature,
                values,
                reasons,
                influences,
            )
        )
    result.sort(
        key=lambda group: (
            BEHAVIORS.index(group.behavior),
            DISPOSITIONS.index(group.disposition),
            -len(group.observations),
            group.family if group.family is not None else -1,
            group.template if group.template is not None else -1,
            group.level if group.level is not None else -1,
            group.playfield if group.playfield is not None else -1,
            group.signature,
        )
    )
    return result


def clean_text(value: str) -> str:
    return value.replace("\x00", "").strip()


def format_float(value: float) -> str:
    return f"{value:.6f}".rstrip("0").rstrip(".")


def dataset_row(observation: Observation) -> dict[str, str]:
    metadata = observation.metadata
    row = observation.row
    return {
        "ObservationId": observation.observation_id,
        "CapturedUtc": row.captured_utc.isoformat().replace("+00:00", "Z"),
        "Sequence": str(row.sequence),
        "Behavior": observation.behavior,
        "Disposition": observation.disposition,
        "Confidence": str(observation.confidence),
        "DecisionReasons": ";".join(observation.decision_reasons),
        "Influences": ";".join(observation.influences),
        "MetadataResolution": observation.metadata_resolution,
        "NpcFamily": "" if metadata is None else str(metadata.family),
        "MonsterData": "" if metadata is None else str(metadata.template),
        "Level": "" if metadata is None else str(metadata.level),
        "PlayfieldId": "" if metadata is None else str(metadata.playfield),
        "Name": clean_text(metadata.name if metadata else row.source_name),
        "RuntimeIdentity": row.source_identity,
        "RuntimeGeneration": str(observation.generation),
        "RouteSignature": observation.route_signature,
        "CurrentX": format_float(row.start.x),
        "CurrentY": format_float(row.start.y),
        "CurrentZ": format_float(row.start.z),
        "DestinationX": format_float(row.end.x),
        "DestinationY": format_float(row.end.y),
        "DestinationZ": format_float(row.end.z),
        "HorizontalDistance": format_float(observation.distance),
        "PathCount": str(row.path_count),
    }


def render_dataset(observations: list[Observation]) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(
        stream,
        fieldnames=DATASET_COLUMNS,
        lineterminator="\n",
    )
    writer.writeheader()
    for observation in observations:
        writer.writerow(dataset_row(observation))
    return stream.getvalue().encode("utf-8")


def partition_observations(
    observations: list[Observation],
) -> dict[str, list[Observation]]:
    partitions = {behavior: [] for behavior in BEHAVIORS}
    for observation in observations:
        if observation.behavior not in partitions:
            raise RuntimeError(f"unsupported behavior: {observation.behavior}")
        partitions[observation.behavior].append(observation)
    if sum(len(values) for values in partitions.values()) != len(observations):
        raise RuntimeError("behavior partition reconciliation failed")
    return partitions


def input_manifest(capture: Path) -> list[dict[str, Any]]:
    return [
        {
            "path": relative_path(capture / name),
            "bytes": (capture / name).stat().st_size,
            "sha256": sha256_file(capture / name),
        }
        for name in REQUIRED_INPUTS
    ]


def table_escape(value: str) -> str:
    return clean_text(value).replace("|", "\\|").replace("\n", " ")


def display_number(value: int | None) -> str:
    return "unresolved" if value is None else str(value)


def render_group_table(groups: list[RouteGroup], limit: int = 15) -> list[str]:
    selected = sorted(
        groups,
        key=lambda group: (
            -len(group.observations),
            -max(value.confidence for value in group.observations),
            group.signature,
        ),
    )[:limit]
    if not selected:
        return ["None.", ""]
    lines = [
        "| Behavior | Disposition | Family | Template | Level | PF | Signature | Observations | Confidence | Names |",
        "| --- | --- | ---: | ---: | ---: | ---: | --- | ---: | --- | --- |",
    ]
    for group in selected:
        confidences = [value.confidence for value in group.observations]
        names = sorted(
            {
                clean_text(
                    value.metadata.name
                    if value.metadata is not None
                    else value.row.source_name
                )
                for value in group.observations
            }
        )
        lines.append(
            "| "
            + " | ".join(
                [
                    group.behavior,
                    group.disposition,
                    display_number(group.family),
                    display_number(group.template),
                    display_number(group.level),
                    display_number(group.playfield),
                    group.signature,
                    str(len(group.observations)),
                    f"{min(confidences)}–{max(confidences)}",
                    table_escape(", ".join(names)),
                ]
            )
            + " |"
        )
    lines.append("")
    return lines


def render_report(
    capture: Path,
    validation: dict[str, Any],
    observations: list[Observation],
    groups: list[RouteGroup],
    datasets: dict[str, Path],
    inputs: list[dict[str, Any]],
) -> bytes:
    expected = int(
        validation["movementSummary"]["counts"]["usableFollowTargetPackets"]
    )
    if len(observations) != expected:
        raise RuntimeError(
            f"observation reconciliation failed: {len(observations)} != {expected}"
        )
    behavior_counts: Counter[str] = Counter(value.behavior for value in observations)
    disposition_counts: Counter[str] = Counter(
        value.disposition for value in observations
    )
    matrix: Counter[tuple[str, str]] = Counter(
        (value.behavior, value.disposition) for value in observations
    )
    reason_counts: dict[str, Counter[str]] = {
        disposition: Counter() for disposition in DISPOSITIONS
    }
    resolution_counts: Counter[str] = Counter(
        value.metadata_resolution for value in observations
    )
    for observation in observations:
        reason_counts[observation.disposition].update(
            observation.decision_reasons
        )

    lines = [
        f"# Corrected Arete Movement Promotion Audit — {capture.name}",
        "",
        "This is an observation-level, analysis-only audit. It does not modify AORebirth runtime behavior.",
        "",
        "## Verdict",
        "",
        f"- Reconciled observations: **{len(observations):,} / {expected:,}**.",
        f"- Promotable: **{disposition_counts['Promotable']:,}**.",
        f"- Ambiguous: **{disposition_counts['Ambiguous']:,}**.",
        f"- Rejected: **{disposition_counts['Rejected']:,}**.",
        f"- Post-decision route groups: **{len(groups):,}**.",
        "",
        "Every path is classified and scored before grouping. A rejected observation cannot alter a clean observation sharing the same route signature.",
        "",
        "## Behavior datasets",
        "",
        "| Behavior | Total | Promotable | Ambiguous | Rejected | Dataset |",
        "| --- | ---: | ---: | ---: | ---: | --- |",
    ]
    for behavior in BEHAVIORS:
        path = datasets[behavior]
        dataset_link = f"{path.parent.name}/{path.name}"
        lines.append(
            f"| {behavior} | {behavior_counts[behavior]:,} | "
            f"{matrix[(behavior, 'Promotable')]:,} | "
            f"{matrix[(behavior, 'Ambiguous')]:,} | "
            f"{matrix[(behavior, 'Rejected')]:,} | "
            f"[`{path.name}`]({dataset_link}) |"
        )

    for disposition in DISPOSITIONS:
        lines.extend(
            [
                "",
                f"## {disposition} observations — exact reasons",
                "",
                "| Reason | Observations |",
                "| --- | ---: |",
            ]
        )
        for reason, count in sorted(
            reason_counts[disposition].items(),
            key=lambda item: (-item[1], item[0]),
        ):
            lines.append(f"| `{reason}` | {count:,} |")

    lines.extend(
        [
            "",
            "## Metadata resolution",
            "",
            "| Resolution | Observations |",
            "| --- | ---: |",
        ]
    )
    for resolution, count in sorted(
        resolution_counts.items(), key=lambda item: (-item[1], item[0])
    ):
        lines.append(f"| `{resolution}` | {count:,} |")

    lines.extend(
        [
            "",
            "## Largest promotable route groups",
            "",
        ]
    )
    lines.extend(
        render_group_table(
            [group for group in groups if group.disposition == "Promotable"]
        )
    )

    lines.extend(
        [
            "## Corrected method",
            "",
            "- Each decoded `FollowTarget/NpcPath` packet is one observation.",
            "- Complete-capture SCFU metadata may resolve movement preceding SCFU when the same generation or stable reused identity has one exact metadata tuple.",
            "- Runtime identity and lifecycle generation are retained only as evidence columns; the reusable group key is behavior, disposition, NPC family, template, level, playfield, and route signature.",
            "- Route grouping happens only after observation disposition. Reasons are aggregated for reporting and never propagated between observations.",
            "- Teleport rejection requires an explicit nearby `SetPos`; no previous-destination versus next-current comparison is performed.",
            "- StopMovingCmd is the only movement-packet interruption rejection.",
            "- Patrol evidence does not require a closed loop, repeated edge, or multiple identities.",
            "- Combat and player influence are retained for chase, flee, and leash observations.",
            "- Scripted classification remains ambiguous when supported only by the bounded family heuristic.",
            "",
            "## Deterministic inputs",
            "",
            "| Path | Bytes | SHA-256 |",
            "| --- | ---: | --- |",
        ]
    )
    for item in inputs:
        lines.append(
            f"| `{item['path']}` | {item['bytes']:,} | `{item['sha256']}` |"
        )
    lines.extend(
        [
            "",
            "## Validation",
            "",
            f"- Capture lifecycle complete: `{str(validation['lifecycleSummary']['captureComplete']).lower()}`",
            f"- Capture processing allowed: `{str(validation['lifecycleSummary']['processingAllowed']).lower()}`",
            f"- SCFU pending/errors: `{validation['lifecycleSummary']['pendingSimpleCharFullUpdateRows']}/{validation['lifecycleSummary']['simpleCharFullUpdateDecodeErrors']}`",
            f"- Movement decode errors: `{validation['movementSummary']['counts']['decodeErrors']}`",
            f"- Dataset manifest: [`manifest.json`]({next(iter(datasets.values())).parent.name}/manifest.json)",
            f"- Report schema: `{SCHEMA_VERSION}`",
            "",
        ]
    )
    return "\n".join(lines).encode("utf-8")


def build_artifacts(
    capture: Path,
    report: Path,
    dataset_dir: Path,
) -> tuple[dict[Path, bytes], dict[str, int]]:
    capture = capture.resolve()
    report = report.resolve()
    dataset_dir = dataset_dir.resolve()
    validation = validate_capture(capture)
    lifecycle, spawn_times = load_lifecycle(capture)
    generation_index = build_generation_index(lifecycle)
    metadata_index, player_identities = load_metadata(capture, generation_index)
    paths, controls = load_movements(capture)
    positions = load_positions(capture)
    combat_intervals = load_combat_intervals(
        capture,
        metadata_index,
        generation_index,
        player_identities,
    )
    observations = [
        build_observation(
            index,
            row,
            generation_index,
            metadata_index,
            controls,
            combat_intervals,
            positions,
            player_identities,
            spawn_times,
        )
        for index, row in enumerate(paths, start=1)
    ]
    expected = int(
        validation["movementSummary"]["counts"]["usableFollowTargetPackets"]
    )
    if len(observations) != expected:
        raise RuntimeError(
            f"usable-path reconciliation failed: {len(observations)} != {expected}"
        )

    datasets = {
        behavior: dataset_dir / f"{behavior}.csv" for behavior in BEHAVIORS
    }
    partitions = partition_observations(observations)
    artifacts: dict[Path, bytes] = {}
    for behavior, path in datasets.items():
        artifacts[path] = render_dataset(partitions[behavior])
    groups = group_routes(observations)
    inputs = input_manifest(capture)
    manifest = {
        "schemaVersion": SCHEMA_VERSION,
        "captureId": capture.name,
        "expectedObservations": expected,
        "reconciledObservations": len(observations),
        "behaviors": {
            behavior: {
                "path": relative_path(datasets[behavior]),
                "observations": sum(
                    value.behavior == behavior for value in observations
                ),
                "promotable": sum(
                    value.behavior == behavior
                    and value.disposition == "Promotable"
                    for value in observations
                ),
                "ambiguous": sum(
                    value.behavior == behavior
                    and value.disposition == "Ambiguous"
                    for value in observations
                ),
                "rejected": sum(
                    value.behavior == behavior
                    and value.disposition == "Rejected"
                    for value in observations
                ),
            }
            for behavior in BEHAVIORS
        },
        "totals": {
            disposition.lower(): sum(
                value.disposition == disposition for value in observations
            )
            for disposition in DISPOSITIONS
        },
        "routeGroups": len(groups),
        "inputs": inputs,
    }
    manifest_path = dataset_dir / "manifest.json"
    artifacts[manifest_path] = (
        json.dumps(manifest, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    artifacts[report] = render_report(
        capture, validation, observations, groups, datasets, inputs
    )
    return artifacts, {
        "observations": len(observations),
        "promotable": sum(
            value.disposition == "Promotable" for value in observations
        ),
        "ambiguous": sum(
            value.disposition == "Ambiguous" for value in observations
        ),
        "rejected": sum(
            value.disposition == "Rejected" for value in observations
        ),
        "groups": len(groups),
    }


def write_artifacts(artifacts: dict[Path, bytes]) -> None:
    for path, payload in sorted(artifacts.items(), key=lambda item: str(item[0])):
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(path.suffix + ".tmp")
        temporary.write_bytes(payload)
        temporary.replace(path)


def check_artifacts(artifacts: dict[Path, bytes]) -> None:
    stale: list[str] = []
    for path, payload in sorted(artifacts.items(), key=lambda item: str(item[0])):
        if not path.is_file() or path.read_bytes() != payload:
            stale.append(relative_path(path))
    if stale:
        raise RuntimeError("stale or missing artifacts: " + ", ".join(stale))


def run_self_test() -> None:
    assert normalize_identity("(SimpleChar:000000AA)") == "SimpleChar:000000AA"
    assert route_signature(Point(0, 0, 0), Point(5, 0, 0)) == route_signature(
        Point(5, 0, 0), Point(0, 0, 0)
    )
    with tempfile.TemporaryDirectory(dir=REPO_ROOT / "tools-temp") as temporary:
        path = Path(temporary) / "sample.bin"
        path.write_bytes(b"corrected-movement-audit")
        assert len(sha256_file(path)) == 64
    print("PASS corrected movement audit smoke self-test")


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    try:
        if args.self_test:
            run_self_test()
            return 0
        artifacts, summary = build_artifacts(
            args.capture_folder,
            args.report,
            args.dataset_dir,
        )
        if args.write:
            write_artifacts(artifacts)
            print(
                "WROTE corrected movement audit "
                f"observations={summary['observations']} "
                f"promotable={summary['promotable']} "
                f"ambiguous={summary['ambiguous']} "
                f"rejected={summary['rejected']} "
                f"groups={summary['groups']}"
            )
        else:
            check_artifacts(artifacts)
            print("PASS corrected movement audit artifacts are current")
        return 0
    except Exception as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
