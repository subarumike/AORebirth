"""Read-only original-journal extraction for mission destination distributions.

All historical labels survive unchanged. Current expected QL is a separate
projection through the current MissionLevels.csv; neither is a live decoded QL.
The immutable duplicate audit is a reconciliation contract, not the row source.
"""
import base64
from collections import Counter, defaultdict
import csv
import gzip
import hashlib
import io
import json
from pathlib import Path, PureWindowsPath
import struct
import subprocess
import sys
import types


EVIDENCE_SHA = "f07bb3c1a99218433e7d459c95ba29ccb22c36b2"
SOURCE_MANIFEST = "docs/generated/missions/location-reconciliation/source-manifest.json"
AUDIT_OFFERS = "docs/generated/missions/destination-duplicate-audit/offer-audit-inventory.jsonl.gz"
AUDIT_REQUESTS = "docs/generated/missions/destination-duplicate-audit/request-audit-inventory.jsonl.gz"
ELIGIBILITY = "docs/generated/missions/destination-eligibility-analysis/mission-offer-analysis-inventory.jsonl.gz"
PLACEMENTS = "AORebirth/GameData/Missions/Destinations/MissionEntrancePlacements.json"
LEVELS = "AORebirth/GameData/Missions/Source/MissionLevels.csv"
RETAINED = "docs/reference/missions/modern-capture/level2-slider-discovery/raw"
ORIGINALS = Path("C:/Users/Mike/AppData/Local/AOSharp/MissionOfferHarvester/sessions")
ARCHIVE = Path("D:/AORebirthCaptures/sessions/Mission-harvester")
SLIDERS = ("good_bad", "order_chaos", "open_hidden", "physical_mystical", "headon_stealth", "credits_xp")


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def offer_key(row):
    return tuple(row[field] for field in ("session_id", "source_line", "request_id", "cohort_id", "offer_index"))


def identity(value):
    if not value or value.get("instance") is None:
        return None
    return {"type": value["type"] & 0xffffffff, "instance_uint32": value["instance"] & 0xffffffff}


def sanitize(value):
    """Retain semantic metadata and hashes without copying raw packets or local paths."""
    if isinstance(value, dict):
        return {key: sanitize(item) for key, item in value.items()
                if key not in ("base64", "hex", "raw_hex", "raw_base64", "offers")
                and not key.lower().endswith("base64")
                and not (isinstance(item, str) and (PureWindowsPath(item).is_absolute() or item.startswith("/")))}
    if isinstance(value, list):
        return [sanitize(item) for item in value]
    if isinstance(value, str) and (PureWindowsPath(value).is_absolute() or value.startswith("/")):
        return None
    return value


def extract(root):
    """Return normalized offer rows and complete sanitized request/source metadata."""
    root = Path(root).resolve()
    input_hashes = {}

    def blob(path):
        data = subprocess.check_output(["git", "show", f"{EVIDENCE_SHA}:{path}"], cwd=root)
        input_hashes[path] = sha256(data)
        return data

    def load_rows(path, key):
        result = {}
        with gzip.GzipFile(fileobj=io.BytesIO(blob(path))) as stream:
            for line in stream:
                row = json.loads(line)
                row_key = key(row)
                require(row_key not in result, "Duplicate pinned input key: " + path)
                result[row_key] = row
        return result

    # Execute only pinned repository decoder/audit definitions, never journal content.
    decoder_name = "acgentrance_mission_decoder"
    previous_decoder = sys.modules.get(decoder_name)
    decoder = types.ModuleType(decoder_name)
    decoder.__file__ = str(root / "Tools/acgentrance_mission_decoder.py")
    exec(compile(blob("Tools/acgentrance_mission_decoder.py"), decoder.__file__, "exec"), decoder.__dict__)
    sys.modules[decoder_name] = decoder
    try:
        audit = types.ModuleType("pinned_mission_destination_duplicate_audit")
        audit.__file__ = str(root / "Tools/mission_destination_duplicate_audit.py")
        exec(compile(blob("Tools/mission_destination_duplicate_audit.py"), audit.__file__, "exec"), audit.__dict__)
    finally:
        if previous_decoder is None:
            sys.modules.pop(decoder_name, None)
        else:
            sys.modules[decoder_name] = previous_decoder

    audit_offers = load_rows(AUDIT_OFFERS, offer_key)
    audit_requests = load_rows(AUDIT_REQUESTS, lambda row: (row["session_id"], row["request_id"]))
    eligibility = load_rows(ELIGIBILITY, offer_key)
    require(len(audit_offers) == 93185 and set(audit_offers) == set(eligibility), "Pinned offer populations differ")
    require(len(audit_requests) == 18642, "Pinned request population differs")

    placement_rows = json.loads(blob(PLACEMENTS))["Placements"]
    coordinates = {}
    for placement in placement_rows:
        key = tuple(placement[field] for field in ("PlayfieldId", "LocalXBits", "LocalYBits", "LocalZBits"))
        require(key not in coordinates, "Ambiguous pinned exact placement coordinates")
        coordinates[key] = placement
    require(len(coordinates) == 2242, "Pinned placement population differs")
    level_bytes = (root / LEVELS).read_bytes()
    input_hashes[LEVELS] = sha256(level_bytes)
    levels = {int(row["Level"]): [int(row[f"Q{index}"]) for index in range(11)]
              for row in csv.DictReader(io.StringIO(level_bytes.decode("utf-8-sig")))}
    require(set(levels) == set(range(1, max(levels) + 1)), "Current MissionLevels rows are not contiguous")

    manifest = json.loads(blob(SOURCE_MANIFEST))
    retained_paths = set(subprocess.check_output(
        ["git", "ls-tree", "-r", "--name-only", EVIDENCE_SHA, RETAINED], cwd=root).decode().splitlines())
    require(len(manifest) == 77 and len({row["session_id"] for row in manifest}) == 77,
            "Source manifest must identify 77 unique logical sessions")
    rows, sources, sessions, contexts = [], [], {}, {}
    seen_offers, seen_requests = set(), set()
    event_counts, populations = Counter(), Counter()
    ql_mismatches = defaultdict(lambda: {"offer_count": 0, "request_ids": set()})
    historical_metadata_discrepancies = []
    terminal_positions = defaultdict(lambda: {"request_ids": set(), "offer_count": 0})

    def packet_bytes(packet):
        raw = base64.b64decode(packet["base64"], validate=True)
        require(len(raw) == packet["byte_length"] and sha256(raw) == packet["sha256"],
                "Original packet length/hash mismatch")
        return raw

    for source in manifest:
        sid = source["session_id"]
        retained = f"{RETAINED}/{sid}/events.jsonl"
        if retained in retained_paths:
            data, logical_path, source_kind = blob(retained), retained, "PINNED_REPOSITORY_JOURNAL"
        else:
            candidates = [Path(source["path"]), ORIGINALS / sid / "events.jsonl", ARCHIVE / sid / "events.jsonl"]
            path = next((candidate for candidate in candidates if candidate.is_file()), None)
            require(path is not None, "Original journal is missing: " + sid)
            data = path.read_bytes()
            logical_path = f"original-mission-harvester/{sid}/events.jsonl"
            source_kind = "HASH_VERIFIED_EXTERNAL_JOURNAL"
        require(sha256(data) == source["sha256"] and len(data) == source["bytes"], "Original journal drift: " + sid)
        source_record = {"session_id": sid, "logical_source": logical_path, "source_kind": source_kind,
                         "sha256": source["sha256"], "bytes": len(data), "event_counts": Counter()}
        sources.append(source_record)
        session, request_payloads, response_hashes = None, {}, defaultdict(set)
        for line_number, line in enumerate(io.BytesIO(data), 1):
            require(line.strip(), "Empty original journal row: " + sid)
            event = json.loads(line)
            require(event.get("session_id") == sid, "Original journal session identity mismatch")
            kind, payload, rid = event["event_type"], event.get("payload") or {}, event.get("request_id")
            event_counts[kind] += 1
            source_record["event_counts"][kind] += 1
            if kind == "session_started":
                require(session is None, "Multiple original session headers")
                session = payload
                sessions[sid] = {"source_line": line_number, "timestamp_utc": event.get("timestamp_utc"),
                                 "original_metadata": sanitize(payload)}
            elif kind == "request_started":
                request_key = (sid, rid)
                require(session is not None and request_key not in seen_requests and rid not in contexts
                        and request_key in audit_requests,
                        "Missing/duplicate/unexpected original request")
                seen_requests.add(request_key)
                expected_request = audit_requests[request_key]
                require(expected_request["header_count"] == 1
                        and expected_request["request_payload_sha256"] == [sha256(canonical(payload).encode())],
                        "Original request does not match pinned request audit")
                request_payloads[rid] = payload
                contexts[rid] = {"session_id": sid, "request_id": rid, "source_line": line_number,
                                 "timestamp_utc": event.get("timestamp_utc"), "session_context_id": sid,
                                 "original_request": sanitize(payload), "transmissions": [],
                                 "raw_response_events": [], "cohorts": []}
            elif kind in ("request_transmitted", "raw_response_received"):
                require(rid in contexts, "Packet event without original request")
                packet = payload.get("raw_packet")
                if packet and packet.get("base64") is not None:
                    packet_bytes(packet)
                    if kind == "raw_response_received":
                        response_hashes[rid].add(packet["sha256"])
                field = "transmissions" if kind == "request_transmitted" else "raw_response_events"
                contexts[rid][field].append({"source_line": line_number, "timestamp_utc": event.get("timestamp_utc"),
                                             "original_metadata": sanitize(payload)})
            elif kind == "cohort_received":
                require(session is not None and rid in request_payloads, "Cohort without original session/request")
                request = request_payloads[rid]
                cid = payload.get("cohort_id") or f"{rid}/line/{line_number}"
                offers = payload.get("offers") or []
                require(len(offers) in (0, 5), "Unexpected original cohort size")
                source_hash = sha256(line.rstrip(b"\r\n"))
                source_event_hash = sha256(canonical(event).encode())
                packet = payload.get("raw_response_packet")
                raw, decoded, packet_hash = None, None, None
                if packet:
                    raw = packet_bytes(packet)
                    packet_hash = sha256(raw)
                    require(not response_hashes[rid] or packet_hash in response_hashes[rid],
                            "Cohort packet differs from original raw response event")
                    decoded = decoder.decode_cohort(raw, offers)
                    require(not audit.audit_offer_ranges(raw, decoded["offers"]), "Original decoded offer ranges conflict")
                contexts[rid]["cohorts"].append({"cohort_id": cid, "source_line": line_number,
                                                "source_row_sha256": source_hash, "offer_count": len(offers),
                                                "original_metadata": sanitize(payload)})
                origin = request.get("roll_origin") or payload.get("roll_origin") or session.get("roll_origin") or {}
                terminal = identity(origin.get("terminal_identity") or request.get("terminal_identity") or session.get("terminal_identity"))
                terminal_pf = identity(origin.get("terminal_playfield_identity") or session.get("terminal_playfield"))
                terminal_xyz = origin.get("terminal_local_coordinates") or session.get("terminal_coordinates")
                sliders = request.get("sliders") or payload.get("returned_sliders")
                detent = request.get("difficulty_detent", request.get("difficulty_slot"))
                if detent is None and sliders:
                    detent = sliders.get("difficulty")
                level = request.get("character_level", session.get("character_level"))
                original_ql = request.get("static_expected_mission_ql")
                if original_ql is None:
                    original_ql = session.get("static_expected_mission_ql")
                operator_target_ql = request.get("target_mission_ql", session.get("target_mission_ql"))
                current_ql = levels[min(max(level, 1), max(levels))][detent - 1] if level is not None and detent in range(1, 12) else None
                for ordinal, offer in enumerate(offers):
                    row = {"session_id": sid, "source_line": line_number, "request_id": rid,
                           "cohort_id": cid, "offer_index": offer["offer_index"]}
                    key = offer_key(row)
                    require(key not in seen_offers and key in audit_offers, "Unexpected/duplicate original offer key")
                    seen_offers.add(key)
                    expected, prior = audit_offers[key], eligibility[key]
                    mission_type = (offer.get("mission_type") or {}).get("canonical_type") or audit.MISSION_TYPES.get(offer.get("mission_icon"))
                    row.update({"context_id": rid, "character_level": level, "original_expected_ql": original_ql,
                                "current_expected_ql": current_ql, "difficulty_detent": detent,
                                "original_planner_target_ql": operator_target_ql,
                                "character_level_source": "REQUEST_STARTED.character_level" if "character_level" in request
                                else "SESSION_STARTED.character_level",
                                "original_expected_ql_source": "REQUEST_STARTED.static_expected_mission_ql"
                                if request.get("static_expected_mission_ql") is not None else "SESSION_STARTED.static_expected_mission_ql"
                                if session.get("static_expected_mission_ql") is not None else "UNAVAILABLE",
                                "current_expected_ql_source": "CURRENT_MISSION_LEVELS_LOOKUP_FROM_ORIGINAL_LEVEL_AND_DETENT"
                                if current_ql is not None else "UNAVAILABLE_ORIGINAL_LEVEL_OR_DETENT",
                                "retained_eligibility_character_level": prior["character_level"],
                                "retained_eligibility_expected_ql": prior["static_expected_mission_ql"],
                                "ql_label_status": "ORIGINAL_STATIC_LABEL_UNAVAILABLE" if original_ql is None
                                else "MATCH" if original_ql == current_ql else "ORIGINAL_LABEL_DIFFERS_FROM_CURRENT_LOOKUP",
                                "raw_sliders": sliders, "secondary_slider_bytes": [sliders.get(name) for name in SLIDERS],
                                "faction_side_raw": session.get("faction_side_raw"), "breed_raw": session.get("breed_raw"),
                                "profession_raw": session.get("profession_raw"), "terminal_playfield": terminal_pf["instance_uint32"],
                                "terminal_identity": terminal, "terminal_coordinates": terminal_xyz,
                                "terminal_name": origin.get("terminal_name"), "mission_type": mission_type,
                                "destination_playfield": None, "destination_name": None, "destination_identity": None,
                                "destination_local_xyz": None, "destination_xyz_bits": None, "world_offsets_xz": None,
                                "population": "NO_RAW_DESTINATION_UNRESOLVED", "source_row_sha256": source_hash,
                                "raw_response_sha256": packet_hash, "raw_offer_sha256": None,
                                "offer_start": None, "offer_end": None, "worldpos_offset": None,
                                "journal_destination": sanitize(offer.get("mission_destination")),
                                "journal_playfield": identity(offer.get("playfield")), "journal_location": offer.get("location")})
                    require(source_hash == expected["source_row_sha256"]
                            and source_event_hash == expected["source_event_sha256"],
                            "Original cohort source hashes differ from audit")
                    for field, other in (("character_level", "character_level"), ("original_expected_ql", "static_expected_mission_ql"),
                                         ("difficulty_detent", "difficulty_detent"), ("raw_sliders", "raw_sliders"),
                                         ("faction_side_raw", "faction_side_raw"), ("breed_raw", "breed_raw"),
                                         ("profession_raw", "profession_raw"), ("terminal_identity", "mission_terminal_identity"),
                                         ("terminal_coordinates", "terminal_coordinates"), ("mission_type", "mission_type")):
                        if (field == "character_level" and row[field] != prior[other]
                                and prior[other] is None and "character_level" not in request
                                and not expected["raw_byte_range_verified"]
                                and row[field] == session.get("character_level") and row[field] is not None):
                            historical_metadata_discrepancies.append({"key": list(key), "field": field,
                                "original_session_value": row[field], "retained_eligibility_value": prior[other],
                                "reason": "Historical trimmed request inserted a null level and shadowed the original session level."})
                            continue
                        require(row[field] == prior[other], "Original condition differs from retained eligibility: "
                                + canonical({"key": key, "field": field, "original": row[field], "retained": prior[other],
                                             "request_level_present": "character_level" in request,
                                             "request_level": request.get("character_level"), "session_level": session.get("character_level")}))
                    require(row["terminal_playfield"] == prior["terminal_playfield"]["instance_uint32"], "Terminal playfield provenance mismatch")
                    if decoded is not None:
                        item = decoded["offers"][ordinal]
                        world = item["worldpos"]
                        start, end = item["offer_start"], item["offer_end"]
                        xyz_bits = struct.unpack(">III", bytes.fromhex(world["local_float32_hex"]))
                        coordinate_key = (world["playfield_identity"]["instance"], *xyz_bits)
                        require(coordinate_key in coordinates, "Original WorldPos lacks exact placement")
                        placement = coordinates[coordinate_key]
                        destination_id = {"type": placement["IdentityType"], "instance_uint32": placement["IdentityInstance"]}
                        require(expected["raw_byte_range_verified"] and packet_hash == expected["raw_response_sha256"]
                                and start == expected["offer_start"] and end == expected["offer_end"]
                                and item["worldpos_offset"] == expected["worldpos_offset"]
                                and sha256(raw[start:end]) == expected["raw_offer_hash"]
                                and world == expected["destination_worldpos"]
                                and destination_id == identity(expected["destination_identity"])
                                and destination_id == prior["destination_identity"], "Original raw offer disagrees with pinned exact audit")
                        row.update({"population": "RAW_BACKED_EXACT_DESTINATION", "destination_identity": destination_id,
                                    "destination_playfield": placement["PlayfieldId"], "destination_name": placement["DisplayName"],
                                    "destination_local_xyz": world["local_position"], "destination_xyz_bits": list(xyz_bits),
                                    "world_offsets_xz": world["playfield_origin_integer_xz"],
                                    "raw_offer_sha256": sha256(raw[start:end]), "offer_start": start, "offer_end": end,
                                    "worldpos_offset": item["worldpos_offset"]})
                    else:
                        require(not expected["raw_byte_range_verified"] and expected["destination_identity"] is None
                                and prior["destination_identity"] is None, "Missing-raw observation was promoted")
                    populations[row["population"]] += 1
                    rows.append(row)
                    if original_ql is not None and original_ql != current_ql:
                        mismatch = ql_mismatches[(level, detent, original_ql, current_ql)]
                        mismatch["offer_count"] += 1
                        mismatch["request_ids"].add(rid)
                    terminal_key = (terminal_pf["instance_uint32"], terminal["type"], terminal["instance_uint32"], canonical(terminal_xyz))
                    terminal_positions[terminal_key]["request_ids"].add(rid)
                    terminal_positions[terminal_key]["offer_count"] += 1
        require(source_record["event_counts"] == Counter(source["events"]), "Original source event totals changed: " + sid)
    require(seen_offers == set(audit_offers) and seen_requests == set(audit_requests), "Original corpus join is incomplete")
    require(populations == {"RAW_BACKED_EXACT_DESTINATION": 92830, "NO_RAW_DESTINATION_UNRESOLVED": 355}, "Original offer populations differ")
    require(len(historical_metadata_discrepancies) == 355, "Unexpected historical level-shadowing discrepancy population")
    require(event_counts["cohort_received"] == 18638, "Original cohort count differs")
    rows.sort(key=offer_key)
    metadata = {"schema_version": 1, "source_evidence_commit": EVIDENCE_SHA, "input_hashes": input_hashes,
                "sources": sources, "session_contexts": sessions, "contexts": contexts,
                "extraction_discrepancies": historical_metadata_discrepancies,
                "counts": {"sessions": len(sources), "requests": len(seen_requests), "cohorts": event_counts["cohort_received"],
                           "offers": len(rows), "events": sum(event_counts.values()), "event_types": dict(event_counts),
                           "populations": dict(populations)},
                "ql_label_mismatches": [{"character_level": key[0], "difficulty_detent": key[1],
                                         "original_expected_ql": key[2], "current_expected_ql": key[3],
                                         "offer_count": value["offer_count"], "request_ids": sorted(value["request_ids"])}
                                        for key, value in sorted(ql_mismatches.items(), key=lambda pair: canonical(pair[0]))],
                "terminal_positions": [{"terminal_playfield": key[0], "terminal_identity": {"type": key[1], "instance_uint32": key[2]},
                                        "terminal_coordinates": json.loads(key[3]), "offer_count": value["offer_count"],
                                        "request_count": len(value["request_ids"])}
                                       for key, value in sorted(terminal_positions.items())],
                "interpretation": ["Original expected QL and current lookup QL are separate; neither is a decoded response QL.",
                                   "Operator target QL remains separate and never substitutes for a missing static expected-QL label.",
                                   "Repeated source offers remain separate observations; no destination or cohort deduplication.",
                                   "Missing-raw destinations remain unresolved even when the journal retains decoded diagnostic fields.",
                                   "Observation frequencies are descriptive, not retail generator probabilities."]}
    return rows, metadata
