"""Read-only duplicate/integrity audit of the fixed mission destination corpus.

Only this audit's outputs are written. Source journals, previous generated
artifacts, names, placement identities and destination assignments are immutable.
The existing capture-schema-assisted verifier supplies byte boundaries; this
tool independently checks ownership and resolves exact binary32 coordinates.
"""
import argparse
import base64
from collections import Counter, defaultdict
import gzip
import hashlib
import io
import json
import os
from pathlib import Path
import struct
import subprocess

from acgentrance_mission_decoder import decode_cohort

ROOT = Path(__file__).resolve().parents[1]
ACG = Path("docs/generated/missions/acgentrance-reconstruction")
ELIGIBILITY = Path("docs/generated/missions/destination-eligibility-analysis")
OUT = Path("docs/generated/missions/destination-duplicate-audit")
MISSION_TYPES = {11329: "RETURN_ITEM", 11330: "KILL_PERSON", 11335: "FIND_PERSON",
                 11337: "FIND_ITEM", 11342: "REPAIR"}
SCHEMA = "aorebirth.mission-destination-duplicate-audit.v1"
AUDIT_BASE_SHA = "ab917a34011e02699a09798226af436ee3a54b5f"


def canonical(value):
    return json.dumps(value, ensure_ascii=True, sort_keys=True, separators=(",", ":"))


def value_hash(value):
    return hashlib.sha256(canonical(value).encode("utf-8")).hexdigest()


def file_hash(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def stable_path(path, root=ROOT):
    path = Path(path).resolve()
    try:
        return path.relative_to(Path(root).resolve()).as_posix()
    except ValueError:
        return str(path)


def read_jsonl(path):
    opener = gzip.open if str(path).endswith(".gz") else open
    with opener(path, "rt", encoding="utf-8-sig") as stream:
        for line in stream:
            if line.strip():
                yield json.loads(line)


def deterministic_jsonl_gzip(rows):
    buffer = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=buffer, mtime=0) as stream:
        for row in rows:
            stream.write((canonical(row) + "\n").encode("utf-8"))
    return buffer.getvalue()


def write_or_check(path, data, check):
    path = Path(path)
    if check:
        if not path.is_file() or path.read_bytes() != data:
            raise ValueError("STALE_ARTIFACT: " + path.name)
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def offer_key(row):
    return (row["session_id"], row["source_line"], row["request_id"],
            row["cohort_id"], row["offer_index"])


def cohort_key(row):
    return (row["session_id"], row["source_line"], row["request_id"], row["cohort_id"])


def response_owner(row):
    occurrence = ("exact_event", row["source_row_sha256"]) if row.get("source_row_sha256") else ("line", row["source_line"])
    return (row["session_id"], row["request_id"], row["cohort_id"], *occurrence)


def byte_owner(row):
    return (*response_owner(row), row.get("raw_response_sha256"), row.get("offer_start"), row.get("offer_end"))


def audit_offer_ranges(raw, decoded_offers):
    issues = []
    indexes = Counter(row["offer_index"] for row in decoded_offers)
    for index, count in sorted(indexes.items()):
        if count != 1:
            issues.append({"kind": "DUPLICATE_OFFER_INDEX", "offer_index": index,
                           "count": count, "severity": "PARSER_DUPLICATION_BUG"})
    expected = 51  # Verified complete QuestAlternative header, not guessed per-offer width.
    for row in decoded_offers:
        start, end = row["offer_start"], row["offer_end"]
        if start != expected or end <= start or start < 51 or end > len(raw):
            issues.append({"kind": "INVALID_OR_OVERLAPPING_BYTE_RANGE",
                           "offer_index": row["offer_index"], "start": start, "end": end,
                           "expected_start": expected, "packet_length": len(raw),
                           "severity": "PARSER_DUPLICATION_BUG"})
        expected = end
    if expected != len(raw):
        issues.append({"kind": "INCOMPLETE_PACKET_COVERAGE", "end": expected,
                       "packet_length": len(raw), "severity": "PARSER_DUPLICATION_BUG"})
    return issues


def _duplicate_groups(rows, key):
    groups = defaultdict(list)
    for row in rows:
        value = key(row)
        if value is not None:
            groups[value].append(row)
    return [(key, group) for key, group in sorted(groups.items(), key=lambda pair: canonical(pair[0]))
            if len(group) > 1]


def classify_offer_duplicates(rows):
    result = []
    for key, group in _duplicate_groups(rows, offer_key):
        hashes = {(row.get("raw_offer_hash"), row.get("decoded_offer_hash")) for row in group}
        identical = len(hashes) == 1 and all(row.get("raw_offer_hash") for row in group)
        missing_raw = not any(row.get("raw_offer_hash") for row in group)
        result.append({"classification": "EXACT_RAW_DUPLICATE_SAME_LOGICAL_KEY" if identical
                       else "MISSING_RAW_UNRESOLVED" if missing_raw else "CONFLICTING_OFFER_LOGICAL_KEY", "logical_keys": [list(key)],
                       "record_count": len(group), "excess_records": len(group) - 1,
                       "severity": "ACCIDENTAL_DOUBLE_INGESTION" if identical else "UNRESOLVED" if missing_raw else "CONFLICTING_SOURCE_DATA"})
    for key, group in _duplicate_groups(rows, lambda row: (*response_owner(row), row["offer_index"])
                                        if row.get("source_row_sha256") else None):
        if len({offer_key(row) for row in group}) > 1:
            identical = len({(row.get("raw_offer_hash"), row.get("decoded_offer_hash")) for row in group}) == 1
            result.append({"classification": "EXACT_SOURCE_EVENT_DUPLICATE" if identical else "CONFLICTING_SOURCE_EVENT_PROJECTION",
                           "severity": "ACCIDENTAL_DOUBLE_INGESTION" if identical else "CONFLICTING_SOURCE_DATA",
                           "source_event_owner": list(key), "logical_keys": [list(offer_key(row)) for row in group],
                           "record_count": len(group), "excess_records": len(group) - 1 if identical else 0})
    for key, group in _duplicate_groups(rows, lambda row: byte_owner(row) if row.get("raw_offer_hash") else None):
        if len({row["offer_index"] for row in group}) > 1:
            result.append({"classification": "SAME_BYTE_RANGE_MULTIPLE_OFFER_INDEXES",
                           "severity": "PARSER_DUPLICATION_BUG", "byte_owner": list(key),
                           "logical_keys": [list(offer_key(row)) for row in group],
                           "record_count": len(group), "excess_records": len(group) - 1})
    for raw_hash, group in _duplicate_groups(rows, lambda row: row.get("raw_offer_hash")):
        keys = sorted({offer_key(row) for row in group})
        owners = {byte_owner(row) for row in group}
        if len(keys) > 1 and len(owners) == len(group):
            # Identical contents under independent source occurrences are not proof of double ingestion.
            result.append({"classification": "EXACT_RAW_DUPLICATE_DIFFERENT_LOGICAL_KEY",
                           "severity": "LEGITIMATE_REPEATED_OBSERVATION",
                           "raw_offer_hash": raw_hash, "logical_keys": [list(key) for key in keys],
                           "record_count": len(group), "count_reduction": 0,
                           "interpretation": "Separate retained provenance; identical bytes alone do not prove accidental duplication."})
    for decoded_hash, group in _duplicate_groups(rows, lambda row: row.get("decoded_offer_hash")):
        raw_hashes = sorted({row["raw_offer_hash"] for row in group if row.get("raw_offer_hash")})
        if len(raw_hashes) > 1:
            result.append({"classification": "DECODED_DUPLICATE_DIFFERENT_RAW",
                           "severity": "LEGITIMATE_REPEATED_OBSERVATION",
                           "decoded_offer_hash": decoded_hash, "raw_offer_hashes": raw_hashes,
                           "logical_keys": [list(offer_key(row)) for row in group], "count_reduction": 0})
    missing = [row for row in rows if not row.get("raw_offer_hash")]
    if missing:
        result.append({"classification": "MISSING_RAW_UNRESOLVED", "severity": "UNRESOLVED",
                       "record_count": len(missing), "count_reduction": 0,
                       "logical_keys": [list(offer_key(row)) for row in missing]})
    return result


def classify_cohort_duplicates(rows):
    result = []
    duplicated_physical_keys = set()
    for key, group in _duplicate_groups(rows, response_owner):
        hashes = {(row.get("raw_response_sha256"), row.get("decoded_cohort_hash")) for row in group}
        proven = len(hashes) == 1 and all(row.get("raw_response_sha256") or row.get("source_row_sha256") for row in group)
        result.append({"classification": ("EXACT_RAW_COHORT_DUPLICATE" if group[0].get("raw_response_sha256") else "EXACT_SOURCE_EVENT_DUPLICATE") if proven else "CONFLICTING_COHORT_PHYSICAL_KEY",
                       "severity": "ACCIDENTAL_DOUBLE_INGESTION" if proven else "CONFLICTING_SOURCE_DATA",
                       "physical_key": list(key), "occurrences": [list(cohort_key(row)) for row in group],
                       "excess_records": len(group) - 1 if proven else 0})
        duplicated_physical_keys.update(cohort_key(row) for row in group)
    for key, group in _duplicate_groups(rows, lambda row: (row["session_id"], row["cohort_id"])):
        if len({response_owner(row) for row in group}) == 1:
            continue
        hashes = {row.get("raw_response_sha256") for row in group}
        payloads = {row.get("decoded_cohort_hash") for row in group}
        conflict = len(hashes) > 1 or len(payloads) > 1
        result.append({"classification": "SAME_COHORT_ID_DIFFERENT_RAW" if conflict else "EXACT_RAW_COHORT_DUPLICATE",
                       "severity": "CONFLICTING_SOURCE_DATA" if conflict else "UNRESOLVED",
                       "cohort_identity": list(key), "occurrences": [list(cohort_key(row)) for row in group],
                       "count_reduction": 0})
    for raw_hash, group in _duplicate_groups(rows, lambda row: row.get("raw_response_sha256")):
        if any(cohort_key(row) in duplicated_physical_keys for row in group):
            continue
        keys = {(row["session_id"], row["cohort_id"]) for row in group}
        result.append({"classification": "SAME_RAW_DIFFERENT_COHORT_ID" if len(keys) > 1
                       else "EXACT_RAW_COHORT_DUPLICATE", "severity": "LEGITIMATE_SERVER_RESPONSE_REPEAT" if len(keys) > 1 else "UNRESOLVED",
                       "raw_response_sha256": raw_hash,
                       "occurrences": [list(cohort_key(row)) for row in group], "count_reduction": 0})
    for decoded_hash, group in _duplicate_groups(rows, lambda row: row.get("decoded_cohort_hash")):
        hashes = {row.get("raw_response_sha256") for row in group if row.get("raw_response_sha256")}
        if len(hashes) > 1:
            result.append({"classification": "IDENTICAL_DECODED_DIFFERENT_RAW",
                           "severity": "LEGITIMATE_SERVER_RESPONSE_REPEAT", "decoded_cohort_hash": decoded_hash,
                           "occurrences": [list(cohort_key(row)) for row in group], "count_reduction": 0})
    return result


def identity_pair(value):
    if not value:
        return None
    instance = value["instance"] if "instance" in value else value["instance_uint32"]
    return (int(value["type"]) & 0xffffffff, int(instance) & 0xffffffff)


def proposed_duplicate_positions(rows, include_parser=True):
    """Union of proven excess occurrences, without mutating or collapsing input.

    Conflicting payloads and unproven missing-raw records are never proposed for removal.
    Same serialized content under a different physical response remains intact.
    """
    logical, events, owners = defaultdict(list), defaultdict(list), defaultdict(list)
    for index, row in enumerate(rows):
        if row.get("source_row_sha256"):
            events[(*response_owner(row), row["offer_index"])].append(index)
        if not row.get("raw_offer_hash"):
            continue
        logical[offer_key(row)].append(index)
        if include_parser:
            owners[byte_owner(row)].append(index)
    removed = set()
    for indexes in list(logical.values()) + list(events.values()) + list(owners.values()):
        hashes = {(rows[index].get("raw_offer_hash"), rows[index].get("decoded_offer_hash")) for index in indexes}
        if len(hashes) == 1:
            removed.update(indexes[1:])
    return removed


def placement_coordinate_key(row):
    # Preserve original bit patterns, including signed zero; never round or use a radius.
    raw = bytes.fromhex(row["raw_transform_bytes"])
    return (row["explicit_playfield_id"], *struct.unpack("<3I", raw[:12]))


def load_comparison_indexes(root):
    prior, metadata = {}, {}
    issues = []
    prior_hashes, metadata_hashes = {}, {}
    for row in read_jsonl(root / ACG / "mission-location-full-corpus-reconciliation.jsonl.gz"):
        key = offer_key(row)
        if key in prior:
            issues.append({"kind": "RECONCILIATION_DUPLICATE_KEY", "key": list(key),
                           "severity": "ACCIDENTAL_DOUBLE_INGESTION" if prior_hashes[key] == value_hash(row) else "CONFLICTING_SOURCE_DATA"})
        prior_hashes.setdefault(key, value_hash(row))
        prior.setdefault(key, []).append({"identity": identity_pair(row.get("resolved_acgentrance_identity")),
                                          "inbound_sha256": row.get("inbound_sha256"),
                                          "decoder": row.get("decoder")})
    fields = ("character_level", "static_expected_mission_ql", "analysis_mission_ql",
              "mission_terminal_identity", "terminal_playfield", "raw_sliders", "secondary_sliders",
              "mission_type", "destination_identity", "destination_local_xyz", "population")
    for row in read_jsonl(root / ELIGIBILITY / "mission-offer-analysis-inventory.jsonl.gz"):
        key = offer_key(row)
        if key in metadata:
            issues.append({"kind": "ELIGIBILITY_DUPLICATE_KEY", "key": list(key),
                           "severity": "ACCIDENTAL_DOUBLE_INGESTION" if metadata_hashes[key] == value_hash(row) else "CONFLICTING_SOURCE_DATA"})
        metadata_hashes.setdefault(key, value_hash(row))
        metadata.setdefault(key, []).append({field: row.get(field) for field in fields})
    return prior, metadata, issues


def decoded_offer_payload(offer):
    # Keep decoded content (including opaque captured spans), not observer provenance.
    return {key: value for key, value in offer.items()
            if key not in {"offer_index", "cohort_id", "slider_state_id", "roll_origin"}}


def audit_offers(root, source_audit):
    from mission_duplicate_sources import iter_cohorts
    placements = list(read_jsonl(root / ACG / "acgentrance-records.jsonl"))
    coordinates = defaultdict(list)
    for row in placements:
        coordinates[placement_coordinate_key(row)].append(row)
    prior, metadata, issues = load_comparison_indexes(root)
    offers, cohorts, repeats = [], [], []
    raw_hashes, offer_hashes, mission_ids = set(), set(), set()
    cohort_bins = Counter()
    seen_generated = set()
    decoded_packets = 0
    for source in iter_cohorts(root, source_audit["source_metadata"]):
        event, payload = source["event"], source["payload"]
        sid, line_number = source["session_id"], source["source_line"]
        rid = event.get("request_id")
        cid = payload.get("cohort_id") or f"{rid}/line/{line_number}"
        packet, raw, packet_hash, decoded = payload.get("raw_response_packet"), None, None, None
        source_offers = payload.get("offers") or []
        range_issues = []
        if packet:
            try:
                raw = base64.b64decode(packet["base64"], validate=True)
                packet_hash = hashlib.sha256(raw).hexdigest()
                if packet_hash != packet["sha256"] or len(raw) != packet["byte_length"]:
                    raise ValueError("RAW_PACKET_HASH_OR_LENGTH_MISMATCH")
                decoded = decode_cohort(raw, source_offers)
                range_issues = audit_offer_ranges(raw, decoded["offers"])
                if not range_issues:
                    decoded_packets += 1
                raw_hashes.update((packet_hash, hashlib.sha256(raw[16:]).hexdigest()))
            except (ValueError, KeyError, struct.error) as error:
                issues.append({"kind": "RAW_RESPONSE_VERIFICATION_FAILED", "session_id": sid,
                               "source_line": line_number, "detail": str(error),
                               "severity": "UNRESOLVED"})
                decoded = None
        for issue in range_issues:
            issues.append(dict(issue, session_id=sid, source_line=line_number))
        cohort_rows = []
        for ordinal, offer in enumerate(source_offers):
            row = {"session_id": sid, "source_path": source["source_path"], "source_line": line_number,
                   "source_row_sha256": source["source_row_sha256"], "source_event_sha256": source["source_event_sha256"],
                   "request_id": rid, "cohort_id": cid, "offer_index": offer["offer_index"],
                   "raw_response_sha256": packet_hash, "raw_offer_hash": None,
                   "decoded_offer_hash": value_hash(decoded_offer_payload(offer)),
                   "offer_start": None, "offer_end": None,
                   "mission_identity": offer.get("mission_identity"),
                   "classification": "MISSING_RAW_UNRESOLVED", "severity": "UNRESOLVED",
                   "destination_identity": None, "destination_playfield": None,
                   "destination_local_xyz_binary32_hex": None, "destination_local_xyz": None,
                   "destination_name": None, "raw_byte_range_verified": False}
            key = offer_key(row)
            seen_generated.add(key)
            meta_group = metadata.get(key, [])
            if len(meta_group) != 1:
                issues.append({"kind": "ELIGIBILITY_PROVENANCE_JOIN", "key": list(key),
                               "matched": len(meta_group), "severity": "UNRESOLVED"})
            meta = meta_group[0] if len(meta_group) == 1 else {}
            row.update({field: meta.get(field) for field in (
                "character_level", "static_expected_mission_ql", "analysis_mission_ql",
                "mission_terminal_identity", "terminal_playfield", "raw_sliders", "secondary_sliders")})
            row["mission_type"] = (offer.get("mission_type") or {}).get("canonical_type") or MISSION_TYPES.get(offer.get("mission_icon"))
            if row["mission_type"] != meta.get("mission_type"):
                issues.append({"kind": "MISSION_TYPE_PROJECTION_MISMATCH", "key": list(key), "severity": "UNRESOLVED"})
            if row["mission_identity"]:
                mission_ids.add(identity_pair(row["mission_identity"]))
            if decoded is not None and not range_issues:
                verified = decoded["offers"][ordinal]
                start, end = verified["offer_start"], verified["offer_end"]
                raw_hash = hashlib.sha256(raw[start:end]).hexdigest()
                world = verified["worldpos"]
                coordinate = (world["playfield_identity"]["instance"],
                              *struct.unpack(">3I", bytes.fromhex(world["local_float32_hex"])))
                candidates = coordinates.get(coordinate, [])
                row.update(raw_offer_hash=raw_hash, offer_start=start, offer_end=end,
                           raw_byte_range_verified=True, worldpos_offset=verified["worldpos_offset"],
                           destination_playfield=coordinate[0],
                           destination_local_xyz_binary32_hex=world["local_float32_hex"],
                           destination_local_xyz=world["local_position"],
                           destination_worldpos=world,
                           classification="UNIQUE_OFFER", severity="LEGITIMATE_REPEATED_OBSERVATION")
                offer_hashes.add(raw_hash)
                if len(candidates) == 1:
                    placement = candidates[0]
                    row["destination_identity"] = {"type": placement["identity_type"], "instance": placement["identity_instance_uint32"]}
                    row["destination_name"] = placement["display_name_exact"]
                else:
                    issues.append({"kind": "NONUNIQUE_EXACT_DESTINATION", "key": list(key),
                                   "candidates": len(candidates), "severity": "UNRESOLVED"})
                previous = prior.get(key, [])
                if len(previous) != 1:
                    issues.append({"kind": "RECONCILIATION_PROVENANCE_JOIN", "key": list(key),
                                   "matched": len(previous), "severity": "UNRESOLVED"})
                else:
                    old = previous[0]
                    boundary = old.get("decoder") or {}
                    if (old["identity"] != identity_pair(row["destination_identity"])
                            or old["inbound_sha256"] != packet_hash
                            or boundary.get("offer_start") != start or boundary.get("offer_end") != end):
                        issues.append({"kind": "RECONSTRUCTION_ASSIGNMENT_OR_RANGE_MISMATCH",
                                       "key": list(key), "severity": "CONFLICTING_SOURCE_DATA"})
                if identity_pair(meta.get("destination_identity")) != identity_pair(row["destination_identity"]):
                    issues.append({"kind": "ELIGIBILITY_DESTINATION_MISMATCH", "key": list(key),
                                   "severity": "CONFLICTING_SOURCE_DATA"})
            elif not packet:
                if any(previous["identity"] is not None for previous in prior.get(key, [])) or meta.get("destination_identity"):
                    issues.append({"kind": "MISSING_RAW_WAS_PROMOTED", "key": list(key),
                                   "severity": "CONFLICTING_SOURCE_DATA"})
            offers.append(row)
            cohort_rows.append(row)
        unique_destinations = {identity_pair(row["destination_identity"]) for row in cohort_rows if row["destination_identity"]}
        full_exact = bool(cohort_rows) and len(cohort_rows) == 5 and all(row["destination_identity"] for row in cohort_rows)
        if full_exact:
            cohort_bins[len(unique_destinations)] += 1
        cohort_row = {"session_id": sid, "source_path": source["source_path"], "source_line": line_number,
                      "source_row_sha256": source["source_row_sha256"], "source_event_sha256": source["source_event_sha256"],
                      "request_id": rid, "cohort_id": cid, "offer_count": len(source_offers),
                      "raw_response_sha256": packet_hash,
                      "decoded_cohort_hash": value_hash([decoded_offer_payload(offer) for offer in source_offers]),
                      "cohort_fingerprint": value_hash([packet_hash, [(row["offer_start"], row["offer_end"], row["raw_offer_hash"]) for row in cohort_rows]]),
                      "ordered_offer_payload_hashes": [row["decoded_offer_hash"] for row in cohort_rows],
                      "ordered_raw_offer_hashes": [row["raw_offer_hash"] for row in cohort_rows],
                      "ordered_destination_worldpos": [row.get("destination_worldpos") for row in cohort_rows],
                      "all_byte_ranges_verified": bool(decoded) and not range_issues,
                      "classification": "UNIQUE_COHORT", "unique_exact_destinations": len(unique_destinations) if full_exact else None}
        cohorts.append(cohort_row)
        if full_exact and len(unique_destinations) < 5:
            repeated = []
            for identity, group in _duplicate_groups(cohort_rows, lambda row: identity_pair(row["destination_identity"])):
                repeated.append({"destination_identity": {"type": identity[0], "instance": identity[1]},
                                 "destination_local_xyz_binary32_hex": group[0]["destination_local_xyz_binary32_hex"],
                                 "offers": [{field: row[field] for field in ("offer_index", "offer_start", "offer_end", "raw_offer_hash")}
                                            for row in group]})
            repeats.append({field: cohort_row[field] for field in ("session_id", "source_path", "source_line", "request_id", "cohort_id", "raw_response_sha256")} |
                           {"classification": "LEGITIMATE_SERVER_COHORT_DESTINATION_REPEAT",
                            "severity": "LEGITIMATE_SERVER_RESPONSE_REPEAT", "offer_count": 5,
                            "unique_destination_identities": len(unique_destinations),
                            "repeated_offer_positions": 5 - len(unique_destinations),
                            "disjoint_byte_ranges_verified": True, "repeated_destinations": repeated})
    for name, index in (("reconstruction", prior), ("eligibility", metadata)):
        extra = set(index) - seen_generated
        if extra:
            issues.append({"kind": "GENERATED_ROWS_WITHOUT_SOURCE", "artifact": name,
                           "keys": [list(key) for key in sorted(extra)], "severity": "ACCIDENTAL_DOUBLE_INGESTION"})
    offer_groups = classify_offer_duplicates(offers)
    cohort_groups = classify_cohort_duplicates(cohorts)
    return {"offers": offers, "cohorts": cohorts, "repeats": repeats, "offer_groups": offer_groups,
            "cohort_groups": cohort_groups, "cohort_bins": dict(sorted(cohort_bins.items())),
            "issues": issues, "decoded_packets": decoded_packets, "raw_hashes": raw_hashes,
            "offer_hashes": offer_hashes, "mission_ids": mission_ids}


def destination_counts(offers):
    groups = defaultdict(list)
    for row in offers:
        if row["destination_identity"] is not None:
            groups[identity_pair(row["destination_identity"])].append(row)
    for identity, rows in sorted(groups.items()):
        first = rows[0]
        yield {"destination_identity": {"type": identity[0], "instance": identity[1]},
               "observation_count": len(rows),
               "request_count": len({(row["session_id"], row["request_id"]) for row in rows}),
               "cohort_count": len({(row["session_id"], row["cohort_id"]) for row in rows}),
               "session_count": len({row["session_id"] for row in rows}),
               "ql_count": len({row["analysis_mission_ql"] for row in rows if row["analysis_mission_ql"] is not None}),
               "expected_qls": sorted({row["analysis_mission_ql"] for row in rows if row["analysis_mission_ql"] is not None}),
               "playfield": first["destination_playfield"], "coordinate": first["destination_local_xyz"],
               "coordinate_binary32_hex": first["destination_local_xyz_binary32_hex"],
               "name": first["destination_name"], "severity": "LEGITIMATE_REPEATED_OBSERVATION"}


def verify_audit_scope(root):
    allowed = {"Tools/mission_destination_duplicate_audit.py", "Tools/mission_destination_duplicate_audit.cmd",
               "Tools/mission_duplicate_sources.py", "Tools/mission_duplicate_catalog.py",
               "Tools/test_mission_destination_duplicate_audit.py", "docs/evidence/MISSION_DESTINATION_DUPLICATE_AUDIT.md"}
    paths = set()
    for args in (("diff", "--name-only", AUDIT_BASE_SHA, "--"), ("ls-files", "--others", "--exclude-standard")):
        paths.update(subprocess.check_output(["git", *args], cwd=root, text=True, encoding="utf-8").splitlines())
    unexpected = sorted(path for path in paths if path not in allowed and not path.startswith(OUT.as_posix() + "/"))
    if unexpected:
        raise ValueError("AUDIT_SCOPE_VIOLATION: " + canonical(unexpected))


def verify_input_hashes(inputs, root, july_data_root):
    for name, expected in inputs.items():
        if name.startswith("external-game-data/"):
            if july_data_root is None:
                raise ValueError("JULY_INPUT_ROOT_REQUIRED")
            path = Path(july_data_root) / name.removeprefix("external-game-data/")
        else:
            path = root / name
        actual = (hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()
                  if isinstance(expected, dict) else file_hash(path))
        if actual != (expected["sha256_lf"] if isinstance(expected, dict) else expected):
            raise ValueError("INPUT_CHANGED_DURING_AUDIT: " + name)


def run(check=False, root=ROOT, july_data_root=None):
    from mission_duplicate_sources import audit_sources
    from mission_duplicate_catalog import audit_catalog, audit_cross_corpus
    root = Path(root)
    verify_audit_scope(root)
    print("DUPLICATE_AUDIT_PHASE=sources", flush=True)
    sources = audit_sources(root)
    print("DUPLICATE_AUDIT_PHASE=raw_offers", flush=True)
    observations = audit_offers(root, sources)
    print("DUPLICATE_AUDIT_PHASE=catalog_artifacts", flush=True)
    catalog = audit_catalog(root)
    cross = audit_cross_corpus(root, observations["raw_hashes"], observations["offer_hashes"],
                               observations["mission_ids"], july_data_root=july_data_root,
                               september_session_ids=[row["session_id"] for row in sources["source_metadata"]])
    destinations = list(destination_counts(observations["offers"]))
    issues = sources.get("issues", []) + observations["issues"] + catalog.get("issues", [])
    if isinstance(cross, dict):
        issues += cross.get("issues", [])
    inputs = {}
    for source_inputs in (sources.get("inputs", {}), catalog.get("inputs", {}), cross.get("inputs", {})):
        if isinstance(source_inputs, dict):
            inputs.update(source_inputs)
        else:
            for row in source_inputs:
                inputs[row["path"]] = row["sha256"]
    for path in sorted((root / "Tools").glob("*mission*duplicate*.py")):
        inputs[stable_path(path, root)] = file_hash(path)
    for name in ("Tools/acgentrance_mission_decoder.py", "Tools/select_python_runtime.cmd"):
        path = root / name
        inputs[name] = {"sha256_lf": hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()}
    wrapper = root / "Tools/mission_destination_duplicate_audit.cmd"
    inputs[stable_path(wrapper, root)] = {"sha256_lf": hashlib.sha256(wrapper.read_bytes().replace(b"\r\n", b"\n")).hexdigest()}
    for relative in (ACG / "acgentrance-records.jsonl", ACG / "mission-location-full-corpus-reconciliation.jsonl.gz",
                     ELIGIBILITY / "mission-offer-analysis-inventory.jsonl.gz"):
        inputs[relative.as_posix()] = file_hash(root / relative)
    verify_input_hashes(inputs, root, july_data_root)
    verify_audit_scope(root)
    exact = sum(row["destination_identity"] is not None for row in observations["offers"])
    raw = sum(row["raw_offer_hash"] is not None for row in observations["offers"])
    accidental_positions = proposed_duplicate_positions(observations["offers"], include_parser=False)
    proposed_positions = proposed_duplicate_positions(observations["offers"])
    remaining_exact = [row for index, row in enumerate(observations["offers"])
                       if index not in proposed_positions and row["destination_identity"] is not None]
    parser_groups = [group for group in observations["offer_groups"] if group["severity"] == "PARSER_DUPLICATION_BUG"]
    parser_issues = [issue for issue in issues if issue.get("severity") == "PARSER_DUPLICATION_BUG"]
    summary = {
        "schema": SCHEMA,
        "PHYSICAL_SOURCE_FILES_FOUND": sources["counts"]["PHYSICAL_SOURCE_FILES_FOUND"],
        "LOGICAL_UNIQUE_SESSIONS": sources["counts"]["LOGICAL_UNIQUE_SESSIONS"],
        "TOTAL_REQUESTS": sources["counts"]["TOTAL_REQUESTS"],
        "TOTAL_COHORTS": len(observations["cohorts"]),
        "TOTAL_OFFERS": len(observations["offers"]), "RAW_BACKED_OFFERS": raw,
        "MISSING_RAW_OFFERS": sum(not row["raw_response_sha256"] for row in observations["offers"]),
        "EXACT_DESTINATION_OBSERVATIONS": exact, "UNIQUE_DESTINATION_IDENTITIES": len(destinations),
        "UNIQUE_OBSERVED_DESTINATION_IDENTITIES": len(destinations),
        "UNIQUE_DESTINATION_PLAYFIELDS": len({row["playfield"] for row in destinations}),
        "UNIQUE_OBSERVED_DESTINATION_PLAYFIELDS": len({row["playfield"] for row in destinations}),
        "PROVEN_ACCIDENTAL_DUPLICATE_SESSIONS": sources["counts"]["PROVEN_ACCIDENTAL_DUPLICATE_SESSIONS"],
        "PROVEN_ACCIDENTAL_DUPLICATE_REQUESTS": sources["counts"]["PROVEN_ACCIDENTAL_DUPLICATE_REQUESTS"],
        "PROVEN_ACCIDENTAL_DUPLICATE_COHORTS": sum(group.get("excess_records", 0)
            for group in observations["cohort_groups"] if group["severity"] == "ACCIDENTAL_DOUBLE_INGESTION"),
        "PROVEN_ACCIDENTAL_DUPLICATE_OFFERS": len(accidental_positions),
        "PROPOSED_REMOVABLE_OFFER_POSITIONS": len(proposed_positions),
        "PARSER_DUPLICATION_BUGS": len(parser_groups) + len(parser_issues),
        "RAW_COHORTS_BYTE_VERIFIED": observations["decoded_packets"],
        "FIVE_OFFER_COHORTS": sum(row["offer_count"] == 5 for row in observations["cohorts"]),
        "EXACT_FIVE_OFFER_COHORTS": sum(observations["cohort_bins"].values()),
        "COHORT_DESTINATION_UNIQUENESS_HISTOGRAM": observations["cohort_bins"],
        "COHORTS_WITH_LEGITIMATE_DESTINATION_REPEATS": len(observations["repeats"]),
        "REPEATED_OFFER_POSITIONS": sum(row["repeated_offer_positions"] for row in observations["repeats"]),
        "CLIENT_PLACEMENTS": catalog["counts"]["CLIENT_PLACEMENTS"],
        "DUPLICATE_COMPLETE_PLACEMENT_IDENTITIES": catalog["counts"]["DUPLICATE_COMPLETE_PLACEMENT_IDENTITIES"],
        "EXACT_SAME_PLAYFIELD_XYZ_COLLISIONS": catalog["counts"]["EXACT_SAME_PLAYFIELD_XYZ_COLLISIONS"],
        "MULTI_IDENTITY_NAME_GROUPS": catalog["counts"]["MULTI_IDENTITY_NAME_GROUPS"],
        "LOGICAL_OFFERS_AFTER_PROVEN_DEDUPLICATION": len(observations["offers"]) - len(proposed_positions),
        "EXACT_OBSERVATIONS_AFTER_PROVEN_DEDUPLICATION": len(remaining_exact),
        "UNIQUE_DESTINATIONS_AFTER_PROVEN_DEDUPLICATION": len({identity_pair(row["destination_identity"]) for row in remaining_exact}),
        "proposed_correction_plan": {"applied": False, "offer_inventory_zero_based_positions": sorted(proposed_positions),
                                    "instruction": "No cleanup is authorized. Review proven excess occurrences before any separate correction task."},
        "HISTORICAL_FILES_MODIFIED": "NO", "RUNTIME_MISSION_LOGIC_CHANGED": "NO",
        "DESTINATION_DATA_DEDUPLICATED": "NO", "ISSUE_COUNT": len(issues), "issues": issues,
        "count_equations": [f"{len(observations['offers'])} = {raw} raw-backed + {len(observations['offers']) - raw} without verified raw",
                            f"{exact} exact observations -> {len(destinations)} distinct placement identities"],
        "classification_policy": "Only repeated physical provenance or proven byte-range double emission can reduce counts. No cleanup is applied.",
        "coordinate_policy": "Explicit playfield plus all three original binary32 bit patterns; no tolerance or rounding.",
    }
    generated = {}
    def emit(name, value, jsonl=False):
        data = deterministic_jsonl_gzip(value) if jsonl else (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=True) + "\n").encode("utf-8")
        write_or_check(root / OUT / name, data, check)
        generated[(OUT / name).as_posix()] = {"sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)}
    emit("source-file-duplicate-groups.json", sources["source_file_report"])
    emit("event-duplicate-groups.json", sources["event_report"])
    request_report = {key: value for key, value in sources["request_report"].items() if key != "requests"}
    request_report["request_inventory"] = (OUT / "request-audit-inventory.jsonl.gz").as_posix()
    emit("request-duplicate-groups.json", request_report)
    emit("request-audit-inventory.jsonl.gz", sources["request_report"]["requests"], True)
    emit("cohort-duplicate-groups.json", {"cohort_count": len(observations["cohorts"]),
         "cohort_inventory": (OUT / "cohort-audit-inventory.jsonl.gz").as_posix(),
         "groups": observations["cohort_groups"]})
    emit("cohort-audit-inventory.jsonl.gz", observations["cohorts"], True)
    emit("offer-duplicate-groups.jsonl.gz", observations["offer_groups"], True)
    emit("offer-audit-inventory.jsonl.gz", observations["offers"], True)
    emit("destination-observation-counts.jsonl.gz", destinations, True)
    emit("same-cohort-destination-repeats.jsonl.gz", observations["repeats"], True)
    emit("placement-identity-collisions.json", catalog["placement_identity"])
    emit("placement-coordinate-collisions.json", catalog["placement_coordinate"])
    emit("placement-name-collisions.json", catalog["placement_name"])
    emit("artifact-primary-key-audit.json", catalog["artifact_primary_keys"])
    emit("cross-corpus-overlap.json", cross)
    emit("duplicate-audit-summary.json", summary)
    emit("duplicate-audit-manifest.json", {"schema": SCHEMA, "inputs": inputs, "outputs": generated.copy(),
         "source_base_sha": AUDIT_BASE_SHA, "input_hashes_reverified": True, "git_change_scope_verified": True,
         "gzip": {"mtime": 0, "filename": "", "serialization": "sorted compact ASCII JSON + LF"},
         "audit_only": True, "historical_inputs_rewritten": False})
    if issues:
        print(f"MISSION_DESTINATION_DUPLICATE_AUDIT=FINDINGS ({len(issues)} integrity issues)", flush=True)
        raise ValueError("Integrity issues retained in duplicate-audit-summary.json; no correction applied")
    print("MISSION_DESTINATION_DUPLICATE_AUDIT_" + ("CHECK" if check else "GENERATE") + "=PASS", flush=True)
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("generate", "test"))
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--july-data-root", type=Path, default=None)
    args = parser.parse_args()
    if args.command == "test":
        import unittest
        suite = unittest.defaultTestLoader.discover(str(ROOT / "Tools"), pattern="test_mission_destination_duplicate_audit.py")
        result = unittest.TextTestRunner(verbosity=1).run(suite)
        print("MISSION_DESTINATION_DUPLICATE_AUDIT_TESTS=" + ("PASS" if result.wasSuccessful() else "FAIL"))
        return 0 if result.wasSuccessful() else 1
    july = args.july_data_root
    if july is None and os.environ.get("AO_REBIRTH_GAMEDATA_PATH"):
        july = Path(os.environ["AO_REBIRTH_GAMEDATA_PATH"])
    run(check=args.check, july_data_root=july)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
