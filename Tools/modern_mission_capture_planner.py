#!/usr/bin/env python3
"""Deterministic modern AO mission reachability and capture-campaign planner.

This tool is evidence-only.  It never contacts AO, starts a client, or changes
ZoneEngine mission generation.  Generated data stays explicit about the
difference between a table-selected target QL and fields actually exposed in a
live AOSharp MissionInfo response.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import tempfile
from collections import Counter, defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CANONICAL = ROOT / "AORebirth/Server/ZoneEngine/XML Data/MissionLevels.csv"
MALIS = ROOT / "docs/generated/missions/malis/character-level-mission-ql.csv"
MALIS_COMPARISON = ROOT / "docs/generated/missions/malis/mission-level-comparison.json"
ARPA_OBSERVATIONS = ROOT / "docs/generated/missions/arpa3/normalized-roll-observations.csv"
ARPA_ITEMS = ROOT / "docs/generated/missions/arpa3/clicksaver-item-catalog.csv"
ARPA_PLAYFIELDS = ROOT / "docs/generated/missions/arpa3/clicksaver-playfield-catalog.json"
MISSION_TYPES = ROOT / "docs/generated/missions/malis/mission-type-catalog.json"
ACCESS = ROOT / "docs/reference/missions/modern-capture/character-level-capture-access.json"
SESSION_INDEX = ROOT / "docs/reference/missions/modern-capture/capture-session-index.json"
GENERATED = ROOT / "docs/generated/missions/modern-capture"
FIXTURE = ROOT / "Tools/tests/fixtures/modern-mission-capture/events.jsonl"
SCHEMA3_FIXTURE = ROOT / "Tools/tests/fixtures/modern-mission-capture/schema3-events.jsonl"

ARPA_BASELINE = "de61fa4cacb3626cb19155b9548c5325df6d8fd6"
MALIS_BASELINE = "1cb8b18c2b3683114e947b0ff42b43cf035d0f23"
SCHEMA_VERSION = 1
SLOTS = tuple(range(1, 12))
SPECIAL_LEVELS = (2, 10, 12, 13, 52, 53, 54, 60, 80, 200, 201, 209, 219, 220)
SPECIAL_REASONS = {
    2: "lowest known table level not blocked by the modern level-1 starter rule; reaches QL1",
    10: "first Malis/canonical value disagreement",
    12: "dense cluster of Malis missing/disputed cells",
    13: "dense cluster of Malis missing/disputed cells",
    52: "historically corrected difficulty-11 boundary",
    53: "historically corrected difficulty-11 boundary",
    54: "historically corrected difficulty-11 boundary",
    60: "historically corrected difficulty-11 boundary",
    80: "historically corrected difficulty-11 value 144 to 143",
    200: "QL200 transition control below level 201",
    201: "Malis QL200 filtering behavior begins above character level 200",
    209: "high-level transition coverage",
    219: "upper-bound neighbor",
    220: "maximum represented character level and QL250 reachability",
}


def canonical_json(value: object) -> str:
    return json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def json_line(value: object) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"))


def load_level_table(path: Path, malis: bool = False) -> dict[int, dict[int, int | None]]:
    result: dict[int, dict[int, int | None]] = {}
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            level = int(row["character_level"] if malis else row["Level"])
            slots: dict[int, int | None] = {}
            for slot in SLOTS:
                key = f"difficulty_{slot}" if malis else f"Q{slot - 1}"
                raw = (row.get(key) or "").strip()
                slots[slot] = int(raw) if raw else None
            result[level] = slots
    return result


def load_access() -> dict[int, dict[str, object]]:
    source = read_json(ACCESS)
    default = source["DefaultForLevels2Through220"]
    result = {level: {"Level": level, **default} for level in range(2, 221)}
    for override in source["Overrides"]:
        result[int(override["Level"])] = dict(override)
    return result


def load_historical_qls() -> set[int]:
    qls: set[int] = set()
    with ARPA_OBSERVATIONS.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            raw = (row.get("mission_ql") or "").strip()
            if raw:
                qls.add(int(raw))
    return qls


def build_graph() -> tuple[list[dict[str, object]], dict[int, set[int]], dict[int, dict[str, object]]]:
    canonical = load_level_table(CANONICAL)
    malis = load_level_table(MALIS, malis=True)
    access = load_access()
    historical_qls = load_historical_qls()
    comparison = read_json(MALIS_COMPARISON)
    difference_keys = {
        (int(item["CharacterLevel"]), int(item["DifficultyWireValue"])): item["Kind"]
        for item in comparison["Differences"]
    }
    graph: list[dict[str, object]] = []
    coverage: dict[int, set[int]] = defaultdict(set)
    for level in sorted(canonical):
        for slot in SLOTS:
            canonical_ql = canonical[level][slot]
            malis_ql = malis.get(level, {}).get(slot)
            classes = ["PROVEN_STATIC", "LIVE_UNCONFIRMED"]
            disagreement = difference_keys.get((level, slot), "NONE")
            if level == 1:
                classes.append("MODERN_ACCESS_BLOCKED")
            if malis_ql is None:
                classes.append("MISSING")
            elif malis_ql == canonical_ql:
                classes.append("MULTI_SOURCE_SUPPORTED")
            else:
                classes.append("SOURCE_DISAGREEMENT")
            if canonical_ql in historical_qls:
                classes.append("HISTORICAL_ONLY")
            special = SPECIAL_REASONS.get(level)
            edge = {
                "CharacterLevel": level,
                "DifficultySlot": slot,
                "DifficultyWireValue": slot,
                "AORebirthCanonicalMissionQl": canonical_ql,
                "MalisMissionQl": malis_ql,
                "SelectedPlanningMissionQl": canonical_ql,
                "Sources": [
                    {
                        "Confidence": "REPOSITORY_CANONICAL_STATIC_TABLE",
                        "MissionQl": canonical_ql,
                        "Name": "AORebirth MissionLevels.csv",
                    },
                    {
                        "Confidence": "RECONSTRUCTED_CLIENT_STATIC_TABLE" if malis_ql is not None else "MISSING_CELL",
                        "MissionQl": malis_ql,
                        "Name": "Malis Mission Roller 2.0",
                    },
                ],
                "Classifications": sorted(set(classes)),
                "DisagreementStatus": disagreement,
                "SpecialCaseStatus": special,
                "CharacterLevelCaptureAccess": access[level]["OrdinaryMissionTerminalAccess"],
                "LiveUsabilityStatus": access[level]["LiveUsabilityStatus"],
                "ModernLiveValidation": "NOT_PERFORMED",
            }
            graph.append(edge)
            if level >= 2 and canonical_ql is not None:
                coverage[level].add(canonical_ql)
    return graph, coverage, access


def greedy_cover(universe: set[int], coverage: dict[int, set[int]]) -> list[int]:
    remaining = set(universe)
    chosen: list[int] = []
    while remaining:
        ranked = sorted(
            ((len(values & remaining), level) for level, values in coverage.items() if values & remaining),
            key=lambda pair: (-pair[0], pair[1]),
        )
        if not ranked:
            break
        level = ranked[0][1]
        chosen.append(level)
        remaining -= coverage[level]
    return chosen


def reduce_dominated(coverage: dict[int, set[int]]) -> dict[int, set[int]]:
    levels = sorted(coverage)
    kept: dict[int, set[int]] = {}
    for level in levels:
        values = coverage[level]
        if any(values < coverage[other] for other in levels if other != level):
            continue
        duplicate_predecessors = [other for other in levels if other < level and coverage[other] == values]
        if duplicate_predecessors:
            continue
        kept[level] = values
    return kept


def exact_or_near_cover(
    universe: set[int], coverage: dict[int, set[int]], node_limit: int = 50_000
) -> tuple[list[int], bool, int, int]:
    """Deterministic branch-and-bound set cover with an explicit proof flag."""
    reduced = reduce_dominated(coverage)
    incumbent = greedy_cover(universe, reduced)
    best = list(incumbent)
    candidates_by_ql: dict[int, tuple[int, ...]] = {
        ql: tuple(level for level in sorted(reduced) if ql in reduced[level]) for ql in sorted(universe)
    }
    nodes = 0
    exhausted = True
    memo: dict[frozenset[int], int] = {}

    def search(covered: set[int], chosen: list[int]) -> None:
        nonlocal best, nodes, exhausted
        if nodes >= node_limit:
            exhausted = False
            return
        nodes += 1
        if len(chosen) >= len(best):
            return
        remaining = universe - covered
        if not remaining:
            best = list(chosen)
            return
        key = frozenset(covered)
        if memo.get(key, 10**9) <= len(chosen):
            return
        memo[key] = len(chosen)
        max_gain = max((len(values & remaining) for values in reduced.values()), default=0)
        if max_gain == 0 or len(chosen) + math.ceil(len(remaining) / max_gain) >= len(best):
            return
        ql = min(
            remaining,
            key=lambda value: (
                sum(1 for level in candidates_by_ql[value] if reduced[level] & remaining),
                value,
            ),
        )
        candidates = sorted(
            candidates_by_ql[ql], key=lambda level: (-len(reduced[level] & remaining), level)
        )
        for level in candidates:
            search(covered | reduced[level], chosen + [level])
            if not exhausted:
                return

    search(set(), [])
    max_set = max((len(values) for values in reduced.values()), default=1)
    lower_bound = math.ceil(len(universe) / max_set)
    return sorted(best), exhausted, lower_bound, nodes


def why_level(level: int, coverage: dict[int, set[int]], selected: list[int]) -> list[str]:
    other_union: set[int] = set()
    for other in selected:
        if other != level:
            other_union |= coverage[other]
    unique = sorted(coverage[level] - other_union)
    reasons = [f"adds {len(unique)} QLs not supplied by the other selected levels"]
    if unique:
        reasons.append("exclusive contribution: " + ", ".join(map(str, unique)))
    if level in SPECIAL_REASONS:
        reasons.append(SPECIAL_REASONS[level])
    return reasons


def build_set_cover(coverage: dict[int, set[int]]) -> dict[str, object]:
    universe = set().union(*coverage.values())
    reduced = reduce_dominated(coverage)
    exact_levels, proven, lower_bound, nodes = exact_or_near_cover(universe, coverage)
    greedy_levels = greedy_cover(universe, coverage)
    practical_levels = list(SPECIAL_LEVELS)

    def describe(name: str, levels: list[int], proof: str) -> dict[str, object]:
        covered = set().union(*(coverage[level] for level in levels)) if levels else set()
        return {
            "Name": name,
            "CharacterLevels": levels,
            "CharacterCount": len(levels),
            "CoveragePercentOfReachableDomain": round(100.0 * len(covered) / len(universe), 6),
            "CoveredMissionQlCount": len(covered),
            "UncoveredReachableMissionQls": sorted(universe - covered),
            "ProofStatus": proof,
            "LevelReasons": {str(level): why_level(level, coverage, levels) for level in levels},
        }

    return {
        "SchemaVersion": SCHEMA_VERSION,
        "ReachableMissionQlDomain": [min(universe), max(universe)],
        "ReachableMissionQlCount": len(universe),
        "SetCoverSearch": {
            "DominanceReductionApplied": True,
            "DominatedOrDuplicateCharacterLevels": sorted(set(coverage) - set(reduced)),
            "DeterministicNodeLimit": 50_000,
            "NodesVisited": nodes,
            "SimpleCardinalityLowerBound": lower_bound,
        },
        "MathematicalMinimumOrBestProven": describe(
            "MATHEMATICAL_MINIMUM" if proven else "BEST_DETERMINISTIC_NEAR_EXACT",
            exact_levels,
            "EXACT_SEARCH_EXHAUSTED" if proven else "NODE_LIMIT_REACHED_NOT_PROVEN_MINIMUM",
        ),
        "GreedyNearOptimal": describe("GREEDY_NEAR_OPTIMAL", greedy_levels, "DETERMINISTIC_GREEDY"),
        "PracticalCaptureSet": describe(
            "PRACTICAL_CAPTURE_SET",
            practical_levels,
            "FOCUSED_DISAGREEMENT_AND_SPECIAL_CASE_VALIDATION_SET_NOT_FULL_DOMAIN_COVER",
        ),
    }


def character_coverage(coverage: dict[int, set[int]], access: dict[int, dict[str, object]]) -> dict[str, object]:
    rows = []
    all_levels_by_ql: dict[int, list[int]] = defaultdict(list)
    for level, qls in coverage.items():
        for ql in qls:
            all_levels_by_ql[ql].append(level)
    for level in range(1, 221):
        qls = sorted(coverage.get(level, set()))
        rows.append(
            {
                "Level": level,
                "CaptureAccess": access[level]["OrdinaryMissionTerminalAccess"],
                "LiveUsabilityStatus": access[level]["LiveUsabilityStatus"],
                "MissionQls": qls,
                "MissionQlCount": len(qls),
                "ExclusiveMissionQlsAmongLevels2Through220": [
                    ql for ql in qls if len(all_levels_by_ql[ql]) == 1
                ],
            }
        )
    return {"SchemaVersion": SCHEMA_VERSION, "Levels": rows}


def ql_reachability(graph: list[dict[str, object]], coverage: dict[int, set[int]]) -> dict[str, object]:
    historical = load_historical_qls()
    edges_by_ql: dict[int, list[dict[str, int]]] = defaultdict(list)
    for edge in graph:
        if int(edge["CharacterLevel"]) >= 2:
            edges_by_ql[int(edge["SelectedPlanningMissionQl"])].append(
                {"CharacterLevel": int(edge["CharacterLevel"]), "DifficultySlot": int(edge["DifficultySlot"])}
            )
    rows = []
    for ql in range(1, 251):
        edges = edges_by_ql.get(ql, [])
        if edges:
            status = "QL1_MODERN_REACHABLE" if ql == 1 else "REACHABLE_FROM_KNOWN_TABLE"
        else:
            status = "QL1_MODERN_UNREACHABLE_FROM_KNOWN_TABLE" if ql == 1 else "UNREACHABLE_FROM_KNOWN_TABLE"
        rows.append(
            {
                "MissionQl": ql,
                "KnownTableReachability": status,
                "LiveValidation": "LIVE_UNCONFIRMED",
                "HistoricalObservation": "HISTORICAL_ONLY" if ql in historical else "NONE_IN_ARPA_ARTIFACT",
                "CandidateEdges": edges,
                "CandidateCharacterLevelCount": len({edge["CharacterLevel"] for edge in edges}),
            }
        )
    ql1 = rows[0]
    return {
        "SchemaVersion": SCHEMA_VERSION,
        "Ql1Investigation": {
            "Classification": ql1["KnownTableReachability"],
            "CandidateEdges": ql1["CandidateEdges"],
            "ModernLiveValidation": "NOT_PERFORMED",
            "NoModernLiveCapturePath": False,
            "Conclusion": "Level 2 difficulty slots 1 through 5 select QL1 in both static tables; ordinary terminal access remains live-unconfirmed.",
        },
        "MissionQls": rows,
    }


def load_item_join() -> dict[int, dict[str, object]]:
    result: dict[int, dict[str, object]] = {}
    with ARPA_ITEMS.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            raw = (row.get("clicksaver_item_id") or "").strip()
            if not raw:
                continue
            result[int(raw)] = {
                "Name": row.get("clicksaver_item_name") or None,
                "NameAuthority": row.get("name_authority") or None,
                "AORebirthResolution": row.get("aorebirth_resolution") or None,
            }
    return result


def load_playfield_join() -> dict[int, dict[str, object]]:
    return {
        int(row["playfield_id"]): {
            "Name": row.get("all_name") or row.get("tiny_name"),
            "NameConflict": bool(row.get("name_conflict")),
            "Authority": "ARPA3_CLICK_SAVER_ALL_CDB",
        }
        for row in read_json(ARPA_PLAYFIELDS)
    }


def load_mission_type_join() -> dict[int, dict[str, object]]:
    return {
        int(row["MissionIcon"]): {
            "CaptureBackedType": row["AORebirthCaptureBackedType"],
            "CanonicalMissionType": row["CanonicalMissionType"],
            "CanonicalDisplayName": row["CanonicalDisplayName"],
            "MalisDisplayName": row["MalisDisplayName"],
            "Confidence": row["Representation"],
        }
        for row in read_json(MISSION_TYPES)
    }


def legacy_roll_origin(
    session_inputs: dict[str, object] | None,
    terminal_identity: object | None = None,
) -> dict[str, object] | None:
    if not session_inputs:
        return None
    identity = terminal_identity or session_inputs.get("terminal_identity")
    playfield = session_inputs.get("terminal_playfield")
    coordinates = session_inputs.get("terminal_coordinates")
    if identity is None and playfield is None and coordinates is None:
        return None
    return {
        "capture_phase": "LEGACY_SESSION_STARTED",
        "terminal_identity": identity,
        "terminal_playfield_identity": playfield,
        "terminal_local_coordinates": coordinates,
        "provenance": "SCHEMA_VERSION_1_SESSION_ORIGIN_FALLBACK",
    }


def enrich_roll_origin(
    roll_origin: dict[str, object] | None,
    playfield_join: dict[int, object],
) -> dict[str, object] | None:
    if roll_origin is None:
        return None
    result = dict(roll_origin)
    identity = result.get("terminal_playfield_identity")
    if isinstance(identity, dict) and identity.get("instance") is not None:
        result["terminal_playfield_name"] = playfield_join.get(
            int(identity["instance"])
        )
    return result


def enrich_mission_destination(
    destination: dict[str, object],
    playfield_join: dict[int, object],
) -> dict[str, object]:
    result = dict(destination)
    identity = result.get("playfield_identity")
    if isinstance(identity, dict) and identity.get("instance") is not None:
        result["playfield_name"] = playfield_join.get(int(identity["instance"]))
    return result


def normalize_events(events_path: Path, output_dir: Path) -> dict[str, object]:
    events = [json.loads(line) for line in events_path.read_text(encoding="utf-8").splitlines() if line.strip()]
    item_join = load_item_join()
    playfield_join = load_playfield_join()
    mission_type_join = load_mission_type_join()
    session: dict[str, object] | None = None
    requests: dict[str, dict[str, object]] = {}
    offers: list[dict[str, object]] = []
    fingerprints: set[str] = set()
    duplicate_count = 0
    for sequence, event in enumerate(events, start=1):
        event_type = event["event_type"]
        payload = event.get("payload", {})
        if event_type == "session_started":
            session = {
                "SessionId": event["session_id"],
                "StartedAtUtc": event["timestamp_utc"],
                "StoppedAtUtc": None,
                "Status": "PARTIAL_OR_CRASHED",
                "Inputs": payload,
                "RawEventSource": events_path.as_posix(),
            }
        elif event_type == "request_started":
            session_inputs = session.get("Inputs") if session is not None else None
            requests[event["request_id"]] = {
                "SessionId": event["session_id"],
                "RequestId": event["request_id"],
                "StartedAtUtc": event["timestamp_utc"],
                "Status": "PARTIAL_NO_COHORT",
                "CohortOfferCount": 0,
                "Inputs": payload,
                "RollOrigin": payload.get("roll_origin")
                or legacy_roll_origin(session_inputs),
                "SliderStateId": payload.get("slider_state_id"),
                "SliderPreset": payload.get("slider_preset"),
                "RequestedSemanticState": payload.get("requested_semantic_state"),
                "OutboundRawPackets": [],
                "InboundRawPackets": [],
                "Errors": [],
            }
        elif event_type == "request_transmitted":
            request = requests[event["request_id"]]
            request["OutboundRawPackets"].append(payload.get("raw_packet"))
            request["TransmittedNativeValues"] = payload.get("transmitted_native_values")
            request["TransmissionVerificationStatus"] = payload.get("verification_status")
        elif event_type == "raw_response_received":
            request = requests[event["request_id"]]
            request["InboundRawPackets"].append(payload.get("raw_packet"))
            request["RawResponseAssociationStatus"] = payload.get("association_status")
        elif event_type == "cohort_received":
            fingerprint = str(payload.get("callback_fingerprint") or "")
            if fingerprint and fingerprint in fingerprints:
                duplicate_count += 1
                continue
            if fingerprint:
                fingerprints.add(fingerprint)
            request = requests[event["request_id"]]
            request["CompletedAtUtc"] = event["timestamp_utc"]
            request["Status"] = "COMPLETE_FIVE_OFFER_COHORT" if len(payload["offers"]) == 5 else "COMPLETE_NONSTANDARD_COHORT"
            request["CohortOfferCount"] = len(payload["offers"])
            request["CohortId"] = payload.get("cohort_id")
            request["SliderStateId"] = payload.get("slider_state_id") or request.get("SliderStateId")
            request["RequestedSemanticState"] = payload.get("requested_semantic_state") or request.get("RequestedSemanticState")
            request["ResponseEnvelope"] = payload.get("message_envelope")
            request["ReturnedSliders"] = payload.get("returned_sliders") or payload.get("sliders")
            request["SliderVerification"] = payload.get("slider_verification")
            request["RawResponsePacket"] = payload.get("raw_response_packet")
            request["RollOrigin"] = payload.get("roll_origin") or request.get("RollOrigin")
            for raw_offer in payload["offers"]:
                session_inputs = session.get("Inputs") if session is not None else None
                roll_origin = (
                    raw_offer.get("roll_origin")
                    or request.get("RollOrigin")
                    or legacy_roll_origin(
                        session_inputs, raw_offer.get("terminal_identity")
                    )
                )
                roll_origin = enrich_roll_origin(roll_origin, playfield_join)
                mission_destination = raw_offer.get("mission_destination") or {
                    "playfield_identity": raw_offer.get("playfield"),
                    "coordinates": raw_offer.get("location"),
                    "availability": "LEGACY_DIRECT_AOSHARP_MISSIONINFO_FIELDS",
                }
                mission_destination = enrich_mission_destination(
                    mission_destination, playfield_join
                )
                rewards = []
                raw_rewards = raw_offer.get("reward_items")
                if raw_rewards is None:
                    raw_rewards = raw_offer.get("mission_items", [])
                for raw_reward in raw_rewards:
                    identity = int(raw_reward["low_id"])
                    high_identity = int(raw_reward["high_id"])
                    rewards.append(
                        {
                            "RawIdentity": {"LowId": identity, "HighId": high_identity},
                            "RewardQl": int(raw_reward["ql"]),
                            "Unknown": raw_reward.get("unknown"),
                            "IdentitySemantics": raw_reward.get("identity_semantics"),
                            "OfflineLowIdJoin": item_join.get(identity, {"AORebirthResolution": "UNRESOLVED"}),
                            "OfflineHighIdJoin": item_join.get(high_identity, {"AORebirthResolution": "UNRESOLVED"}),
                        }
                    )
                offers.append(
                    {
                        "SessionId": event["session_id"],
                        "RequestId": event["request_id"],
                        "CohortId": raw_offer.get("cohort_id") or payload.get("cohort_id"),
                        "SliderStateId": raw_offer.get("slider_state_id") or payload.get("slider_state_id"),
                        "OfferId": f'{event["request_id"]}/offer/{int(raw_offer["offer_index"]):02d}',
                        "OfferIndex": int(raw_offer["offer_index"]),
                        "ObservedAtUtc": event["timestamp_utc"],
                        "MissionIdentity": raw_offer.get("mission_identity"),
                        "MissionQl": None,
                        "MissionQlAvailability": "NOT_EXPOSED_BY_AOSHARP_MISSIONINFO_1_0_106",
                        "PlannedTargetMissionQl": request["Inputs"].get("static_expected_mission_ql", request["Inputs"].get("target_mission_ql")),
                        "MissionType": mission_type_join.get(int(raw_offer["mission_icon"])),
                        "MissionTemplateOrTypeId": None,
                        "MissionIcon": raw_offer.get("mission_icon"),
                        "Title": raw_offer.get("title"),
                        "Description": raw_offer.get("description"),
                        "DescriptionIdentifiers": None,
                        "TerminalIdentity": raw_offer.get("terminal_identity"),
                        "RollOrigin": roll_origin,
                        "RewardDescriptorVersion": raw_offer.get("reward_descriptor_version"),
                        "Rewards": rewards,
                        "ObjectiveItemIdentity": None,
                        "ObjectiveItemQl": None,
                        "ObjectiveType": None,
                        "Credits": raw_offer.get("credits"),
                        "XpReward": raw_offer.get("xp_reward"),
                        "TokenReward": None,
                        "PlayfieldIdentity": raw_offer.get("playfield"),
                        "PlayfieldName": playfield_join.get(int(raw_offer["playfield"]["instance"])),
                        "Location": raw_offer.get("location"),
                        "MissionDestination": mission_destination,
                        "RawMissionTypeEvidence": raw_offer.get("mission_type"),
                        "DestinationEntranceIdentity": None,
                        "FactionRequirements": None,
                        "UnknownFields": raw_offer.get("unknown_fields", {}),
                        "RawOffer": raw_offer,
                    }
                )
        elif event_type == "duplicate_callback":
            duplicate_count += 1
        elif event_type == "request_timeout" and event.get("request_id") in requests:
            requests[event["request_id"]]["Status"] = "FAILED_CLOSED_REQUEST_TIMEOUT"
            requests[event["request_id"]]["Errors"].append(payload)
        elif event_type == "error" and event.get("request_id") in requests:
            requests[event["request_id"]]["Errors"].append(payload)
            if payload.get("disposition") == "FAILED_CLOSED_NO_FURTHER_REQUESTS":
                requests[event["request_id"]]["Status"] = "FAILED_CLOSED"
        elif event_type == "session_stopped" and session is not None:
            session["StoppedAtUtc"] = event["timestamp_utc"]
            session["Status"] = "STOPPED"
            session["StopReason"] = payload.get("reason")
    if session is None:
        raise ValueError("session_started event is required")
    session["RequestCount"] = len(requests)
    session["CompleteCohortCount"] = sum(1 for row in requests.values() if str(row["Status"]).startswith("COMPLETE_"))
    session["OfferCount"] = len(offers)
    session["DuplicateCallbackCount"] = duplicate_count
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / "capture_session.jsonl").write_text(json_line(session), encoding="utf-8", newline="\n")
    (output_dir / "mission_request.jsonl").write_text(
        "".join(json_line(requests[key]) for key in sorted(requests)), encoding="utf-8", newline="\n"
    )
    (output_dir / "mission_offer.jsonl").write_text(
        "".join(json_line(row) for row in offers), encoding="utf-8", newline="\n"
    )
    return {
        "Session": session,
        "Requests": [requests[key] for key in sorted(requests)],
        "Offers": offers,
    }


def load_observation_counts() -> tuple[Counter[int], Counter[int], Counter[tuple[int, int, int]]]:
    by_ql: Counter[int] = Counter()
    by_level: Counter[int] = Counter()
    by_edge: Counter[tuple[int, int, int]] = Counter()
    index = read_json(SESSION_INDEX)
    for entry in index["Sessions"]:
        requests: dict[str, tuple[int, int]] = {}
        request_path = ROOT / entry["NormalizedRequestJsonl"]
        with request_path.open("r", encoding="utf-8") as stream:
            for line in stream:
                request = json.loads(line)
                inputs = request.get("Inputs", {})
                mission_ql = inputs.get("static_expected_mission_ql", inputs.get("target_mission_ql"))
                difficulty = inputs.get("difficulty_detent", inputs.get("difficulty_slot"))
                if mission_ql is not None and difficulty is not None:
                    requests[request["RequestId"]] = (
                        int(difficulty),
                        int(mission_ql),
                    )
        offer_path = ROOT / entry["NormalizedOfferJsonl"]
        session_level = int(entry["CharacterLevel"])
        with offer_path.open("r", encoding="utf-8") as stream:
            for line in stream:
                offer = json.loads(line)
                ql = offer.get("PlannedTargetMissionQl")
                request_input = requests.get(offer["RequestId"])
                slot = request_input[0] if request_input else None
                if ql is None and request_input:
                    ql = request_input[1]
                if ql is not None:
                    by_ql[int(ql)] += 1
                    by_level[session_level] += 1
                    if slot is not None:
                        by_edge[(session_level, int(slot), int(ql))] += 1
    return by_ql, by_level, by_edge


def readiness(offers: int) -> list[str]:
    if offers < 100:
        stage = "LOW_SAMPLE"
    elif offers < 1000:
        stage = "EXPANDING"
    elif offers < 5000:
        stage = "STABILIZING"
    else:
        stage = "SATURATED_FOR_DISCOVERY"
    return [stage, "DISTRIBUTION_NOT_PROVEN"]


def build_statistical_readiness() -> dict[str, object]:
    observations: dict[int, dict[str, object]] = {
        ql: {
            "request_ids": set(),
            "complete_request_ids": set(),
            "offers": [],
            "reward_keys": [],
            "mission_icons": set(),
            "playfields": set(),
            "objective_keys": set(),
        }
        for ql in range(1, 251)
    }
    index = read_json(SESSION_INDEX)
    for entry in index["Sessions"]:
        request_path = ROOT / entry["NormalizedRequestJsonl"]
        request_qls: dict[str, int] = {}
        with request_path.open("r", encoding="utf-8") as stream:
            for line in stream:
                request = json.loads(line)
                inputs = request.get("Inputs", {})
                ql = inputs.get("static_expected_mission_ql", inputs.get("target_mission_ql"))
                if ql is None:
                    continue
                ql = int(ql)
                request_qls[request["RequestId"]] = ql
                observations[ql]["request_ids"].add(request["RequestId"])
                if str(request.get("Status", "")).startswith("COMPLETE_"):
                    observations[ql]["complete_request_ids"].add(request["RequestId"])
        offer_path = ROOT / entry["NormalizedOfferJsonl"]
        with offer_path.open("r", encoding="utf-8") as stream:
            for line in stream:
                offer = json.loads(line)
                ql = offer.get("PlannedTargetMissionQl") or request_qls.get(offer["RequestId"])
                if ql is None:
                    continue
                ql = int(ql)
                record = observations[ql]
                record["offers"].append(offer)
                if offer.get("MissionIcon") is not None:
                    record["mission_icons"].add(int(offer["MissionIcon"]))
                playfield = offer.get("PlayfieldIdentity") or {}
                if playfield.get("instance") is not None:
                    record["playfields"].add(int(playfield["instance"]))
                for reward in offer.get("Rewards", []):
                    raw = reward["RawIdentity"]
                    record["reward_keys"].append((int(raw["LowId"]), int(raw["HighId"])))
                objective = offer.get("ObjectiveItemIdentity")
                if objective:
                    record["objective_keys"].add(json.dumps(objective, sort_keys=True))

    rows = []
    for ql in range(1, 251):
        record = observations[ql]
        offers = record["offers"]
        rewards = record["reward_keys"]
        window = min(1000, len(rewards))
        recent_new = len(set(rewards[-window:]) - set(rewards[:-window])) if window else 0
        stability = None
        if len(rewards) >= 200:
            midpoint = len(rewards) // 2
            left = Counter(rewards[:midpoint])
            right = Counter(rewards[midpoint:])
            keys = set(left) | set(right)
            stability = round(
                0.5 * sum(abs(left[key] / midpoint - right[key] / (len(rewards) - midpoint)) for key in keys),
                8,
            )
        labels = readiness(len(offers))
        if len(offers) >= 5000 and recent_new > 0:
            labels[0] = "STABILIZING"
        rows.append(
            {
                "MissionQl": ql,
                "RequestAttemptCount": len(record["request_ids"]),
                "CompleteCohortCount": len(record["complete_request_ids"]),
                "OfferCount": len(offers),
                "RewardObservationCount": len(rewards),
                "UniqueRewardIdentityPairCount": len(set(rewards)),
                "UniqueMissionIconCount": len(record["mission_icons"]),
                "UniqueDestinationPlayfieldCount": len(record["playfields"]),
                "UniqueObjectiveIdentityCount": len(record["objective_keys"]),
                "RecentRewardObservationWindow": window,
                "NewRewardIdentitiesInRecentWindow": recent_new,
                "RewardFrequencyHalfSplitTotalVariation": stability,
                "ReadinessLabels": labels,
            }
        )
    return {
        "SchemaVersion": SCHEMA_VERSION,
        "CountUnits": ["requests", "complete cohorts", "offers", "reward observations", "objective observations"],
        "ProbabilityCaution": "Observed output frequencies and stability diagnostics are not Funcom internal RNG weights or proven server probabilities.",
        "MissionQls": rows,
    }


def build_targets(graph: list[dict[str, object]], coverage: dict[int, set[int]]) -> tuple[list[dict[str, object]], dict[str, object]]:
    by_ql, by_level, by_edge = load_observation_counts()
    level_count_by_ql: Counter[int] = Counter()
    for values in coverage.values():
        level_count_by_ql.update(values)
    scored = []
    for edge in graph:
        level = int(edge["CharacterLevel"])
        if level < 2:
            continue
        slot = int(edge["DifficultySlot"])
        ql = int(edge["SelectedPlanningMissionQl"])
        reasons = []
        score = 0
        if "SOURCE_DISAGREEMENT" in edge["Classifications"] or "MISSING" in edge["Classifications"]:
            score += 100
            reasons.append("Malis and AORebirth disagree or the Malis cell is missing")
        if level in SPECIAL_REASONS:
            score += 45
            reasons.append(SPECIAL_REASONS[level])
        scarcity = level_count_by_ql[ql]
        if scarcity <= 2:
            score += 60
            reasons.append(f"QL{ql} is represented by only {scarcity} known character levels")
        elif scarcity <= 5:
            score += 30
            reasons.append(f"QL{ql} is sparsely represented by {scarcity} known character levels")
        observed = by_edge[(level, slot, ql)]
        if observed == 0:
            score += 25
            reasons.append("no indexed modern live offers exist for this exact planned edge")
        if not reasons:
            continue
        recommendation = 250 if score >= 160 else 100 if score >= 100 else 50
        substitutes = sorted(other for other, values in coverage.items() if other != level and ql in values)
        scored.append(
            {
                "CharacterLevel": level,
                "DifficultySlot": slot,
                "TargetMissionQl": ql,
                "PriorityScore": score,
                "ExistingOfferCountForExactEdge": observed,
                "ExistingOfferCountForMissionQl": by_ql[ql],
                "ExistingOfferCountForCharacterLevel": by_level[level],
                "RecommendedAdditionalOffers": recommendation,
                "RecommendedAdditionalRequestsAssumingFiveOffers": math.ceil(recommendation / 5),
                "Purpose": "SPECIFIC_HYPOTHESIS" if score >= 100 else "BROAD_COVERAGE",
                "Reasons": reasons,
                "ReachabilityConfidence": edge["Classifications"],
                "SubstituteCharacterLevels": substitutes,
                "ModernAccessStatus": edge["LiveUsabilityStatus"],
                "BlockedInModernAo": False,
                "Readiness": readiness(by_ql[ql]),
            }
        )
    scored.sort(key=lambda row: (-row["PriorityScore"], row["ExistingOfferCountForExactEdge"], row["CharacterLevel"], row["DifficultySlot"]))
    high = {
        "SchemaVersion": SCHEMA_VERSION,
        "HighValueCharacterLevels": [
            {"CharacterLevel": level, "Reason": SPECIAL_REASONS[level]} for level in SPECIAL_LEVELS
        ],
        "HighValueMissionQls": [
            {
                "MissionQl": ql,
                "CandidateCharacterLevelCount": level_count_by_ql[ql],
                "Reason": "unique or sparse static reachability" if level_count_by_ql[ql] <= 5 else "special/disputed edge",
            }
            for ql in sorted({int(row["TargetMissionQl"]) for row in scored[:100]})
        ],
    }
    return scored, high


def pick_edges(graph: list[dict[str, object]], levels: tuple[int, ...], predicate) -> list[dict[str, int]]:
    rows = []
    for edge in graph:
        if int(edge["CharacterLevel"]) in levels and predicate(edge):
            rows.append(
                {
                    "CharacterLevel": int(edge["CharacterLevel"]),
                    "DifficultySlot": int(edge["DifficultySlot"]),
                    "TargetMissionQl": int(edge["SelectedPlanningMissionQl"]),
                }
            )
    return rows


def campaign(graph: list[dict[str, object]], set_cover: dict[str, object]) -> dict[str, object]:
    disagreement = lambda edge: "SOURCE_DISAGREEMENT" in edge["Classifications"] or "MISSING" in edge["Classifications"]
    control_edges: list[dict[str, int]] = []
    for target_ql in (200, 80, 100, 60):
        candidates = pick_edges(
            graph,
            SPECIAL_LEVELS,
            lambda edge, ql=target_ql: int(edge["SelectedPlanningMissionQl"]) == ql,
        )
        distinct_levels = []
        for candidate in candidates:
            if candidate["CharacterLevel"] not in {row["CharacterLevel"] for row in distinct_levels}:
                distinct_levels.append(candidate)
        if len(distinct_levels) >= 2:
            control_edges.extend(distinct_levels[:2])
        if len(control_edges) >= 4:
            break
    waves = [
        ("Wave 1 - reachability validation", pick_edges(graph, (2,), lambda edge: True), 5,
         "Validate that a terminal-capable level-2 character can request all table slots, especially QL1."),
        ("Wave 2 - disputed low-level cells", pick_edges(graph, (10, 12, 13), disagreement), 10,
         "Resolve the first and densest Malis versus canonical table disagreements."),
        ("Wave 3 - corrected historical boundaries", pick_edges(graph, (52, 53, 54, 60, 80), lambda edge: int(edge["DifficultySlot"]) == 11), 10,
         "Retest known difficulty-11 corrections, including the level-80 QL143 boundary."),
        ("Wave 4 - QL200 and above-200 behavior", pick_edges(graph, (200, 201, 209, 219, 220), lambda edge: int(edge["SelectedPlanningMissionQl"]) >= 200), 10,
         "Test QL200 controls and distinguish planned table selection from Malis client filtering above level 200."),
        ("Wave 5 - coarse practical-set sweep", pick_edges(graph, tuple(set_cover["PracticalCaptureSet"]["CharacterLevels"]), lambda edge: int(edge["DifficultySlot"]) in (1, 6, 11)), 5,
         "Acquire modest low/neutral/high cohorts across the 14-level practical validation set before deep sampling."),
        ("Wave 6 - controlled refinement", control_edges, 20,
         "Run one-variable-at-a-time character-level controls at the same target QL; split terminal and faction controls into separate sessions."),
    ]
    output = []
    for name, targets, requests_each, purpose in waves:
        request_count = len(targets) * requests_each
        output.append(
            {
                "Name": name,
                "Targets": targets,
                "RequestsPerTarget": requests_each,
                "ApproximateRequestCount": request_count,
                "ApproximateCohortCountIfAllComplete": request_count,
                "ApproximateOfferCountIfFivePerCohort": request_count * 5,
                "Purpose": purpose,
                "CountStatus": "OPERATIONAL_RECOMMENDATION_NOT_PROOF_THRESHOLD",
            }
        )
    return {"SchemaVersion": SCHEMA_VERSION, "Waves": output}


def harvester_schema() -> dict[str, object]:
    return {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "title": "AORebirth mission offer harvester event",
        "type": "object",
        "required": ["event_type", "schema_version", "session_id", "timestamp_utc", "payload"],
        "properties": {
            "event_type": {"enum": ["session_started", "request_started", "request_transmitted", "raw_response_received", "cohort_received", "request_timeout", "duplicate_callback", "session_stopped", "error"]},
            "schema_version": {"enum": [1, 2, 3]},
            "session_id": {"type": "string"},
            "request_id": {"type": ["string", "null"]},
            "timestamp_utc": {"type": "string"},
            "payload": {"type": "object", "additionalProperties": True},
        },
        "additionalProperties": True,
        "Durability": "one JSON object per line; append and Flush(true) after every event",
        "SemanticBoundary": "schema 3 static_expected_mission_ql is resolved from character level plus explicit difficulty detent; it is not a response-side mission QL",
        "CaptureContractV2": "request-time terminal origin, mission destination, capture-backed mission-icon type, reward-item descriptors, every public MissionInfo field, and every public QuestAlternativeMessage envelope field",
        "CaptureContractV3": "explicit seven-slider state, stable slider-state id, native request readback, exact serialized and transmitted request packets, exact received response packet, returned sliders, and verified request/cohort association",
        "BackwardCompatibility": "schema 1 and 2 journals remain accepted; schema 1 uses session-level roll-origin fallback when terminal location fields are present",
    }


def build_outputs() -> dict[str, str]:
    graph, coverage, access = build_graph()
    cover = build_set_cover(coverage)
    qls = ql_reachability(graph, coverage)
    targets, high = build_targets(graph, coverage)
    first_campaign = campaign(graph, cover)
    class_counts = Counter(value for edge in graph for value in edge["Classifications"])
    reachable = sorted(set().union(*coverage.values()))
    summary = {
        "SchemaVersion": SCHEMA_VERSION,
        "Baselines": {"ARPA3": ARPA_BASELINE, "Malis": MALIS_BASELINE},
        "CharacterLevelDomain": [1, 220],
        "DifficultySlotsPerLevel": 11,
        "CombinedEdgeCount": len(graph),
        "ReachableFromLevels2Through220": {"MissionQlCount": len(reachable), "Minimum": min(reachable), "Maximum": max(reachable), "UncoveredWithin1Through250": sorted(set(range(1, 251)) - set(reachable))},
        "ClassificationCounts": dict(sorted(class_counts.items())),
        "Ql1Reachability": qls["Ql1Investigation"],
        "SetCover": {
            "Mathematical": cover["MathematicalMinimumOrBestProven"],
            "Practical": cover["PracticalCaptureSet"],
        },
        "ModernIndexedLiveSessions": len(read_json(SESSION_INDEX)["Sessions"]),
        "RewardProbabilityInferencePerformed": False,
        "RuntimeMissionLogicChanged": False,
    }
    outputs = {
        "reachability-graph.jsonl": "".join(json_line(row) for row in graph),
        "character-level-coverage.json": canonical_json(character_coverage(coverage, access)),
        "mission-ql-reachability.json": canonical_json(qls),
        "set-cover-solutions.json": canonical_json(cover),
        "high-value-targets.json": canonical_json(high),
        "next-best-capture-targets.json": canonical_json({"SchemaVersion": SCHEMA_VERSION, "Targets": targets}),
        "first-wave-campaign.json": canonical_json(first_campaign),
        "harvester-schema.json": canonical_json(harvester_schema()),
        "statistical-readiness.json": canonical_json(build_statistical_readiness()),
        "analysis-summary.json": canonical_json(summary),
    }
    manifest_files = []
    for name, content in sorted(outputs.items()):
        manifest_files.append({"Path": f"docs/generated/missions/modern-capture/{name}", "Sha256": hashlib.sha256(content.encode("utf-8")).hexdigest()})
    manifest = {
        "SchemaVersion": SCHEMA_VERSION,
        "Inputs": [
            {"Path": path.relative_to(ROOT).as_posix(), "Sha256": sha256(path)}
            for path in (CANONICAL, MALIS, MALIS_COMPARISON, ARPA_OBSERVATIONS, ARPA_ITEMS, ARPA_PLAYFIELDS, MISSION_TYPES, ACCESS, SESSION_INDEX)
        ],
        "GeneratedFiles": manifest_files,
        "Generator": "Tools/modern_mission_capture_planner.py",
        "RuntimeMissionLogicChanged": False,
    }
    outputs["evidence-manifest.json"] = canonical_json(manifest)
    return outputs


def write_outputs(outputs: dict[str, str]) -> None:
    GENERATED.mkdir(parents=True, exist_ok=True)
    for name, content in outputs.items():
        (GENERATED / name).write_text(content, encoding="utf-8", newline="\n")


def check_outputs(outputs: dict[str, str]) -> None:
    stale = []
    for name, expected in outputs.items():
        path = GENERATED / name
        if not path.exists() or path.read_text(encoding="utf-8") != expected:
            stale.append(path.relative_to(ROOT).as_posix())
    if stale:
        raise SystemExit("STALE_GENERATED_ARTIFACTS: " + ", ".join(stale))


def self_test() -> None:
    graph, coverage, access = build_graph()
    assert len(graph) == 2420
    assert access[1]["LiveUsabilityStatus"] == "CHARACTER_LEVEL_1_CAPTURE_BLOCKED"
    assert any(edge["CharacterLevel"] == 2 and edge["SelectedPlanningMissionQl"] == 1 for edge in graph)
    assert any("SOURCE_DISAGREEMENT" in edge["Classifications"] for edge in graph)
    assert any("MISSING" in edge["Classifications"] for edge in graph)
    assert set().union(*coverage.values()) == set(range(1, 251))
    tiny = {2: {1, 2}, 3: {2, 3}, 4: {1, 3}}
    selected, proven, _, _ = exact_or_near_cover({1, 2, 3}, tiny, node_limit=1000)
    assert proven and len(selected) == 2
    with tempfile.TemporaryDirectory(prefix="aorebirth-mission-normalizer-") as temp:
        normalized = normalize_events(FIXTURE, Path(temp))
        assert len(normalized["Requests"]) == 2
        assert normalized["Requests"][1]["Status"] == "PARTIAL_NO_COHORT"
        assert len(normalized["Offers"]) == 5
        assert normalized["Session"]["DuplicateCallbackCount"] == 1
        assert normalized["Offers"][0]["Rewards"][0]["OfflineLowIdJoin"]["Name"] == "Flamethrower Ammunition"
        assert normalized["Offers"][0]["MissionType"]["CaptureBackedType"] == "FindItemReturn"
        assert normalized["Offers"][0]["MissionType"]["CanonicalMissionType"] == "RETURN_ITEM"
        assert normalized["Offers"][0]["MissionType"]["CanonicalDisplayName"] == "Return Item"
        assert normalized["Offers"][0]["PlayfieldName"]["Name"]
        assert normalized["Offers"][0]["RollOrigin"]["provenance"] == "SCHEMA_VERSION_1_SESSION_ORIGIN_FALLBACK"
        assert normalized["Offers"][0]["RollOrigin"]["terminal_identity"]["instance"] == 1234
        assert normalized["Offers"][0]["MissionDestination"]["playfield_name"]["Name"]
        assert normalized["Offers"][4]["UnknownFields"]["FutureUnknown"] == "preserve-me"
        assert normalized["Offers"][0]["MissionQl"] is None
    with tempfile.TemporaryDirectory(prefix="aorebirth-mission-normalizer-v3-") as temp:
        normalized = normalize_events(SCHEMA3_FIXTURE, Path(temp))
        request = normalized["Requests"][0]
        assert request["Status"] == "COMPLETE_NONSTANDARD_COHORT"
        assert request["SliderStateId"] == "slider-state-test"
        assert request["TransmissionVerificationStatus"] == "MATCH"
        assert request["OutboundRawPackets"][0]["sha256"] == "request-sha"
        assert request["InboundRawPackets"][0]["sha256"] == "response-sha"
        assert request["SliderVerification"]["status"] == "MATCH"
        assert normalized["Offers"][0]["CohortId"] == "request-1/cohort/0001"
        assert normalized["Offers"][0]["SliderStateId"] == "slider-state-test"
    first = build_outputs()
    second = build_outputs()
    assert first == second
    print("MODERN_MISSION_CAPTURE_PLANNER_SELF_TEST=PASS")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument("--write", action="store_true")
    modes.add_argument("--check", action="store_true")
    modes.add_argument("--self-test", action="store_true")
    modes.add_argument("--normalize-session", type=Path, metavar="EVENTS_JSONL")
    parser.add_argument("--output-dir", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.self_test:
        self_test()
        return 0
    if args.normalize_session:
        if args.output_dir is None:
            raise SystemExit("--normalize-session requires --output-dir")
        normalize_events(args.normalize_session.resolve(), args.output_dir.resolve())
        print("MODERN_MISSION_CAPTURE_NORMALIZE=PASS")
        return 0
    outputs = build_outputs()
    if args.write:
        write_outputs(outputs)
        print("MODERN_MISSION_CAPTURE_PLANNER_WRITE=PASS")
    else:
        check_outputs(outputs)
        print("MODERN_MISSION_CAPTURE_PLANNER_CHECK=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
