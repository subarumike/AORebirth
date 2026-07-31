#!/usr/bin/env python3
"""Deterministically reconcile a live Arete capture with promoted movement data."""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import math
import sys
import tempfile
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_RUNTIME_DATASET_DIR = (
    REPO_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "Content"
    / "Captured"
    / "Arete"
    / "movement-full"
)
DEFAULT_LEGACY_ROBOT_DATASET = (
    DEFAULT_RUNTIME_DATASET_DIR.parent / "cleaning_robot_patrol_replay.csv"
)
BEHAVIORS = ("patrol", "spawn", "chase", "flee", "leash")
COORDINATE_TOLERANCE = 0.001
TIMING_TOLERANCE_SECONDS = 0.250
NEAREST_VARIANT_TOLERANCE = 0.001


@dataclass(frozen=True)
class Point:
    x: float
    y: float
    z: float


@dataclass(frozen=True)
class Constraint:
    family: int
    template: int
    level: int
    playfield: int
    name: str


@dataclass(frozen=True)
class RuntimeRow:
    behavior: str
    constraint: Constraint
    observation_id: str
    capture_id: str
    source_identity: str
    source_generation: int
    start: Point
    end: Point
    delay_after_seconds: float


@dataclass(frozen=True)
class LiveNpc:
    captured_utc: datetime
    identity: str
    constraint: Constraint
    position: Point


@dataclass(frozen=True)
class LivePath:
    captured_utc: datetime
    sequence: int
    identity: str
    name: str
    start: Point
    end: Point


@dataclass(frozen=True)
class LegacyPath:
    start: Point
    end: Point


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--self-test", action="store_true")
    parser.add_argument("--capture-folder", type=Path)
    parser.add_argument(
        "--runtime-dataset-dir",
        type=Path,
        default=DEFAULT_RUNTIME_DATASET_DIR,
    )
    parser.add_argument(
        "--legacy-robot-dataset",
        type=Path,
        default=DEFAULT_LEGACY_ROBOT_DATASET,
    )
    parser.add_argument("--report", type=Path)
    parser.add_argument("--identity-report", type=Path)
    parser.add_argument("--path-report", type=Path)
    parser.add_argument("--visual-no-displacement", action="store_true")
    parser.add_argument("--visual-movement-confirmed", action="store_true")
    parser.add_argument("--visual-no-attacks", action="store_true")
    return parser.parse_args(argv)


def parse_time(value: str) -> datetime:
    result = datetime.fromisoformat(value.strip().replace("Z", "+00:00"))
    if result.tzinfo is None:
        result = result.replace(tzinfo=timezone.utc)
    return result.astimezone(timezone.utc)


def parse_int(value: str | None) -> int | None:
    try:
        return int((value or "").strip(), 10)
    except ValueError:
        return None


def parse_float(value: str | None) -> float | None:
    try:
        result = float((value or "").strip())
    except ValueError:
        return None
    return result if math.isfinite(result) else None


def normalize_identity(value: str | None) -> str | None:
    text = (value or "").strip()
    if text.startswith("(") and text.endswith(")"):
        text = text[1:-1]
    if ":" not in text:
        return None
    identity_type, instance = text.split(":", 1)
    try:
        return f"{identity_type}:{int(instance, 16):08X}"
    except ValueError:
        return None


def point_from(row: dict[str, str], prefix: str) -> Point | None:
    values = (
        parse_float(row.get(prefix + "X")),
        parse_float(row.get(prefix + "Y")),
        parse_float(row.get(prefix + "Z")),
    )
    if any(value is None for value in values):
        return None
    return Point(values[0], values[1], values[2])  # type: ignore[arg-type]


def read_csv(path: Path) -> Iterable[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        yield from csv.DictReader(stream)


def horizontal_distance(first: Point, second: Point) -> float:
    return math.hypot(second.x - first.x, second.z - first.z)


def points_equal(first: Point, second: Point) -> bool:
    return (
        abs(first.x - second.x) <= COORDINATE_TOLERANCE
        and abs(first.y - second.y) <= COORDINATE_TOLERANCE
        and abs(first.z - second.z) <= COORDINATE_TOLERANCE
    )


def load_runtime_rows(directory: Path) -> list[RuntimeRow]:
    result: list[RuntimeRow] = []
    for behavior in BEHAVIORS:
        path = directory / f"{behavior}.csv"
        for row in read_csv(path):
            family = parse_int(row.get("NpcFamily"))
            template = parse_int(row.get("MonsterData"))
            level = parse_int(row.get("Level"))
            playfield = parse_int(row.get("RuntimePlayfieldId"))
            generation = parse_int(row.get("SourceGeneration"))
            delay = parse_float(row.get("DelayAfterSeconds"))
            capture_id = (row.get("CaptureId") or "").strip()
            start = point_from(row, "Start")
            end = point_from(row, "End")
            if (
                family is None
                or template is None
                or level is None
                or playfield is None
                or generation is None
                or delay is None
                or not capture_id
                or start is None
                or end is None
            ):
                raise RuntimeError(f"incomplete runtime row in {path}")
            result.append(
                RuntimeRow(
                    behavior=behavior,
                    constraint=Constraint(
                        family,
                        template,
                        level,
                        playfield,
                        (row.get("Name") or "").strip(),
                    ),
                    observation_id=(row.get("ObservationId") or "").strip(),
                    capture_id=capture_id,
                    source_identity=(row.get("SourceIdentity") or "").strip(),
                    source_generation=generation,
                    start=start,
                    end=end,
                    delay_after_seconds=delay,
                )
            )
    return result


def load_live_npcs(capture: Path) -> tuple[list[LiveNpc], dict[str, str]]:
    by_identity: dict[str, list[LiveNpc]] = defaultdict(list)
    failures: dict[str, str] = {}
    for row in read_csv(capture / "scfu-appearance.csv"):
        identity = normalize_identity(row.get("Identity"))
        family = parse_int(row.get("NpcFamily"))
        template = parse_int(row.get("MonsterData"))
        level = parse_int(row.get("Level"))
        playfield = parse_int(row.get("PlayfieldId"))
        position = point_from(row, "Position")
        is_npc = (row.get("CharacterInfoType") or "").strip().lower() == "npcinfo"
        complete = (row.get("DecodeFullyConsumed") or "").strip().lower() == "true"
        if identity is None:
            continue
        if (
            not is_npc
            or not complete
            or family is None
            or template is None
            or level is None
            or playfield is None
            or position is None
        ):
            failures[identity] = "live_scfu_metadata_incomplete"
            continue
        by_identity[identity].append(
            LiveNpc(
                parse_time(row["CapturedUtc"]),
                identity,
                Constraint(
                    family,
                    template,
                    level,
                    playfield,
                    (row.get("Name") or "").strip(),
                ),
                position,
            )
        )

    return collapse_live_npcs(by_identity, failures)


def collapse_live_npcs(
    by_identity: dict[str, list[LiveNpc]], failures: dict[str, str]
) -> tuple[list[LiveNpc], dict[str, str]]:
    result: list[LiveNpc] = []
    for identity, values in sorted(by_identity.items()):
        exact_constraints = {value.constraint for value in values}
        if len(exact_constraints) != 1:
            failures[identity] = "live_identity_regenerated_or_conflicting"
            continue
        # Position updates do not imply a regenerated identity. The first complete
        # update is the position seen by the runtime at capture attachment.
        result.append(min(values, key=lambda value: value.captured_utc))
    return result, failures


def load_live_paths(capture: Path) -> list[LivePath]:
    result: list[LivePath] = []
    for row in read_csv(capture / "movement-packets.csv"):
        if (
            (row.get("MessageType") or "").strip() != "FollowTarget"
            or (row.get("FollowKind") or "").strip() != "NpcPath"
        ):
            continue
        identity = normalize_identity(row.get("SourceIdentity"))
        start = point_from(row, "Current")
        end = point_from(row, "Destination")
        if identity is None or start is None or end is None:
            continue
        result.append(
            LivePath(
                parse_time(row["CapturedUtc"]),
                parse_int(row.get("Sequence")) or 0,
                identity,
                (row.get("SourceName") or "").strip(),
                start,
                end,
            )
        )
    result.sort(key=lambda value: (value.captured_utc, value.sequence, value.identity))
    return result


def load_legacy_paths(path: Path) -> list[LegacyPath]:
    result: list[LegacyPath] = []
    for row in read_csv(path):
        if (
            (row.get("MessageType") or "").strip() != "FollowTarget"
            or (row.get("FollowKind") or "").strip() != "NpcPath"
        ):
            continue
        start = point_from(row, "Current")
        end = point_from(row, "Destination")
        if start is not None and end is not None:
            result.append(LegacyPath(start, end))
    return result


def exact_constraint_rows(
    npc: LiveNpc, runtime_rows: list[RuntimeRow]
) -> list[RuntimeRow]:
    return [row for row in runtime_rows if row.constraint == npc.constraint]


def select_source_variant(
    npc: LiveNpc,
    candidates: list[RuntimeRow],
    spawn_generation: int = 1,
) -> tuple[str, str, int, float] | None:
    distances: dict[tuple[str, str, int], float] = {}
    for row in candidates:
        if row.behavior != "patrol":
            continue
        key = (row.capture_id, row.source_identity, row.source_generation)
        distance = horizontal_distance(npc.position, row.start)
        distances[key] = min(distance, distances.get(key, float("inf")))
    variants = sorted(
        (
            (capture_id, source_identity, source_generation, distance)
            for (
                capture_id,
                source_identity,
                source_generation,
            ), distance in distances.items()
        ),
        key=lambda value: (value[3], value[0], value[1], value[2]),
    )
    if not variants:
        return None
    nearest_distance = variants[0][3]
    nearest_variants = [
        variant
        for variant in variants
        if abs(variant[3] - nearest_distance) <= NEAREST_VARIANT_TOLERANCE
    ]
    return nearest_variants[(spawn_generation - 1) % len(nearest_variants)]


def matching_paths(
    path: LivePath, candidates: Iterable[RuntimeRow]
) -> list[RuntimeRow]:
    return [
        row
        for row in candidates
        if points_equal(path.start, row.start) and points_equal(path.end, row.end)
    ]


def matching_legacy_paths(
    path: LivePath, candidates: Iterable[LegacyPath]
) -> list[LegacyPath]:
    return [
        row
        for row in candidates
        if points_equal(path.start, row.start) and points_equal(path.end, row.end)
    ]


def family_label(constraint: Constraint) -> str:
    return (
        f"{constraint.name} "
        f"(family={constraint.family}, template={constraint.template}, "
        f"level={constraint.level}, pf={constraint.playfield})"
    )


def build_results(
    capture: Path,
    runtime_rows: list[RuntimeRow],
    legacy_paths: list[LegacyPath],
    visual_no_displacement: bool,
    visual_movement_confirmed: bool,
    visual_no_attacks: bool,
) -> tuple[bytes, bytes, bytes, dict[str, int]]:
    live_npcs, identity_failures = load_live_npcs(capture)
    live_paths = load_live_paths(capture)
    npc_by_identity = {npc.identity: npc for npc in live_npcs}
    paths_by_identity: dict[str, list[LivePath]] = defaultdict(list)
    for path in live_paths:
        paths_by_identity[path.identity].append(path)

    identity_rows: list[dict[str, str]] = []
    constraint_rows: dict[Constraint, list[RuntimeRow]] = defaultdict(list)
    for row in runtime_rows:
        constraint_rows[row.constraint].append(row)

    identities_with_any_candidate = 0
    identities_with_promoted_patrol = 0
    identities_with_packets = 0
    reason_counts: Counter[str] = Counter(identity_failures.values())
    family_summary: dict[Constraint, Counter[str]] = defaultdict(Counter)

    for npc in live_npcs:
        candidates = constraint_rows.get(npc.constraint, [])
        observed_paths = paths_by_identity.get(npc.identity, [])
        if candidates:
            identities_with_any_candidate += 1
        if observed_paths:
            identities_with_packets += 1
        behavior_counts = Counter(row.behavior for row in candidates)
        nearest_by_behavior: dict[str, float | None] = {}
        for behavior in BEHAVIORS:
            behavior_rows = [row for row in candidates if row.behavior == behavior]
            nearest_by_behavior[behavior] = (
                min(horizontal_distance(npc.position, row.start) for row in behavior_rows)
                if behavior_rows
                else None
            )
        patrol_nearest = nearest_by_behavior["patrol"]
        bindable_patrol = patrol_nearest is not None
        selected_variant = select_source_variant(npc, candidates)
        selected_patrol_rows = (
            []
            if selected_variant is None
            else [
                row
                for row in candidates
                if row.behavior == "patrol"
                and row.capture_id == selected_variant[0]
                and row.source_identity == selected_variant[1]
                and row.source_generation == selected_variant[2]
            ]
        )
        selected_patrol_nearest = (
            None
            if not selected_patrol_rows
            else min(
                horizontal_distance(npc.position, row.start)
                for row in selected_patrol_rows
            )
        )
        if bindable_patrol:
            identities_with_promoted_patrol += 1

        if not candidates:
            reason = "no_exact_promoted_metadata_constraint"
        elif not bindable_patrol:
            reason = "no_promoted_patrol_for_exact_metadata_constraint"
        elif selected_variant is None:
            reason = "promoted_patrol_variant_unresolved"
        elif not selected_patrol_rows:
            reason = "selected_source_variant_has_no_patrol_observation"
        elif not observed_paths:
            reason = "promoted_patrol_no_packet_in_observation_window"
        else:
            reason = "live_movement_packet_observed"
        reason_counts[reason] += 1
        family_summary[npc.constraint]["identities"] += 1
        family_summary[npc.constraint]["candidate_identities"] += int(bool(candidates))
        family_summary[npc.constraint]["bindable_patrol_identities"] += int(
            bindable_patrol
        )
        family_summary[npc.constraint]["packet_identities"] += int(bool(observed_paths))

        identity_rows.append(
            {
                "LiveIdentity": npc.identity,
                "Name": npc.constraint.name,
                "NpcFamily": str(npc.constraint.family),
                "MonsterData": str(npc.constraint.template),
                "Level": str(npc.constraint.level),
                "PlayfieldId": str(npc.constraint.playfield),
                "PositionX": format(npc.position.x, ".9g"),
                "PositionY": format(npc.position.y, ".9g"),
                "PositionZ": format(npc.position.z, ".9g"),
                "PatrolRows": str(behavior_counts["patrol"]),
                "SpawnRows": str(behavior_counts["spawn"]),
                "ChaseRows": str(behavior_counts["chase"]),
                "FleeRows": str(behavior_counts["flee"]),
                "LeashRows": str(behavior_counts["leash"]),
                "NearestPatrolStartMeters": (
                    ""
                    if patrol_nearest is None
                    else format(patrol_nearest, ".6f")
                ),
                "PromotedPatrolEvidence": str(bindable_patrol).lower(),
                "SelectedCaptureId": (
                    "" if selected_variant is None else selected_variant[0]
                ),
                "SelectedSourceIdentity": (
                    "" if selected_variant is None else selected_variant[1]
                ),
                "SelectedSourceGeneration": (
                    "" if selected_variant is None else str(selected_variant[2])
                ),
                "SelectedVariantDistanceMeters": (
                    "" if selected_variant is None else format(selected_variant[3], ".6f")
                ),
                "SelectedPatrolRows": str(len(selected_patrol_rows)),
                "SelectedPatrolNearestMeters": (
                    ""
                    if selected_patrol_nearest is None
                    else format(selected_patrol_nearest, ".6f")
                ),
                "ObservedNpcPathPackets": str(len(observed_paths)),
                "ExactReason": reason,
            }
        )

    path_rows: list[dict[str, str]] = []
    path_match_counts: Counter[str] = Counter()
    previous_by_identity: dict[str, tuple[LivePath, list[RuntimeRow]]] = {}
    timing_exact = 0
    timing_deviation = 0
    for path in live_paths:
        npc = npc_by_identity.get(path.identity)
        candidates = [] if npc is None else constraint_rows.get(npc.constraint, [])
        promoted_matches = matching_paths(path, candidates)
        legacy_matches = matching_legacy_paths(path, legacy_paths)
        if promoted_matches:
            route_result = "exact_promoted_route"
        elif legacy_matches:
            route_result = "exact_legacy_robot_route"
        elif npc is None:
            route_result = "live_identity_metadata_unresolved"
        elif not candidates:
            route_result = "no_exact_promoted_metadata_constraint"
        else:
            route_result = "coordinates_not_in_promoted_dataset"
        path_match_counts[route_result] += 1

        interval = ""
        expected_delay = ""
        timing_result = "not_comparable"
        previous = previous_by_identity.get(path.identity)
        if previous is not None:
            previous_path, previous_matches = previous
            elapsed = (path.captured_utc - previous_path.captured_utc).total_seconds()
            interval = format(elapsed, ".6f")
            if previous_matches:
                expected_values = sorted(
                    {row.delay_after_seconds for row in previous_matches}
                )
                expected_delay = ",".join(format(value, ".6f") for value in expected_values)
                if any(
                    abs(elapsed - value) <= TIMING_TOLERANCE_SECONDS
                    for value in expected_values
                ):
                    timing_result = "captured_timing_match"
                    timing_exact += 1
                else:
                    timing_result = "captured_timing_deviation"
                    timing_deviation += 1
        previous_by_identity[path.identity] = (path, promoted_matches)

        path_rows.append(
            {
                "CapturedUtc": path.captured_utc.isoformat().replace("+00:00", "Z"),
                "Sequence": str(path.sequence),
                "LiveIdentity": path.identity,
                "Name": path.name,
                "StartX": format(path.start.x, ".9g"),
                "StartY": format(path.start.y, ".9g"),
                "StartZ": format(path.start.z, ".9g"),
                "EndX": format(path.end.x, ".9g"),
                "EndY": format(path.end.y, ".9g"),
                "EndZ": format(path.end.z, ".9g"),
                "RouteResult": route_result,
                "PromotedObservationIds": ",".join(
                    sorted(row.observation_id for row in promoted_matches)
                ),
                "IntervalSincePreviousSeconds": interval,
                "ExpectedCapturedDelaySeconds": expected_delay,
                "TimingResult": timing_result,
            }
        )

    identity_payload = render_csv(identity_rows)
    path_payload = render_csv(path_rows)
    report_lines = [
        f"# Arete Movement Live Verification — {capture.name}",
        "",
        "This report reconciles the complete live observation capture against every behavior-specific promoted runtime row. Regenerated live identities are evidence labels only and are not used as promotion keys.",
        "",
        "## Verdict",
        "",
        f"- Promoted runtime rows loaded: **{len(runtime_rows):,}**.",
        f"- Live NPC identities with complete stable metadata: **{len(live_npcs):,}**.",
        f"- Live identities rejected as incomplete or regenerated/conflicting: **{len(identity_failures):,}**.",
        f"- Live identities with any exact promoted metadata constraint: **{identities_with_any_candidate:,}**.",
        f"- Live identities with an exact promoted patrol constraint: **{identities_with_promoted_patrol:,}**.",
        f"- Live identities that emitted `FollowTarget/NpcPath`: **{identities_with_packets:,}**.",
        f"- Live path packets reconciled: **{len(live_paths):,} / {len(live_paths):,}**.",
        "",
        "## Exact identity outcomes",
        "",
        "| Reason | Identities |",
        "| --- | ---: |",
    ]
    for reason, count in sorted(reason_counts.items(), key=lambda item: (-item[1], item[0])):
        report_lines.append(f"| `{reason}` | {count:,} |")

    report_lines.extend(
        [
            "",
            "## Family and constraint coverage",
            "",
            "| Exact constraint | Live identities | Metadata candidates | Promoted patrol | Packet emitters |",
            "| --- | ---: | ---: | ---: | ---: |",
        ]
    )
    for constraint, counts in sorted(
        family_summary.items(),
        key=lambda item: (
            item[0].name,
            item[0].family,
            item[0].template,
            item[0].level,
        ),
    ):
        report_lines.append(
            f"| {family_label(constraint)} | {counts['identities']} | "
            f"{counts['candidate_identities']} | "
            f"{counts['bindable_patrol_identities']} | "
            f"{counts['packet_identities']} |"
        )

    report_lines.extend(
        [
            "",
            "## Live packet route comparison",
            "",
            "| Result | Packets |",
            "| --- | ---: |",
        ]
    )
    for result, count in sorted(
        path_match_counts.items(), key=lambda item: (-item[1], item[0])
    ):
        report_lines.append(f"| `{result}` | {count:,} |")
    report_lines.extend(
        [
            "",
            f"- Comparable promoted timing matches: **{timing_exact:,}**.",
            f"- Comparable promoted timing deviations: **{timing_deviation:,}**.",
            f"- Timing tolerance: **±{TIMING_TOLERANCE_SECONDS:.3f} seconds**.",
            "",
            "## Manual original-client observations",
            "",
            (
                "- Visible NPC movement was confirmed."
                if visual_movement_confirmed
                else (
                    "- No visible enemy displacement was observed."
                    if visual_no_displacement
                    else "- Visible displacement result was not supplied to this verifier."
                )
            ),
            (
                "- No enemy attacks were observed."
                if visual_no_attacks
                else "- Attack behavior result was not supplied to this verifier."
            ),
            "- Original-client input was manual; this verifier performs no client automation.",
            "",
            "## Evidence boundary",
            "",
            "- Exact family, template, level, playfield, and name metadata activates catalog eligibility; no invented distance gate is applied.",
            "- Patrol source selection is behavior-specific, capture-scoped, nearest-cohort based, and deterministic per spawn generation.",
            "- A promoted patrol with no packet in this observation window remains corpus-backed evidence; window absence does not reject it.",
            "- Spawn, chase, flee, and leash rows are reported as exact metadata evidence; this baseline did not force their lifecycle conditions.",
            "- Attack absence is recorded separately because movement datasets do not prove aggression or attack initiation semantics.",
            "",
            "Detailed deterministic evidence:",
            "",
            "- Identity reconciliation: `arete-movement-live-identities.csv`",
            "- Packet reconciliation: `arete-movement-live-paths.csv`",
            "",
        ]
    )
    summary = {
        "runtime_rows": len(runtime_rows),
        "live_npcs": len(live_npcs),
        "identity_failures": len(identity_failures),
        "candidate_identities": identities_with_any_candidate,
        "bindable_patrol_identities": identities_with_promoted_patrol,
        "packet_identities": identities_with_packets,
        "live_paths": len(live_paths),
        "timing_exact": timing_exact,
        "timing_deviation": timing_deviation,
    }
    return (
        "\n".join(report_lines).encode("utf-8"),
        identity_payload,
        path_payload,
        summary,
    )


def render_csv(rows: list[dict[str, str]]) -> bytes:
    if not rows:
        return b""
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)
    return stream.getvalue().encode("utf-8")


def sha256(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def write_or_check(path: Path, payload: bytes, write: bool) -> None:
    if write:
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(path.suffix + ".tmp")
        temporary.write_bytes(payload)
        temporary.replace(path)
    elif not path.is_file() or path.read_bytes() != payload:
        raise RuntimeError(f"stale or missing artifact: {path}")


def run_self_test() -> None:
    first = Point(1.0, 2.0, 3.0)
    assert points_equal(first, Point(1.0005, 2.0005, 3.0005))
    assert not points_equal(first, Point(1.002, 2.0, 3.0))
    assert horizontal_distance(Point(0, 50, 0), Point(3, -50, 4)) == 5.0
    constraint = Constraint(25, 17657, 1, 6553, "Garbage Flea")
    npc = LiveNpc(datetime.now(timezone.utc), "SimpleChar:00000001", constraint, Point(0, 0, 0))
    row = RuntimeRow(
        "patrol",
        constraint,
        "m00001",
        "20260722-104809",
        "SimpleChar:ABCDEF01",
        2,
        Point(0, 0, 0),
        Point(1, 0, 0),
        1.0,
    )
    assert exact_constraint_rows(npc, [row]) == [row]
    assert select_source_variant(npc, [row]) == (
        row.capture_id,
        row.source_identity,
        row.source_generation,
        0.0,
    )
    far_variant = select_source_variant(
        LiveNpc(
            npc.captured_utc,
            npc.identity,
            constraint,
            Point(7, 0, 0),
        ),
        [row],
    )
    assert far_variant is not None and far_variant[3] == 7.0
    chase = RuntimeRow(
        "chase",
        constraint,
        "m00002",
        "20260722-152454",
        "SimpleChar:ABCDEF02",
        1,
        Point(0, 0, 0),
        Point(2, 0, 0),
        1.0,
    )
    assert select_source_variant(npc, [chase]) is None
    tied = RuntimeRow(
        "patrol",
        constraint,
        "m00003",
        "20260722-152454",
        "SimpleChar:ABCDEF02",
        1,
        Point(0, 0, 0),
        Point(3, 0, 0),
        1.0,
    )
    assert select_source_variant(npc, [row, tied], spawn_generation=2) == (
        tied.capture_id,
        tied.source_identity,
        tied.source_generation,
        0.0,
    )
    regenerated = LiveNpc(
        npc.captured_utc,
        "SimpleChar:99999999",
        constraint,
        Point(0, 0, 0),
    )
    assert exact_constraint_rows(regenerated, [row]) == [row]
    later_position = LiveNpc(
        npc.captured_utc + timedelta(seconds=1),
        npc.identity,
        constraint,
        Point(5, 0, 5),
    )
    collapsed, failures = collapse_live_npcs(
        {npc.identity: [later_position, npc]}, {}
    )
    assert collapsed == [npc]
    assert not failures
    conflicting = LiveNpc(
        npc.captured_utc + timedelta(seconds=2),
        npc.identity,
        Constraint(55, 17687, 5, 6553, "Rollerrat"),
        Point(0, 0, 0),
    )
    collapsed, failures = collapse_live_npcs(
        {npc.identity: [npc, conflicting]}, {}
    )
    assert not collapsed
    assert failures[npc.identity] == "live_identity_regenerated_or_conflicting"
    path = LivePath(
        npc.captured_utc,
        1,
        npc.identity,
        constraint.name,
        Point(0, 0, 0),
        Point(1, 0, 0),
    )
    assert matching_paths(path, [row]) == [row]
    assert not matching_paths(
        LivePath(
            npc.captured_utc,
            2,
            npc.identity,
            constraint.name,
            Point(0, 0, 0),
            Point(2, 0, 0),
        ),
        [row],
    )
    with tempfile.TemporaryDirectory(dir=REPO_ROOT / "tools-temp") as directory:
        sample = Path(directory) / "evidence.bin"
        sample.write_bytes(b"arete-live-verification")
        assert len(sha256(sample.read_bytes())) == 64
    print("PASS Arete movement live verifier self-tests")


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    try:
        if args.self_test:
            run_self_test()
            return 0
        if args.capture_folder is None or args.report is None:
            raise RuntimeError("--capture-folder and --report are required")
        identity_report = args.identity_report or args.report.with_name(
            "arete-movement-live-identities.csv"
        )
        path_report = args.path_report or args.report.with_name(
            "arete-movement-live-paths.csv"
        )
        runtime_rows = load_runtime_rows(args.runtime_dataset_dir.resolve())
        legacy_paths = load_legacy_paths(args.legacy_robot_dataset.resolve())
        report, identities, paths, summary = build_results(
            args.capture_folder.resolve(),
            runtime_rows,
            legacy_paths,
            args.visual_no_displacement,
            args.visual_movement_confirmed,
            args.visual_no_attacks,
        )
        write_or_check(args.report.resolve(), report, args.write)
        write_or_check(identity_report.resolve(), identities, args.write)
        write_or_check(path_report.resolve(), paths, args.write)
        verb = "WROTE" if args.write else "PASS"
        print(
            f"{verb} Arete movement live verification "
            f"live_npcs={summary['live_npcs']} "
            f"candidates={summary['candidate_identities']} "
            f"bindable_patrol={summary['bindable_patrol_identities']} "
            f"packet_identities={summary['packet_identities']} "
            f"paths={summary['live_paths']}"
        )
        return 0
    except Exception as exception:
        print(f"ERROR: {exception}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
