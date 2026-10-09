"""Read-only identity, artifact-key, and cross-corpus duplicate audit helpers.

No tolerance, name normalization, or identity merging is used here. JSONL
artifacts are streamed, including the complete destination-by-QL matrix.
"""
from __future__ import annotations

import base64
import gzip
import hashlib
import json
import math
import re
import struct
from collections import defaultdict
from pathlib import Path


ACG = "docs/generated/missions/acgentrance-reconstruction"
ELIG = "docs/generated/missions/destination-eligibility-analysis"
LOC = "docs/generated/missions/location-reconciliation"


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
                      allow_nan=False)


def sha256_file(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def input_identity(path, root=None, label=None):
    path = Path(path)
    return {"path": label or (path.relative_to(root).as_posix() if root else path.name),
            "bytes": path.stat().st_size, "sha256": sha256_file(path)}


def read_rows(path):
    path = Path(path)
    opener = gzip.open if path.suffix == ".gz" else open
    decoder = json.JSONDecoder()
    with opener(path, "rt", encoding="utf-8-sig") as stream:
        for line_number, line in enumerate(stream, 1):
            if line.strip():
                try:
                    row = decoder.decode(line)
                except json.JSONDecodeError as error:
                    raise ValueError(
                        f"INVALID_JSONL: {path.as_posix()} source_line={line_number}: {error}"
                    ) from error
                yield line_number, row


def field(row, name):
    for component in name.split("."):
        row = row[component]
    return row


def primary_key_audit(path, keyfields, root=None, design=""):
    """Audit actual JSONL rows; duplicate groups retain every full source row.

    Missing key fields are errors, not silently replaced with nulls. Explicit
    null field values, where present in the input, remain part of the key.
    """
    path = Path(path)
    seen, duplicate_keys, missing, count = {}, set(), [], 0
    for line_number, row in read_rows(path):
        count += 1
        try:
            key = canonical([field(row, name) for name in keyfields])
        except (KeyError, TypeError) as error:
            missing.append({"source_line": line_number, "missing_component": str(error),
                            "row": row})
            continue
        if key in seen:
            duplicate_keys.add(key)
        else:
            seen[key] = line_number
    groups = defaultdict(list)
    if duplicate_keys:
        for line_number, row in read_rows(path):
            try:
                key = canonical([field(row, name) for name in keyfields])
            except (KeyError, TypeError):
                continue
            if key in duplicate_keys:
                groups[key].append({"source_line": line_number, "row": row,
                                    "decoded_row_sha256": hashlib.sha256(canonical(row).encode()).hexdigest()})
    result_groups = []
    for key, rows in sorted(groups.items()):
        identical = len({row["decoded_row_sha256"] for row in rows}) == 1
        result_groups.append({"primary_key": json.loads(key), "rows": rows,
                              "identical_row_payloads": identical,
                              "classification": "UNRESOLVED" if identical else "CONFLICTING_SOURCE_DATA",
                              "reason": "Duplicate artifact key does not by itself prove a duplicate physical capture."})
    return {**input_identity(path, root), "primary_key": list(keyfields),
            "row_design": design, "classification": "GENERATED_ARTIFACT_DESIGN",
            "row_count": count, "unique_primary_keys": len(seen),
            "duplicate_primary_keys": len(duplicate_keys),
            "duplicate_rows": sum(len(rows) - 1 for rows in groups.values()),
            "groups": result_groups, "missing_primary_key_rows": missing,
            "issues": (["MISSING_PRIMARY_KEY_FIELDS"] if missing else []) +
                      (["DUPLICATE_PRIMARY_KEYS"] if duplicate_keys else [])}


def placement_collision_audit(rows):
    """Independently group complete identities, raw bits, numeric XYZ and names.

    The numeric tuple intentionally treats +0 and -0 as equal. The authoritative
    bit tuple does not. Non-finite positions and any discrepancy between raw
    transform bytes and JSON component bits remain explicit integrity issues.
    """
    maps = {name: defaultdict(list) for name in (
        "complete_identity", "instance", "bits", "numeric", "name_pf", "name",
        "template", "transform", "pf_transform")}
    issues = []
    for index, row in enumerate(rows, 1):
        identity = (row["identity_type"], row["identity_instance_uint32"])
        transform = bytes.fromhex(row["raw_transform_bytes"])
        if len(transform) != 28:
            raise ValueError(f"Placement row {index}: transform is not seven binary32 values")
        xyz = struct.unpack("<fff", transform[:12])
        xyz_bits = tuple(f"0x{value:08X}" for value in struct.unpack("<III", transform[:12]))
        pf = row["explicit_playfield_id"]
        if not all(math.isfinite(value) for value in xyz):
            raise ValueError(f"Placement row {index}: non-finite position")
        expected_components = list(row["raw_position_components"]) + list(row["raw_rotation_components"])
        if struct.pack("<7f", *expected_components) != transform:
            issues.append({"classification": "CONFLICTING_SOURCE_DATA", "source_line": index,
                           "reason": "JSON transform components differ from serialized transform bits"})
        member = {"source_line": index, "identity_type": identity[0],
                  "identity_instance": identity[1], "playfield": pf,
                  "name": row["display_name_exact"], "xyz_bits": list(xyz_bits),
                  "xyz": list(xyz), "raw_transform_bytes": row["raw_transform_bytes"],
                  "source_record_offset": row.get("source_record_offset")}
        keys = {"complete_identity": identity, "instance": identity[1],
                "bits": (pf,) + xyz_bits, "numeric": (pf,) + xyz,
                "name_pf": (pf, row["display_name_exact"]), "name": row["display_name_exact"],
                "template": (row.get("template_resource_type"), row.get("template_identity_instance")),
                "transform": transform.hex(), "pf_transform": (pf, transform.hex())}
        for category, key in keys.items():
            maps[category][key].append(member)

    def duplicate_groups(category, classification, different_identities=False):
        result = []
        for key, members in maps[category].items():
            identities = {(r["identity_type"], r["identity_instance"]) for r in members}
            if len(members) < 2 or (different_identities and len(identities) < 2):
                continue
            result.append({"key": list(key) if isinstance(key, tuple) else key,
                           "row_count": len(members), "distinct_identities": len(identities),
                           "classification": classification, "rows": members})
        return sorted(result, key=lambda group: canonical(group["key"]))

    identities = duplicate_groups("complete_identity", "CONFLICTING_SOURCE_DATA")
    bits = duplicate_groups("bits", "UNRESOLVED", True)
    numeric = duplicate_groups("numeric", "UNRESOLVED", True)
    different_bits = [group for group in numeric if len({tuple(r["xyz_bits"]) for r in group["rows"]}) > 1]
    names = duplicate_groups("name", "DESCRIPTIVE_METADATA_COLLISION", True)
    pf_names = duplicate_groups("name_pf", "DESCRIPTIVE_METADATA_COLLISION", True)
    spanning = [group for group in names if len({r["playfield"] for r in group["rows"]}) > 1]
    for group in names:
        group["playfields"] = sorted({row["playfield"] for row in group["rows"]})
    return {
        "placement_identity": {
            "complete_identity_key": ["identity_type", "identity_instance_uint32"],
            "complete_identity_groups": identities,
            "instance_groups": duplicate_groups("instance", "UNRESOLVED"),
            "referenced_template_groups": duplicate_groups("template", "DESCRIPTIVE_METADATA_COLLISION", True),
            "complete_transform_groups": duplicate_groups("transform", "UNRESOLVED", True),
            "same_playfield_complete_transform_groups": duplicate_groups("pf_transform", "UNRESOLVED", True),
            "interpretation": "Shared templates and cross-playfield transforms are not placement identity. No rows are removed."},
        "placement_coordinate": {
            "bit_key": ["explicit_playfield_id", "X_binary32_bits", "Y_binary32_bits", "Z_binary32_bits"],
            "exact_bit_groups": bits, "numeric_xyz_groups": numeric,
            "numeric_xyz_different_bits_groups": different_bits,
            "comparison": "Raw little-endian binary32 XYZ; no rounding, tolerance, or nearest-neighbor comparison."},
        "placement_name": {"classification": "DESCRIPTIVE_METADATA_COLLISION",
                           "unique_names": len(maps["name"]),
                           "multi_identity_name_groups": names,
                           "same_playfield_name_groups": pf_names,
                           "names_spanning_playfields": [group["key"] for group in spanning],
                           "largest_families": [{"name": g["key"], "identities": g["distinct_identities"],
                                                 "playfields": g["playfields"]}
                                                for g in sorted(names, key=lambda g: (-g["distinct_identities"], g["key"]))[:20]],
                           "interpretation": "Names are preserved verbatim as descriptive metadata; never identity keys."},
        "counts": {"CLIENT_PLACEMENTS": sum(len(v) for v in maps["complete_identity"].values()),
                   "UNIQUE_COMPLETE_PLACEMENT_IDENTITIES": len(maps["complete_identity"]),
                   "DUPLICATE_COMPLETE_PLACEMENT_IDENTITIES": len(identities),
                   "DUPLICATE_PLACEMENT_IDENTITY_ROWS": sum(g["row_count"] - 1 for g in identities),
                   "EXACT_SAME_PLAYFIELD_XYZ_COLLISIONS": len(bits),
                   "NUMERIC_XYZ_DIFFERENT_BITS_COLLISIONS": len(different_bits),
                   "MULTI_IDENTITY_NAME_GROUPS": len(names),
                   "SAME_PLAYFIELD_NAME_GROUPS": len(pf_names),
                   "NAMES_SPANNING_PLAYFIELDS": len(spanning),
                   "UNIQUE_PLACEMENT_NAMES": len(maps["name"])},
        "issues": issues}


def audit_catalog(root: Path):
    root = Path(root)
    catalog_path = root / ACG / "acgentrance-records.jsonl"
    result = placement_collision_audit([row for _, row in read_rows(catalog_path)])
    offer_key = ("session_id", "source_line", "request_id", "cohort_id", "offer_index")
    specifications = [
        (ACG, "acgentrance-records.jsonl", ("identity_type", "identity_instance_uint32"), "One row per complete client placement identity."),
        (ACG, "mission-location-full-corpus-reconciliation.jsonl.gz", offer_key, "One row per retained offer provenance key; repeated destinations are valid observations."),
        (LOC, "all-offers.jsonl.gz", offer_key, "One row per retained offer provenance key; historical unresolved assignments remain unchanged."),
        (ELIG, "mission-offer-analysis-inventory.jsonl.gz", offer_key, "One row per retained offer provenance key."),
        (ELIG, "destination-condition-evidence-matrix.jsonl.gz", ("identity_type", "identity_instance", "experimental_condition"), "One row per placement and complete condition including mission type; repeated placement identities across conditions are intentional."),
        (ELIG, "destination-ql-evidence-matrix.jsonl.gz", ("identity_type", "identity_instance", "mission_ql"), "One row per complete placement identity and QL; unobserved QLs are retained."),
        (ELIG, "observed-destination-frequency.jsonl.gz", ("condition", "destination_identity_instance"), "One row per coherent input condition and observed destination; condition is part of identity."),
        (ELIG, "observed-playfield-frequency.jsonl.gz", ("condition", "destination_playfield"), "One row per coherent input condition and observed playfield."),
        (ELIG, "character-level-destination-matrix.jsonl.gz", ("character_level", "destination_identity"), "One row per character level and complete destination identity."),
        (ELIG, "character-level-playfield-matrix.jsonl.gz", ("character_level", "destination_playfield"), "One row per character level and destination playfield."),
        (ELIG, "terminal-destination-matrix.jsonl.gz", ("mission_terminal_identity", "destination_identity"), "One row per terminal identity and complete destination identity."),
        (ELIG, "terminal-playfield-matrix.jsonl.gz", ("mission_terminal_identity", "destination_playfield"), "One row per terminal identity and destination playfield."),
        (ELIG, "mission-type-destination-matrix.jsonl.gz", ("mission_type", "destination_identity"), "One row per mission type and complete destination identity."),
        (ELIG, "mission-type-playfield-matrix.jsonl.gz", ("mission_type", "destination_playfield"), "One row per mission type and destination playfield."),
    ]
    artifacts = [primary_key_audit(root / folder / name, keys, root, design)
                 for folder, name, keys, design in specifications]
    result["artifact_primary_keys"] = {"artifacts": artifacts,
        "duplicate_primary_key_groups": sum(a["duplicate_primary_keys"] for a in artifacts),
        "duplicate_rows": sum(a["duplicate_rows"] for a in artifacts),
        "interpretation": "Evidence count reduction requires physical provenance proof; matrix condition dimensions must not be dropped."}
    result["inputs"] = [{k: a[k] for k in ("path", "bytes", "sha256")} for a in artifacts]
    result["issues"].extend({"artifact": a["path"], "reason": issue} for a in artifacts for issue in a["issues"])
    return result


def _decode_wire(value):
    """Current data stores hex; older extracted data can use strict base64."""
    if re.fullmatch(r"[0-9a-fA-F]+", value) and len(value) % 2 == 0:
        return bytes.fromhex(value)
    return base64.b64decode(value, validate=True)


def audit_cross_corpus(root, september_raw_hashes, september_offer_hashes,
                       september_mission_ids, july_data_root=None,
                       september_body_hashes=None, september_session_ids=()):
    """Compare available exact hashes, retaining missing July provenance.

    Optional private data is read-only and recorded only by stable logical
    labels and hashes. No private payload, absolute repository/worktree path,
    or runtime content is copied into output.
    """
    root = Path(root)
    raw_hashes = set(september_raw_hashes)
    # The caller may provide one combined full-packet/body digest index.
    body_hashes = set(september_body_hashes) if september_body_hashes is not None else raw_hashes
    metadata_path = root / "docs/evidence/RK_TERMINAL_MISSION_STAGE1_EVIDENCE_20260728.md"
    metadata = metadata_path.read_text(encoding="utf-8-sig")
    sessions = sorted(set(re.findall(r"`(202607\d{2}-\d{6})`", metadata)))
    inputs = [input_identity(metadata_path, root)]
    report_counts = re.search(r"contains (\d+) five-offer responses, or ([\d,]+) observed offers", metadata)
    result = {"classification": "UNRESOLVED",
              "july_reported_responses": int(report_counts[1]) if report_counts else None,
              "july_reported_offers": int(report_counts[2].replace(",", "")) if report_counts else None,
              "july_session_ids": sessions,
              "session_id_intersection": sorted(set(sessions) & set(september_session_ids)),
              "report_date": "2026-07-28", "september_offer_hashes_compared": len(set(september_offer_hashes)),
              "september_mission_identities_available": len(set(september_mission_ids)),
              "roll_body_records": [], "full_packet_records": [], "layout_provenance": [],
              "exact_hash_matches": [], "inputs": inputs,
              "unresolved": ["Original July session journals and complete per-offer raw provenance are unavailable to this audit.",
                  "Session labels and capture dates differ but do not alone prove physical packet non-overlap.",
                  "No complete July offer boundary inventory with request/mission identities is retained in these metadata artifacts; only fixed first-offer identity fields can be read independently.",
                  "A body hash covers response bytes without its transport header; matching bodies alone do not prove accidental ingestion."]}
    if july_data_root is None:
        result["unresolved"].append("Optional private July-derived wire data was not supplied.")
        return result
    july_data_root = Path(july_data_root)
    if (july_data_root / "Missions").is_dir():
        july_data_root = july_data_root / "Missions"
    field_map_path = root / ACG / "mission-offer-destination-field-map.json"
    inputs.append(input_identity(field_map_path, root))
    for filename in ("RollBodies.json", "RollTemplate.json", "Layouts.json"):
        path = july_data_root / filename
        if not path.is_file():
            result["unresolved"].append(f"Optional July-derived input unavailable: {filename}")
            continue
        inputs.append(input_identity(path, label=f"external-game-data/Missions/{filename}"))
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if filename == "Layouts.json":
            for layout in data:
                for record in layout.get("Provenance", []):
                    entry = {key: record.get(key) for key in ("CaptureId", "CapturedUtc", "CsvLine", "Direction",
                             "MessageType", "RawPacketLength", "RawPacketSha256", "Source")}
                    digest = (entry.get("RawPacketSha256") or "").lower()
                    entry["matches_september_full_packet"] = digest in raw_hashes
                    result["layout_provenance"].append(entry)
                    if entry["matches_september_full_packet"]:
                        result["exact_hash_matches"].append({"kind": "JULY_LAYOUT_PROVENANCE_HASH", "sha256": digest,
                                                            "classification": "UNRESOLVED"})
            continue
        records = data if isinstance(data, list) else [data]
        for index, encoded in enumerate(records):
            raw = _decode_wire(encoded)
            is_packet = len(raw) >= 20 and raw[16:20] == bytes.fromhex("5c436609")
            is_body = raw[:4] == bytes.fromhex("5c436609")
            digest = hashlib.sha256(raw).hexdigest()
            entry = {"source": f"external-game-data/Missions/{filename}", "index": index,
                     "bytes": len(raw), "sha256": digest, "transport_header_present": is_packet,
                     "quest_alternative_type_verified": is_packet or is_body,
                     "exact_september_hash_match": digest in (raw_hashes if is_packet else body_hashes),
                     "capture_provenance": "UNRESOLVED_NO_PER_BODY_SESSION_REQUEST_MAPPING"}
            # Fixed envelope fields are independently documented in the field
            # map. Variable later offer starts require missing July schemas.
            offset = 16 if is_packet else 0
            if (is_packet or is_body) and len(raw) >= offset + 43:
                count = raw[offset + 34]
                first_identity = struct.unpack_from(">II", raw, offset + 35)
                entry["advertised_offer_count"] = count
                entry["first_mission_identity"] = {"type": first_identity[0], "instance": first_identity[1]}
                entry["first_mission_identity_matches_september"] = first_identity in september_mission_ids
                entry["first_mission_identity_interpretation"] = "A reused mission identity alone is not physical response overlap."
            if is_packet:
                entry["body_sha256"] = hashlib.sha256(raw[16:]).hexdigest()
                entry["body_matches_september"] = entry["body_sha256"] in body_hashes
                result["full_packet_records"].append(entry)
            else:
                result["roll_body_records"].append(entry)
            if entry["exact_september_hash_match"]:
                result["exact_hash_matches"].append({"kind": "FULL_PACKET" if is_packet else "RESPONSE_BODY", "sha256": digest,
                                                    "classification": "UNRESOLVED"})
    result["available_full_packet_hash_matches"] = sum(r["exact_september_hash_match"] for r in result["full_packet_records"])
    result["available_body_hash_matches"] = sum(r["exact_september_hash_match"] for r in result["roll_body_records"])
    result["distinct_available_july_response_body_hashes"] = len(
        {r["sha256"] for r in result["roll_body_records"]} |
        {r["body_sha256"] for r in result["full_packet_records"]})
    result["retained_template_body_relationships"] = [
        {"template_source": packet["source"], "template_index": packet["index"],
         "roll_body_indexes": [r["index"] for r in result["roll_body_records"]
                               if r["sha256"] == packet["body_sha256"]],
         "classification": "GENERATED_ARTIFACT_DESIGN",
         "interpretation": "Matching template/body bytes, if any, do not establish two independent captured cohorts; unmatched bytes retain unresolved capture provenance."}
        for packet in result["full_packet_records"]]
    result["disposition"] = "Keep July and September separate. Available exact hash comparisons are bounded; absent July raw provenance remains UNRESOLVED."
    return result
