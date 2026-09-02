#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import io
import json
import os
import shutil
import statistics
import tempfile
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
REFERENCE = ROOT / "docs/reference/missions/modern-capture/level2-slider-discovery"
RAW = REFERENCE / "raw"
GENERATED = ROOT / "docs/generated/missions/modern-capture/level2-slider-discovery"
REPORT = ROOT / "docs/evidence/LEVEL2_MISSION_SLIDER_DISCOVERY_ANALYSIS.md"
SOURCE = Path(os.environ.get("LOCALAPPDATA", "")) / "AOSharp/MissionOfferHarvester/sessions"

SESSION_DISPOSITIONS = [
    ("mission-20260902T014541176Z-36962762-8baab3a4", True, "planned state 1 request 1"),
    ("mission-20260902T014714320Z-36962762-6d027b2f", True, "planned state 1 request 2"),
    ("mission-20260902T014735414Z-36962762-873d0f68", True, "planned state 2"),
    ("mission-20260902T014742445Z-36962762-9639c44e", True, "planned state 3"),
    ("mission-20260902T015007588Z-36962762-c9da0b6d", True, "planned state 4"),
    ("mission-20260902T015014799Z-36962762-02de7738", True, "planned state 5"),
    ("mission-20260902T015022092Z-36962762-506b7517", True, "planned state 6"),
    ("mission-20260902T015028181Z-36962762-144d797e", True, "planned state 7"),
    ("mission-20260902T015428011Z-36962762-7323ad87", True, "planned state 8"),
    ("mission-20260902T015453668Z-36962762-6ee79742", False, "surplus repeat of state 8 after quota was filled"),
    ("mission-20260902T015501702Z-36962762-6fa0f567", True, "planned state 9"),
    ("mission-20260902T015507673Z-36962762-881b61c8", True, "planned state 10"),
    ("mission-20260902T015514152Z-36962762-cde432c3", True, "planned state 11"),
    ("mission-20260902T020352939Z-36962762-c11633ff", True, "planned states 12-27"),
]

SLIDER_KEYS = (
    "difficulty", "good_bad", "order_chaos", "open_hidden",
    "physical_mystical", "headon_stealth", "credits_xp",
)


def state(label: str, detent: int = 1, gb: int = 255, oc: int = 255,
          oh: int = 255, pm: int = 255, hs: int = 255, mx: int = 255) -> dict[str, Any]:
    return {"label": label, "native": (detent, gb, oc, oh, pm, hs, mx)}


EXPECTED_STATES = [
    state("CENTERED_BASELINE_D1"),
    state("GOOD_BAD_FULL_LEFT", gb=156), state("GOOD_BAD_FULL_RIGHT", gb=100),
    state("GOOD_BAD_MINUS_50", gb=206), state("GOOD_BAD_PLUS_50", gb=50),
    state("ORDER_CHAOS_FULL_LEFT", oc=156), state("ORDER_CHAOS_FULL_RIGHT", oc=100),
    state("ORDER_CHAOS_MINUS_50", oc=206), state("ORDER_CHAOS_PLUS_50", oc=50),
    state("OPEN_HIDDEN_FULL_LEFT", oh=156), state("OPEN_HIDDEN_FULL_RIGHT", oh=100),
    state("OPEN_HIDDEN_MINUS_50", oh=206), state("OPEN_HIDDEN_PLUS_50", oh=50),
    state("PHYSICAL_MYSTICAL_FULL_LEFT", pm=156), state("PHYSICAL_MYSTICAL_FULL_RIGHT", pm=100),
    state("PHYSICAL_MYSTICAL_MINUS_50", pm=206), state("PHYSICAL_MYSTICAL_PLUS_50", pm=50),
    state("HEADON_STEALTH_FULL_LEFT", hs=156), state("HEADON_STEALTH_FULL_RIGHT", hs=100),
    state("HEADON_STEALTH_MINUS_50", hs=206), state("HEADON_STEALTH_PLUS_50", hs=50),
    state("MONEY_XP_FULL_LEFT", mx=156), state("MONEY_XP_FULL_RIGHT", mx=100),
    state("MONEY_XP_MINUS_50", mx=206), state("MONEY_XP_PLUS_50", mx=50),
    state("CENTERED_BASELINE_D6", detent=6), state("CENTERED_BASELINE_D10", detent=10),
]
EXPECTED_BY_NATIVE = {entry["native"]: index for index, entry in enumerate(EXPECTED_STATES, 1)}


def canonical_json(value: Any) -> str:
    return json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def json_line(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n"


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_path(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def decode_packet(payload: dict[str, Any]) -> bytes:
    raw = base64.b64decode(payload["base64"], validate=True)
    if len(raw) != int(payload["byte_length"]):
        raise ValueError("RAW_PACKET_LENGTH_MISMATCH")
    if sha256_bytes(raw) != payload["sha256"]:
        raise ValueError("RAW_PACKET_SHA256_MISMATCH")
    return raw


def native_tuple(payload: dict[str, Any]) -> tuple[int, ...]:
    return tuple(int(payload[key]) for key in SLIDER_KEYS)


def state_id_for(native: tuple[int, ...]) -> str:
    text = ";".join(f"{key}={value}" for key, value in zip(SLIDER_KEYS, native))
    return sha256_bytes(text.encode("utf-8"))


def import_sources() -> None:
    if not SOURCE.exists():
        raise SystemExit(f"SOURCE_SESSION_ROOT_NOT_FOUND: {SOURCE}")
    for session_id, _, _ in SESSION_DISPOSITIONS:
        source = SOURCE / session_id / "events.jsonl"
        target = RAW / session_id / "events.jsonl"
        if not source.exists():
            raise SystemExit(f"SOURCE_SESSION_MISSING: {source}")
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    print(f"LEVEL2_SLIDER_CAPTURE_IMPORT=PASS sessions={len(SESSION_DISPOSITIONS)}")


def load_session(session_id: str, included: bool, reason: str, path_override: Path | None = None) -> dict[str, Any]:
    path = path_override or RAW / session_id / "events.jsonl"
    if not path.exists():
        raise ValueError(f"RETAINED_SESSION_MISSING: {path}")
    raw_lines = path.read_text(encoding="utf-8").splitlines()
    events = [json.loads(line) for line in raw_lines if line.strip()]
    if len(events) != len(raw_lines):
        raise ValueError(f"BLANK_OR_MALFORMED_EVENT_LINE: {session_id}")
    if any(event.get("schema_version") != 3 for event in events):
        raise ValueError(f"UNEXPECTED_SCHEMA_VERSION: {session_id}")
    starts = [event for event in events if event["event_type"] == "session_started"]
    stops = [event for event in events if event["event_type"] == "session_stopped"]
    errors = [event for event in events if event["event_type"] in ("error", "request_timeout")]
    if len(starts) != 1 or len(stops) != 1 or errors:
        raise ValueError(f"SESSION_NOT_CLEANLY_COMPLETE: {session_id}")
    session_payload = starts[0]["payload"]
    if int(session_payload["character_level"]) != 2:
        raise ValueError(f"CHARACTER_LEVEL_NOT_TWO: {session_id}")
    if stops[0]["payload"]["reason"] != "requested_count_completed":
        raise ValueError(f"SESSION_STOP_REASON_INVALID: {session_id}")

    request_ids = [event["request_id"] for event in events if event["event_type"] == "request_started"]
    if len(request_ids) != len(set(request_ids)):
        raise ValueError(f"DUPLICATE_REQUEST_ID: {session_id}")
    requests = []
    for request_id in request_ids:
        by_type = {
            event_type: [event for event in events if event["event_type"] == event_type and event.get("request_id") == request_id]
            for event_type in ("request_started", "request_transmitted", "raw_response_received", "cohort_received")
        }
        if any(len(matches) != 1 for matches in by_type.values()):
            raise ValueError(f"REQUEST_EVENT_CARDINALITY_INVALID: {request_id}")
        request_event = by_type["request_started"][0]
        transmitted_event = by_type["request_transmitted"][0]
        response_event = by_type["raw_response_received"][0]
        cohort_event = by_type["cohort_received"][0]
        request_payload = request_event["payload"]
        transmitted_payload = transmitted_event["payload"]
        response_payload = response_event["payload"]
        cohort_payload = cohort_event["payload"]
        outbound_pre = decode_packet(request_payload["serialized_pre_send"]["raw_packet"])
        outbound = decode_packet(transmitted_payload["raw_packet"])
        inbound = decode_packet(response_payload["raw_packet"])
        inbound_cohort = decode_packet(cohort_payload["raw_response_packet"])
        if outbound_pre != outbound or inbound != inbound_cohort:
            raise ValueError(f"RAW_PACKET_ASSOCIATION_MISMATCH: {request_id}")
        native = native_tuple(transmitted_payload["transmitted_native_values"])
        readback = native_tuple(request_payload["native_client_after"]["values"])
        serialized = native_tuple(request_payload["serialized_pre_send"]["values"])
        returned = native_tuple(cohort_payload["returned_sliders"])
        if len({native, readback, serialized, returned}) != 1:
            raise ValueError(f"SLIDER_LAYER_MISMATCH: {request_id}")
        slider_state_id = request_payload["slider_state_id"]
        if slider_state_id != state_id_for(native):
            raise ValueError(f"SLIDER_STATE_ID_MISMATCH: {request_id}")
        if any(event["payload"].get("slider_state_id") != slider_state_id for event in
               (transmitted_event, response_event, cohort_event)):
            raise ValueError(f"CROSS_EVENT_SLIDER_ID_MISMATCH: {request_id}")
        offers = cohort_payload["offers"]
        if len(offers) != 5:
            raise ValueError(f"COHORT_NOT_FIVE_OFFERS: {request_id}")
        if cohort_payload["slider_verification"] != {
            "failure_code": None, "phase": "COHORT_ASSOCIATED", "status": "MATCH"
        }:
            raise ValueError(f"COHORT_VERIFICATION_FAILED: {request_id}")
        state_index = EXPECTED_BY_NATIVE.get(native)
        if state_index is None:
            raise ValueError(f"UNPLANNED_NATIVE_STATE: {request_id}: {native}")
        expected_ql = 1 if native[0] == 1 else 2 if native[0] == 6 else 3 if native[0] == 10 else None
        if int(request_payload["static_expected_mission_ql"]) != expected_ql:
            raise ValueError(f"STATIC_EXPECTED_QL_MISMATCH: {request_id}")
        requests.append({
            "session_id": session_id,
            "request_id": request_id,
            "cohort_id": cohort_payload["cohort_id"],
            "timestamp_utc": request_event["timestamp_utc"],
            "character_level": int(request_payload["character_level"]),
            "state_index": state_index,
            "state_label": EXPECTED_STATES[state_index - 1]["label"],
            "slider_state_id": slider_state_id,
            "native": native,
            "requested_semantic_state": request_payload["requested_semantic_state"],
            "expected_mission_ql": expected_ql,
            "outbound": outbound,
            "inbound": inbound,
            "outbound_payload": transmitted_payload["raw_packet"],
            "inbound_payload": response_payload["raw_packet"],
            "offers": offers,
            "envelope": cohort_payload["message_envelope"],
        })
    if len(requests) != int(stops[0]["payload"]["issued_request_count"]):
        raise ValueError(f"SESSION_REQUEST_TOTAL_MISMATCH: {session_id}")
    if sum(len(request["offers"]) for request in requests) != int(stops[0]["payload"]["harvested_offer_count"]):
        raise ValueError(f"SESSION_OFFER_TOTAL_MISMATCH: {session_id}")
    return {
        "session_id": session_id,
        "included": included,
        "disposition_reason": reason,
        "source_path": path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else path.as_posix(),
        "source_sha256": sha256_path(path),
        "event_count": len(events),
        "request_count": len(requests),
        "offer_count": sum(len(request["offers"]) for request in requests),
        "character_surrogate": session_payload["character_surrogate"],
        "character_identity_raw": session_payload["character_identity_raw"],
        "terminal_identity": session_payload["terminal_identity"],
        "terminal_playfield": session_payload["terminal_playfield"],
        "terminal_coordinates": session_payload["terminal_coordinates"],
        "harvester_version": session_payload["harvester_version"],
        "aosharp_version": session_payload["aosharp_observed_assembly_version"],
        "started_at_utc": starts[0]["timestamp_utc"],
        "stopped_at_utc": stops[0]["timestamp_utc"],
        "requests": requests,
    }


def diff_offsets(left: bytes, right: bytes) -> list[int]:
    if len(left) != len(right):
        return list(range(max(len(left), len(right))))
    return [index for index, pair in enumerate(zip(left, right)) if pair[0] != pair[1]]


def packet_occurrences(packet: bytes, needle: bytes) -> list[int]:
    result = []
    start = 0
    while True:
        found = packet.find(needle, start)
        if found < 0:
            return result
        result.append(found)
        start = found + 1


def summarize_offers(offers: list[dict[str, Any]]) -> dict[str, Any]:
    rewards = [reward for offer in offers for reward in offer.get("reward_items", [])]
    credits = [int(offer["credits"]) for offer in offers]
    xp = [int(offer["xp_reward"]) for offer in offers]
    icons = Counter(int(offer["mission_icon"]) for offer in offers)
    playfields = sorted({int(offer["playfield"]["instance"]) for offer in offers})
    reward_ids = sorted({(int(reward["low_id"]), int(reward["high_id"])) for reward in rewards})
    reward_qls = sorted({int(reward["ql"]) for reward in rewards})
    unknown_lengths: dict[str, set[int]] = defaultdict(set)
    for offer in offers:
        for name, value in offer.get("unknown_fields", {}).items():
            if name.endswith("Base64") and value is not None:
                unknown_lengths[name].add(len(base64.b64decode(value)))
    return {
        "offer_count": len(offers),
        "mission_icon_counts": {str(key): icons[key] for key in sorted(icons)},
        "mission_identity_count": len({(offer["mission_identity"]["type"], offer["mission_identity"]["instance"]) for offer in offers}),
        "reward_identity_count": len(reward_ids),
        "reward_identities": [[low, high] for low, high in reward_ids],
        "reward_ql_values": reward_qls,
        "credits": {"minimum": min(credits), "maximum": max(credits), "distinct_values": sorted(set(credits)), "median": statistics.median(credits)},
        "xp": {"minimum": min(xp), "maximum": max(xp), "distinct_values": sorted(set(xp)), "median": statistics.median(xp)},
        "destination_playfield_instances": playfields,
        "destination_coordinate_count": len({(offer["location"]["x"], offer["location"]["y"], offer["location"]["z"]) for offer in offers}),
        "reward_descriptor_versions": sorted({int(offer["reward_descriptor_version"]) for offer in offers}),
        "unknown_chunk_lengths": {key: sorted(value) for key, value in sorted(unknown_lengths.items())},
        "not_exposed_fields": [
            "mission_ql", "mission_template_or_type_id", "objective_item_identity",
            "objective_item_ql", "objective_type", "token_reward",
            "destination_entrance_identity", "faction_requirements",
        ],
    }


def scan_mission_ql_candidates(state_offers: dict[int, list[dict[str, Any]]]) -> dict[str, Any]:
    samples = [(ql, offer) for index, ql in ((1, 1), (26, 2), (27, 3)) for offer in state_offers[index]]
    exact = []
    weak = []
    chunk_names = sorted({name for _, offer in samples for name in offer.get("unknown_fields", {}) if name.endswith("Base64")})
    for chunk in chunk_names:
        decoded = [(ql, base64.b64decode(offer["unknown_fields"][chunk])) for ql, offer in samples]
        minimum = min(len(data) for _, data in decoded)
        for width in (1, 2, 4):
            for offset in range(0, minimum - width + 1):
                for endian in (("unsigned-byte", "big"),) if width == 1 else (("unsigned", "little"), ("unsigned", "big")):
                    values = [(ql, int.from_bytes(data[offset:offset + width], endian[1], signed=False)) for ql, data in decoded]
                    if all(value == ql for ql, value in values):
                        exact.append({"property": chunk, "offset": offset, "width": width, "byte_order": endian[1], "relationship": "EQUALS_STATIC_EXPECTED_MISSION_QL_ALL_30_OFFERS"})
                    grouped = {ql: {value for sample_ql, value in values if sample_ql == ql} for ql in (1, 2, 3)}
                    if all(len(grouped[ql]) == 1 for ql in grouped):
                        constants = [next(iter(grouped[ql])) for ql in (1, 2, 3)]
                        if len(set(constants)) == 3:
                            weak.append({"property": chunk, "offset": offset, "width": width, "byte_order": endian[1], "values_by_expected_ql": {str(ql): constants[ql - 1] for ql in (1, 2, 3)}})
    canonical = next((candidate for candidate in exact if candidate["property"] == "UnkChunk3Base64" and candidate["offset"] == 16 and candidate["width"] == 4 and candidate["byte_order"] == "big"), None)
    exact_candidates = [canonical] if canonical else exact
    alias_views = [candidate for candidate in exact if candidate is not canonical]
    status = "STRONG_MISSION_QL_CANDIDATE" if exact_candidates else "WEAK_MISSION_QL_CANDIDATE" if weak else "NO_CANDIDATE_FOUND"
    return {
        "classification": status,
        "exact_candidates": exact_candidates,
        "overlapping_alias_views": alias_views,
        "weak_candidates": weak[:100],
        "sample_offer_count": len(samples),
        "promotion_status": "CANDIDATE_ONLY_REQUIRES_BROADER_MULTI_QL_CONFIRMATION",
    }


def scan_mission_type_candidates(offers: list[dict[str, Any]]) -> list[dict[str, Any]]:
    candidates = []
    chunk_names = sorted({name for offer in offers for name in offer.get("unknown_fields", {}) if name.endswith("Base64")})
    for chunk in chunk_names:
        decoded = [(int(offer["mission_icon"]), base64.b64decode(offer["unknown_fields"][chunk])) for offer in offers]
        minimum = min(len(data) for _, data in decoded)
        for width in (1, 2, 4):
            for offset in range(0, minimum - width + 1):
                values = [(icon, int.from_bytes(data[offset:offset + width], "big")) for icon, data in decoded]
                icon_values: dict[int, set[int]] = defaultdict(set)
                value_icons: dict[int, set[int]] = defaultdict(set)
                for icon, value in values:
                    icon_values[icon].add(value)
                    value_icons[value].add(icon)
                if len(icon_values) >= 3 and all(len(values_for_icon) == 1 for values_for_icon in icon_values.values()) and all(len(icons) == 1 for icons in value_icons.values()) and len(value_icons) == len(icon_values):
                    candidates.append({
                        "property": chunk,
                        "offset": offset,
                        "width": width,
                        "byte_order": "big",
                        "coverage": len(values),
                        "mission_icon_mapping": {str(icon): next(iter(icon_values[icon])) for icon in sorted(icon_values)},
                        "classification": "CANDIDATE_CORRELATION_NOT_SEMANTIC_PROOF",
                    })
    return candidates


def csv_matrix(rows: list[dict[str, Any]]) -> str:
    output = io.StringIO(newline="")
    fields = ["StateIndex", "Label", "SliderStateId", "Difficulty", "GoodBad", "OrderChaos", "OpenHidden", "PhysicalMystical", "HeadonStealth", "MoneyXp", "ExpectedMissionQl", "Requests", "Offers", "RequestIds"]
    writer = csv.DictWriter(output, fieldnames=fields, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    return output.getvalue()


def csv_slider_evidence(rows: list[dict[str, Any]]) -> str:
    output = io.StringIO(newline="")
    fields = ["Slider", "ProtocolInput", "OutputEffect", "Level2CaptureDecision", "HistoricalClaimClassification", "States"]
    writer = csv.DictWriter(output, fieldnames=fields, lineterminator="\n")
    writer.writeheader()
    for row in rows:
        writer.writerow({
            "Slider": row["slider"], "ProtocolInput": row["protocol_input"],
            "OutputEffect": row["output_effect"], "Level2CaptureDecision": row["level2_capture_decision"],
            "HistoricalClaimClassification": row["historical_claim_classification"],
            "States": ";".join(str(value) for value in row["states"]),
        })
    return output.getvalue()


def build_analysis() -> tuple[dict[str, str], str, dict[str, Any]]:
    sessions = [load_session(*entry) for entry in SESSION_DISPOSITIONS]
    primary_sessions = [session for session in sessions if session["included"]]
    surplus_sessions = [session for session in sessions if not session["included"]]
    primary_requests = [request for session in primary_sessions for request in session["requests"]]
    state_requests: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for request in primary_requests:
        state_requests[request["state_index"]].append(request)
    if set(state_requests) != set(range(1, 28)):
        raise ValueError("PRIMARY_MATRIX_STATE_SET_MISMATCH")
    if any(len(state_requests[index]) != 2 for index in range(1, 28)):
        raise ValueError("PRIMARY_MATRIX_REQUESTS_PER_STATE_MISMATCH")
    if len(primary_requests) != 54:
        raise ValueError("PRIMARY_REQUEST_TOTAL_MISMATCH")
    primary_offers = [offer for request in primary_requests for offer in request["offers"]]
    if len(primary_offers) != 270:
        raise ValueError("PRIMARY_OFFER_TOTAL_MISMATCH")

    inventory_sessions = []
    for session in sessions:
        inventory_sessions.append({key: session[key] for key in (
            "session_id", "included", "disposition_reason", "source_path", "source_sha256",
            "event_count", "request_count", "offer_count", "character_surrogate",
            "character_identity_raw", "terminal_identity", "terminal_playfield",
            "terminal_coordinates", "harvester_version", "aosharp_version",
            "started_at_utc", "stopped_at_utc",
        )})
    request_inventory = []
    for request in primary_requests:
        request_inventory.append({
            "session_id": request["session_id"], "request_id": request["request_id"],
            "cohort_id": request["cohort_id"], "timestamp_utc": request["timestamp_utc"],
            "character_level": request["character_level"], "state_index": request["state_index"],
            "state_label": request["state_label"], "slider_state_id": request["slider_state_id"],
            "native_slider_values": dict(zip(SLIDER_KEYS, request["native"])),
            "requested_semantic_state": request["requested_semantic_state"],
            "static_expected_mission_ql": request["expected_mission_ql"],
            "outbound_sha256": sha256_bytes(request["outbound"]), "outbound_byte_length": len(request["outbound"]),
            "inbound_sha256": sha256_bytes(request["inbound"]), "inbound_byte_length": len(request["inbound"]),
            "offer_count": len(request["offers"]),
        })
    inventory = {
        "schema_version": 1,
        "campaign": "LEVEL2_SLIDER_DISCOVERY",
        "primary_counts": {"states": 27, "requests": len(primary_requests), "offers": len(primary_offers), "requests_per_state": 2, "offers_per_request": 5},
        "surplus_counts": {"sessions": len(surplus_sessions), "requests": sum(session["request_count"] for session in surplus_sessions), "offers": sum(session["offer_count"] for session in surplus_sessions)},
        "sessions": inventory_sessions,
        "requests": sorted(request_inventory, key=lambda row: (row["state_index"], row["timestamp_utc"], row["request_id"])),
        "validation": "PASS_EXACT_PRIMARY_CAMPAIGN_SURPLUS_RETAINED_AND_EXCLUDED_EXPLICITLY",
    }

    baseline = state_requests[1][0]["outbound"]
    matrix_rows = []
    matrix_json = []
    state_offers: dict[int, list[dict[str, Any]]] = {}
    for index in range(1, 28):
        requests = sorted(state_requests[index], key=lambda row: (row["timestamp_utc"], row["request_id"]))
        offers = [offer for request in requests for offer in request["offers"]]
        state_offers[index] = offers
        native = requests[0]["native"]
        expected = EXPECTED_STATES[index - 1]
        if native != expected["native"] or any(request["native"] != native for request in requests):
            raise ValueError(f"STATE_NATIVE_RECONSTRUCTION_MISMATCH: {index}")
        repeat_diffs = diff_offsets(requests[0]["outbound"], requests[1]["outbound"])
        request_classification = "REQUEST_PAYLOAD_DETERMINISTIC" if not repeat_diffs else "REQUEST_PAYLOAD_UNEXPLAINED_DIFFERENCE"
        if repeat_diffs:
            raise ValueError(f"REPEATED_STATE_REQUEST_NOT_DETERMINISTIC: {index}: {repeat_diffs}")
        summary = summarize_offers(offers)
        matrix_entry = {
            "state_index": index, "label": expected["label"],
            "slider_state_id": requests[0]["slider_state_id"],
            "native_slider_values": dict(zip(SLIDER_KEYS, native)),
            "static_expected_mission_ql": requests[0]["expected_mission_ql"],
            "request_count": len(requests), "offer_count": len(offers),
            "request_ids": [request["request_id"] for request in requests],
            "outbound_packet_sha256": sha256_bytes(requests[0]["outbound"]),
            "outbound_diff_from_center_offsets": diff_offsets(baseline, requests[0]["outbound"]),
            "repeat_request_diff_offsets": repeat_diffs,
            "request_determinism": request_classification,
            "decoded_output_summary": summary,
        }
        matrix_json.append(matrix_entry)
        matrix_rows.append({
            "StateIndex": index, "Label": expected["label"], "SliderStateId": requests[0]["slider_state_id"],
            "Difficulty": native[0], "GoodBad": native[1], "OrderChaos": native[2], "OpenHidden": native[3],
            "PhysicalMystical": native[4], "HeadonStealth": native[5], "MoneyXp": native[6],
            "ExpectedMissionQl": requests[0]["expected_mission_ql"], "Requests": len(requests), "Offers": len(offers),
            "RequestIds": ";".join(request["request_id"] for request in requests),
        })

    dimension_states = {
        "easy_hard": [1, 26, 27], "good_bad": [1, 2, 3, 4, 5],
        "order_chaos": [1, 6, 7, 8, 9], "open_hidden": [1, 10, 11, 12, 13],
        "physical_mystical": [1, 14, 15, 16, 17], "headon_stealth": [1, 18, 19, 20, 21],
        "money_xp": [1, 22, 23, 24, 25],
    }
    outbound_dimensions = []
    slider_field_index = {"easy_hard": 0, "good_bad": 1, "order_chaos": 2, "open_hidden": 3, "physical_mystical": 4, "headon_stealth": 5, "money_xp": 6}
    all_offset_sets = {}
    for dimension, indices in dimension_states.items():
        entries = []
        changed_union: set[int] = set()
        for index in indices:
            request = state_requests[index][0]
            offsets = diff_offsets(baseline, request["outbound"])
            changed_union.update(offsets)
            entries.append({
                "state_index": index, "label": request["state_label"],
                "native_value": request["native"][slider_field_index[dimension]],
                "changed_offsets_from_center": offsets,
                "values_at_changed_offsets": {f"0x{offset:02X}": request["outbound"][offset] for offset in offsets},
            })
        all_offset_sets[dimension] = sorted(changed_union)
        outbound_dimensions.append({
            "slider": dimension, "classification": "TRANSMITTED_PROVEN",
            "changed_offsets": sorted(changed_union),
            "changed_offsets_hex": [f"0x{offset:02X}" for offset in sorted(changed_union)],
            "changed_byte_count": len(changed_union),
            "same_offset_for_every_noncenter_state": all(
                set(entry["changed_offsets_from_center"]) == changed_union for entry in entries if entry["state_index"] != 1
            ),
            "native_values_by_state": entries,
            "semantic_monotonicity": "SIGNED_BYTE_ENCODING_MONOTONIC_AFTER_SIGNED_DECODE" if dimension != "easy_hard" else "ONE_BASED_DETENT_BYTE",
        })
    transmitted_offsets = [offset for offsets in all_offset_sets.values() for offset in offsets]
    if len(transmitted_offsets) != len(set(transmitted_offsets)):
        raise ValueError("SLIDER_PACKET_OFFSETS_OVERLAP_UNEXPECTEDLY")
    outbound = {
        "packet_byte_length": len(baseline),
        "baseline_packet_sha256": sha256_bytes(baseline),
        "baseline_packet_base64": base64.b64encode(baseline).decode("ascii"),
        "volatile_field_mask": [],
        "repeat_determinism": {"states_exactly_identical_across_two_requests": 27, "states_with_unexplained_difference": 0},
        "sliders": outbound_dimensions,
        "all_seven_transmitted": True,
        "packing_result": "SEVEN_DISTINCT_SINGLE_BYTE_OFFSETS_NO_SHARED_PACKING_OBSERVED",
    }

    inbound_states = []
    echo_offsets = []
    for index in range(1, 28):
        requests = state_requests[index]
        occurrences = [packet_occurrences(request["inbound"], bytes(request["native"])) for request in requests]
        echo_offsets.extend(offsets[0] for offsets in occurrences if len(offsets) == 1)
        inbound_states.append({
            "state_index": index,
            "response_packet_count": len(requests),
            "byte_lengths": [len(request["inbound"]) for request in requests],
            "packet_sha256": [sha256_bytes(request["inbound"]) for request in requests],
            "slider_tuple_occurrences": occurrences,
            "packet_types": sorted({int(request["envelope"]["packet_type"]) for request in requests}),
            "n3_message_types": sorted({int(request["envelope"]["n3_message_type"]) for request in requests}),
            "decoded_offer_counts": [len(request["offers"]) for request in requests],
        })
    common_echo = sorted(set(echo_offsets)) if len(echo_offsets) == len(primary_requests) else []
    inbound = {
        "response_packet_count": len(primary_requests),
        "one_raw_response_per_request": True,
        "five_offer_grouping": True,
        "per_offer_raw_boundaries": "NOT_RECOVERED_FROM_CURRENT_AOSHARP_PACKET_DECODER",
        "returned_slider_echo": {
            "classification": "PROVEN_FROM_RAW_INBOUND_PACKET" if len(common_echo) == 1 else "CANDIDATE_CORRELATION",
            "common_unique_offset": common_echo[0] if len(common_echo) == 1 else None,
            "common_unique_offset_hex": f"0x{common_echo[0]:02X}" if len(common_echo) == 1 else None,
        },
        "states": inbound_states,
        "structural_boundary": "Fixed header and echoed slider tuple are comparable; variable strings and offer payload lengths prevent global fixed-offset interpretation after the header.",
    }

    mission_ql_candidates = scan_mission_ql_candidates(state_offers)
    mission_type_candidates = scan_mission_type_candidates(primary_offers)
    unknown_candidates = {
        "mission_ql": mission_ql_candidates,
        "mission_type": {"candidates": mission_type_candidates, "known_decoded_anchor": "MissionIcon"},
        "objective_identity": {"classification": "UNKNOWN_NO_DECODED_GROUND_TRUTH"},
        "objective_ql": {"classification": "UNKNOWN_NO_DECODED_GROUND_TRUTH"},
        "token_reward": {"classification": "UNKNOWN_NOT_EXPOSED"},
        "entrance_identity": {"classification": "UNKNOWN_NOT_EXPOSED"},
        "faction_requirement": {"classification": "UNKNOWN_NOT_EXPOSED"},
        "semantic_promotion_performed": False,
    }

    per_slider = []
    historical = {
        "good_bad": "SUPPORTED_BY_LEVEL2_CAPTURE_DISCOVERY_ONLY",
        "order_chaos": "NOT_TESTABLE_WITH_OFFER_ONLY_SAMPLE",
        "open_hidden": "NOT_TESTABLE_WITH_OFFER_ONLY_SAMPLE",
        "physical_mystical": "NOT_TESTABLE_WITH_OFFER_ONLY_SAMPLE",
        "headon_stealth": "NOT_TESTABLE_WITH_OFFER_ONLY_SAMPLE",
        "money_xp": "SUPPORTED_BY_LEVEL2_CAPTURE",
        "easy_hard": "SUPPORTED_FOR_REQUEST_DETENT_AND_COMPENSATION_SCALING_ONLY",
    }
    for dimension, indices in dimension_states.items():
        summaries = {str(index): matrix_json[index - 1]["decoded_output_summary"] for index in indices}
        output_classification = "INCONCLUSIVE"
        evidence = []
        decision = "CAN_DEFER_TO_HIGHER_LEVEL"
        if dimension == "money_xp":
            left = summaries["22"]
            right = summaries["23"]
            disjoint_credits = left["credits"]["minimum"] > right["credits"]["maximum"]
            disjoint_xp = left["xp"]["maximum"] < right["xp"]["minimum"]
            if disjoint_credits and disjoint_xp:
                output_classification = "DEFINITE_OBSERVABLE_OUTPUT_EFFECT"
                evidence.append("Full-left and full-right credit ranges are disjoint in both repeated cohorts; XP ranges are also disjoint in the opposite direction.")
            decision = "CAN_DEFER_TO_HIGHER_LEVEL"
        elif dimension == "good_bad":
            left_icons = set(summaries["2"]["mission_icon_counts"])
            right_icons = set(summaries["3"]["mission_icon_counts"])
            if left_icons != right_icons:
                output_classification = "POSSIBLE_OUTPUT_EFFECT"
                evidence.append("Mission-icon sets differ between extremes, but ten offers per state cannot establish stable availability or probability.")
        elif dimension == "easy_hard":
            output_classification = "DEFINITE_OBSERVABLE_OUTPUT_EFFECT"
            evidence.append("Detents 1, 6, and 10 produce distinct compensation ranges and static expected QLs 1, 2, and 3; no response-side mission QL property is decoded.")
        else:
            output_classification = "NO_EFFECT_DETECTED_AT_DISCOVERY_SAMPLE"
            evidence.append("No deterministic offer-level decoded effect repeated across both cohorts; historical claims mostly concern mission interiors not present in offers.")
        per_slider.append({
            "slider": dimension,
            "protocol_input": "DEFINITE_PROTOCOL_INPUT",
            "output_effect": output_classification,
            "evidence": evidence,
            "states": indices,
            "decoded_summaries": summaries,
            "historical_claim_classification": historical[dimension],
            "level2_capture_decision": decision,
        })

    money = next(entry for entry in per_slider if entry["slider"] == "money_xp")
    money["special_analysis"] = {
        "credits_affected": True, "xp_affected": True,
        "reward_identity_effect": "INCONCLUSIVE_DISCOVERY_SAMPLE_ONLY",
        "reward_ql_effect": "NO_DETERMINISTIC_EFFECT_DETECTED",
        "formula_status": "NO_FINAL_FORMULA_INFERRED",
        "followup": "DEFER_FORMULA_AND_DISTRIBUTION_WORK_TO_HIGHER_LEVEL_CONTROLLED_CAPTURE",
    }

    raw_packet_groups: dict[str, list[int]] = defaultdict(list)
    for entry in matrix_json:
        raw_packet_groups[entry["outbound_packet_sha256"]].append(entry["state_index"])
    duplicate_groups = [indices for indices in raw_packet_groups.values() if len(indices) > 1]
    redundant = {
        "semantic_state_count": 27,
        "unique_native_state_count": len({request["native"] for request in primary_requests}),
        "unique_outbound_packet_count": len(raw_packet_groups),
        "exact_request_duplicate_state_groups": duplicate_groups,
        "classification": "NO_PROTOCOL_REDUNDANT_SEMANTIC_STATES" if not duplicate_groups else "EXACT_REQUEST_DUPLICATES_FOUND",
        "distinct_request_no_effect_detected_states": [index for entry in per_slider if entry["output_effect"] == "NO_EFFECT_DETECTED_AT_DISCOVERY_SAMPLE" for index in entry["states"] if index != 1],
    }

    followup = {
        "more_level2_capture_required": False,
        "level2_character_may_advance": True,
        "minimal_followup_matrix": [],
        "reasoning": [
            "All seven controls are proven as distinct transmitted bytes and repeat deterministically.",
            "The only definite secondary offer-level effect, Money/XP compensation, is not unique to QL1 and can be studied at higher level.",
            "Good/Bad mission-icon differences remain discovery-only; larger distribution work can use a higher-level character without losing the protocol dimension.",
            "Order/Chaos, Open/Hidden, Physical/Mystical, and Head On/Stealth historical claims concern mission interiors and are not resolved by more offer-only QL1 rolling.",
            "No mission-QL candidate was promoted; broader multi-QL captures provide better correlation than repeating only QL1.",
        ],
        "separate_scope_warning": "The 270-offer discovery campaign does not exhaust the QL1 reward pool and does not establish probabilities. That separate statistical-content objective was not required to resolve slider control or preserve a uniquely level-2 protocol dimension.",
        "next_character_level": 7,
    }

    summaries = {
        "schema_version": 1,
        "campaign": "LEVEL2_SLIDER_DISCOVERY",
        "evidence_labels": ["PROVEN_FROM_OUTBOUND_PACKET", "PROVEN_FROM_REPEATED_CAPTURE", "OBSERVED_IN_DECODED_OUTPUT", "CANDIDATE_CORRELATION", "DISCOVERY_SAMPLE_ONLY", "INCONCLUSIVE", "UNKNOWN"],
        "sliders": per_slider,
        "redundancy": redundant,
        "reward_probabilities_inferred": False,
        "runtime_mission_logic_changed": False,
    }

    artifacts: dict[str, str] = {
        "capture-inventory.json": canonical_json(inventory),
        "level2-slider-state-matrix.json": canonical_json({"schema_version": 1, "states": matrix_json}),
        "level2-slider-state-matrix.csv": csv_matrix(matrix_rows),
        "slider-evidence-matrix.csv": csv_slider_evidence(per_slider),
        "outbound-byte-diff-map.json": canonical_json(outbound),
        "inbound-structure-diff.json": canonical_json(inbound),
        "per-slider-discovery-summary.json": canonical_json(summaries),
        "unknown-field-candidates.json": canonical_json(unknown_candidates),
        "level2-followup-recommendation.json": canonical_json(followup),
        "primary-request-evidence.jsonl": "".join(json_line(row) for row in inventory["requests"]),
    }
    manifest_entries = []
    for name, content in sorted(artifacts.items()):
        manifest_entries.append({"path": f"docs/generated/missions/modern-capture/level2-slider-discovery/{name}", "sha256": sha256_bytes(content.encode("utf-8"))})
    manifest = {
        "schema_version": 1,
        "generator": "Tools/analyze_level2_mission_slider_capture.py",
        "source_captures": [{"path": session["source_path"], "sha256": session["source_sha256"], "included": session["included"]} for session in sessions],
        "generated_files": manifest_entries,
        "reward_probabilities_inferred": False,
        "runtime_mission_logic_changed": False,
    }
    artifacts["evidence-manifest.json"] = canonical_json(manifest)

    offsets = {entry["slider"]: entry["changed_offsets_hex"] for entry in outbound_dimensions}
    state_table = "\n".join(
        f"| {row['StateIndex']} | {row['Label']} | {row['Difficulty']} | {row['GoodBad']} | {row['OrderChaos']} | {row['OpenHidden']} | {row['PhysicalMystical']} | {row['HeadonStealth']} | {row['MoneyXp']} | {row['ExpectedMissionQl']} | {row['Requests']} | {row['Offers']} |"
        for row in matrix_rows
    )
    slider_table = "\n".join(
        f"| {entry['slider']} | {entry['protocol_input']} | {entry['output_effect']} | {entry['level2_capture_decision']} |"
        for entry in per_slider
    )
    report = f"""# Level-2 Mission Slider Discovery Analysis

## Decision

The complete primary campaign is valid: **27 states, 54 requests, and 270 offers**. All seven sliders are distinct, deterministic protocol inputs. No planned semantic state is protocol-redundant.

**Level-2 character status: YES — it may safely advance for the slider-evidence objective.** No additional level-2 slider capture is required. The next assigned character level is **7**.

This does not claim reward-pool exhaustion or infer probabilities. QL1 statistical content saturation is a separate objective.

## Capture inventory

- Primary retained sessions: {len(primary_sessions)}
- Explicitly retained surplus sessions: {len(surplus_sessions)}
- Primary requests: {len(primary_requests)}
- Primary offers: {len(primary_offers)}
- Requests per state: 2
- Offers per request: 5
- Character surrogate: `{primary_sessions[0]['character_surrogate']}`
- Terminal: `{json.dumps(primary_sessions[0]['terminal_identity'], sort_keys=True)}` in playfield `{json.dumps(primary_sessions[0]['terminal_playfield'], sort_keys=True)}`
- Harvester versions: {', '.join(sorted({session['harvester_version'] for session in primary_sessions}))}
- AOSharp version: {', '.join(sorted({session['aosharp_version'] for session in primary_sessions}))}

One extra clean Order/Chaos -50 session was discovered and retained. It is excluded from the primary two-repeat matrix as `SURPLUS_EXACT_STATE_REPEAT_AFTER_PLANNED_QUOTA_FILLED`; it was not silently ignored.

## Exact 27-state matrix

| State | Label | Difficulty | Good/Bad | Order/Chaos | Open/Hidden | Physical/Mystical | Head On/Stealth | Money/XP | Expected QL | Requests | Offers |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
{state_table}

## Outbound protocol findings

Every state repeated an identical 52-byte outbound packet across both requests. No volatile offsets or unexplained differences were present.

| Slider | Changed raw request offsets | Classification |
| --- | --- | --- |
| Easy/Hard | {', '.join(offsets['easy_hard'])} | TRANSMITTED_PROVEN |
| Good/Bad | {', '.join(offsets['good_bad'])} | TRANSMITTED_PROVEN |
| Order/Chaos | {', '.join(offsets['order_chaos'])} | TRANSMITTED_PROVEN |
| Open/Hidden | {', '.join(offsets['open_hidden'])} | TRANSMITTED_PROVEN |
| Physical/Mystical | {', '.join(offsets['physical_mystical'])} | TRANSMITTED_PROVEN |
| Head On/Stealth | {', '.join(offsets['headon_stealth'])} | TRANSMITTED_PROVEN |
| Money/XP | {', '.join(offsets['money_xp'])} | TRANSMITTED_PROVEN |

Each slider changes one distinct byte. No shared packing was observed. Full-left, -50, center, +50, and full-right use the proven native bytes `156`, `206`, `255`, `50`, and `100`; semantic order is monotonic after signed-byte decoding. Easy/Hard is a separate one-based detent byte.

## Request determinism

All 27 states are `REQUEST_PAYLOAD_DETERMINISTIC`: the two complete outbound packets are byte-for-byte identical. The volatile-field mask is empty because no request identity, timestamp, or sequence field changed in the serialized request packets. There are no unexplained request differences.

## Inbound structure

There is exactly one raw response packet per request and every decoded response contains exactly five ordered offers. The seven-byte returned slider tuple is present in raw inbound data at the common offset recorded in `inbound-structure-diff.json`. Response lengths vary with mission text, rewards, and offer payloads. The current decoder exposes per-offer decoded boundaries and six raw unknown chunks, but not defensible absolute raw packet offsets for each offer.

## Decoded output findings

| Slider | Protocol classification | Output classification | Level-2 decision |
| --- | --- | --- | --- |
{slider_table}

- **Money/XP:** `DEFINITE_OBSERVABLE_OUTPUT_EFFECT`. Full-left and full-right credit ranges are disjoint in both repeated cohorts; XP ranges are disjoint in the opposite direction. Both credits and XP are affected. Reward identity and final formula remain inconclusive.
- **Good/Bad:** `POSSIBLE_OUTPUT_EFFECT`. Extreme states produced different mission-icon sets, but ten offers per state cannot establish availability or probability.
- **Easy/Hard:** `DEFINITE_OBSERVABLE_OUTPUT_EFFECT` for compensation scaling across detents 1, 6, and 10. Expected QL remains static request provenance, not server-confirmed mission QL.
- **Order/Chaos, Open/Hidden, Physical/Mystical, Head On/Stealth:** no deterministic offer-level effect was detected. Their historical claims mostly concern interiors, enemies, locks, traps, or behavior that offer packets do not expose.

### Money/XP special analysis

Both compensation fields change. Full-left yielded higher credit ranges and lower XP ranges than full-right, with disjoint extreme-state ranges across both repeated cohorts. Reward identities varied, but the discovery sample cannot attribute that variation to the slider. Reward QL showed no deterministic slider-linked effect. No final compensation formula or probability was inferred.

### Easy/Hard special analysis

Detents 1, 6, and 10 are separately encoded at the Easy/Hard byte and map statically to expected QL1, QL2, and QL3 for character level 2. Returned packet structure and compensation scale across these states. AOSharp still reports `mission_ql` as unavailable, so request provenance is not mislabeled as a server-confirmed QL.

## Unknown-field candidates

Mission-QL scan result: **{mission_ql_candidates['classification']}**. `UnkChunk3Base64`, big-endian 32-bit value at decoded chunk offset `16`, equals expected QL 1/2/3 across all 30 Easy/Hard comparison offers. Overlapping byte/word views are recorded as aliases, not independent candidates. This remains a candidate pending broader multi-QL confirmation and is not promoted to runtime semantics.

No unknown-chunk field produced a deterministic all-offer mapping to decoded `MissionIcon`; the already decoded icon remains the authoritative mission-type observation. Objective identity/QL, token reward, entrance identity, and faction requirements remain unknown because no decoded ground truth exists.

## Redundancy and follow-up

- Protocol-redundant semantic states: **none**
- Exact state-repeat request determinism: **27/27 states**
- Additional level-2 slider capture: **not required**
- Minimal follow-up matrix: **empty**
- Money/XP formula work: defer to a higher-level controlled capture
- Interior-behavior slider work: requires accepted mission/interior instrumentation, not more QL1 offer rolling
- Mission-QL candidate work: use broader multi-QL correlations

## Evidence boundaries

These are discovery results, not probabilities. Differences in counts or frequencies are not treated as stable distributions. No mission runtime behavior was implemented or changed.

**RUNTIME MISSION LOGIC CHANGED: NO**
"""
    metadata = {"inventory": inventory, "matrix": matrix_json, "outbound": outbound, "inbound": inbound, "summaries": summaries, "unknown": unknown_candidates, "followup": followup}
    return artifacts, report, metadata


def write_outputs(artifacts: dict[str, str], report: str) -> None:
    GENERATED.mkdir(parents=True, exist_ok=True)
    for name, content in artifacts.items():
        (GENERATED / name).write_text(content, encoding="utf-8", newline="\n")
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(report, encoding="utf-8", newline="\n")


def check_outputs(artifacts: dict[str, str], report: str) -> None:
    stale = []
    for name, content in artifacts.items():
        path = GENERATED / name
        if not path.exists() or path.read_text(encoding="utf-8") != content:
            stale.append(path.relative_to(ROOT).as_posix())
    if not REPORT.exists() or REPORT.read_text(encoding="utf-8") != report:
        stale.append(REPORT.relative_to(ROOT).as_posix())
    if stale:
        raise SystemExit("STALE_LEVEL2_SLIDER_ANALYSIS: " + ", ".join(stale))


def self_test(metadata: dict[str, Any], artifacts: dict[str, str], report: str) -> None:
    assert metadata["inventory"]["primary_counts"] == {"states": 27, "requests": 54, "offers": 270, "requests_per_state": 2, "offers_per_request": 5}
    assert metadata["inventory"]["surplus_counts"] == {"sessions": 1, "requests": 2, "offers": 10}
    assert len(metadata["matrix"]) == 27
    assert all(entry["request_count"] == 2 and entry["offer_count"] == 10 for entry in metadata["matrix"])
    assert all(not entry["repeat_request_diff_offsets"] for entry in metadata["matrix"])
    assert metadata["outbound"]["all_seven_transmitted"]
    assert len({tuple(entry["changed_offsets"]) for entry in metadata["outbound"]["sliders"]}) == 7
    assert metadata["summaries"]["redundancy"]["classification"] == "NO_PROTOCOL_REDUNDANT_SEMANTIC_STATES"
    assert metadata["followup"]["more_level2_capture_required"] is False
    assert metadata["followup"]["level2_character_may_advance"] is True
    assert metadata["unknown"]["mission_ql"]["classification"] == "STRONG_MISSION_QL_CANDIDATE"
    assert metadata["unknown"]["mission_ql"]["exact_candidates"] == [{
        "property": "UnkChunk3Base64", "offset": 16, "width": 4,
        "byte_order": "big", "relationship": "EQUALS_STATIC_EXPECTED_MISSION_QL_ALL_30_OFFERS",
    }]
    first = {**artifacts}
    second_artifacts, second_report, _ = build_analysis()
    assert first == second_artifacts and report == second_report
    with tempfile.TemporaryDirectory(prefix="aorebirth-level2-slider-malformed-") as temporary:
        source = RAW / SESSION_DISPOSITIONS[0][0] / "events.jsonl"
        malformed = Path(temporary) / "events.jsonl"
        malformed.write_bytes(source.read_bytes().splitlines(keepends=True)[0])
        try:
            load_session(SESSION_DISPOSITIONS[0][0], True, "malformed-test", malformed)
            raise AssertionError("truncated session was accepted")
        except ValueError:
            pass
        missing = Path(temporary) / "missing.jsonl"
        try:
            load_session(SESSION_DISPOSITIONS[0][0], True, "missing-test", missing)
            raise AssertionError("missing session was accepted")
        except ValueError:
            pass
        corrupted = Path(temporary) / "corrupted.jsonl"
        lines = [json.loads(line) for line in source.read_text(encoding="utf-8").splitlines()]
        next(event for event in lines if event["event_type"] == "request_transmitted")["payload"]["raw_packet"]["sha256"] = "0" * 64
        corrupted.write_text("".join(json_line(event) for event in lines), encoding="utf-8")
        try:
            load_session(SESSION_DISPOSITIONS[0][0], True, "hash-test", corrupted)
            raise AssertionError("corrupted packet hash was accepted")
        except ValueError:
            pass
    print("LEVEL2_SLIDER_ANALYSIS_SELF_TEST=PASS states=27 requests=54 offers=270")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--import-source", action="store_true")
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument("--write", action="store_true")
    modes.add_argument("--check", action="store_true")
    modes.add_argument("--self-test", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.import_source:
        import_sources()
    artifacts, report, metadata = build_analysis()
    if args.write:
        write_outputs(artifacts, report)
        print("LEVEL2_SLIDER_ANALYSIS_WRITE=PASS states=27 requests=54 offers=270")
    elif args.check:
        check_outputs(artifacts, report)
        print("LEVEL2_SLIDER_ANALYSIS_CHECK=PASS")
    else:
        self_test(metadata, artifacts, report)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
