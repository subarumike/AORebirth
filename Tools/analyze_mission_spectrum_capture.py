#!/usr/bin/env python3
"""Build a compact, evidence-validated baseline from a completed spectrum capture."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_jsonl(path: Path):
    with path.open("r", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            if line.strip():
                yield line_number, json.loads(line)


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")


def write_csv(path: Path, fieldnames: list[str], rows: list[dict[str, object]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--progress", required=True, type=Path)
    parser.add_argument("--sessions-root", required=True, type=Path)
    parser.add_argument("--normalized-root", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    progress_path = args.progress.resolve()
    sessions_root = args.sessions_root.resolve()
    normalized_root = args.normalized_root.resolve()
    output_dir = args.output_dir.resolve()

    completions: dict[str, dict[str, object]] = {}
    mappings: dict[int, int] = {}
    completion_ids: set[str] = set()
    character_level: int | None = None
    character_identity_instance: int | None = None
    campaign_name: str | None = None
    for line_number, event in read_jsonl(progress_path):
        event_type = event.get("event_type")
        payload = event.get("payload", {})
        if event_type == "difficulty_mapping_verified":
            detent = int(payload["difficulty_detent"])
            actual_ql = int(payload["actual_mission_ql"])
            previous = mappings.setdefault(detent, actual_ql)
            if previous != actual_ql:
                raise SystemExit(f"difficulty mapping conflict at progress line {line_number}")
        if event_type != "campaign_request_completed":
            continue
        request_id = str(event["request_id"])
        completion_id = str(payload["completion_id"])
        if request_id in completions or completion_id in completion_ids:
            raise SystemExit(f"duplicate completion at progress line {line_number}")
        if payload.get("verification_status") != "VERIFIED_COMPLETE_FIVE_OFFER_COHORT":
            raise SystemExit(f"unverified completion at progress line {line_number}")
        if int(payload["offer_count"]) != 5:
            raise SystemExit(f"non-five-offer completion at progress line {line_number}")
        completed_level = int(payload["character_level"])
        completed_identity = int(payload["character_identity_instance"])
        completed_campaign = str(payload["campaign_name"])
        if character_level is None:
            character_level = completed_level
            character_identity_instance = completed_identity
            campaign_name = completed_campaign
        elif (
            completed_level != character_level
            or completed_identity != character_identity_instance
            or completed_campaign != campaign_name
        ):
            raise SystemExit(f"mixed character or campaign completion at progress line {line_number}")
        completions[request_id] = {"session_id": str(event["session_id"]), **payload}
        completion_ids.add(completion_id)

    if not completions:
        raise SystemExit("no verified completions")

    session_ids = sorted({str(row["session_id"]) for row in completions.values()})
    input_files = [{"path": str(progress_path), "sha256": sha256(progress_path)}]
    events_by_request: dict[str, dict[str, dict[str, object]]] = defaultdict(dict)
    session_summaries = []
    for session_id in session_ids:
        events_path = sessions_root / session_id / "events.jsonl"
        if not events_path.exists():
            raise SystemExit(f"missing session journal: {events_path}")
        input_files.append({"path": str(events_path), "sha256": sha256(events_path)})
        started = None
        stopped = None
        for _, event in read_jsonl(events_path):
            event_type = str(event.get("event_type"))
            if event_type == "session_started":
                started = event
            elif event_type == "session_stopped":
                stopped = event
            request_id = event.get("request_id")
            if request_id in completions and event_type in {
                "request_started", "request_transmitted", "raw_response_received", "cohort_received"
            }:
                if event_type in events_by_request[request_id]:
                    raise SystemExit(f"duplicate {event_type} for {request_id}")
                events_by_request[request_id][event_type] = event
        session_summaries.append(
            {
                "session_id": session_id,
                "harvester_version": None if started is None else started["payload"].get("harvester_version"),
                "started_at_utc": None if started is None else started.get("timestamp_utc"),
                "stopped_at_utc": None if stopped is None else stopped.get("timestamp_utc"),
                "stop_reason": None if stopped is None else stopped["payload"].get("reason"),
                "verified_completions_used": sum(1 for row in completions.values() if row["session_id"] == session_id),
            }
        )

    normalized_offers: dict[tuple[str, int], dict[str, object]] = {}
    for session_id in session_ids:
        normalized_path = normalized_root / session_id / "mission_offer.jsonl"
        if not normalized_path.exists():
            raise SystemExit(f"missing normalized offers: {normalized_path}")
        input_files.append({"path": str(normalized_path), "sha256": sha256(normalized_path)})
        for _, offer in read_jsonl(normalized_path):
            request_id = str(offer["RequestId"])
            if request_id in completions:
                normalized_offers[(request_id, int(offer["OfferIndex"]))] = offer

    by_ql_requests: Counter[int] = Counter()
    by_ql_offers: Counter[int] = Counter()
    mission_types: Counter[tuple[int, str, int]] = Counter()
    destinations: Counter[tuple[int, int, str]] = Counter()
    items: Counter[tuple[int, int, int, int, int, str]] = Counter()
    field_presence: Counter[str] = Counter()
    title_values: set[str] = set()
    description_values: set[str] = set()
    mission_identities: set[tuple[int, int]] = set()
    outbound_hashes: set[str] = set()
    inbound_hashes: set[str] = set()

    required_events = {"request_started", "request_transmitted", "raw_response_received", "cohort_received"}
    for request_id, completion in sorted(completions.items()):
        request_events = events_by_request.get(request_id, {})
        missing = required_events - set(request_events)
        if missing:
            raise SystemExit(f"missing events for {request_id}: {sorted(missing)}")
        started = request_events["request_started"]["payload"]
        transmitted = request_events["request_transmitted"]["payload"]
        raw_response = request_events["raw_response_received"]["payload"]
        cohort = request_events["cohort_received"]["payload"]
        actual_ql = int(completion["actual_mission_ql"])
        detent = int(completion["difficulty_detent"])
        if cohort["slider_verification"]["status"] != "MATCH":
            raise SystemExit(f"slider verification failed for {request_id}")
        if len(cohort.get("offers", [])) != 5:
            raise SystemExit(f"cohort does not contain five offers: {request_id}")
        if int(cohort["mission_ql_candidate"]["observed_mission_ql_candidate"]) != actual_ql:
            raise SystemExit(f"mission QL candidate mismatch: {request_id}")
        request_sliders = started["sliders"]
        for name in ("good_bad", "order_chaos", "open_hidden", "physical_mystical", "headon_stealth", "credits_xp"):
            if int(request_sliders[name]) != 255:
                raise SystemExit(f"secondary slider not centered for {request_id}: {name}")
        if int(request_sliders["difficulty"]) != detent:
            raise SystemExit(f"difficulty mismatch for {request_id}")
        outbound_hash = str(transmitted["raw_packet"]["sha256"])
        inbound_hash = str(raw_response["raw_packet"]["sha256"])
        if outbound_hash != str(started["serialized_pre_send"]["raw_packet"]["sha256"]):
            raise SystemExit(f"outbound raw hash mismatch for {request_id}")
        if inbound_hash != str(cohort["raw_response_packet"]["sha256"]):
            raise SystemExit(f"inbound raw hash mismatch for {request_id}")
        if inbound_hash != str(completion["raw_response_sha256"]):
            raise SystemExit(f"completion raw hash mismatch for {request_id}")
        outbound_hashes.add(outbound_hash)
        inbound_hashes.add(inbound_hash)
        by_ql_requests[actual_ql] += 1

        for offer in cohort["offers"]:
            offer_index = int(offer["offer_index"])
            normalized = normalized_offers.get((request_id, offer_index))
            if normalized is None:
                raise SystemExit(f"normalized offer missing for {request_id}/{offer_index}")
            by_ql_offers[actual_ql] += 1
            title = offer.get("title") or ""
            description = offer.get("description") or ""
            if title:
                field_presence["title"] += 1
                title_values.add(title)
            if description:
                field_presence["description"] += 1
                description_values.add(description)
            for field in ("credits", "xp_reward", "mission_icon", "mission_type", "mission_destination", "unknown_fields"):
                if offer.get(field) is not None:
                    field_presence[field] += 1
            identity = offer.get("mission_identity") or {}
            mission_identities.add((int(identity.get("type", 0)), int(identity.get("instance", 0))))
            mission_type = offer["mission_type"]["canonical_type"]
            mission_icon = int(offer["mission_icon"])
            mission_types[(actual_ql, mission_type, mission_icon)] += 1
            playfield = offer["mission_destination"]["playfield_identity"]
            playfield_id = int(playfield["instance"])
            playfield_name = normalized["MissionDestination"]["playfield_name"]["Name"]
            destinations[(actual_ql, playfield_id, playfield_name)] += 1
            raw_items = offer.get("mission_items", [])
            rewards = normalized.get("Rewards", [])
            if raw_items:
                field_presence["mission_items"] += 1
            for item_index, item in enumerate(raw_items):
                name = ""
                if item_index < len(rewards):
                    low_join = rewards[item_index].get("OfflineLowIdJoin") or {}
                    high_join = rewards[item_index].get("OfflineHighIdJoin") or {}
                    name = low_join.get("Name") or high_join.get("Name") or ""
                key = (
                    actual_ql,
                    int(item["low_id"]),
                    int(item["high_id"]),
                    int(item["ql"]),
                    int(item["unknown"]),
                    name,
                )
                items[key] += 1

    ql_rows = []
    for mission_ql in sorted(by_ql_requests):
        ql_item_keys = [key for key in items if key[0] == mission_ql]
        ql_rows.append(
            {
                "mission_ql": mission_ql,
                "verified_requests": by_ql_requests[mission_ql],
                "offers": by_ql_offers[mission_ql],
                "observed_item_references": sum(items[key] for key in ql_item_keys),
                "unique_item_pairs": len({(key[1], key[2]) for key in ql_item_keys}),
                "unique_exact_item_descriptors": len({key[1:5] for key in ql_item_keys}),
                "minimum_observed_item_ql": min(key[3] for key in ql_item_keys),
                "maximum_observed_item_ql": max(key[3] for key in ql_item_keys),
                "mission_types_observed": len({key[1] for key in mission_types if key[0] == mission_ql}),
                "destination_playfields_observed": len({key[1] for key in destinations if key[0] == mission_ql}),
            }
        )

    item_rows = [
        {
            "mission_ql": key[0],
            "low_id": key[1],
            "high_id": key[2],
            "observed_item_ql": key[3],
            "unknown": key[4],
            "item_name": key[5],
            "observation_count": count,
        }
        for key, count in sorted(items.items())
    ]
    type_rows = [
        {"mission_ql": key[0], "mission_type": key[1], "mission_icon": key[2], "offer_count": count}
        for key, count in sorted(mission_types.items())
    ]
    destination_rows = [
        {"mission_ql": key[0], "playfield_id": key[1], "playfield_name": key[2], "offer_count": count}
        for key, count in sorted(destinations.items())
    ]

    total_offers = sum(by_ql_offers.values())
    summary = {
        "schema_version": 1,
        "baseline": f"LEVEL_{character_level}_{campaign_name}",
        "character_level": character_level,
        "character_identity_instance": character_identity_instance,
        "input_files": input_files,
        "sessions": session_summaries,
        "difficulty_position_to_actual_mission_ql": [
            {"difficulty_position": detent, "actual_mission_ql": mappings[detent]}
            for detent in sorted(mappings)
        ],
        "totals": {
            "verified_completed_requests": len(completions),
            "five_offer_cohorts": len(completions),
            "offers": total_offers,
            "unique_mission_identities": len(mission_identities),
            "observed_item_references": sum(items.values()),
            "unique_item_pairs": len({(key[1], key[2]) for key in items}),
            "unique_exact_item_descriptors": len({key[1:5] for key in items}),
            "unique_titles": len(title_values),
            "unique_descriptions": len(description_values),
            "unique_outbound_packet_hashes": len(outbound_hashes),
            "unique_inbound_packet_hashes": len(inbound_hashes),
        },
        "by_mission_ql": ql_rows,
        "field_coverage": {name: {"present": field_presence[name], "offers": total_offers} for name in sorted(field_presence)},
        "validation": {
            "completion_markers_unique": True,
            "all_completion_markers_verified": True,
            "all_requests_have_exact_outbound_raw_evidence": True,
            "all_requests_have_exact_inbound_raw_evidence": True,
            "all_raw_hash_links_match": True,
            "all_cohorts_have_five_offers": True,
            "all_secondary_sliders_centered": True,
            "all_response_ql_candidates_match_completion_records": True,
        },
        "useful_for": [
            "candidate mission-item observations by roller level and actual mission QL",
            "observed item QL values without requiring every database-supported interpolation",
            "mission-type, credits, XP, text, and destination distributions",
            "comparison with overlapping mission QLs captured by other roller levels",
            "replay and parser validation through exact request/response packet provenance",
        ],
        "not_proven_by_this_baseline": [
            "complete item-pool membership or exclusions",
            "server probability weights",
            "item-family interpolation or every valid item QL",
            "separately typed objective-item identity because AOSharp does not expose one here",
            "mission interior layout, enemies, or semantic-slider effects",
        ],
    }

    output_dir.mkdir(parents=True, exist_ok=True)
    write_json(output_dir / "summary.json", summary)
    write_csv(output_dir / "mission-ql-summary.csv", list(ql_rows[0]), ql_rows)
    write_csv(output_dir / "observed-items.csv", list(item_rows[0]), item_rows)
    write_csv(output_dir / "mission-types.csv", list(type_rows[0]), type_rows)
    write_csv(output_dir / "destinations.csv", list(destination_rows[0]), destination_rows)

    print(
        "MISSION_SPECTRUM_BASELINE=PASS "
        f"requests={len(completions)} offers={total_offers} "
        f"unique_pairs={summary['totals']['unique_item_pairs']} "
        f"unique_descriptors={summary['totals']['unique_exact_item_descriptors']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
