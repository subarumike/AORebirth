#!/usr/bin/env python3
"""Audit position evidence for capture-certified non-equipped NPC attacks.

The combat inventory is intentionally large, so this helper streams only its
top-level ``profiles`` array.  Every selected AttackInfo is then re-opened from
the canonical raw capture stream.  Packet ordering uses GlobalOrdinal when the
whole canonical session has it, otherwise the physical packets.hex.log order.

An exact stationary hit distance is deliberately strict:

* source and target must both have an exact raw-derived position before the hit;
* their movement state must be FullStop at the hit;
* a later exact position must bracket the hit for each identity;
* no translation-start, FollowTarget, or changed SetPos may occur first; and
* the later position must equal the at-hit position within FLOAT_EPSILON.

Even a distance passing those checks is only a lower bound on attack range.  A
landed hit does not prove the server's maximum acceptance threshold.

An exact maximum may instead be proven when the captured SpecialAttackWeapon
template endpoints both carry the same positive ItemDb AttackRange (Stat 287).
That authority is generated into the combat inventory; runtime content literals
are never accepted as evidence by this audit.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import re
import struct
import subprocess
import sys
import tempfile
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


REPO_ROOT = Path(
    os.environ.get(
        "AO_REBIRTH_NPC_COMBAT_AUDIT_REPO_ROOT",
        str(Path(__file__).resolve().parents[2]),
    )
).resolve(strict=True)
DEFAULT_INVENTORY = (
    REPO_ROOT / "docs" / "generated" / "capture_backed_npc_combat_inventory.json"
)
DEFAULT_INVENTORY_LOGICAL_PATH = (
    "docs/generated/capture_backed_npc_combat_inventory.json"
)
DEFAULT_OUTPUT = (
    REPO_ROOT
    / "docs"
    / "generated"
    / "capture_backed_npc_attack_range_audit.json"
)

sys.path.insert(0, str(REPO_ROOT / "tools-temp" / "AOSharpLiveCapture"))
sys.path.insert(0, str(REPO_ROOT / "tools-temp" / "AOSharpCaptureAnalyzer"))

from decode_npc_lifecycle_capture import (  # noqa: E402
    PACKET_RE,
    SOURCE_PACKET_RE,
    load_packet_records,
)
from extract_capture_backed_npc_combat import (  # noqa: E402
    decode_attack_info,
    first_value,
    load_scfu_projection_rows,
    parse_identity,
)


ATTACK_INFO = 0x46002F16
CHAR_DC_MOVE = 0x54111123
SET_POS = 0x195E496E
FOLLOW_TARGET = 0x260F3671
STOP_MOVING_CMD = 0x742E2314
SIMPLE_CHAR_FULL_UPDATE = 0x271B3A6B
SIMPLE_CHAR = 0x0000C350
FLOAT_EPSILON = 0.025
TRANSLATION_START_MOVE_TYPES = frozenset({1, 3, 5, 7, 15, 17})
TRANSLATION_STOP_MOVE_TYPES = frozenset({2, 4, 6, 8, 16, 18})
FULL_STOP_MOVE_TYPE = 21
CAPTURE_WORKER_MAX_ATTEMPTS = 8


def signed32(value: int) -> int:
    return value - 0x100000000 if value & 0x80000000 else value


def identity_hex(value: int) -> str:
    return f"0x{value & 0xFFFFFFFF:08X}"


def read_identity(packet: bytes, offset: int) -> tuple[int, int]:
    identity_type, raw_instance = struct.unpack_from(">II", packet, offset)
    return identity_type, signed32(raw_instance)


def read_vector3(packet: bytes, offset: int) -> tuple[float, float, float]:
    return struct.unpack_from(">fff", packet, offset)


def finite_position(values: Iterable[Any]) -> tuple[float, float, float] | None:
    try:
        result = tuple(float(value) for value in values)
    except (TypeError, ValueError):
        return None
    if len(result) != 3 or not all(math.isfinite(value) for value in result):
        return None
    return result  # type: ignore[return-value]


def same_position(
    left: tuple[float, float, float], right: tuple[float, float, float]
) -> bool:
    return all(abs(a - b) <= FLOAT_EPSILON for a, b in zip(left, right))


def distance_3d(
    left: tuple[float, float, float], right: tuple[float, float, float]
) -> float:
    return math.sqrt(sum((a - b) ** 2 for a, b in zip(left, right)))


def distance_xz(
    left: tuple[float, float, float], right: tuple[float, float, float]
) -> float:
    return math.hypot(left[0] - right[0], left[2] - right[2])


def iter_top_level_array_objects(path: Path, key: str) -> Iterable[dict[str, Any]]:
    """Yield objects from a named top-level JSON array."""

    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{path}: generated inventory JSON is malformed: {error}") from error
    values = document.get(key)
    if not isinstance(values, list):
        raise ValueError(f"{path}: top-level key {key!r} is not an array")
    for index, value in enumerate(values):
        if not isinstance(value, dict):
            raise ValueError(f"{path}: expected object in {key!r} at index {index}")
        yield value


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                return hasher.hexdigest()
            hasher.update(chunk)


@dataclass(frozen=True)
class StreamRef:
    profile_key: str
    semantic_profile_id: str
    stream_signature_id: str
    signature: dict[str, Any]
    attack_info_packet_ids: tuple[str, ...]
    captured_attack_range: float | None
    captured_attack_range_evidence: dict[str, Any] | None


@dataclass(frozen=True)
class Event:
    order: int
    sequence: int
    identity: int
    kind: str
    position: tuple[float, float, float] | None
    move_type: int | None = None


def collect_natural_special_streams(inventory: Path) -> list[StreamRef]:
    result = []
    for profile in iter_top_level_array_objects(inventory, "profiles"):
        for variant in profile.get("variants", []):
            if not variant.get("captureCertified"):
                continue
            if (
                variant.get("baseSignature", {}).get("weaponContextKind")
                != "natural-or-special"
            ):
                continue
            for stream in variant.get("streams", []):
                result.append(
                    StreamRef(
                        profile_key=profile["profileKey"],
                        semantic_profile_id=variant["semanticProfileId"],
                        stream_signature_id=stream["streamSignatureId"],
                        signature=stream["signature"],
                        attack_info_packet_ids=tuple(stream["attackInfoPacketIds"]),
                        captured_attack_range=stream.get("capturedAttackRange"),
                        captured_attack_range_evidence=variant.get(
                            "capturedAttackRangeEvidence"
                        ),
                    )
                )
    return result


def packet_id_parts(packet_id: str) -> tuple[str, int]:
    capture, suffix = packet_id.rsplit("|IN|", 1)
    sequence_text, _ = suffix.split("|", 1)
    return capture, int(sequence_text)


def physical_packet_order(
    capture: Path, canonical: dict[tuple[str, int], dict[str, Any]]
) -> dict[tuple[str, int], int]:
    path = capture / "packets.hex.log"
    if not path.exists():
        return {}
    result = {}
    with path.open("r", encoding="utf-8-sig", errors="replace") as handle:
        for physical_row, line in enumerate(handle, 1):
            text = line.strip().replace("\0", "")
            match = PACKET_RE.match(text) or SOURCE_PACKET_RE.match(text)
            if match is None:
                continue
            key = (match.group("direction"), int(match.group("sequence")))
            record = canonical.get(key)
            if record is None or record["rawHex"] != match.group("hex").upper():
                continue
            result.setdefault(key, physical_row)
    return result


def session_order(
    capture: Path, records: list[dict[str, Any]]
) -> tuple[str, dict[tuple[str, int], int], list[str]]:
    canonical = {(row["direction"], row["sequence"]): row for row in records}
    if records and all(row.get("globalOrdinal") is not None for row in records):
        return (
            "globalOrdinal",
            {key: int(row["globalOrdinal"]) for key, row in canonical.items()},
            [],
        )
    physical = physical_packet_order(capture, canonical)
    missing = [
        f"{direction}:{sequence}"
        for direction, sequence in canonical
        if (direction, sequence) not in physical
    ]
    return "physical-packets.hex.log", physical, missing


def scfu_events(
    capture: Path,
    canonical: dict[tuple[str, int], dict[str, Any]],
    order: dict[tuple[str, int], int],
) -> tuple[list[Event], list[str]]:
    events = []
    issues = []
    rows, projection, errors = load_scfu_projection_rows(capture, canonical)
    for error in errors:
        issues.append("SCFU projection error: " + str(error.get("error") or error))
    for row in rows:
        if first_value(row, "Direction").upper() != "IN":
            continue
        sequence_text = first_value(row, "Sequence")
        try:
            sequence = int(sequence_text)
        except ValueError:
            continue
        key = ("IN", sequence)
        packet_order = order.get(key)
        record = canonical.get(key)
        if packet_order is None or record is None:
            continue
        raw = bytes.fromhex(record["rawHex"])
        if len(raw) < 20 or struct.unpack_from(">I", raw, 16)[0] != SIMPLE_CHAR_FULL_UPDATE:
            issues.append(f"SCFU row IN:{sequence} does not map to raw SCFU")
            continue
        projected_hex = first_value(row, "RawPacketHex").upper()
        if projected_hex and projected_hex != record["rawHex"]:
            issues.append(f"SCFU row IN:{sequence} raw bytes disagree")
            continue
        identity = parse_identity(first_value(row, "Identity"))
        position = finite_position(
            (
                first_value(row, "PositionX"),
                first_value(row, "PositionY"),
                first_value(row, "PositionZ"),
            )
        )
        if identity is None or position is None:
            continue
        events.append(
            Event(packet_order, sequence, identity, "SimpleCharFullUpdate", position)
        )
    if rows and not events:
        issues.append("SCFU projection produced no identity-linked positioned events")
    if projection:
        issues.append("SCFU projection source: " + projection)
    return events, issues


def movement_events(
    records: list[dict[str, Any]], order: dict[tuple[str, int], int]
) -> tuple[list[Event], list[str]]:
    events = []
    issues = []
    for record in records:
        raw = bytes.fromhex(record["rawHex"])
        if len(raw) < 29:
            continue
        message_type = struct.unpack_from(">I", raw, 16)[0]
        if message_type not in {
            CHAR_DC_MOVE,
            SET_POS,
            FOLLOW_TARGET,
            STOP_MOVING_CMD,
        }:
            continue
        key = (record["direction"], record["sequence"])
        packet_order = order.get(key)
        if packet_order is None:
            issues.append(
                f"{record['direction']}:{record['sequence']} movement order unavailable"
            )
            continue
        identity_type, identity = read_identity(raw, 20)
        if identity_type != SIMPLE_CHAR or identity == 0:
            continue
        try:
            if message_type == CHAR_DC_MOVE:
                if len(raw) < 58:
                    raise ValueError("CharDCMove shorter than coordinate fields")
                move_type = raw[29]
                events.append(
                    Event(
                        packet_order,
                        record["sequence"],
                        identity,
                        "CharDCMove",
                        finite_position(read_vector3(raw, 46)),
                        move_type,
                    )
                )
            elif message_type == SET_POS:
                if len(raw) < 41:
                    raise ValueError("SetPos shorter than coordinate fields")
                events.append(
                    Event(
                        packet_order,
                        record["sequence"],
                        identity,
                        "SetPos",
                        finite_position(read_vector3(raw, 29)),
                    )
                )
            elif message_type == FOLLOW_TARGET:
                position = None
                if len(raw) >= 44 and raw[29] == 1 and raw[31] > 0:
                    position = finite_position(read_vector3(raw, 32))
                events.append(
                    Event(
                        packet_order,
                        record["sequence"],
                        identity,
                        "FollowTarget",
                        position,
                    )
                )
            else:
                events.append(
                    Event(
                        packet_order,
                        record["sequence"],
                        identity,
                        "StopMovingCmd",
                        None,
                    )
                )
        except (ValueError, struct.error) as exc:
            issues.append(
                f"{record['direction']}:{record['sequence']} movement decode: {exc}"
            )
    return events, issues


def state_before_hit(
    timeline: list[Event], hit_order: int
) -> tuple[str, tuple[float, float, float] | None, Event | None]:
    state = "unknown"
    position = None
    position_event = None
    for event in timeline:
        if event.order >= hit_order:
            break
        if event.kind == "SimpleCharFullUpdate":
            position = event.position
            position_event = event
            if state == "moving":
                state = "unknown"
        elif event.kind == "SetPos":
            position = event.position
            position_event = event
        elif event.kind == "FollowTarget":
            position = event.position
            position_event = event if event.position is not None else position_event
            state = "moving"
        elif event.kind == "StopMovingCmd":
            if state == "moving":
                position = None
                position_event = None
            state = "stationary"
        elif event.kind == "CharDCMove":
            position = event.position
            position_event = event
            if event.move_type == FULL_STOP_MOVE_TYPE:
                state = "stationary"
            elif event.move_type in TRANSLATION_START_MOVE_TYPES:
                state = "moving"
            elif event.move_type in TRANSLATION_STOP_MOVE_TYPES:
                state = "unknown"
    return state, position, position_event


def post_hit_bracket(
    timeline: list[Event],
    hit_order: int,
    at_hit_position: tuple[float, float, float],
) -> tuple[Event | None, str | None]:
    for event in timeline:
        if event.order <= hit_order:
            continue
        if event.kind == "FollowTarget":
            return None, "movement FollowTarget occurred before a post-hit position bracket"
        if event.kind == "CharDCMove":
            if event.move_type in TRANSLATION_START_MOVE_TYPES:
                return None, "translation-start CharDCMove occurred before a post-hit bracket"
            if event.move_type in TRANSLATION_STOP_MOVE_TYPES:
                return None, "partial-stop CharDCMove leaves translation state ambiguous"
            if event.position is not None:
                if same_position(at_hit_position, event.position):
                    return event, None
                return None, "post-hit CharDCMove position differs from the at-hit position"
        elif event.kind in {"SetPos", "SimpleCharFullUpdate"}:
            if event.position is not None:
                if same_position(at_hit_position, event.position):
                    return event, None
                return None, f"post-hit {event.kind} position differs from the at-hit position"
        # StopMovingCmd has no position and therefore cannot close the bracket.
    return None, "no post-hit exact position brackets the landed hit"


def audit_identity(
    identity: int,
    hit_order: int,
    timelines: dict[int, list[Event]],
    role: str,
) -> tuple[tuple[float, float, float] | None, dict[str, Any], list[str]]:
    timeline = timelines.get(identity, [])
    state, position, before = state_before_hit(timeline, hit_order)
    reasons = []
    detail: dict[str, Any] = {
        "identity": identity_hex(identity),
        "stateAtHit": state,
        "preHitPositionEvent": None,
        "postHitPositionEvent": None,
    }
    if before is not None:
        detail["preHitPositionEvent"] = {
            "kind": before.kind,
            "sequence": before.sequence,
            "order": before.order,
            "position": list(before.position) if before.position is not None else None,
        }
    if position is None:
        reasons.append(f"{role}: no exact position is established at the hit")
    if state != "stationary":
        reasons.append(f"{role}: movement state at the hit is {state}, not FullStop")
    if reasons:
        return None, detail, reasons
    after, bracket_problem = post_hit_bracket(timeline, hit_order, position)
    if bracket_problem is not None:
        reasons.append(f"{role}: {bracket_problem}")
        return None, detail, reasons
    detail["postHitPositionEvent"] = {
        "kind": after.kind,
        "sequence": after.sequence,
        "order": after.order,
        "position": list(after.position) if after.position is not None else None,
    }
    return position, detail, reasons


def same_position_bracket(
    identity: int,
    hit_order: int,
    timelines: dict[int, list[Event]],
) -> tuple[tuple[float, float, float] | None, str | None]:
    """Return a weaker same-coordinate bracket, without claiming FullStop."""

    timeline = timelines.get(identity, [])
    before_candidates = [
        event
        for event in timeline
        if event.order < hit_order
        and event.position is not None
        and event.kind != "FollowTarget"
    ]
    after_candidates = [
        event
        for event in timeline
        if event.order > hit_order
        and event.position is not None
        and event.kind != "FollowTarget"
    ]
    if not before_candidates:
        return None, "no positioned event before the hit"
    if not after_candidates:
        return None, "no positioned event after the hit"
    before = max(before_candidates, key=lambda event: event.order)
    after = min(after_candidates, key=lambda event: event.order)
    if not same_position(before.position, after.position):
        return None, "nearest bracketing positions differ"
    for event in timeline:
        if event.order <= before.order or event.order >= after.order:
            continue
        if event.kind == "FollowTarget":
            return None, "FollowTarget occurs between the position brackets"
        if event.kind == "CharDCMove":
            if event.move_type in TRANSLATION_START_MOVE_TYPES:
                return None, "translation-start CharDCMove occurs between brackets"
            if event.position is not None and not same_position(
                before.position, event.position
            ):
                return None, "CharDCMove changes position between brackets"
        elif event.kind in {"SetPos", "SimpleCharFullUpdate"}:
            if event.position is not None and not same_position(
                before.position, event.position
            ):
                return None, f"{event.kind} changes position between brackets"
    return before.position, None


def selected_capture_hits(
    capture_key: str,
    sequences: set[int],
    packet_to_stream: dict[str, StreamRef],
) -> list[dict[str, Any]]:
    selected = []
    for sequence in sorted(sequences):
        packet_id = next(
            value
            for value in packet_to_stream
            if packet_id_parts(value) == (capture_key, sequence)
        )
        stream = packet_to_stream[packet_id]
        selected.append(
            {
                "packetId": packet_id,
                "sequence": sequence,
                "profileKey": stream.profile_key,
                "semanticProfileId": stream.semantic_profile_id,
                "streamSignatureId": stream.stream_signature_id,
            }
        )
    return selected


def audit_capture(
    capture_key: str,
    selected_hits: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], dict[str, Any] | None]:
    capture = (REPO_ROOT / Path(capture_key)).resolve(strict=True)
    try:
        capture.relative_to(REPO_ROOT)
    except ValueError as error:
        raise RuntimeError(f"capture is outside the repository: {capture}") from error

    records, source_summary = load_packet_records(capture)
    canonical = {(row["direction"], row["sequence"]): row for row in records}
    if not source_summary["canonicalValid"]:
        return (
            [
                {
                    "packetId": selected["packetId"],
                    "capture": capture_key,
                    "sequence": selected["sequence"],
                    "exactStationaryDistance": False,
                    "reasons": ["capture canonical raw reconciliation is invalid"],
                }
                for selected in selected_hits
            ],
            None,
        )

    order_mode, order, order_missing = session_order(capture, records)
    movement, movement_issues = movement_events(records, order)
    scfu, scfu_issues = scfu_events(capture, canonical, order)
    timelines: dict[int, list[Event]] = defaultdict(list)
    for event in movement + scfu:
        timelines[event.identity].append(event)
    for values in timelines.values():
        values.sort(key=lambda event: (event.order, event.sequence, event.kind))
    session_audit = {
        "capture": capture_key,
        "orderMode": order_mode,
        "canonicalPacketCount": len(records),
        "orderMissingCanonicalPacketCount": len(order_missing),
        "positionedScfuEventCount": len(scfu),
        "rawMovementEventCount": len(movement),
        "movementIssues": movement_issues,
        "scfuNotes": scfu_issues,
    }

    hit_results = []
    for selected in selected_hits:
        packet_id = selected["packetId"]
        sequence = selected["sequence"]
        reasons = []
        record = canonical.get(("IN", sequence))
        hit_order = order.get(("IN", sequence))
        decoded = None
        if record is None:
            reasons.append("AttackInfo is absent from the canonical raw stream")
        elif hit_order is None:
            reasons.append(
                "AttackInfo has neither GlobalOrdinal nor physical packet-log order"
            )
        else:
            raw = bytes.fromhex(record["rawHex"])
            if len(raw) < 20 or struct.unpack_from(">I", raw, 16)[0] != ATTACK_INFO:
                reasons.append("packet id does not point to a canonical raw AttackInfo")
            else:
                try:
                    decoded = decode_attack_info(raw[16:])
                except Exception as exc:  # fail closed and retain exact reason
                    reasons.append(
                        f"raw AttackInfo decode failed: {type(exc).__name__}: {exc}"
                    )
        result: dict[str, Any] = {
            "packetId": packet_id,
            "capture": capture_key,
            "sequence": sequence,
            "profileKey": selected["profileKey"],
            "semanticProfileId": selected["semanticProfileId"],
            "streamSignatureId": selected["streamSignatureId"],
            "exactStationaryDistance": False,
            "reasons": reasons,
        }
        if decoded is not None and hit_order is not None:
            source = decoded["source"]["instance"]
            target = decoded["target"]["instance"]
            result.update(
                {
                    "orderMode": order_mode,
                    "order": hit_order,
                    "sourceIdentity": identity_hex(source),
                    "targetIdentity": identity_hex(target),
                    "amount": decoded["amount"],
                    "ammo": decoded["ammo"],
                    "weaponSlot": decoded["weaponSlot"],
                    "damageTypeWire": decoded["damageTypeWire"],
                    "hitTypeWire": decoded["hitTypeWire"],
                    "weaponInstance": decoded["weaponInstance"],
                }
            )
            source_position, source_detail, source_reasons = audit_identity(
                source, hit_order, timelines, "source"
            )
            target_position, target_detail, target_reasons = audit_identity(
                target, hit_order, timelines, "target"
            )
            result["sourcePositionAudit"] = source_detail
            result["targetPositionAudit"] = target_detail
            result["reasons"].extend(source_reasons + target_reasons)
            source_bracket, source_bracket_problem = same_position_bracket(
                source, hit_order, timelines
            )
            target_bracket, target_bracket_problem = same_position_bracket(
                target, hit_order, timelines
            )
            result["samePositionBracketAudit"] = {
                "sourceProblem": source_bracket_problem,
                "targetProblem": target_bracket_problem,
                "bothIdentitiesBracketed": (
                    source_bracket is not None and target_bracket is not None
                ),
            }
            if source_bracket is not None and target_bracket is not None:
                result["samePositionBracketDistance3d"] = distance_3d(
                    source_bracket, target_bracket
                )
                result["samePositionBracketDistanceXZ"] = distance_xz(
                    source_bracket, target_bracket
                )
            if source_position is not None and target_position is not None:
                result["exactStationaryDistance"] = True
                result["sourcePosition"] = list(source_position)
                result["targetPosition"] = list(target_position)
                result["distance3d"] = distance_3d(source_position, target_position)
                result["distanceXZ"] = distance_xz(source_position, target_position)
        hit_results.append(result)
    return hit_results, session_audit


def _selected_hit_payload(selected: dict[str, Any]) -> dict[str, Any]:
    return {
        "packetId": selected["packetId"],
        "sequence": selected["sequence"],
        "profileKey": selected["profileKey"],
        "semanticProfileId": selected["semanticProfileId"],
        "streamSignatureId": selected["streamSignatureId"],
    }


def _selected_hit_from_payload(value: Any) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise RuntimeError("capture worker selected hit is not an object")
    expected = {
        "packetId": str,
        "sequence": int,
        "profileKey": str,
        "semanticProfileId": str,
        "streamSignatureId": str,
    }
    for key, value_type in expected.items():
        if not isinstance(value.get(key), value_type):
            raise RuntimeError(f"capture worker selected hit has invalid {key}")
    return {key: value[key] for key in expected}


def _temporary_worker_path(path: Path, must_exist: bool) -> Path:
    resolved = path.resolve(strict=must_exist)
    temporary_root = Path(tempfile.gettempdir()).resolve()
    try:
        resolved.relative_to(temporary_root)
    except ValueError as error:
        raise RuntimeError(f"capture worker files must stay under {temporary_root}") from error
    return resolved


def _write_audit_capture_worker_shard(request: Path, shard: Path) -> None:
    request = _temporary_worker_path(request, must_exist=True)
    shard = _temporary_worker_path(shard, must_exist=False)
    if not shard.parent.is_dir():
        raise RuntimeError(f"capture worker shard directory is missing: {shard.parent}")
    if shard == DEFAULT_OUTPUT.resolve():
        raise RuntimeError("capture worker cannot write the production audit output")
    try:
        payload = json.loads(request.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise RuntimeError(f"invalid capture worker request JSON: {error}") from error
    if not isinstance(payload, dict) or not isinstance(payload.get("capture"), str):
        raise RuntimeError("capture worker request is missing its capture")
    if not isinstance(payload.get("selectedHits"), list):
        raise RuntimeError("capture worker request is missing selectedHits")
    selected_hits = [
        _selected_hit_from_payload(value) for value in payload["selectedHits"]
    ]
    hit_results, session_audit = audit_capture(payload["capture"], selected_hits)
    result = {
        "hitResults": hit_results,
        "sessionAudit": session_audit,
    }
    with shard.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(
            result,
            handle,
            ensure_ascii=True,
            separators=(",", ":"),
        )


def _is_native_child_failure(return_code: int) -> bool:
    if return_code < 0:
        return True
    normalized = return_code & 0xFFFFFFFF
    return 0xC0000000 <= normalized <= 0xCFFFFFFF


def _capture_worker_failure_detail(completed: subprocess.CompletedProcess[str]) -> str:
    detail = (completed.stderr or completed.stdout or "").strip()
    if len(detail) > 2000:
        detail = detail[-2000:]
    return detail


def audit_capture_isolated(
    capture_key: str,
    selected_hits: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], dict[str, Any] | None]:
    with tempfile.TemporaryDirectory(
        prefix="aorebirth-npc-attack-range-worker-"
    ) as staging_name:
        staging = Path(staging_name)
        request = staging / "capture-request.json"
        shard = staging / "capture-result.json"
        request_payload = {
            "capture": capture_key,
            "selectedHits": [
                _selected_hit_payload(selected) for selected in selected_hits
            ],
        }
        request.write_text(
            json.dumps(
                request_payload,
                ensure_ascii=True,
                separators=(",", ":"),
                sort_keys=True,
            ),
            encoding="utf-8",
        )
        command = [
            sys.executable,
            "-I",
            "-X",
            "faulthandler",
            str(Path(__file__).resolve()),
            "--_audit-capture-worker-request",
            str(request),
            "--_audit-capture-worker-shard",
            str(shard),
        ]
        for attempt in range(1, CAPTURE_WORKER_MAX_ATTEMPTS + 1):
            shard.unlink(missing_ok=True)
            completed = subprocess.run(
                command,
                cwd=REPO_ROOT,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                check=False,
            )
            if completed.returncode == 0:
                try:
                    if not shard.is_file():
                        raise RuntimeError(
                            "capture worker succeeded without writing its shard"
                        )
                    result = json.loads(shard.read_text(encoding="utf-8"))
                    if not isinstance(result, dict) or not isinstance(
                        result.get("hitResults"), list
                    ):
                        raise RuntimeError("capture worker result has invalid hitResults")
                    hit_results = result["hitResults"]
                    expected_hits = [
                        (selected["packetId"], selected["sequence"])
                        for selected in selected_hits
                    ]
                    actual_hits = [
                        (value.get("packetId"), value.get("sequence"))
                        for value in hit_results
                        if isinstance(value, dict)
                    ]
                    if actual_hits != expected_hits:
                        raise RuntimeError(
                            "capture worker result does not match the requested hits"
                        )
                    session_audit = result.get("sessionAudit")
                    if session_audit is not None and not isinstance(
                        session_audit, dict
                    ):
                        raise RuntimeError("capture worker result has invalid sessionAudit")
                    if (
                        session_audit is not None
                        and session_audit.get("capture") != capture_key
                    ):
                        raise RuntimeError(
                            "capture worker session does not match the request"
                        )
                    return hit_results, session_audit
                except (OSError, json.JSONDecodeError, RuntimeError) as error:
                    if attempt < CAPTURE_WORKER_MAX_ATTEMPTS:
                        continue
                    raise RuntimeError(
                        "capture worker produced an invalid shard "
                        f"on attempt {attempt}/{CAPTURE_WORKER_MAX_ATTEMPTS}: "
                        f"{error}"
                    ) from error

            native_failure = _is_native_child_failure(completed.returncode)
            if attempt < CAPTURE_WORKER_MAX_ATTEMPTS:
                continue
            kind = "native capture worker" if native_failure else "capture worker"
            detail = _capture_worker_failure_detail(completed)
            suffix = f": {detail}" if detail else ""
            raise RuntimeError(
                f"{kind} failed with exit code {completed.returncode} "
                f"on attempt {attempt}/{CAPTURE_WORKER_MAX_ATTEMPTS}{suffix}"
            )
    raise AssertionError("capture worker retry loop exited unexpectedly")


def audit(
    inventory: Path,
    *,
    inventory_logical_path: str = DEFAULT_INVENTORY_LOGICAL_PATH,
) -> dict[str, Any]:
    before_stat = inventory.stat()
    inventory_hash = sha256_file(inventory)
    streams = collect_natural_special_streams(inventory)
    after_stat = inventory.stat()
    if (before_stat.st_size, before_stat.st_mtime_ns) != (
        after_stat.st_size,
        after_stat.st_mtime_ns,
    ):
        raise RuntimeError(f"{inventory}: inventory changed during the audit")

    packet_to_stream = {}
    captures: dict[str, set[int]] = defaultdict(set)
    for stream in streams:
        for packet_id in stream.attack_info_packet_ids:
            if packet_id in packet_to_stream:
                raise ValueError(f"AttackInfo packet appears in two streams: {packet_id}")
            packet_to_stream[packet_id] = stream
            capture, sequence = packet_id_parts(packet_id)
            captures[capture].add(sequence)

    hit_results = []
    session_audits = []
    for capture_key in sorted(captures):
        selected_hits = selected_capture_hits(
            capture_key, captures[capture_key], packet_to_stream
        )
        capture_hits, session_audit = audit_capture_isolated(
            capture_key, selected_hits
        )
        hit_results.extend(capture_hits)
        if session_audit is not None:
            session_audits.append(session_audit)

    hits_by_stream: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for hit in hit_results:
        if hit.get("semanticProfileId") and hit.get("streamSignatureId"):
            hits_by_stream[
                (hit["semanticProfileId"], hit["streamSignatureId"])
            ].append(hit)
    stream_results = []
    for stream in streams:
        hits = hits_by_stream[(stream.semantic_profile_id, stream.stream_signature_id)]
        exact = [row for row in hits if row["exactStationaryDistance"]]
        same_bracket = [
            row
            for row in hits
            if row.get("samePositionBracketAudit", {}).get("bothIdentitiesBracketed")
        ]
        reason_counts = Counter(
            reason for row in hits for reason in row.get("reasons", [])
        )
        template_range_evidence = stream.captured_attack_range_evidence
        template_proven_maximum = (
            isinstance(stream.captured_attack_range, (int, float))
            and stream.captured_attack_range > 0
            and isinstance(template_range_evidence, dict)
            and template_range_evidence.get("attackRangeMeters")
            == stream.captured_attack_range
            and template_range_evidence.get("statId") == 287
            and bool(template_range_evidence.get("templateEndpoints"))
        )
        stream_results.append(
            {
                "profileKey": stream.profile_key,
                "semanticProfileId": stream.semantic_profile_id,
                "streamSignatureId": stream.stream_signature_id,
                "signature": stream.signature,
                "landedHitCount": len(hits),
                "exactStationaryHitDistanceCount": len(exact),
                "samePositionBracketHitDistanceCountWithoutFullStopProof": len(
                    same_bracket
                ),
                "maximumExactStationaryLandedDistance3d": (
                    max(row["distance3d"] for row in exact) if exact else None
                ),
                "maximumExactStationaryLandedDistanceXZ": (
                    max(row["distanceXZ"] for row in exact) if exact else None
                ),
                "maximumSamePositionBracketDistance3dWithoutFullStopProof": (
                    max(row["samePositionBracketDistance3d"] for row in same_bracket)
                    if same_bracket
                    else None
                ),
                "maximumSamePositionBracketDistanceXZWithoutFullStopProof": (
                    max(row["samePositionBracketDistanceXZ"] for row in same_bracket)
                    if same_bracket
                    else None
                ),
                "attackRangeConclusion": (
                    "exact maximum from captured SAW template identity and immutable ItemDb Stat 287"
                    if template_proven_maximum
                    else "observed landed-hit distance lower bound only; maximum threshold unproven"
                    if exact
                    else "no exact stationary landed-hit distance; maximum threshold unproven"
                ),
                "templateProvenMaximumAttackRangeMeters": (
                    stream.captured_attack_range
                    if template_proven_maximum
                    else None
                ),
                "templateAttackRangeEvidence": (
                    template_range_evidence if template_proven_maximum else None
                ),
                "maximumThresholdMissingEvidence": (
                    []
                    if template_proven_maximum
                    else [
                        "no captured attack template linked to an immutable item/stat maximum",
                        "no controlled stationary boundary pair proves acceptance at one distance and rejection beyond it",
                    ]
                ),
                "hitFailureReasons": [
                    {"reason": reason, "count": count}
                    for reason, count in sorted(reason_counts.items())
                ],
            }
        )

    exact_hits = [row for row in hit_results if row["exactStationaryDistance"]]
    same_position_bracket_hits = [
        row
        for row in hit_results
        if row.get("samePositionBracketAudit", {}).get("bothIdentitiesBracketed")
    ]
    same_position_bracket_profiles = {
        row["profileKey"] for row in same_position_bracket_hits
    }
    same_position_bracket_variants = {
        row["semanticProfileId"] for row in same_position_bracket_hits
    }
    same_position_bracket_streams = {
        (row["semanticProfileId"], row["streamSignatureId"])
        for row in same_position_bracket_hits
    }
    exact_streams = {
        (row["semanticProfileId"], row["streamSignatureId"])
        for row in exact_hits
    }
    exact_variants = {row["semanticProfileId"] for row in exact_hits}
    exact_profiles = {row["profileKey"] for row in exact_hits}
    hit_failure_reasons = Counter(
        reason for row in hit_results for reason in row.get("reasons", [])
    )
    stream_failure_reasons = Counter()
    for stream in stream_results:
        for reason_row in stream["hitFailureReasons"]:
            stream_failure_reasons[reason_row["reason"]] += 1
    source_states = Counter(
        row.get("sourcePositionAudit", {}).get("stateAtHit", "unavailable")
        for row in hit_results
    )
    target_states = Counter(
        row.get("targetPositionAudit", {}).get("stateAtHit", "unavailable")
        for row in hit_results
    )
    session_order_modes = Counter(row["orderMode"] for row in session_audits)
    weak_stream_summaries = [
        {
            "profileKey": row["profileKey"],
            "semanticProfileId": row["semanticProfileId"],
            "streamSignatureId": row["streamSignatureId"],
            "hitCount": row["samePositionBracketHitDistanceCountWithoutFullStopProof"],
            "maximumDistance3d": row[
                "maximumSamePositionBracketDistance3dWithoutFullStopProof"
            ],
            "maximumDistanceXZ": row[
                "maximumSamePositionBracketDistanceXZWithoutFullStopProof"
            ],
        }
        for row in stream_results
        if row["samePositionBracketHitDistanceCountWithoutFullStopProof"]
    ]
    canonical_packets_without_order = sum(
        row["orderMissingCanonicalPacketCount"] for row in session_audits
    )
    movement_issue_count = sum(
        len(row["movementIssues"]) for row in session_audits
    )
    scfu_issue_count = sum(
        sum(
            1
            for note in row["scfuNotes"]
            if not note.startswith("SCFU projection source:")
        )
        for row in session_audits
    )
    template_proven_streams = [
        row
        for row in stream_results
        if row["templateProvenMaximumAttackRangeMeters"] is not None
    ]
    template_proven_variants = {
        row["semanticProfileId"] for row in template_proven_streams
    }
    template_proven_profiles = {
        row["profileKey"] for row in template_proven_streams
    }
    return {
        "schemaVersion": 2,
        "inventory": inventory_logical_path,
        "inventorySha256": inventory_hash,
        "criteria": {
            "positionTolerance": FLOAT_EPSILON,
            "stationaryProof": "CharDCMove MoveType 21 FullStop before hit",
            "postHitBracket": "same exact raw-derived position before any translation movement",
            "rangeMeaning": "landed distances are lower bounds, never inferred maxima",
            "exactTemplateAuthority": (
                "captured SpecialAttackWeapon template endpoints joined to "
                "immutable ItemDb Stat 287"
            ),
        },
        "summary": {
            "captureCertifiedNaturalSpecialProfileCount": len(
                {stream.profile_key for stream in streams}
            ),
            "captureCertifiedNaturalSpecialVariantCount": len(
                {stream.semantic_profile_id for stream in streams}
            ),
            "captureCertifiedNaturalSpecialStreamCount": len(streams),
            "captureSessionCount": len(captures),
            "landedHitCount": len(hit_results),
            "exactStationaryHitDistanceCount": len(exact_hits),
            "samePositionBracketHitDistanceCountWithoutFullStopProof": len(
                same_position_bracket_hits
            ),
            "profilesWithSamePositionBracketWithoutFullStopProof": len(
                same_position_bracket_profiles
            ),
            "variantsWithSamePositionBracketWithoutFullStopProof": len(
                same_position_bracket_variants
            ),
            "streamsWithSamePositionBracketWithoutFullStopProof": len(
                same_position_bracket_streams
            ),
            "samePositionBracketStreamsWithoutFullStopProof": (
                weak_stream_summaries
            ),
            "profilesWithExactStationaryHitDistance": len(exact_profiles),
            "variantsWithExactStationaryHitDistance": len(exact_variants),
            "streamsWithExactStationaryHitDistance": len(exact_streams),
            "profilesWithTemplateProvenMaximumAttackThreshold": len(
                template_proven_profiles
            ),
            "variantsWithTemplateProvenMaximumAttackThreshold": len(
                template_proven_variants
            ),
            "streamsWithProvenMaximumAttackThreshold": len(
                template_proven_streams
            ),
            "canonicalPacketsWithoutSelectedOrderCount": (
                canonical_packets_without_order
            ),
            "rawMovementDecodeIssueCount": movement_issue_count,
            "scfuProjectionIssueCount": scfu_issue_count,
            "sessionOrderModeCounts": dict(sorted(session_order_modes.items())),
            "sourceMovementStateAtHitCounts": dict(sorted(source_states.items())),
            "targetMovementStateAtHitCounts": dict(sorted(target_states.items())),
            "hitFailureReasonCounts": dict(sorted(hit_failure_reasons.items())),
            "streamsAffectedByFailureReasonCounts": dict(
                sorted(stream_failure_reasons.items())
            ),
        },
        "sessions": session_audits,
        "streams": stream_results,
        "hits": hit_results,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=Path, default=DEFAULT_INVENTORY)
    parser.add_argument(
        "--inventory-logical-path",
        default=DEFAULT_INVENTORY_LOGICAL_PATH,
        help="stable repository-relative identity rendered for the physical inventory input",
    )
    parser.add_argument("--output", type=Path)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    mode.add_argument(
        "--_audit-capture-worker-request", type=Path, help=argparse.SUPPRESS
    )
    parser.add_argument(
        "--_audit-capture-worker-shard", type=Path, help=argparse.SUPPRESS
    )
    parser.add_argument("--summary-only", action="store_true")
    args = parser.parse_args()
    if (args._audit_capture_worker_request is None) != (
        args._audit_capture_worker_shard is None
    ):
        parser.error("private capture worker mode requires request and shard arguments")
    if args._audit_capture_worker_request is not None:
        _write_audit_capture_worker_shard(
            args._audit_capture_worker_request,
            args._audit_capture_worker_shard,
        )
        return 0
    inventory_logical_path = PurePosixPath(args.inventory_logical_path)
    if (
        inventory_logical_path.is_absolute()
        or ".." in inventory_logical_path.parts
        or inventory_logical_path.as_posix() != args.inventory_logical_path
    ):
        parser.error("--inventory-logical-path must be a normalized relative POSIX path")
    inventory = args.inventory.resolve()
    report = audit(
        inventory,
        inventory_logical_path=inventory_logical_path.as_posix(),
    )
    summary = report["summary"]
    hard_issue_fields = (
        "canonicalPacketsWithoutSelectedOrderCount",
        "rawMovementDecodeIssueCount",
        "scfuProjectionIssueCount",
    )
    hard_issues = {
        field: summary[field]
        for field in hard_issue_fields
        if summary[field] != 0
    }
    if hard_issues:
        print(
            "ERROR: attack-range audit has raw ordering or decode issues: "
            + json.dumps(hard_issues, sort_keys=True),
            file=sys.stderr,
        )
        return 1
    rendered = json.dumps(report, indent=2, ensure_ascii=True) + "\n"
    output = (args.output or DEFAULT_OUTPUT).resolve()
    if args.write or (args.output and not args.check):
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered, encoding="utf-8")
    elif args.check:
        if not output.exists():
            print(f"ERROR: attack-range audit is missing: {output}", file=sys.stderr)
            return 1
        if output.read_text(encoding="utf-8") != rendered:
            print(f"ERROR: attack-range audit is stale: {output}", file=sys.stderr)
            return 1
    payload = report["summary"] if args.summary_only else report
    print(json.dumps(payload, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
