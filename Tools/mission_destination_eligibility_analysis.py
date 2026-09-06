"""Evidence-only analysis of resolved mission destinations and captured conditions."""
import argparse
import gzip
import hashlib
import io
import json
import math
import pathlib
from collections import Counter, defaultdict

ROOT = pathlib.Path(__file__).resolve().parents[1]
ACG = ROOT / "docs/generated/missions/acgentrance-reconstruction"
PRIOR = ROOT / "docs/generated/missions/location-reconciliation"
OUT = ROOT / "docs/generated/missions/destination-eligibility-analysis"
CHECK = False
GENERATED = {}
SIDE_NAMES = {0: "Neutral", 1: "Clan", 2: "Omni", 3: "Monster", 4: "Advisor", 5: "Guardian", 6: "Gm", 7: "Mixed"}
MISSION_TYPES = {11329: "RETURN_ITEM", 11330: "KILL_PERSON", 11335: "FIND_PERSON", 11337: "FIND_ITEM", 11342: "REPAIR"}
SECONDARY_SLIDERS = ("good_bad", "order_chaos", "open_hidden", "physical_mystical", "headon_stealth", "money_xp")


def sha256(path):
    raw = path.read_bytes()
    try:
        relative = path.resolve().relative_to(ROOT.resolve())
    except ValueError:
        relative = None
    if relative is not None and path.suffix.lower() == ".cmd":
        raw = raw.replace(b"\r\n", b"\n").replace(b"\r", b"\n")
    return hashlib.sha256(raw).hexdigest()


def stable_path(path):
    resolved = path.resolve()
    try:
        return resolved.relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        return str(resolved)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)


def emit(name, data):
    if isinstance(data, str):
        data = data.encode("utf-8")
    GENERATED[name] = hashlib.sha256(data).hexdigest()
    path = OUT / name
    if CHECK:
        if not path.exists() or path.read_bytes() != data:
            raise ValueError("STALE_ARTIFACT: " + name)
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


def emit_json(name, value):
    emit(name, json.dumps(value, indent=2, sort_keys=True, ensure_ascii=True) + "\n")


def emit_jsonl(name, rows):
    buffer = io.BytesIO()
    if name.endswith(".gz"):
        with gzip.GzipFile(filename="", mode="wb", fileobj=buffer, mtime=0) as stream:
            for row in rows:
                stream.write((canonical(row) + "\n").encode("utf-8"))
    else:
        for row in rows:
            buffer.write((canonical(row) + "\n").encode("utf-8"))
    emit(name, buffer.getvalue())


def read_jsonl(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as stream:
        for line in stream:
            yield json.loads(line)


def ident(value):
    if not value or value.get("instance") is None:
        return None
    return {"type": value.get("type"), "instance_uint32": value["instance"] & 0xFFFFFFFF}


def identity_key(value):
    value = ident(value)
    return None if value is None else (value["type"], value["instance_uint32"])


def slider_value(request, cohort, name):
    semantic = request.get("requested_semantic_state") or cohort.get("requested_semantic_state") or {}
    semantic_name = name
    if name == "money_xp" and name not in semantic:
        semantic_name = "credits_xp"
    item = semantic.get(semantic_name)
    if isinstance(item, dict):
        return {"semantic_state": item.get("semantic_state"), "semantic_value": item.get("semantic_value"), "native_raw_value": item.get("native_raw_value")}
    raw = request.get("sliders") or cohort.get("returned_sliders") or {}
    raw_name = "credits_xp" if name == "money_xp" else name
    return {"semantic_state": None, "semantic_value": None, "native_raw_value": raw.get(raw_name)} if raw_name in raw else None


def slider_label(value):
    if value is None:
        return None
    state = value.get("semantic_state")
    if state == "SIGNED_VALUE" and value.get("semantic_value") is not None:
        return "SIGNED_VALUE_{:+d}".format(int(value["semantic_value"]))
    return state or ("RAW_" + str(value.get("native_raw_value")) if value.get("native_raw_value") is not None else None)


def terminal_metadata(session, request, cohort):
    origin = request.get("roll_origin") or cohort.get("roll_origin") or session.get("roll_origin") or {}
    terminal = ident(origin.get("terminal_identity") or request.get("terminal_identity") or session.get("terminal_identity"))
    playfield = ident(origin.get("terminal_playfield_identity") or session.get("terminal_playfield"))
    coordinates = origin.get("terminal_local_coordinates") or session.get("terminal_coordinates")
    return terminal, playfield, coordinates, origin.get("terminal_name")


def load_inputs():
    tool_files = [
        pathlib.Path(__file__).resolve(),
        ROOT / "Tools/mission_destination_eligibility_analysis.cmd",
        ROOT / "Tools/test_mission_destination_eligibility_analysis.py",
    ]
    input_files = [
        ACG / "acgentrance-records.jsonl",
        ACG / "acgentrance-external-catalog-comparison.jsonl",
        ACG / "mission-location-full-corpus-reconciliation.jsonl.gz",
        ACG / "mission-location-level2-reconciliation.jsonl",
        ACG / "mission-location-reconstruction-summary.json",
        PRIOR / "source-manifest.json",
    ]
    inputs = {stable_path(path): sha256(path) for path in tool_files + input_files}
    placements = list(read_jsonl(input_files[0]))
    comparisons = list(read_jsonl(input_files[1]))
    resolved = list(read_jsonl(input_files[2]))
    source_manifest = json.loads(input_files[5].read_text(encoding="utf-8"))
    return inputs, placements, comparisons, resolved, source_manifest


def load_capture_metadata(source_manifest, inputs):
    sessions, requests, offers = {}, {}, {}
    session_order = []
    retained_root = ROOT / "docs/reference/missions/modern-capture/level2-slider-discovery/raw"
    for source in source_manifest:
        sid = source["session_id"]
        retained = retained_root / sid / "events.jsonl"
        path = retained if retained.exists() else pathlib.Path(source["path"])
        actual = sha256(path)
        if actual != source["sha256"]:
            raise ValueError("CAPTURE_SOURCE_DRIFT: " + sid)
        inputs[stable_path(path)] = actual
        session_order.append(sid)
        session_payload = {}
        request_payloads = {}
        with path.open("r", encoding="utf-8") as stream:
            for line_number, line in enumerate(stream, 1):
                event = json.loads(line)
                payload = event.get("payload") or {}
                event_type = event["event_type"]
                if event_type == "session_started":
                    session_payload = {key: payload.get(key) for key in
                                       ("character_surrogate", "character_level", "profession_raw", "breed_raw", "faction_side_raw",
                                        "terminal_identity", "terminal_playfield", "terminal_coordinates", "roll_origin",
                                        "static_expected_mission_ql", "target_mission_ql")}
                elif event_type == "request_started":
                    trimmed_request = {key: payload.get(key) for key in
                                       ("character_level", "difficulty_detent", "difficulty_slot", "static_expected_mission_ql",
                                        "target_mission_ql", "requested_semantic_state", "sliders", "roll_origin", "terminal_identity")}
                    request_payloads[event.get("request_id")] = {"payload": trimmed_request, "timestamp_utc": event.get("timestamp_utc")}
                elif event_type == "cohort_received":
                    rid = event.get("request_id")
                    cohort_id = payload.get("cohort_id") or f"{rid}/line/{line_number}"
                    trimmed_cohort = {key: payload.get(key) for key in
                                      ("requested_semantic_state", "returned_sliders", "mission_ql_candidate", "roll_origin")}
                    for offer in payload.get("offers") or []:
                        key = (sid, line_number, offer["offer_index"])
                        if key in offers:
                            raise ValueError("DUPLICATE_CAPTURE_OFFER_KEY")
                        text_hits = []
                        for field in ("title", "description"):
                            text = offer.get(field)
                            if isinstance(text, str):
                                for token in ("Ænima HQ", "?nima HQ"):
                                    if token in text:
                                        text_hits.append({"field": field, "representation": token})
                        trimmed_offer = {field: offer.get(field) for field in
                                         ("reward_items", "mission_items", "mission_icon", "mission_type", "mission_ql",
                                          "decoded_fields", "objective_type", "not_exposed_fields", "credits", "xp_reward")}
                        trimmed_offer["field_presence"] = {field: field in offer for field in ("reward_items", "mission_items")}
                        trimmed_offer["text_encoding_hits"] = text_hits
                        offers[key] = {"session": session_payload, "request": request_payloads.get(rid, {}).get("payload", {}),
                                       "request_timestamp": request_payloads.get(rid, {}).get("timestamp_utc"),
                                       "cohort": trimmed_cohort, "offer": trimmed_offer, "cohort_id": cohort_id}
        sessions[sid] = session_payload
        requests.update({(sid, rid): value for rid, value in request_payloads.items()})
    return sessions, requests, offers, session_order


def normalize_offers(resolved, event_offers, placement_index):
    rows = []
    unmatched = []
    for sequence, prior in enumerate(resolved, 1):
        key = (prior["session_id"], prior["source_line"], prior["offer_index"])
        event = event_offers.get(key)
        if event is None:
            unmatched.append(key)
            continue
        session, request, cohort, offer = event["session"], event["request"], event["cohort"], event["offer"]
        terminal, terminal_pf, terminal_coords, terminal_name = terminal_metadata(session, request, cohort)
        expected_ql = request.get("static_expected_mission_ql", request.get("target_mission_ql"))
        if expected_ql is None:
            expected_ql = session.get("static_expected_mission_ql", session.get("target_mission_ql"))
        decoded_ql = offer.get("mission_ql")
        if decoded_ql is None:
            decoded_ql = (offer.get("decoded_fields") or {}).get("mission_ql")
        ql_candidate = cohort.get("mission_ql_candidate") or {}
        candidate_value = ql_candidate.get("observed_mission_ql_candidate")
        presence = offer.get("field_presence") or {}
        reward_key = "reward_items" if presence.get("reward_items") else "mission_items" if presence.get("mission_items") else None
        rewards = offer.get(reward_key) or [] if reward_key else []
        mission_icon = offer.get("mission_icon")
        mission_type_data = offer.get("mission_type") or {}
        mission_type = mission_type_data.get("canonical_type") or MISSION_TYPES.get(mission_icon)
        destination_identity = prior.get("resolved_acgentrance_identity")
        destination_key = identity_key(destination_identity)
        placement = placement_index.get(destination_key)
        semantic_sliders = {name: slider_value(request, cohort, name) for name in SECONDARY_SLIDERS}
        raw_sliders = request.get("sliders") or cohort.get("returned_sliders")
        difficulty = request.get("difficulty_detent", request.get("difficulty_slot"))
        if difficulty is None and raw_sliders:
            difficulty = raw_sliders.get("difficulty")
        side_raw = session.get("faction_side_raw")
        destination_pf = placement.get("explicit_playfield_id") if placement else (prior.get("destination", {}).get("playfield_identity") or {}).get("instance")
        local_position = placement.get("raw_position_components") if placement else None
        worldpos = (prior.get("decoder") or {}).get("worldpos") or {}
        text_hits = offer.get("text_encoding_hits") or []
        rows.append({
            "sequence": sequence, "session_id": prior["session_id"], "request_id": prior["request_id"],
            "cohort_id": prior["cohort_id"], "offer_index": prior["offer_index"], "source_line": prior["source_line"],
            "population": "RAW_BACKED_EXACT_DESTINATION" if destination_identity else "NO_RAW_DESTINATION_UNRESOLVED",
            "character_identity_surrogate": session.get("character_surrogate"),
            "character_level": request.get("character_level", session.get("character_level", prior.get("level"))),
            "profession_raw": session.get("profession_raw"), "breed_raw": session.get("breed_raw"),
            "faction_side_raw": side_raw, "faction_side": SIDE_NAMES.get(side_raw) if side_raw is not None else None,
            "mission_terminal_identity": terminal, "terminal_name": terminal_name,
            "terminal_playfield": terminal_pf, "terminal_coordinates": terminal_coords,
            "difficulty_detent": difficulty, "secondary_sliders": semantic_sliders, "raw_sliders": raw_sliders,
            "static_expected_mission_ql": expected_ql, "live_decoded_mission_ql": decoded_ql,
            "mission_ql_candidate": candidate_value, "mission_ql_candidate_status": ql_candidate.get("status"),
            "analysis_mission_ql": decoded_ql if decoded_ql is not None else expected_ql,
            "analysis_mission_ql_source": "LIVE_DECODED_MISSION_QL" if decoded_ql is not None else "STATIC_EXPECTED_MISSION_QL" if expected_ql is not None else None,
            "mission_type": mission_type, "mission_icon": mission_icon,
            "objective_type": (offer.get("objective_type") or (offer.get("not_exposed_fields") or {}).get("objective_type")),
            "reward_list_captured": reward_key is not None, "reward_items": rewards,
            "credits": offer.get("credits"), "xp": offer.get("xp_reward"),
            "destination_playfield": destination_pf,
            "destination_world_offsets_xz": worldpos.get("playfield_origin_integer_xz"),
            "destination_local_xyz": local_position,
            "destination_identity": ident(destination_identity),
            "destination_display_name": placement.get("display_name_exact") if placement else None,
            "request_timestamp_utc": event["request_timestamp"], "text_encoding_hits": text_hits,
        })
    if unmatched or len(rows) != len(resolved):
        raise ValueError(f"CAPTURE_JOIN_INCOMPLETE: matched={len(rows)} unmatched={len(unmatched)}")
    return rows


def coverage(rows):
    checks = {
        "session_id": lambda r: r["session_id"] is not None,
        "request_id": lambda r: r["request_id"] is not None,
        "cohort_id": lambda r: r["cohort_id"] is not None,
        "offer_index": lambda r: r["offer_index"] is not None,
        "character_identity_surrogate": lambda r: r["character_identity_surrogate"] is not None,
        "character_level": lambda r: r["character_level"] is not None,
        "profession": lambda r: r["profession_raw"] is not None,
        "breed": lambda r: r["breed_raw"] is not None,
        "faction_side": lambda r: r["faction_side_raw"] is not None,
        "mission_terminal_identity": lambda r: r["mission_terminal_identity"] is not None,
        "terminal_playfield": lambda r: r["terminal_playfield"] is not None,
        "terminal_coordinates": lambda r: r["terminal_coordinates"] is not None,
        "difficulty_detent": lambda r: r["difficulty_detent"] is not None,
        "all_six_secondary_sliders": lambda r: all(r["secondary_sliders"].get(k) is not None for k in SECONDARY_SLIDERS),
        "static_expected_mission_ql": lambda r: r["static_expected_mission_ql"] is not None,
        "live_decoded_mission_ql": lambda r: r["live_decoded_mission_ql"] is not None,
        "mission_ql_candidate": lambda r: r["mission_ql_candidate"] is not None,
        "mission_type": lambda r: r["mission_type"] is not None,
        "objective_type": lambda r: r["objective_type"] is not None,
        "reward_list_captured": lambda r: r["reward_list_captured"],
        "reward_identity_present": lambda r: bool(r["reward_items"]),
        "reward_ql_present": lambda r: any(item.get("ql") is not None for item in r["reward_items"]),
        "credits": lambda r: r["credits"] is not None,
        "xp": lambda r: r["xp"] is not None,
        "destination_playfield": lambda r: r["destination_playfield"] is not None,
        "world_offsets": lambda r: r["destination_world_offsets_xz"] is not None,
        "local_xyz": lambda r: r["destination_local_xyz"] is not None,
        "resolved_acgentrance_identity": lambda r: r["destination_identity"] is not None,
        "resolved_acgentrance_display_name": lambda r: r["destination_display_name"] is not None,
    }
    return {name: {"available_offers": sum(test(r) for r in rows), "missing_offers": sum(not test(r) for r in rows)} for name, test in checks.items()}


def condition_of(row, include_type=False):
    result = {
        "character_level": row["character_level"], "mission_ql": row["analysis_mission_ql"],
        "mission_ql_source": row["analysis_mission_ql_source"], "faction_side": row["faction_side"],
        "terminal_identity": row["mission_terminal_identity"], "terminal_playfield": row["terminal_playfield"],
        "difficulty_detent": row["difficulty_detent"],
        "secondary_sliders": {name: slider_label(row["secondary_sliders"].get(name)) for name in SECONDARY_SLIDERS},
    }
    if include_type:
        result["mission_type"] = row["mission_type"]
    return result


def summarize_observations(rows, key_fields):
    groups = defaultdict(list)
    for row in rows:
        key = tuple(row[field] if not isinstance(row[field], dict) else canonical(row[field]) for field in key_fields)
        groups[key].append(row)
    output = []
    for key, items in sorted(groups.items(), key=lambda item: canonical(item[0])):
        entry = {field: value for field, value in zip(key_fields, key)}
        entry.update(request_count=len({r["request_id"] for r in items}), offer_count=len(items),
                     first_observed_session=min(r["session_id"] for r in items), last_observed_session=max(r["session_id"] for r in items))
        output.append(entry)
    return output


def wilson(successes, total, z=1.959963984540054):
    if not total:
        return [None, None]
    p = successes / total
    denominator = 1 + z * z / total
    centre = (p + z * z / (2 * total)) / denominator
    half = z * math.sqrt((p * (1 - p) + z * z / (4 * total)) / total) / denominator
    return [centre - half, centre + half]


def discovery(items):
    seen, curve, last_new = set(), [], None
    marks = {5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000}
    for index, item in enumerate(items, 1):
        identity = item["destination_identity"]["instance_uint32"]
        if identity not in seen:
            seen.add(identity)
            last_new = index
        if index in marks:
            curve.append({"offers": index, "unique_destinations": len(seen)})
    if items and len(items) not in marks:
        curve.append({"offers": len(items), "unique_destinations": len(seen)})
    window = min(len(items), max(25, len(items) // 5))
    before = {r["destination_identity"]["instance_uint32"] for r in items[:-window]} if window < len(items) else set()
    new_last = len({r["destination_identity"]["instance_uint32"] for r in items[-window:]} - before) if items else 0
    if len(items) < 50:
        classification = "LOW_SAMPLE"
    elif len(items) >= 500 and new_last == 0:
        classification = "SATURATED_FOR_DISCOVERY"
    elif new_last <= max(1, len(seen) // 100):
        classification = "STABILIZING"
    else:
        classification = "EXPANDING"
    return {"unique_destination_curve": curve, "last_new_destination_position": last_new,
            "tail_window_offers": window, "new_destinations_in_tail_window": new_last, "classification": classification}


def independent_duplicate_probability(counter):
    total = sum(counter.values())
    if total == 0:
        return None
    elementary = [1.0] + [0.0] * 5
    for count in counter.values():
        p = count / total
        for degree in range(5, 0, -1):
            elementary[degree] += elementary[degree - 1] * p
    return 1.0 - math.factorial(5) * elementary[5]


def analyze(rows, placements, comparisons, session_order):
    exact = [row for row in rows if row["population"] == "RAW_BACKED_EXACT_DESTINATION"]
    unresolved = [row for row in rows if row["population"] == "NO_RAW_DESTINATION_UNRESOLVED"]
    # The prior row flag is represented by the fixed 270-offer level-2 file; use its exact keys.
    level2_keys = {(r["session_id"], r["source_line"], r["offer_index"]) for r in read_jsonl(ACG / "mission-location-level2-reconciliation.jsonl")}
    level2 = [r for r in exact if (r["session_id"], r["source_line"], r["offer_index"]) in level2_keys]
    placement_by_id = {r["identity_instance_uint32"]: r for r in placements}

    ql_counts = Counter(r["analysis_mission_ql"] for r in exact if r["analysis_mission_ql"] is not None)
    ql_combo = defaultdict(list)
    for row in exact:
        if row["analysis_mission_ql"] is not None:
            ql_combo[(row["destination_identity"]["instance_uint32"], row["analysis_mission_ql"])].append(row)
    def ql_matrix_rows():
        for ql in range(1, 251):
            for placement in placements:
                did = placement["identity_instance_uint32"]
                items = ql_combo.get((did, ql), [])
                classification = "OBSERVED" if items else "NOT_YET_OBSERVED" if ql_counts[ql] else "NO_CAPTURE_COVERAGE"
                yield {"identity_type": placement["identity_type"], "identity_instance": did,
                       "identity_instance_hex": placement["identity_instance_hex"], "display_name": placement["display_name_exact"],
                       "destination_playfield": placement["explicit_playfield_id"], "mission_ql": ql,
                       "mission_ql_source": "STATIC_EXPECTED_MISSION_QL", "classification": classification,
                       "request_count": len({r["request_id"] for r in items}), "offer_count": len(items),
                       "first_observed_session": min((r["session_id"] for r in items), default=None),
                       "last_observed_session": max((r["session_id"] for r in items), default=None)}
    ql_matrix = ql_matrix_rows()

    condition_groups = defaultdict(list)
    for row in exact:
        condition_groups[(row["destination_identity"]["instance_uint32"], canonical(condition_of(row, True)))].append(row)
    condition_matrix = []
    for (did, condition_json), items in sorted(condition_groups.items()):
        placement = placement_by_id[did]
        condition_matrix.append({"identity_type": placement["identity_type"], "identity_instance": did,
                                 "identity_instance_hex": placement["identity_instance_hex"], "display_name": placement["display_name_exact"],
                                 "destination_playfield": placement["explicit_playfield_id"], "experimental_condition": json.loads(condition_json),
                                 "classification": "OBSERVED_ELIGIBLE_UNDER_CAPTURED_CONDITIONS",
                                 "request_count": len({r["request_id"] for r in items}), "offer_count": len(items),
                                 "first_observed_session": min(r["session_id"] for r in items), "last_observed_session": max(r["session_id"] for r in items)})
    print("ANALYSIS_PHASE=condition_matrix", flush=True)

    level_summary = []
    for level, items in sorted(group_by(exact, lambda r: r["character_level"]).items(), key=lambda pair: (pair[0] is None, pair[0])):
        level_summary.append({"character_level": level, "request_count": len({r["request_id"] for r in items}), "offer_count": len(items),
                              "unique_destinations": len({r["destination_identity"]["instance_uint32"] for r in items}),
                              "unique_destination_playfields": len({r["destination_playfield"] for r in items}),
                              "mission_qls": sorted({r["analysis_mission_ql"] for r in items if r["analysis_mission_ql"] is not None}),
                              "terminals": [json.loads(value) for value in sorted({canonical(r["mission_terminal_identity"]) for r in items if r["mission_terminal_identity"]})],
                              "slider_states": len({canonical(r["secondary_sliders"]) for r in items})})
    level_destination = observed_matrix(exact, "character_level", "destination_identity", "LEVEL_ASSOCIATION_OBSERVED")
    level_playfield = observed_matrix(exact, "character_level", "destination_playfield", "LEVEL_ASSOCIATION_OBSERVED")
    print("ANALYSIS_PHASE=level_matrices", flush=True)

    level_controls = compare_level_controls(exact)
    print("ANALYSIS_PHASE=level_controls", flush=True)
    faction = faction_analysis(exact)
    terminals = terminal_analysis(exact)
    mission_types = mission_type_analysis(exact)
    sliders = slider_analysis(level2)
    difficulty = difficulty_analysis(exact)
    print("ANALYSIS_PHASE=condition_dimensions", flush=True)
    frequencies, frequency_rows, playfield_frequency_rows = frequency_analysis(exact)
    print("ANALYSIS_PHASE=frequencies", flush=True)
    cohorts = cohort_analysis(exact)
    names = repeated_name_analysis(exact)
    universe = universe_analysis(exact, placements)
    local_only = local_only_analysis(exact, placements, comparisons)
    anima = anima_analysis(rows)
    print("ANALYSIS_PHASE=cohorts_and_coverage", flush=True)

    ql_values = [r["analysis_mission_ql"] for r in rows if r["analysis_mission_ql"] is not None]
    ql_summary = []
    for ql, items in sorted(group_by(exact, lambda r: r["analysis_mission_ql"]).items(), key=lambda pair: (pair[0] is None, pair[0])):
        if ql is None:
            continue
        ql_summary.append({"mission_ql": ql, "mission_ql_source": "STATIC_EXPECTED_MISSION_QL",
                           "request_count": len({r["request_id"] for r in items}), "offer_count": len(items),
                           "unique_destinations": len({r["destination_identity"]["instance_uint32"] for r in items}),
                           "unique_destination_playfields": len({r["destination_playfield"] for r in items})})
    candidate_statuses = Counter((r["mission_ql_candidate_status"] or "STATUS_UNAVAILABLE") for r in rows if r["mission_ql_candidate"] is not None)
    summary = {
        "populations": {"RAW_BACKED_EXACT_DESTINATION": len(exact), "NO_RAW_DESTINATION_UNRESOLVED": len(unresolved),
                        "LEVEL2_CONTROLLED_SLIDER_CORPUS": len(level2), "total_offers": len(rows), "sessions": len(session_order)},
        "mission_ql_availability": {"live_decoded_offers": sum(r["live_decoded_mission_ql"] is not None for r in rows),
                                    "expected_ql_only_offers": sum(r["live_decoded_mission_ql"] is None and r["static_expected_mission_ql"] is not None for r in rows),
                                    "neither_offers": sum(r["live_decoded_mission_ql"] is None and r["static_expected_mission_ql"] is None for r in rows),
                                    "candidate_not_promoted_offers": sum(r["mission_ql_candidate"] is not None for r in rows),
                                    "candidate_status_counts": dict(sorted(candidate_statuses.items())),
                                    "analysis_range": [min(ql_values), max(ql_values)] if ql_values else None,
                                    "represented_values": sorted(set(ql_values)),
                                    "represented_value_count": len(set(ql_values)),
                                    "no_capture_coverage_values_1_through_250": sorted(set(range(1, 251)) - set(ql_values)),
                                    "primary_dimension": "STATIC_EXPECTED_MISSION_QL"},
        "coverage": coverage(rows), "mission_ql_summary": ql_summary, "character_level_summary": level_summary,
        "same_ql_level_comparisons": level_controls, "faction_analysis": faction,
        "terminal_analysis": terminals, "mission_type_analysis": mission_types,
        "secondary_slider_analysis": sliders, "difficulty_analysis": difficulty,
        "frequency_group_summary": frequencies, "cohort_analysis": cohorts,
        "frequency_group_classifications": dict(sorted(Counter(item["classification"] for item in frequencies).items())),
        "repeated_name_analysis": names, "universe_coverage": universe,
        "local_only_placements": local_only, "anima_encoding_diagnostic": anima,
        "capture_priorities": capture_priorities(rows, level_controls, faction, terminals, sliders),
        "interpretation_boundaries": ["Unobserved combinations are not ineligible.", "Observed frequency is not server probability or generator weight.",
                                      "Mission QL is static expected QL unless a future live decoded field is proven.",
                                      "Saturation applies only to discovery under the exact captured condition."],
        "LIVE_MISSION_CAPTURE_PERFORMED": "NO", "RUNTIME_MISSION_LOGIC_CHANGED": "NO",
        "DESTINATION_SELECTION_IMPLEMENTED": "NO", "DESTINATION_PROBABILITIES_INFERRED": "NO",
    }
    return summary, ql_matrix, condition_matrix, level_destination, level_playfield, frequency_rows, playfield_frequency_rows


def group_by(rows, key):
    result = defaultdict(list)
    for row in rows:
        result[key(row)].append(row)
    return result


def exact_rows(rows):
    return [row for row in rows if row["population"] == "RAW_BACKED_EXACT_DESTINATION"]


def observed_pair_matrix(rows, left_field, right_field, classification):
    groups = defaultdict(list)
    for row in rows:
        left = canonical(row[left_field]) if isinstance(row[left_field], dict) else row[left_field]
        right = canonical(row[right_field]) if isinstance(row[right_field], dict) else row[right_field]
        groups[(left, right)].append(row)
    output = []
    for (left, right), items in sorted(groups.items(), key=lambda item: canonical(item[0])):
        output.append({left_field: json.loads(left) if isinstance(left, str) and left.startswith("{") else left,
                       right_field: json.loads(right) if isinstance(right, str) and right.startswith("{") else right,
                       "request_count": len({r["request_id"] for r in items}), "offer_count": len(items),
                       "classification": classification})
    return output


def observed_matrix(rows, dimension, target, classification):
    groups = defaultdict(list)
    for row in rows:
        value = row[target]
        key = canonical(value) if isinstance(value, dict) else value
        groups[(row[dimension], key)].append(row)
    return [{dimension: dim, target: json.loads(value) if isinstance(value, str) and value.startswith("{") else value,
             "request_count": len({r["request_id"] for r in items}), "offer_count": len(items), "classification": classification}
            for (dim, value), items in sorted(groups.items(), key=lambda item: canonical(item[0]))]


def compare_level_controls(rows):
    groups = defaultdict(list)
    for row in rows:
        key = (row["analysis_mission_ql"], row["faction_side"], canonical(row["mission_terminal_identity"]),
               canonical(row["terminal_playfield"]), canonical({k: slider_label(row["secondary_sliders"].get(k)) for k in SECONDARY_SLIDERS}))
        groups[key].append(row)
    output = []
    for key, items in groups.items():
        by_level = group_by(items, lambda r: r["character_level"])
        levels = sorted(level for level in by_level if level is not None)
        for left_index, left in enumerate(levels):
            for right in levels[left_index + 1:]:
                a, b = by_level[left], by_level[right]
                ca = Counter(r["destination_identity"]["instance_uint32"] for r in a)
                cb = Counter(r["destination_identity"]["instance_uint32"] for r in b)
                union = set(ca) | set(cb)
                intersection = set(ca) & set(cb)
                jaccard = len(intersection) / len(union) if union else 1.0
                tv = 0.5 * sum(abs(ca[d] / len(a) - cb[d] / len(b)) for d in union)
                if min(len(a), len(b)) < 200:
                    classification = "INSUFFICIENT_CONTROLLED_DATA"
                elif jaccard >= 0.8 and tv <= 0.1:
                    classification = "NO_LEVEL_EFFECT_DETECTED"
                elif jaccard <= 0.2 and min(len(a), len(b)) >= 500:
                    classification = "STRONG_LEVEL_EFFECT"
                else:
                    classification = "POSSIBLE_LEVEL_EFFECT"
                output.append({"mission_ql": key[0], "faction_side": key[1], "terminal_identity": json.loads(key[2]),
                               "terminal_playfield": json.loads(key[3]), "secondary_sliders": json.loads(key[4]),
                               "left_level": left, "right_level": right, "left_offers": len(a), "right_offers": len(b),
                               "left_requests": len({r["request_id"] for r in a}), "right_requests": len({r["request_id"] for r in b}),
                               "destination_jaccard": jaccard, "frequency_total_variation": tv, "classification": classification})
    return sorted(output, key=canonical)


def faction_analysis(rows):
    represented = Counter(r["faction_side"] or "UNAVAILABLE" for r in rows)
    by_dest = defaultdict(set)
    by_pf = defaultdict(set)
    for row in rows:
        if row["faction_side"]:
            by_dest[row["destination_identity"]["instance_uint32"]].add(row["faction_side"])
            by_pf[row["destination_playfield"]].add(row["faction_side"])
    destination_rows = [{"destination_identity_instance": did, "observed_by_clan": "Clan" in sides,
                         "observed_by_omni": "Omni" in sides, "observed_by_neutral": "Neutral" in sides,
                         "classification": "FACTION_SPECIFIC_IN_CURRENT_CORPUS" if len(sides) == 1 else "OBSERVED_MULTIPLE_FACTIONS"}
                        for did, sides in sorted(by_dest.items())]
    playfield_rows = [{"destination_playfield": pf, "observed_sides": sorted(sides),
                       "classification": "FACTION_SPECIFIC_IN_CURRENT_CORPUS" if len(sides) == 1 else "OBSERVED_MULTIPLE_FACTIONS"}
                      for pf, sides in sorted(by_pf.items())]
    comparable = defaultdict(set)
    for row in rows:
        key = (row["analysis_mission_ql"], canonical(row["mission_terminal_identity"]), canonical(row["terminal_playfield"]),
               canonical(row["secondary_sliders"]))
        comparable[key].add(row["faction_side"])
    controls = sum(len({s for s in sides if s}) >= 2 for sides in comparable.values())
    return {"offer_counts_by_side": dict(sorted(represented.items())), "destination_observations": destination_rows,
            "playfield_observations": playfield_rows, "same_ql_slider_terminal_multi_faction_control_groups": controls,
            "restriction_conclusion": "NO_FACTION_RESTRICTION_PROVEN"}


def terminal_analysis(rows):
    by_terminal = group_by(rows, lambda r: canonical({"identity": r["mission_terminal_identity"], "playfield": r["terminal_playfield"], "name": r["terminal_name"]}))
    summaries = []
    dest_terminals = defaultdict(set)
    for terminal_json, items in by_terminal.items():
        same = [r for r in items if r["terminal_playfield"] and r["destination_playfield"] == r["terminal_playfield"]["instance_uint32"]]
        distances = []
        for row in same:
            a, b = row["terminal_coordinates"], row["destination_local_xyz"]
            if a and b:
                distances.append(math.sqrt(sum((float(a[k]) - float(b[i])) ** 2 for i, k in enumerate(("x", "y", "z")))))
        for row in items:
            dest_terminals[row["destination_identity"]["instance_uint32"]].add(terminal_json)
        summaries.append({"terminal": json.loads(terminal_json), "requests": len({r["request_id"] for r in items}), "offers": len(items),
                          "unique_destinations": len({r["destination_identity"]["instance_uint32"] for r in items}),
                          "unique_destination_playfields": len({r["destination_playfield"] for r in items}),
                          "same_playfield_offers": len(same), "cross_playfield_offers": len(items) - len(same),
                          "same_playfield_rate": len(same) / len(items),
                          "same_playfield_local_distance_min": min(distances) if distances else None,
                          "same_playfield_local_distance_max": max(distances) if distances else None,
                          "cross_playfield_distance_model": "UNAVAILABLE_UNPROVEN_COMMON_COORDINATE_SYSTEM"})
    terminal_ids_by_pf = defaultdict(set)
    controlled = defaultdict(set)
    for row in rows:
        terminal_ids_by_pf[canonical(row["terminal_playfield"])].add(canonical(row["mission_terminal_identity"]))
        key = (row["character_level"], row["analysis_mission_ql"], row["faction_side"],
               canonical({k: slider_label(row["secondary_sliders"].get(k)) for k in SECONDARY_SLIDERS}))
        controlled[key].add(canonical({"identity": row["mission_terminal_identity"], "playfield": row["terminal_playfield"]}))
    return {"terminal_count": len(by_terminal), "terminals": sorted(summaries, key=canonical),
            "destinations_observed_from_multiple_terminals": sum(len(v) > 1 for v in dest_terminals.values()),
            "destinations_observed_from_one_terminal": sum(len(v) == 1 for v in dest_terminals.values()),
            "terminal_playfields_with_multiple_terminal_identities": sum(len(v) > 1 for v in terminal_ids_by_pf.values()),
            "same_level_ql_side_slider_multi_terminal_control_groups": sum(len(v) > 1 for v in controlled.values())}


def mission_type_analysis(rows):
    by_type = group_by(rows, lambda r: r["mission_type"] or "UNAVAILABLE")
    summaries = []
    destination_types = defaultdict(set)
    for mission_type, items in sorted(by_type.items()):
        for row in items:
            destination_types[row["destination_identity"]["instance_uint32"]].add(mission_type)
        summaries.append({"mission_type": mission_type, "requests": len({r["request_id"] for r in items}), "offers": len(items),
                          "unique_destinations": len({r["destination_identity"]["instance_uint32"] for r in items}),
                          "unique_destination_playfields": len({r["destination_playfield"] for r in items}),
                          "classification": "OBSERVED_TYPE_ASSOCIATION" if mission_type != "UNAVAILABLE" else "INSUFFICIENT_DATA"})
    conditions = defaultdict(set)
    for row in rows:
        conditions[canonical(condition_of(row))].add(row["mission_type"])
    return {"types": summaries, "destinations_observed_across_multiple_types": sum(len(types) > 1 for types in destination_types.values()),
            "destinations_observed_in_one_type_only": sum(len(types) == 1 for types in destination_types.values()),
            "input_condition_groups_with_multiple_observed_mission_types": sum(len(types) > 1 for types in conditions.values()),
            "restriction_conclusion": "NO_TYPE_RESTRICTION_PROVEN"}


def slider_analysis(rows):
    output = {}
    for slider in SECONDARY_SLIDERS:
        groups = defaultdict(lambda: defaultdict(list))
        for row in rows:
            fixed = condition_of(row)
            fixed["secondary_sliders"].pop(slider)
            state = slider_label(row["secondary_sliders"].get(slider))
            groups[canonical(fixed)][state].append(row)
        comparisons = []
        for fixed_json, states in groups.items():
            valid = {k: v for k, v in states.items() if k is not None}
            if len(valid) < 2:
                continue
            state_rows = []
            for state, items in sorted(valid.items()):
                state_rows.append({"state": state, "requests": len({r["request_id"] for r in items}), "offers": len(items),
                                   "unique_destinations": len({r["destination_identity"]["instance_uint32"] for r in items}),
                                   "unique_playfields": len({r["destination_playfield"] for r in items}),
                                   "repeated_destination_offers": len(items) - len({r["destination_identity"]["instance_uint32"] for r in items})})
            overlaps = []
            keys = sorted(valid)
            any_difference = False
            for index, left in enumerate(keys):
                for right in keys[index + 1:]:
                    left_ids = {r["destination_identity"]["instance_uint32"] for r in valid[left]}
                    right_ids = {r["destination_identity"]["instance_uint32"] for r in valid[right]}
                    left_pfs = {r["destination_playfield"] for r in valid[left]}
                    right_pfs = {r["destination_playfield"] for r in valid[right]}
                    any_difference |= left_ids != right_ids or left_pfs != right_pfs
                    overlaps.append({"left": left, "right": right, "destination_overlap": len(left_ids & right_ids),
                                     "left_unique_destinations": len(left_ids - right_ids), "right_unique_destinations": len(right_ids - left_ids),
                                     "playfield_overlap": len(left_pfs & right_pfs), "left_unique_playfields": len(left_pfs - right_pfs),
                                     "right_unique_playfields": len(right_pfs - left_pfs)})
            classification = "POSSIBLE_DESTINATION_EFFECT" if any_difference else "NO_EFFECT_DETECTED_IN_DISCOVERY_SAMPLE"
            comparisons.append({"fixed_conditions": json.loads(fixed_json), "states": state_rows, "overlaps": overlaps,
                                "classification": classification, "probability_inference": "NOT_PERMITTED_FROM_DISCOVERY_SAMPLE"})
        output[slider] = {"controlled_comparisons": comparisons, "classification": "INCONCLUSIVE" if not comparisons else
                          "POSSIBLE_DESTINATION_EFFECT" if any(c["classification"] == "POSSIBLE_DESTINATION_EFFECT" for c in comparisons)
                          else "NO_EFFECT_DETECTED_IN_DISCOVERY_SAMPLE"}
    return output


def difficulty_analysis(rows):
    groups = defaultdict(list)
    for row in rows:
        key = (row["character_level"], row["difficulty_detent"], row["analysis_mission_ql"], canonical(row["mission_terminal_identity"]),
               canonical({k: slider_label(row["secondary_sliders"].get(k)) for k in SECONDARY_SLIDERS}))
        groups[key].append(row)
    summaries = []
    for key, items in sorted(groups.items(), key=lambda item: canonical(item[0])):
        summaries.append({"character_level": key[0], "difficulty_detent": key[1], "mission_ql": key[2],
                          "terminal_identity": json.loads(key[3]), "secondary_sliders": json.loads(key[4]),
                          "requests": len({r["request_id"] for r in items}), "offers": len(items),
                          "unique_destinations": len({r["destination_identity"]["instance_uint32"] for r in items}),
                          "unique_destination_playfields": len({r["destination_playfield"] for r in items})})
    same_ql = defaultdict(list)
    for row in rows:
        same_ql[(row["character_level"], row["analysis_mission_ql"], canonical(row["mission_terminal_identity"]), canonical(row["secondary_sliders"]))].append(row)
    controls = []
    for key, items in same_ql.items():
        by_detent = group_by(items, lambda r: r["difficulty_detent"])
        detents = sorted(value for value in by_detent if value is not None)
        if len(detents) < 2:
            continue
        pairs = []
        for index, left in enumerate(detents):
            for right in detents[index + 1:]:
                a, b = by_detent[left], by_detent[right]
                ca = Counter(r["destination_identity"]["instance_uint32"] for r in a)
                cb = Counter(r["destination_identity"]["instance_uint32"] for r in b)
                union, intersection = set(ca) | set(cb), set(ca) & set(cb)
                jaccard = len(intersection) / len(union) if union else 1.0
                tv = 0.5 * sum(abs(ca[d] / len(a) - cb[d] / len(b)) for d in union)
                if min(len(a), len(b)) < 200:
                    classification = "INSUFFICIENT_CONTROLLED_DATA"
                elif jaccard >= 0.8 and tv <= 0.1:
                    classification = "NO_DIFFICULTY_DETENT_EFFECT_DETECTED"
                else:
                    classification = "POSSIBLE_DIFFICULTY_DETENT_EFFECT"
                pairs.append({"left_detent": left, "right_detent": right, "left_offers": len(a), "right_offers": len(b),
                              "left_requests": len({r["request_id"] for r in a}), "right_requests": len({r["request_id"] for r in b}),
                              "destination_jaccard": jaccard, "frequency_total_variation": tv, "classification": classification})
        controls.append({"character_level": key[0], "mission_ql": key[1], "terminal_identity": json.loads(key[2]),
                         "secondary_sliders": json.loads(key[3]), "detents": detents, "pairwise_comparisons": pairs})
    return {"groups": summaries, "same_level_same_ql_same_terminal_slider_multi_detent_controls": len(controls),
            "same_ql_detent_controls": sorted(controls, key=canonical),
            "interpretation": "MISSION_QL_EFFECT_SEPARATE_FROM_DIFFICULTY_DETENT_EFFECT"}


def frequency_analysis(rows):
    groups = group_by(rows, lambda r: canonical(condition_of(r)))
    summaries, frequencies, playfield_frequencies = [], [], []
    for condition_json, items in sorted(groups.items()):
        destination_counts = Counter(r["destination_identity"]["instance_uint32"] for r in items)
        playfield_counts = Counter(r["destination_playfield"] for r in items)
        item = {"condition": json.loads(condition_json), "requests": len({r["request_id"] for r in items}), "offers": len(items),
                "unique_destinations": len(destination_counts), "unique_playfields": len(playfield_counts), **discovery(items)}
        summaries.append(item)
        for did, count in sorted(destination_counts.items()):
            frequencies.append({"condition": json.loads(condition_json), "destination_identity_instance": did,
                                "observation_count": count, "observed_proportion": count / len(items),
                                "wilson_95_interval": wilson(count, len(items)), "sample_size": len(items),
                                "classification": "OBSERVED_FREQUENCY"})
        for playfield, count in sorted(playfield_counts.items()):
            playfield_frequencies.append({"condition": json.loads(condition_json), "destination_playfield": playfield,
                                          "observation_count": count, "observed_proportion": count / len(items),
                                          "wilson_95_interval": wilson(count, len(items)), "sample_size": len(items),
                                          "classification": "OBSERVED_FREQUENCY"})
    return summaries, frequencies, playfield_frequencies


def cohort_analysis(rows):
    cohorts = group_by(rows, lambda r: r["cohort_id"])
    distribution = Counter()
    exact_duplicates = 0
    name_different_id = 0
    duplicate_playfields = 0
    duplicate_coordinates = 0
    ordering = Counter()
    diagnostics = []
    condition_cohorts = defaultdict(list)
    for cohort_id, items in cohorts.items():
        items.sort(key=lambda r: r["offer_index"])
        ids = [r["destination_identity"]["instance_uint32"] for r in items]
        names = [r["destination_display_name"] for r in items]
        pfs = [r["destination_playfield"] for r in items]
        coords = [(r["destination_playfield"], tuple(r["destination_local_xyz"] or [])) for r in items]
        distribution[len(set(ids))] += 1
        has_exact = len(set(ids)) < len(ids)
        exact_duplicates += has_exact
        has_name_diff = any(len({ids[i] for i, value in enumerate(names) if value == name}) > 1 for name in set(names))
        name_different_id += has_name_diff
        duplicate_playfields += len(set(pfs)) < len(pfs)
        duplicate_coordinates += len(set(coords)) < len(coords)
        ordering[tuple(pfs)] += 1
        condition_cohorts[canonical(condition_of(items[0]))].append(items)
    for condition_json, grouped in condition_cohorts.items():
        five = [items for items in grouped if len(items) == 5]
        if len(five) < 100:
            continue
        counter = Counter(r["destination_identity"]["instance_uint32"] for items in five for r in items)
        observed = sum(len({r["destination_identity"]["instance_uint32"] for r in items}) < 5 for items in five) / len(five)
        diagnostics.append({"condition": json.loads(condition_json), "cohorts": len(five),
                            "observed_exact_duplicate_cohort_rate": observed,
                            "independent_draw_expected_duplicate_rate_using_empirical_frequencies": independent_duplicate_probability(counter),
                            "classification": "DIAGNOSTIC_MODEL_ONLY_NOT_SERVER_BEHAVIOR"})
    return {"cohorts": len(cohorts), "unique_destination_count_distribution": {str(k): v for k, v in sorted(distribution.items())},
            "cohorts_with_exact_duplicate_destination_ids": exact_duplicates,
            "cohorts_with_duplicate_names_but_different_ids": name_different_id,
            "cohorts_with_duplicate_playfields": duplicate_playfields, "cohorts_with_duplicate_coordinates": duplicate_coordinates,
            "distinct_playfield_order_patterns": len(ordering),
            "top_playfield_order_patterns": [{"playfields": list(k), "cohorts": v} for k, v in ordering.most_common(25)],
            "independent_draw_diagnostics": diagnostics, "independence_conclusion": "NOT_INFERRED"}


def repeated_name_analysis(rows):
    by_name = defaultdict(lambda: {"ids": set(), "playfields": set(), "offers": 0})
    for row in rows:
        entry = by_name[row["destination_display_name"]]
        entry["ids"].add(row["destination_identity"]["instance_uint32"])
        entry["playfields"].add(row["destination_playfield"])
        entry["offers"] += 1
    families = [{"display_name": name, "observed_identity_count": len(value["ids"]),
                 "observed_playfield_count": len(value["playfields"]), "offer_count": value["offers"],
                 "identity_instances": sorted(value["ids"]), "playfields": sorted(value["playfields"])}
                for name, value in by_name.items() if len(value["ids"]) > 1]
    families.sort(key=lambda row: (-row["observed_identity_count"], row["display_name"] or ""))
    same_name_pf = defaultdict(set)
    for row in rows:
        same_name_pf[(row["destination_display_name"], row["destination_playfield"])].add(row["destination_identity"]["instance_uint32"])
    return {"observed_exact_identity_count": len({r["destination_identity"]["instance_uint32"] for r in rows}),
            "observed_exact_display_name_count": len(by_name), "multi_identity_name_count": len(families),
            "same_name_same_playfield_multi_identity_groups": sum(len(ids) > 1 for ids in same_name_pf.values()),
            "names_spanning_multiple_observed_playfields": sum(len(v["playfields"]) > 1 for v in by_name.values()),
            "largest_observed_same_name_families": families[:50]}


def universe_analysis(rows, placements):
    observed_ids = {r["destination_identity"]["instance_uint32"] for r in rows}
    all_ids = {r["identity_instance_uint32"] for r in placements}
    observed_pfs = {r["destination_playfield"] for r in rows}
    all_pfs = {r["explicit_playfield_id"] for r in placements}
    return {"total_client_placements": len(all_ids), "observed_placements": len(observed_ids),
            "never_observed_placements": len(all_ids - observed_ids), "coverage_percentage": 100 * len(observed_ids) / len(all_ids),
            "observed_playfields": len(observed_pfs), "never_observed_playfields": len(all_pfs - observed_pfs),
            "never_observed_playfield_ids": sorted(all_pfs - observed_pfs),
            "observed_classification": "CLIENT_PLACEMENT_OBSERVED_IN_MISSION_CAPTURE",
            "unobserved_classification": "CLIENT_PLACEMENT_NOT_YET_OBSERVED"}


def local_only_analysis(rows, placements, comparisons):
    local_ids = {r["identity_instance_uint32"] for r in comparisons if r["classification"] == "LOCAL_ONLY"}
    by_id = group_by(rows, lambda r: r["destination_identity"]["instance_uint32"])
    result = []
    for placement in placements:
        did = placement["identity_instance_uint32"]
        if did not in local_ids:
            continue
        items = by_id.get(did, [])
        result.append({"identity_type": placement["identity_type"], "identity_instance": did,
                       "identity_instance_hex": placement["identity_instance_hex"], "display_name": placement["display_name_exact"],
                       "playfield": placement["explicit_playfield_id"], "coordinates": placement["raw_position_components"],
                       "observation_count": len(items), "sessions": sorted({r["session_id"] for r in items}),
                       "mission_qls": sorted({r["analysis_mission_ql"] for r in items if r["analysis_mission_ql"] is not None}),
                       "character_levels": sorted({r["character_level"] for r in items if r["character_level"] is not None}),
                       "terminals": [json.loads(value) for value in sorted({canonical(r["mission_terminal_identity"]) for r in items if r["mission_terminal_identity"]})],
                       "mission_types": sorted({r["mission_type"] for r in items if r["mission_type"]}),
                       "slider_states": sorted({canonical(r["secondary_sliders"]) for r in items})})
    return result


def anima_analysis(rows):
    identity = 0xC0000280
    destination_items = [r for r in rows if r["destination_identity"] and r["destination_identity"]["instance_uint32"] == identity]
    text_hits = [{"session_id": r["session_id"], "request_id": r["request_id"], "offer_index": r["offer_index"], **hit}
                 for r in rows for hit in r["text_encoding_hits"]]
    return {"identity_instance": identity, "local_client_name": "Ænima HQ", "local_raw_name_hex": "c66e696d61204851",
            "external_supplied_name": "?nima HQ", "destination_observation_count": len(destination_items),
            "captured_text_hits": text_hits, "captured_text_source": "AOSHARP_CLIENT_STRING_SERIALIZED_TO_CAPTURE_JSON",
            "wire_location_name_field": "NOT_PROVEN"}


def capture_priorities(rows, level_controls, faction, terminals, sliders):
    represented_sides = {r["faction_side"] for r in rows if r["faction_side"]}
    priorities = []
    missing_sides = [side for side in ("Clan", "Omni", "Neutral") if side not in represented_sides]
    if missing_sides:
        priorities.append({"priority": 1, "capture": "Same QL, secondary sliders, and geographically comparable terminal for missing factions",
                           "unique_information": "Adds the absent faction controls: " + ", ".join(missing_sides)})
    if not level_controls or all(item["classification"] in ("INSUFFICIENT_CONTROLLED_DATA", "POSSIBLE_LEVEL_EFFECT") for item in level_controls):
        priorities.append({"priority": 2, "capture": "Two character levels producing the same expected QL at the same terminal and secondary sliders",
                           "unique_information": "Replicates same-QL controls sufficiently to separate character-level association from mission-QL association"})
    if terminals["same_level_ql_side_slider_multi_terminal_control_groups"] == 0:
        priorities.append({"priority": 3, "capture": "A second terminal in the same playfield plus a terminal in another playfield under matched QL/sliders",
                           "unique_information": "Separates terminal identity from terminal-playfield geography"})
    low_slider = [name for name, value in sliders.items() if value["classification"] in ("INCONCLUSIVE", "POSSIBLE_DESTINATION_EFFECT")]
    if low_slider:
        priorities.append({"priority": 4, "capture": "Larger controlled repeats for secondary slider states",
                           "unique_information": "Discovery samples cannot distinguish random pool discovery from slider effects: " + ", ".join(low_slider)})
    priorities.append({"priority": 5, "capture": "Repeat sufficiently large coherent groups whose discovery classification is EXPANDING",
                       "unique_information": "Measures additional identities without aggregating incompatible conditions"})
    represented_qls = {r["analysis_mission_ql"] for r in rows if r["analysis_mission_ql"] is not None}
    priorities.append({"priority": 6, "capture": "Expected mission QLs not represented in the current 1-250 matrix",
                       "unique_information": f"Adds QL coverage beyond {len(represented_qls)} represented values; no expected QL above {max(represented_qls)} is captured"})
    return priorities


def run(check=False):
    global CHECK
    CHECK = check
    inputs, placements, comparisons, resolved, source_manifest = load_inputs()
    print("ANALYSIS_PHASE=inputs_loaded", flush=True)
    if len(placements) != 2242 or len({r["explicit_playfield_id"] for r in placements}) != 202:
        raise ValueError("ACGENTRANCE_CATALOG_COUNT_MISMATCH")
    if len([r for r in comparisons if r["classification"] == "LOCAL_ONLY"]) != 7:
        raise ValueError("LOCAL_ONLY_COUNT_MISMATCH")
    sessions, requests, event_offers, session_order = load_capture_metadata(source_manifest, inputs)
    print("ANALYSIS_PHASE=capture_metadata_loaded", flush=True)
    placement_index = {(r["identity_type"], r["identity_instance_uint32"]): r for r in placements}
    rows = normalize_offers(resolved, event_offers, placement_index)
    print("ANALYSIS_PHASE=offers_joined", flush=True)
    summary, ql_matrix, condition_matrix, level_destination, level_playfield, frequency_rows, playfield_frequency_rows = analyze(rows, placements, comparisons, session_order)
    if summary["populations"] != {"RAW_BACKED_EXACT_DESTINATION": 92830, "NO_RAW_DESTINATION_UNRESOLVED": 355,
                                  "LEVEL2_CONTROLLED_SLIDER_CORPUS": 270, "total_offers": 93185, "sessions": 77}:
        raise ValueError("FIXED_POPULATION_COUNT_MISMATCH")
    emit_jsonl("mission-offer-analysis-inventory.jsonl.gz", rows)
    emit_jsonl("destination-ql-evidence-matrix.jsonl.gz", ql_matrix)
    emit_jsonl("destination-condition-evidence-matrix.jsonl.gz", condition_matrix)
    emit_jsonl("character-level-destination-matrix.jsonl.gz", level_destination)
    emit_jsonl("character-level-playfield-matrix.jsonl.gz", level_playfield)
    emit_jsonl("observed-destination-frequency.jsonl.gz", frequency_rows)
    emit_jsonl("observed-playfield-frequency.jsonl.gz", playfield_frequency_rows)
    emit_jsonl("terminal-destination-matrix.jsonl.gz", observed_pair_matrix(exact_rows(rows), "mission_terminal_identity", "destination_identity", "OBSERVED"))
    emit_jsonl("terminal-playfield-matrix.jsonl.gz", observed_pair_matrix(exact_rows(rows), "mission_terminal_identity", "destination_playfield", "OBSERVED"))
    emit_jsonl("mission-type-destination-matrix.jsonl.gz", observed_pair_matrix(exact_rows(rows), "mission_type", "destination_identity", "OBSERVED_TYPE_ASSOCIATION"))
    emit_jsonl("mission-type-playfield-matrix.jsonl.gz", observed_pair_matrix(exact_rows(rows), "mission_type", "destination_playfield", "OBSERVED_TYPE_ASSOCIATION"))
    emit_json("mission-destination-eligibility-summary.json", summary)
    manifest = {"schema_version": 1, "analysis_boundary": "OBSERVED_CAPTURE_EVIDENCE_ONLY",
                "inputs": dict(sorted(inputs.items())), "generated_outputs": dict(sorted(GENERATED.items())),
                "source_sessions": len(source_manifest), "base_reconstruction_sha": "c09869d5028ad455569eef70c7a4abc86480b253"}
    emit_json("mission-destination-eligibility-manifest.json", manifest)
    print("MISSION_DESTINATION_ELIGIBILITY_STALE_CHECK=PASS" if check else "MISSION_DESTINATION_ELIGIBILITY_GENERATION=PASS")
    print(canonical({"offers": len(rows), "exact": 92830, "unresolved": 355,
                     "observed_placements": summary["universe_coverage"]["observed_placements"],
                     "ql_range": summary["mission_ql_availability"]["analysis_range"]}))


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    generate = sub.add_parser("generate")
    generate.add_argument("--check", action="store_true")
    sub.add_parser("test")
    args = parser.parse_args()
    if args.command == "generate":
        run(args.check)
    else:
        from test_mission_destination_eligibility_analysis import run_tests
        run_tests()


if __name__ == "__main__":
    main()
