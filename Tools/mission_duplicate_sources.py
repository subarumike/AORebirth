"""Read-only source, event and request audit for the fixed mission corpus.

Physical copies are hashed independently. Only the single journal selected by
the existing corpus manifests contributes logical events. Raw cohort payloads
are exposed by a second-pass iterator so the 690 MB journals are never retained
as a second in-memory corpus. No source is repaired, normalized or deduplicated.
"""
from collections import Counter, defaultdict
from itertools import combinations
from pathlib import Path
import hashlib
import json


SOURCE_MANIFEST = "docs/generated/missions/location-reconciliation/source-manifest.json"
COVERAGE_MANIFEST = "docs/generated/missions/acgentrance-reconstruction/mission-location-capture-source-coverage.json"
RETAINED = "docs/reference/missions/modern-capture/level2-slider-discovery/raw"
ARCHIVE = Path("D:/AORebirthCaptures/sessions/Mission-harvester")
ORIGINALS = Path("C:/Users/Mike/AppData/Local/AOSharp/MissionOfferHarvester/sessions")
SOURCE_CLASSES = (
    "SAME_PATH_SAME_HASH", "DIFFERENT_PATH_SAME_HASH",
    "SAME_SESSION_ID_SAME_HASH", "SAME_SESSION_ID_DIFFERENT_HASH",
    "DIFFERENT_SESSION_ID_SAME_HASH", "PARTIAL_PREFIX_DUPLICATE",
    "PARTIAL_SUFFIX_DUPLICATE", "OVERLAPPING_EVENT_RANGE", "UNIQUE_SOURCE",
)
REQUEST_CLASSES = (
    "UNIQUE_REQUEST", "EXACT_DUPLICATE_REQUEST_RECORD",
    "REUSED_REQUEST_ID_IDENTICAL", "REUSED_REQUEST_ID_CONFLICT",
    "MISSING_REQUEST_HEADER", "MULTIPLE_RESPONSES_FOR_ONE_REQUEST",
    "ORPHAN_RESPONSE",
)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)


def digest(value):
    return hashlib.sha256(canonical(value).encode("utf-8")).hexdigest()


def file_hash(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def stable_path(root, path):
    path = path.resolve()
    try:
        return path.relative_to(root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def resolve(root, value):
    path = Path(value)
    return path if path.is_absolute() else root / path


def without_packet_bytes(value):
    """Retain all metadata and recorded hashes, but omit verbose packet strings."""
    if isinstance(value, dict):
        return {key: without_packet_bytes(item) for key, item in value.items()
                if key not in ("base64", "hex", "raw_hex", "raw_base64")}
    if isinstance(value, list):
        return [without_packet_bytes(item) for item in value]
    return value


def packet_descriptors(value, prefix=""):
    """Compact hashes/lengths with field paths; source payload hash covers all."""
    result = []
    if isinstance(value, dict):
        if "base64" in value:
            result.append({"field": prefix, "metadata": without_packet_bytes(value)})
        else:
            for key, item in sorted(value.items()):
                result.extend(packet_descriptors(item, prefix + "." + key if prefix else key))
    return result


def compact_reference(ref):
    # The containing request supplies session, request ID and source path.
    return {key: ref[key] for key in ("source_line", "timestamp_utc", "row_sha256", "payload_sha256")}


def read_events(path):
    """Yield physical line number, exact-row hash, parsed event, canonical hash."""
    with path.open("rb") as stream:
        for line_number, raw in enumerate(stream, 1):
            if not raw.strip():
                raise ValueError(f"EMPTY_JOURNAL_LINE: {path.name}:{line_number}")
            event = json.loads(raw)
            yield line_number, hashlib.sha256(raw.rstrip(b"\r\n")).hexdigest(), event, digest(event)


def event_reference(session_id, source_path, line_number, event, raw_hash, event_hash):
    payload = event.get("payload") or {}
    return {
        "session_id": session_id, "declared_session_id": event.get("session_id"),
        "source_path": source_path, "source_line": line_number,
        "request_id": event.get("request_id"), "cohort_id": payload.get("cohort_id"),
        "event_type": event.get("event_type"), "timestamp_utc": event.get("timestamp_utc"),
        "row_sha256": raw_hash, "canonical_event_sha256": event_hash,
        "payload_sha256": digest(payload),
    }


def duplicate_groups(index, classification, severity):
    return [{"classification": classification, "severity": severity,
             "group_key": key if isinstance(key, str) else list(key), "records": records}
            for key, records in sorted(index.items(), key=lambda pair: str(pair[0]))
            if len(records) > 1]


def analyze_event_rows(rows, session_id, source_path):
    """Fixture-friendly single logical journal analysis; retains compact metadata."""
    events = []
    session_headers = []
    request_phases = defaultdict(lambda: defaultdict(list))
    responses = defaultdict(list)
    cohorts = defaultdict(list)
    issues = []
    counts = Counter()
    timestamps = []
    for line_number, raw_hash, event, event_hash in rows:
        payload = event.get("payload") or {}
        event_type = event.get("event_type")
        ref = event_reference(session_id, source_path, line_number, event, raw_hash, event_hash)
        events.append(ref)
        counts[event_type] += 1
        if event.get("timestamp_utc") is not None:
            timestamps.append(event["timestamp_utc"])
        if event.get("session_id") != session_id:
            issues.append({"classification": "CONFLICTING_SOURCE_DATA", "reason": "DECLARED_SESSION_ID_MISMATCH", "event": ref})
        if event_type == "session_started":
            session_headers.append({"source_line": line_number, "payload_sha256": ref["payload_sha256"],
                                    "payload": without_packet_bytes(payload)})
        elif event_type in ("request_started", "request_transmitted"):
            request_phases[event.get("request_id")][event_type].append({
                "event": ref, "payload": without_packet_bytes(payload), "raw_packet_descriptors": packet_descriptors(payload)})
        elif event_type == "cohort_received":
            record = {"event": ref, "offer_count": len(payload.get("offers") or []),
                      "cohort_id": payload.get("cohort_id"),
                      "raw_packet_descriptors": packet_descriptors({key: val for key, val in payload.items() if key != "offers"})}
            responses[event.get("request_id")].append(record)
            if payload.get("cohort_id") is not None:
                cohorts[payload["cohort_id"]].append(record)
            counts["offers"] += record["offer_count"]

    requests = []
    request_findings = []
    phase_findings = []
    for request_id in sorted(set(request_phases) | set(responses), key=str):
        phases = request_phases.get(request_id, {})
        starts = phases.get("request_started", [])
        received = responses.get(request_id, [])
        classifications = []
        for phase, records in sorted(phases.items()):
            if len(records) < 2:
                continue
            hashes = {record["event"]["payload_sha256"] for record in records}
            exact = defaultdict(list)
            for record in records:
                exact[record["event"]["row_sha256"]].append(record["event"])
            for group in duplicate_groups(exact, "EXACT_DUPLICATE_REQUEST_RECORD", "ACCIDENTAL_DOUBLE_INGESTION"):
                classifications.append("EXACT_DUPLICATE_REQUEST_RECORD")
                group.update({"session_id": session_id, "request_id": request_id, "event_phase": phase})
                request_findings.append(group)
            classification = "REUSED_REQUEST_ID_IDENTICAL" if len(hashes) == 1 else "REUSED_REQUEST_ID_CONFLICT"
            classifications.append(classification)
            finding = {"classification": classification,
                       "severity": "UNRESOLVED" if len(hashes) == 1 else "CONFLICTING_SOURCE_DATA",
                       "session_id": session_id, "request_id": request_id, "event_phase": phase,
                       "records": [record["event"] for record in records]}
            request_findings.append(finding)
            phase_findings.append(finding)
        if not starts:
            classifications.append("MISSING_REQUEST_HEADER")
            request_findings.append({"classification": "MISSING_REQUEST_HEADER", "severity": "UNRESOLVED",
                                     "session_id": session_id, "request_id": request_id,
                                     "response_lines": [record["event"]["source_line"] for record in received]})
            if received:
                classifications.append("ORPHAN_RESPONSE")
                request_findings.append({"classification": "ORPHAN_RESPONSE", "severity": "UNRESOLVED",
                                         "session_id": session_id, "request_id": request_id,
                                         "records": [record["event"] for record in received]})
        if len(received) > 1:
            classifications.append("MULTIPLE_RESPONSES_FOR_ONE_REQUEST")
            request_findings.append({"classification": "MULTIPLE_RESPONSES_FOR_ONE_REQUEST", "severity": "UNRESOLVED",
                                     "session_id": session_id, "request_id": request_id,
                                     "records": [record["event"] for record in received]})
        if not classifications:
            classifications.append("UNIQUE_REQUEST")
        request_payloads = [record["payload"] for record in starts]
        contexts = []
        for payload in request_payloads or [{}]:
            for header in session_headers or [{"payload": {}}]:
                session = header["payload"]
                origin = payload.get("roll_origin") or session.get("roll_origin") or {}
                contexts.append({
                    "terminal_identity": origin.get("terminal_identity") or payload.get("terminal_identity") or session.get("terminal_identity"),
                    "terminal_playfield": origin.get("terminal_playfield_identity") or session.get("terminal_playfield"),
                    "terminal_coordinates": origin.get("terminal_local_coordinates") or session.get("terminal_coordinates"),
                    "sliders": payload.get("sliders"),
                    "requested_semantic_state": payload.get("requested_semantic_state") or session.get("requested_semantic_state"),
                    "expected_ql": payload.get("static_expected_mission_ql", session.get("static_expected_mission_ql")),
                    "target_ql_operator_input": payload.get("target_mission_ql", session.get("target_mission_ql")),
                    "character_metadata": {key: payload.get(key, session.get(key)) for key in (
                        "character_surrogate", "character_identity_raw", "character_level", "profession_raw", "breed_raw", "faction_side_raw")},
                })
        compact_phases = {phase: [{**compact_reference(record["event"]), "raw_packet_descriptors": record["raw_packet_descriptors"]}
                                  for record in records] for phase, records in sorted(phases.items())}
        compact_responses = [{**compact_reference(record["event"]), "cohort_id": record["cohort_id"],
                              "offer_count": record["offer_count"], "raw_packet_descriptors": record["raw_packet_descriptors"]}
                             for record in received]
        requests.append({"session_id": session_id, "request_id": request_id,
                         "classifications": list(dict.fromkeys(classifications)), "source_path": source_path,
                         "header_count": len(starts), "request_payload_sha256": [record["event"]["payload_sha256"] for record in starts],
                         "phases": compact_phases, "contexts": contexts,
                         "response_cohorts": compact_responses, "response_cohort_count": len(received),
                         "offer_count": sum(record["offer_count"] for record in received)})
    cohort_findings = []
    for cohort_id, records in sorted(cohorts.items()):
        if len(records) > 1:
            different = len({record["event"]["payload_sha256"] for record in records}) > 1
            cohort_findings.append({"classification": "CONFLICTING_COHORT_CONTENTS" if different else "SAME_COHORT_ID_REPEATED",
                                    "severity": "CONFLICTING_SOURCE_DATA" if different else "UNRESOLVED",
                                    "session_id": session_id, "cohort_id": cohort_id,
                                    "records": [record["event"] for record in records]})
    if len(session_headers) != 1:
        issues.append({"classification": "CONFLICTING_SOURCE_DATA", "reason": "SESSION_HEADER_COUNT", "session_id": session_id, "count": len(session_headers)})
    return {"events": events, "requests": requests, "request_findings": request_findings,
            "phase_findings": phase_findings, "cohort_findings": cohort_findings,
            "session_headers": session_headers, "issues": issues,
            "metadata": {"event_count": len(events), "event_type_counts": dict(sorted(counts.items())),
                         "request_count": len(set(request_phases) | set(responses)),
                         "request_started_count": counts["request_started"],
                         "declared_session_ids": sorted({event["declared_session_id"] for event in events}, key=str),
                         "cohort_count": counts["cohort_received"], "offer_count": counts["offers"],
                         "first_timestamp": timestamps[0] if timestamps else None,
                         "last_timestamp": timestamps[-1] if timestamps else None,
                         "minimum_timestamp": min(timestamps) if timestamps else None,
                         "maximum_timestamp": max(timestamps) if timestamps else None}}


def compare_event_sequences(left, right):
    """Exact fingerprints, not coincident timestamps, establish shared ranges."""
    if left == right:
        return []  # Whole-file equivalent streams are handled by file/event hashes.
    results = []
    prefix = 0
    for a, b in zip(left, right):
        if a != b:
            break
        prefix += 1
    suffix = 0
    for a, b in zip(reversed(left), reversed(right)):
        if a != b:
            break
        suffix += 1
    if prefix:
        results.append({"classification": "PARTIAL_PREFIX_DUPLICATE", "shared_event_count": prefix})
    if suffix:
        results.append({"classification": "PARTIAL_SUFFIX_DUPLICATE", "shared_event_count": suffix})
    shared = set(left) & set(right)
    if shared:
        results.append({"classification": "OVERLAPPING_EVENT_RANGE", "shared_event_fingerprints": sorted(shared),
                        "shared_distinct_event_count": len(shared),
                        "left_lines": [index + 1 for index, value in enumerate(left) if value in shared],
                        "right_lines": [index + 1 for index, value in enumerate(right) if value in shared]})
    return results


def _load_sources(root):
    manifest_path = root / SOURCE_MANIFEST
    coverage_path = root / COVERAGE_MANIFEST
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    coverage = json.loads(coverage_path.read_text(encoding="utf-8"))
    return manifest, coverage, [{"path": stable_path(root, path), "sha256": file_hash(path), "bytes": path.stat().st_size}
                                for path in (manifest_path, coverage_path)]


def audit_sources(root, retain_cohorts=False):
    root = Path(root).resolve()
    manifest, coverage, inputs = _load_sources(root)
    issues = []
    manifest_by_id = defaultdict(list)
    coverage_by_id = defaultdict(list)
    for record in manifest:
        manifest_by_id[record["session_id"]].append(record)
    for record in coverage:
        coverage_by_id[record["session_id"]].append(record)
    for sid in sorted(set(manifest_by_id) | set(coverage_by_id)):
        if len(manifest_by_id[sid]) != 1 or len(coverage_by_id[sid]) != 1:
            same_hash = len({row["sha256"] for row in manifest_by_id[sid]}) == 1
            issues.append({"classification": "ACCIDENTAL_DOUBLE_INGESTION" if len(manifest_by_id[sid]) > 1 and same_hash else "CONFLICTING_SOURCE_DATA",
                           "reason": "MANIFEST_SESSION_MULTIPLICITY", "session_id": sid,
                           "manifest_records": manifest_by_id[sid], "coverage_records": coverage_by_id[sid]})
    # The raw manifest row list is retained even if duplicate IDs are found.
    # Only byte-identical references to the same session/hash can select one
    # logical source. Conflicting variants each remain visible in the audit.
    physical_paths = {}
    selected = []
    path_references = defaultdict(list)
    archive_by_sid = defaultdict(list)
    if ARCHIVE.exists():
        for path in sorted(ARCHIVE.rglob("events.jsonl")):
            archive_by_sid[path.parent.name].append(path)
    for source_index, source in enumerate(manifest):
        sid = source["session_id"]
        local_copy = root / RETAINED / sid / "events.jsonl"
        original = resolve(root, source["path"])
        coverage_rows = coverage_by_id.get(sid, [])
        candidate_paths = [("manifest_original", original), ("repository_retained", local_copy),
                           ("current_original", ORIGINALS / sid / "events.jsonl")]
        candidate_paths.extend(("canonical_archive", path) for path in archive_by_sid.get(sid, []))
        candidate_paths.extend(("reconstruction_path", resolve(root, row["path_read"])) for row in coverage_rows)
        preferred = local_copy if local_copy.exists() else original
        if not preferred.exists() and archive_by_sid.get(sid):
            preferred = archive_by_sid[sid][0]
        if not preferred.exists():
            raise ValueError("MISSING_AUTHORITATIVE_SESSION: " + sid)
        selected.append({"session_id": sid, "source_index": source_index,
                         "canonical_source_path": stable_path(root, preferred), "expected_sha256": source["sha256"],
                         "candidate_paths": [stable_path(root, path) for _, path in candidate_paths]})
        for role, path in candidate_paths:
            normalized = stable_path(root, path)
            path_references[normalized].append({"role": role, "session_id": sid, "manifest_index": source_index})
            if path.exists():
                physical_paths.setdefault(normalized, path)

    # Every physical file is independently read and hashed; equal hashes allow
    # its already verified event counts to be reused without parsing copies.
    physical = []
    hashes = defaultdict(list)
    for normalized, path in sorted(physical_paths.items()):
        actual = file_hash(path)
        record = {"canonical_source_path": normalized, "session_id": path.parent.name,
                  "sha256": actual, "file_size": path.stat().st_size,
                  "references": path_references[normalized]}
        physical.append(record)
        hashes[actual].append(record)
        inputs.append({"path": normalized, "sha256": actual, "bytes": record["file_size"]})
    physical_by_path = {record["canonical_source_path"]: record for record in physical}
    cache = {}
    source_metadata = []
    logical = []
    selected_keys = set()
    for entry in selected:
        # A changed repository copy must not conceal a separately retained
        # historical hash variant. Select an exact manifest match if available;
        # all mismatching physical copies remain in the conflict report.
        candidate_paths = entry.pop("candidate_paths")
        current = physical_by_path[entry["canonical_source_path"]]
        if current["sha256"] != entry["expected_sha256"]:
            matches = [path for path in candidate_paths if path in physical_by_path
                       and physical_by_path[path]["sha256"] == entry["expected_sha256"]]
            if matches:
                entry["canonical_source_path"] = matches[0]
        physical_record = physical_by_path[entry["canonical_source_path"]]
        actual = physical_record["sha256"]
        if actual != entry["expected_sha256"]:
            issues.append({"classification": "CONFLICTING_SOURCE_DATA", "reason": "AUTHORITATIVE_SOURCE_HASH_MISMATCH", **entry, "actual_sha256": actual})
        for coverage_row in coverage_by_id.get(entry["session_id"], []):
            if coverage_row["prior_source_sha256"] != actual:
                issues.append({"classification": "CONFLICTING_SOURCE_DATA", "reason": "COVERAGE_SOURCE_HASH_MISMATCH", **entry})
        key = (entry["session_id"], actual)
        if key in selected_keys:
            continue
        selected_keys.add(key)
        analysis = analyze_event_rows(read_events(resolve(root, entry["canonical_source_path"])), entry["session_id"], entry["canonical_source_path"])
        cache[actual] = analysis
        metadata = {**entry, **analysis["metadata"], "sha256": actual, "file_size": physical_record["file_size"],
                    "session_headers": analysis["session_headers"]}
        source_metadata.append(metadata)
        logical.append(analysis)
        issues.extend(analysis["issues"])
        expected = manifest[entry["source_index"]]
        if expected.get("bytes") != metadata["file_size"]:
            issues.append({"classification": "CONFLICTING_SOURCE_DATA", "reason": "MANIFEST_SIZE_MISMATCH", "session_id": entry["session_id"]})
        actual_events = {key: value for key, value in metadata["event_type_counts"].items() if key != "offers"}
        if expected.get("events") != actual_events:
            issues.append({"classification": "CONFLICTING_SOURCE_DATA", "reason": "MANIFEST_EVENT_COUNT_MISMATCH", "session_id": entry["session_id"],
                           "expected": expected.get("events"), "actual": actual_events})
        for row in coverage_by_id.get(entry["session_id"], []):
            if row["counts"].get("offers") != metadata["offer_count"] or row["counts"].get("cohorts") != metadata["cohort_count"]:
                issues.append({"classification": "CONFLICTING_SOURCE_DATA", "reason": "COVERAGE_COUNT_MISMATCH", "session_id": entry["session_id"]})
    for record in physical:
        if record["sha256"] not in cache:
            analysis = analyze_event_rows(read_events(resolve(root, record["canonical_source_path"])), record["session_id"], record["canonical_source_path"])
            cache[record["sha256"]] = analysis
            issues.extend(analysis["issues"])
        record.update(cache[record["sha256"]]["metadata"])
        record["logical_contributor"] = any(entry["canonical_source_path"] == record["canonical_source_path"] for entry in source_metadata)

    source_groups = []
    for path, refs in sorted(path_references.items()):
        if len(refs) > 1 and path in physical_by_path:
            source_groups.append({"classification": "SAME_PATH_SAME_HASH", "severity": "BENIGN_ARCHIVAL_COPY",
                                  "canonical_source_path": path, "sha256": physical_by_path[path]["sha256"],
                                  "references": refs, "meaning": "Multiple provenance references to one physical file; counted once."})
    for value, records in sorted(hashes.items()):
        if len(records) > 1:
            ids = sorted({record["session_id"] for record in records})
            source_groups.append({"classifications": ["DIFFERENT_PATH_SAME_HASH", "SAME_SESSION_ID_SAME_HASH" if len(ids) == 1 else "DIFFERENT_SESSION_ID_SAME_HASH"],
                                  "severity": "BENIGN_ARCHIVAL_COPY", "sha256": value,
                                  "session_ids": ids, "paths": [record["canonical_source_path"] for record in records],
                                  "logical_contributors": [record["canonical_source_path"] for record in records if record["logical_contributor"]]})
        else:
            source_groups.append({"classification": "UNIQUE_SOURCE", "sha256": value, "paths": [records[0]["canonical_source_path"]]})
    physical_by_sid = defaultdict(list)
    for record in physical:
        physical_by_sid[record["session_id"]].append(record)
    for sid, records in sorted(physical_by_sid.items()):
        if len({record["sha256"] for record in records}) > 1:
            group = {"classification": "SAME_SESSION_ID_DIFFERENT_HASH", "severity": "CONFLICTING_SOURCE_DATA", "session_id": sid,
                     "records": [{key: record[key] for key in ("canonical_source_path", "sha256", "file_size")} for record in records]}
            source_groups.append(group)
            issues.append({"classification": "CONFLICTING_SOURCE_DATA", "reason": "SAME_SESSION_ID_DIFFERENT_HASH", "group": group})
    # Compare every distinct-content journal pair. Equal archived files are
    # already classified; comparing their full copies again would only inflate
    # overlap counts with archival repetition.
    for (hash_a, analysis_a), (hash_b, analysis_b) in combinations(sorted(cache.items()), 2):
        sequence_a = [event["canonical_event_sha256"] for event in analysis_a["events"]]
        sequence_b = [event["canonical_event_sha256"] for event in analysis_b["events"]]
        for group in compare_event_sequences(sequence_a, sequence_b):
            source_groups.append({**group, "severity": "UNRESOLVED", "left_sha256": hash_a, "right_sha256": hash_b,
                                  "left_paths": [record["canonical_source_path"] for record in hashes[hash_a]],
                                  "right_paths": [record["canonical_source_path"] for record in hashes[hash_b]]})

    all_events = [event for analysis in logical for event in analysis["events"]]
    row_index = defaultdict(list)
    canonical_index = defaultdict(list)
    logical_payload_index = defaultdict(list)
    for event in all_events:
        row_index[event["row_sha256"]].append(event)
        canonical_index[event["canonical_event_sha256"]].append(event)
        logical_payload_index[(event["declared_session_id"], event["request_id"], event["cohort_id"],
                               event["event_type"], event["payload_sha256"])].append(event)
    event_groups = duplicate_groups(row_index, "EXACT_DUPLICATE_EVENT_ROWS", "ACCIDENTAL_DOUBLE_INGESTION")
    event_groups += duplicate_groups(canonical_index, "SAME_LOGICAL_EVENT_DIFFERENT_SOURCE_LINES", "UNRESOLVED")
    event_groups += duplicate_groups(logical_payload_index, "SAME_LOGICAL_EVENT_IDENTICAL_PAYLOAD", "UNRESOLVED")
    event_groups += [group for analysis in logical for group in analysis["phase_findings"] + analysis["cohort_findings"]]
    requests = [request for analysis in logical for request in analysis["requests"]]
    request_findings = [group for analysis in logical for group in analysis["request_findings"]]
    request_class_counts = Counter(classification for request in requests for classification in request["classifications"])
    source_class_counts = Counter(classification for group in source_groups for classification in group.get("classifications", [group.get("classification")]))
    totals = {
        "PHYSICAL_SOURCE_FILES_FOUND": len(physical),
        "LOGICAL_UNIQUE_SESSIONS": len({entry["session_id"] for entry in source_metadata}),
        "LOGICAL_SOURCE_VARIANTS": len(source_metadata),
        "TOTAL_EVENTS": len(all_events),
        "TOTAL_REQUESTS": sum(entry["request_count"] for entry in source_metadata),
        "TOTAL_REQUEST_STARTED_RECORDS": sum(entry["request_started_count"] for entry in source_metadata),
        "TOTAL_COHORTS": sum(entry["cohort_count"] for entry in source_metadata),
        "TOTAL_OFFERS": sum(entry["offer_count"] for entry in source_metadata),
        "PROVEN_ACCIDENTAL_DUPLICATE_SESSIONS": sum(count - 1 for count in Counter(
            (record["session_id"], record["sha256"]) for record in manifest).values()),
        "PROVEN_ACCIDENTAL_DUPLICATE_REQUESTS": sum(len(group["records"]) - 1 for group in request_findings
                                                    if group["classification"] == "EXACT_DUPLICATE_REQUEST_RECORD" and group["event_phase"] == "request_started"),
    }
    result = {
        "source_file_report": {"schema_version": 1, "authority": SOURCE_MANIFEST,
                               "coverage_authority": COVERAGE_MANIFEST,
                               "selection_rule": "Use repository-retained copy when present, otherwise historical original, otherwise retained archive; verify exact authoritative hash before accepting evidence.",
                               "copy_counting_rule": "One source per manifest session/hash; physical copies are inspected but do not contribute another experiment.",
                               "class_counts": {key: source_class_counts[key] for key in SOURCE_CLASSES},
                               "manifest_rows": len(manifest), "physical_sources": physical,
                               "logical_sources": source_metadata, "duplicate_groups": source_groups, "issues": issues},
        "event_report": {"schema_version": 1, "logical_event_count": len(all_events),
                         "event_type_counts": dict(sorted(Counter(event["event_type"] for event in all_events).items())),
                         "duplicate_group_class_counts": dict(sorted(Counter(group["classification"] for group in event_groups).items())),
                         "exact_duplicate_event_rows": sum(len(group["records"]) - 1 for group in event_groups if group["classification"] == "EXACT_DUPLICATE_EVENT_ROWS"),
                         "identity_fields": ["declared_session_id", "request_id", "cohort_id", "event_type", "timestamp_utc", "payload_sha256"],
                         "physical_provenance_fields": ["source_path", "source_line", "row_sha256"],
                         "phase_rule": "request_started and request_transmitted are different lifecycle phases, never conflicting records merely because their request ID matches.",
                         "archival_events": "Reported through physical source hash groups; not re-ingested into logical event comparison.",
                         "duplicate_groups": event_groups},
        "request_report": {"schema_version": 1, "logical_request_count": totals["TOTAL_REQUESTS"],
                           "class_counts": {key: request_class_counts[key] for key in REQUEST_CLASSES},
                           "same_inputs_rule": "Distinct request IDs with equal terminal/slider/character inputs remain distinct rolls.",
                           "requests": requests, "duplicate_groups": request_findings},
        "source_metadata": source_metadata, "counts": totals, "inputs": inputs, "issues": issues,
    }
    if retain_cohorts:
        result["cohorts"] = list(iter_cohorts(root, source_metadata))
    return result


def iter_cohorts(root, source_metadata):
    """Stream every original response with unambiguous or explicit variant context.

    A repeated request header is never silently overwritten. Only one unique
    payload hash permits a primary request; otherwise request is empty and all
    variants remain attached. The same rule applies to session headers.
    """
    root = Path(root)
    for source in source_metadata:
        sid = source["session_id"]
        path_name = source["canonical_source_path"]
        path = resolve(root, path_name)
        sessions = []
        requests = defaultdict(list)
        for line_number, row_hash, event, event_hash in read_events(path):
            payload = event.get("payload") or {}
            if event.get("event_type") == "session_started":
                sessions.append({"source_line": line_number, "payload_sha256": digest(payload), "payload": without_packet_bytes(payload)})
            elif event.get("event_type") == "request_started":
                requests[event.get("request_id")].append({"source_line": line_number, "payload_sha256": digest(payload), "payload": without_packet_bytes(payload)})
            elif event.get("event_type") == "cohort_received":
                variants = requests.get(event.get("request_id"), [])
                request_hashes = {record["payload_sha256"] for record in variants}
                session_hashes = {record["payload_sha256"] for record in sessions}
                yield {"session_id": sid, "source_path": path_name, "source_line": line_number,
                       "source_row_sha256": row_hash, "source_event_sha256": event_hash,
                       "event": event, "payload": payload,
                       "request": variants[0]["payload"] if len(request_hashes) == 1 else {},
                       "request_variants": list(variants),
                       "session_metadata": sessions[0]["payload"] if len(session_hashes) == 1 else {},
                       "session_metadata_variants": list(sessions),
                       "request_context_ambiguous": len(request_hashes) > 1,
                       "session_context_ambiguous": len(session_hashes) > 1}
