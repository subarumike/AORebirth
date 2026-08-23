#!/usr/bin/env python3
"""Build an evidence-first inventory of every AOSharp capture in the repository."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
from collections import Counter, defaultdict
from pathlib import Path


CAPTURE_ID = re.compile(r"^20\d{6}-\d{6}$")
CAPTURE_ID_IN_TEXT = re.compile(r"20\d{6}-\d{6}")
MESSAGE_PLAYFIELD = re.compile(r"\bPlayfieldId=(\d+)\b")
STATIC_PLAYFIELD = re.compile(r"\bplayfield=\(Playfield:([0-9A-Fa-f]+)\)")
RESOURCE_PLAYFIELD = re.compile(r"\bresource:\s*(\d+)\b", re.IGNORECASE)
IDENTITY = re.compile(r"\((Playfield|Playfield2):([0-9A-Fa-f]+)\)")
EVENT_PLAYFIELD = re.compile(
    r"\bplayfield=(\((?:Playfield|Playfield2|None):[0-9A-Fa-f]+\)|\d+)",
    re.IGNORECASE,
)

CAPTURE_MARKERS = {
    "capture-session.json",
    "capture_info.json",
    "events.log",
    "packets.hex.log",
    "raw-packets.csv",
}

STRONG_SUBWAY_NAMES = {
    "abmouth supremus",
    "architect striker",
    "bloodcreeper",
    "discarded pet",
    "disobedient bot",
    "eumenides",
    "filth flea",
    "infected attendant",
    "lost thought",
    "melded patterns",
    "neural burnout",
    "redundant scan",
    "slum runner",
    "stim fiend",
    "vergil aeneid",
    "workman striker",
}

SUBWAY_LOCATION_TERMS = {
    "abandoned mall",
    "abandoned subway",
    "condemned subway",
    "shopping arcade",
}

TEXT_EVIDENCE_FILES = (
    "events.log",
    "system-messages.log",
    "npc-interactions.log",
)

MAX_DECODED_RESOURCE_PLAYFIELD = 65535

# The complete 298-folder corpus through this timestamp was manually reviewed
# against capture/event playfields, packet presence, PF127 artifacts, and zoning
# boundaries on 2026-07-17. Newer captures fall through to the evidence rules.
REVIEWED_CAPTURE_CUTOFF = "20260717-220340"
PF127_CORPUS_RUNTIME_INSTANCES = {127, 1187842, 1363982, 1388552, 1407006}
EXPECTED_REVIEWED_SUBWAY_ONLY = {
    "20260708-143600",
    "20260709-205921",
    "20260709-210452",
    "20260709-212115",
    "20260709-212336",
    "20260709-213711",
    "20260709-225408",
    "20260710-205400",
    "20260710-211430",
    "20260712-160257",
    "20260712-161506",
    "20260712-195019",
    "20260712-223719",
    "20260712-224608",
    "20260712-224840",
    "20260712-232137",
    "20260712-232711",
    "20260712-232848",
    "20260712-234401",
    "20260713-014714",
    "20260713-033511",
    "20260714-171439",
    "20260714-185728",
    "20260714-202820",
    "20260716-033326",
    "20260716-034104",
    "20260716-034433",
    "20260716-034559",
    "20260716-034656",
    "20260716-215947",
    "20260716-220255",
    "20260716-220400",
    "20260716-221358",
    "20260716-221748",
    "20260716-222007",
    "20260716-222201",
    "20260717-012651",
    "20260717-214612",
    "20260717-214751",
    "20260717-215250",
    "20260717-220340",
}
EXPECTED_REVIEWED_MIXED = {
    "20260708-004038",
    "20260708-175514",
    "20260708-180248",
    "20260708-181729",
    "20260708-182237",
    "20260708-185451",
    "20260708-185543",
    "20260708-223814",
    "20260708-225850",
    "20260709-164219",
    "20260709-164414",
    "20260709-165805",
    "20260709-165538",
    "20260709-174823",
    "20260709-184655",
    "20260709-193914",
    "20260709-220439",
    "20260709-222339",
    "20260710-202132",
    "20260710-202553",
    "20260710-211346",
    "20260710-212455",
    "20260711-170337",
    "20260711-172140",
    "20260711-172309",
    "20260712-153918",
    "20260712-154941",
    "20260712-155528",
    "20260713-013906",
    "20260714-182132",
    "20260717-012522",
}
EXPECTED_REVIEWED_INSUFFICIENT = {
    "20260509-182711",
    "20260528-210106",
    "20260621-013227",
    "20260622-081426",
}
EXPECTED_REVIEWED_SUBWAY_CAPTURE_COUNT = 72
EXPECTED_REVIEWED_SUBWAY_RAW_CAPTURE_COUNT = 69
EXPECTED_REVIEWED_SUBWAY_NO_RAW = {
    "20260714-171439",
    "20260714-185728",
    "20260714-202820",
}

ARTIFACT_COLUMNS = (
    "capture-session.json",
    "capture_info.json",
    "capture-health.json",
    "enemy-dossier.json",
    "events.log",
    "packets.hex.log",
    "raw-packets.csv",
    "enemy-state.csv",
    "enemy-combat.csv",
    "enemy-movement.csv",
    "npc-lifecycle.csv",
    "corpse-full-updates.csv",
    "corpse-loot-observations.csv",
    "enemy-respawns.csv",
)

EVIDENCE_DIGEST_FILES = (
    "capture-session.json",
    "capture_info.json",
    "capture-health.json",
    "events.log",
    "packets.hex.log",
    "raw-packets.csv",
)

INVENTORY_COLUMNS = (
    "capture_id",
    "capture_path",
    "evidence_digest",
    "classification",
    "confidence",
    "pf127_signal",
    "capture_playfield_id",
    "event_playfield_ids",
    "resource_playfield_ids",
    "runtime_playfield_ids",
    "character",
    "validation_status",
    "raw_packet_evidence",
    "packets_hex_bytes",
    "raw_packets_bytes",
    "packets_hex_rows",
    "raw_packets_rows",
    "enemy_name_count",
    "enemy_names",
    "subway_terms",
    "repository_reference_count",
    "repository_references",
    "implementation_reference_count",
    "implementation_references",
    "artifacts",
    "reason",
)

RETENTION_COLUMNS = (
    "capture_id",
    "evidence_digest",
    "analysis_state",
    "evidence_coverage",
    "used_by",
    "derived_artifacts",
    "raw_archive_path",
    "raw_archive_digest",
    "unresolved_gaps",
    "retention_state",
    "approved_by",
    "approved_at",
    "reason",
)

RETENTION_STATES = {"retain", "archive_required", "discard_approved"}
ANALYSIS_STATES = {"unreviewed", "partial", "complete"}
EVIDENCE_COVERAGE_STATES = {"unknown", "partial", "complete"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    parser.add_argument(
        "--output-csv",
        default="docs/generated/aosharp_capture_inventory.csv",
    )
    parser.add_argument(
        "--output-md",
        default="docs/generated/aosharp_capture_inventory.md",
    )
    parser.add_argument(
        "--retention-ledger",
        default="docs/evidence/aosharp_capture_retention.csv",
        help="Tracked fail-closed raw-capture retention authority.",
    )
    parser.add_argument(
        "--retention-md",
        default="docs/generated/aosharp_capture_retention.md",
        help="Generated human-readable retention report.",
    )
    parser.add_argument(
        "--validate-current",
        action="store_true",
        help=(
            "Validate current folders against the accepted inventory without "
            "rewriting either inventory output."
        ),
    )
    parser.add_argument(
        "--exclude-capture-id",
        action="append",
        default=[],
        help=(
            "Exclude a newly discovered capture identity from this run. "
            "An already accepted identity cannot be excluded."
        ),
    )
    parser.add_argument(
        "--capture-id-cutoff",
        default="",
        help=(
            "Process discovered capture identities at or before this timestamp ID. "
            "Newer accepted rows remain preserved but are not refreshed."
        ),
    )
    return parser.parse_args()


def load_json(path: Path) -> dict:
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            value = json.load(stream)
        return value if isinstance(value, dict) else {}
    except (OSError, UnicodeError, json.JSONDecodeError):
        return {}


def discover_capture_directories(repo_root: Path) -> list[Path]:
    captures: list[Path] = []
    ignored = {".git", "obj", "packages"}
    for current, directories, files in os.walk(repo_root):
        path = Path(current)
        directories[:] = [name for name in directories if name not in ignored]
        if capture_id_from_directory_name(path.name) and CAPTURE_MARKERS.intersection(files):
            captures.append(path)
            directories[:] = []
    return sorted(captures, key=lambda path: path.relative_to(repo_root).as_posix())


def capture_id_from_directory_name(directory_name: str) -> str:
    match = CAPTURE_ID_IN_TEXT.search(directory_name)
    return match.group(0) if match else ""


def collect_repository_references(repo_root: Path) -> tuple[dict[str, set[str]], dict[str, set[str]]]:
    documented: dict[str, set[str]] = defaultdict(set)
    indexed: dict[str, set[str]] = defaultdict(set)
    roots = (repo_root / "docs", repo_root / "AORebirth" / "Server" / "ZoneEngine")
    extensions = {".cs", ".csv", ".json", ".md", ".py", ".txt"}
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in extensions:
                continue
            if path.name in {
                "aosharp_capture_inventory.csv",
                "aosharp_capture_inventory.md",
                "aosharp_capture_retention.csv",
                "aosharp_capture_retention.md",
                "aosharp_subway_capture_content.csv",
                "aosharp_subway_capture_content.md",
            }:
                continue
            try:
                text = path.read_text(encoding="utf-8-sig", errors="replace")
            except OSError:
                continue
            ids = set(CAPTURE_ID_IN_TEXT.findall(text))
            if not ids:
                continue
            relative = path.relative_to(repo_root).as_posix()
            for capture_id in ids:
                documented[capture_id].add(relative)
                if relative.startswith("docs/generated/") or relative.endswith(".cs"):
                    indexed[capture_id].add(relative)
    return documented, indexed


def integer_value(value: object) -> int | None:
    if isinstance(value, int):
        return value
    if isinstance(value, str) and value.isdigit():
        return int(value)
    return None


def add_resource_values(value: object, destination: set[int]) -> None:
    if isinstance(value, dict):
        for key, item in value.items():
            if key.lower() == "resourceplayfieldid":
                parsed = integer_value(item)
                if parsed and parsed > 0:
                    destination.add(parsed)
            add_resource_values(item, destination)
    elif isinstance(value, list):
        for item in value:
            add_resource_values(item, destination)


def runtime_identity(value: object) -> str:
    return value if isinstance(value, str) and IDENTITY.search(value) else ""


def runtime_instance(value: str) -> int | None:
    match = IDENTITY.search(value)
    if not match or match.group(1) != "Playfield2":
        return None
    return int(match.group(2), 16)


def normalize_playfield(value: object) -> int | None:
    parsed = integer_value(value)
    if parsed is not None:
        return parsed if parsed > 0 else None
    if not isinstance(value, str):
        return None
    match = IDENTITY.search(value)
    if match:
        parsed = int(match.group(2), 16)
        return parsed if parsed > 0 else None
    return None


def event_playfields(path: Path) -> set[int]:
    result: set[int] = set()
    if not path.exists():
        return result
    try:
        with path.open("r", encoding="utf-8-sig", errors="replace") as stream:
            for line in stream:
                for match in EVENT_PLAYFIELD.finditer(line):
                    parsed = normalize_playfield(match.group(1))
                    if parsed is not None:
                        result.add(parsed)
    except OSError:
        return set()
    return result


def nested_true(value: object, key: str) -> bool:
    if isinstance(value, dict):
        for candidate, item in value.items():
            if candidate == key and item is True:
                return True
            if nested_true(item, key):
                return True
    elif isinstance(value, list):
        return any(nested_true(item, key) for item in value)
    return False


def file_size(path: Path) -> int:
    return path.stat().st_size if path.exists() else 0


def packets_hex_row_count(path: Path) -> int:
    try:
        with path.open("r", encoding="utf-8-sig", errors="replace") as stream:
            return sum(1 for line in stream if line.strip())
    except OSError:
        return 0


def raw_packets_csv_row_count(path: Path) -> int:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            reader = csv.reader(stream)
            if next(reader, None) is None:
                return 0
            return sum(
                1
                for row in reader
                if any(value.strip() for value in row)
            )
    except (OSError, UnicodeError, csv.Error):
        return 0


def raw_packet_evidence(capture_path: Path) -> tuple[str, int, int]:
    packets_hex_rows = packets_hex_row_count(capture_path / "packets.hex.log")
    raw_packets_rows = raw_packets_csv_row_count(capture_path / "raw-packets.csv")
    has_packets_hex = packets_hex_rows > 0
    has_raw_packets = raw_packets_rows > 0
    status = "both" if has_packets_hex and has_raw_packets else (
        "packets.hex.log" if has_packets_hex else (
            "raw-packets.csv" if has_raw_packets else "none"
        )
    )
    return status, packets_hex_rows, raw_packets_rows


def capture_evidence_digest(capture_path: Path) -> str:
    digest = hashlib.sha256()
    found = False
    for filename in EVIDENCE_DIGEST_FILES:
        path = capture_path / filename
        if not path.is_file():
            continue
        found = True
        digest.update(filename.encode("utf-8"))
        digest.update(b"\0")
        digest.update(str(path.stat().st_size).encode("ascii"))
        digest.update(b"\0")
        with path.open("rb") as stream:
            while chunk := stream.read(1024 * 1024):
                digest.update(chunk)
    return digest.hexdigest() if found else ""


def capture_source_signature(capture_path: Path) -> tuple[tuple[str, int, int], ...]:
    signature: list[tuple[str, int, int]] = []
    for filename in EVIDENCE_DIGEST_FILES:
        path = capture_path / filename
        if not path.is_file():
            continue
        stat = path.stat()
        signature.append((filename, stat.st_size, stat.st_mtime_ns))
    return tuple(signature)


def scan_text_evidence(path: Path, resource_ids: set[int], terms: set[str]) -> None:
    try:
        with path.open("r", encoding="utf-8-sig", errors="replace") as stream:
            for line in stream:
                for match in MESSAGE_PLAYFIELD.finditer(line):
                    value = int(match.group(1))
                    if 0 < value <= MAX_DECODED_RESOURCE_PLAYFIELD:
                        resource_ids.add(value)
                for match in STATIC_PLAYFIELD.finditer(line):
                    value = int(match.group(1), 16)
                    if value > 0:
                        resource_ids.add(value)
                for match in RESOURCE_PLAYFIELD.finditer(line):
                    value = int(match.group(1))
                    if value > 0:
                        resource_ids.add(value)
                lowered = line.lower()
                if path.name in {"events.log", "npc-interactions.log"}:
                    for term in STRONG_SUBWAY_NAMES:
                        if f"name={term}" in lowered or f'name="{term}"' in lowered:
                            terms.add(term)
                if path.name == "system-messages.log" and any(
                    marker in lowered for marker in ("zone:", "area:", "room:")
                ):
                    for term in SUBWAY_LOCATION_TERMS:
                        if term in lowered:
                            terms.add(term)
    except OSError:
        return


def inspect_capture(
    repo_root: Path,
    capture_path: Path,
    documented: dict[str, set[str]],
    indexed: dict[str, set[str]],
) -> dict[str, object]:
    capture_id = capture_id_from_directory_name(capture_path.name) or capture_path.name
    session = load_json(capture_path / "capture-session.json")
    info = load_json(capture_path / "capture_info.json")
    health = load_json(capture_path / "capture-health.json")
    dossier = load_json(capture_path / "enemy-dossier.json")

    resource_ids: set[int] = set()
    for value in (session, info, health, dossier):
        add_resource_values(value, resource_ids)

    runtime_ids: set[str] = set()
    for value in (
        info.get("playfieldId"),
        dossier.get("runtimePlayfieldId"),
        dossier.get("capturePlayfieldIdentity"),
    ):
        parsed = runtime_identity(value)
        if parsed:
            runtime_ids.add(parsed)
    runtime_instances = {
        value
        for value in (runtime_instance(identity) for identity in runtime_ids)
        if value is not None
    }

    character = ""
    if isinstance(info.get("characterName"), str):
        character = info["characterName"]
    process = session.get("aoClientProcess")
    if not character and isinstance(process, dict):
        title = process.get("mainWindowTitle")
        if isinstance(title, str) and " - " in title:
            character = title.rsplit(" - ", 1)[-1]

    names: set[str] = set()
    enemies = dossier.get("enemies")
    if isinstance(enemies, list):
        for enemy in enemies:
            if isinstance(enemy, dict) and isinstance(enemy.get("name"), str):
                name = enemy["name"].strip()
                if name:
                    names.add(name)

    terms = {name.lower() for name in names if name.lower() in STRONG_SUBWAY_NAMES}
    for filename in TEXT_EVIDENCE_FILES:
        path = capture_path / filename
        if path.exists():
            scan_text_evidence(path, resource_ids, terms)
    resource_ids.difference_update(runtime_instances)

    if (capture_path / "pf127-door-state.csv").exists() or (
        capture_path / "pf127-line-of-sight.csv"
    ).exists():
        terms.add("pf127 diagnostic artifact")

    capture_playfield_id = normalize_playfield(info.get("playfieldId"))
    observed_event_playfields = event_playfields(capture_path / "events.log")
    non_pf127_event_playfields = sorted(
        value
        for value in observed_event_playfields
        if value not in PF127_CORPUS_RUNTIME_INSTANCES
    )
    dossier_resource_id = integer_value(dossier.get("resourcePlayfieldId"))
    if dossier_resource_id == 127:
        pf127_signal = "dossier-resource-127"
        confidence = "confirmed"
    elif capture_playfield_id in PF127_CORPUS_RUNTIME_INSTANCES:
        pf127_signal = "observed-instance-map"
        confidence = "high"
    elif observed_event_playfields.intersection(PF127_CORPUS_RUNTIME_INSTANCES):
        pf127_signal = "event-observed-instance-map"
        confidence = "high"
    elif nested_true(info, "pf127Observed"):
        pf127_signal = "geometry-pf127-observed"
        confidence = "confirmed"
    else:
        pf127_signal = ""
        confidence = "unresolved"

    if pf127_signal and non_pf127_event_playfields:
        classification = "MIXED"
        reason = (
            "PF127 signal "
            + pf127_signal
            + " conflicts with explicit non-PF127 event playfield evidence."
        )
    elif pf127_signal:
        classification = "SUBWAY"
        reason = "PF127 signal " + pf127_signal + " has no outside event playfield."
    elif capture_playfield_id is None and not observed_event_playfields:
        classification = "UNRESOLVED"
        confidence = "unresolved"
        reason = "No usable capture-info or event playfield evidence exists."
    else:
        classification = "ELSEWHERE"
        confidence = "confirmed"
        reason = "Explicit playfield evidence is present without a PF127 signal."

    validation = ""
    validation_value = info.get("validation")
    if isinstance(validation_value, dict) and isinstance(validation_value.get("status"), str):
        validation = validation_value["status"]

    artifacts = [name for name in ARTIFACT_COLUMNS if (capture_path / name).exists()]
    packets_hex_bytes = file_size(capture_path / "packets.hex.log")
    raw_packets_bytes = file_size(capture_path / "raw-packets.csv")
    raw_status, packets_hex_rows, raw_packets_rows = raw_packet_evidence(
        capture_path
    )
    repository_refs = documented.get(capture_id, set())
    implementation_refs = indexed.get(capture_id, set())
    return {
        "capture_id": capture_id,
        "capture_path": capture_path.relative_to(repo_root).as_posix(),
        "evidence_digest": capture_evidence_digest(capture_path),
        "classification": classification,
        "confidence": confidence,
        "pf127_signal": pf127_signal,
        "capture_playfield_id": capture_playfield_id or "",
        "event_playfield_ids": ";".join(
            str(value) for value in sorted(observed_event_playfields)
        ),
        "resource_playfield_ids": ";".join(str(value) for value in sorted(resource_ids)),
        "runtime_playfield_ids": ";".join(sorted(runtime_ids)),
        "character": character,
        "validation_status": validation,
        "raw_packet_evidence": raw_status,
        "packets_hex_bytes": packets_hex_bytes,
        "raw_packets_bytes": raw_packets_bytes,
        "packets_hex_rows": packets_hex_rows,
        "raw_packets_rows": raw_packets_rows,
        "enemy_name_count": len(names),
        "enemy_names": ";".join(sorted(names, key=str.lower)),
        "subway_terms": ";".join(sorted(terms)),
        "repository_reference_count": len(repository_refs),
        "repository_references": ";".join(sorted(repository_refs)),
        "implementation_reference_count": len(implementation_refs),
        "implementation_references": ";".join(sorted(implementation_refs)),
        "artifacts": ";".join(artifacts),
        "reason": reason,
    }


def normalize_inventory_row(row: dict[str, object]) -> dict[str, object]:
    return {column: row.get(column, "") for column in INVENTORY_COLUMNS}


def load_inventory(path: Path) -> list[dict[str, object]]:
    if not path.exists():
        return []
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            reader = csv.DictReader(stream)
            fieldnames = set(reader.fieldnames or ())
            required = set(INVENTORY_COLUMNS) - {"evidence_digest"}
            missing = sorted(required - fieldnames)
            unknown = sorted(fieldnames - set(INVENTORY_COLUMNS))
            if missing or unknown:
                raise SystemExit(
                    "Accepted inventory schema conflict: missing={0} unknown={1}".format(
                        ",".join(missing) or "none",
                        ",".join(unknown) or "none",
                    )
                )
            return [normalize_inventory_row(row) for row in reader]
    except (OSError, UnicodeError, csv.Error) as error:
        raise SystemExit(f"Unable to read accepted inventory {path}: {error}") from error


def conflict_record(row: dict[str, object]) -> str:
    return json.dumps(
        {
            "capture_id": str(row.get("capture_id", "")),
            "capture_path": str(row.get("capture_path", "")),
            "evidence_digest": str(row.get("evidence_digest", "")),
        },
        sort_keys=True,
    )


def fail_conflict(
    conflict_type: str,
    left_label: str,
    left: dict[str, object],
    right_label: str,
    right: dict[str, object],
) -> None:
    raise SystemExit(
        f"Capture inventory {conflict_type} conflict: "
        f"{left_label}={conflict_record(left)} "
        f"{right_label}={conflict_record(right)}"
    )


def validate_unique_inventory(
    rows: list[dict[str, object]], source: str
) -> None:
    identities: dict[str, dict[str, object]] = {}
    digests: dict[str, dict[str, object]] = {}
    for row in rows:
        capture_id = str(row.get("capture_id", ""))
        capture_path = str(row.get("capture_path", ""))
        evidence_digest = str(row.get("evidence_digest", ""))
        if not capture_id or not capture_path:
            raise SystemExit(
                f"Capture inventory invalid record in {source}: {conflict_record(row)}"
            )
        previous_identity = identities.get(capture_id)
        if previous_identity is not None:
            fail_conflict(
                "identity",
                f"{source}-first",
                previous_identity,
                f"{source}-second",
                row,
            )
        identities[capture_id] = row
        if not evidence_digest:
            continue
        previous_digest = digests.get(evidence_digest)
        if previous_digest is not None:
            fail_conflict(
                "digest",
                f"{source}-first",
                previous_digest,
                f"{source}-second",
                row,
            )
        digests[evidence_digest] = row


def merge_inventory(
    accepted_rows: list[dict[str, object]],
    current_rows: list[dict[str, object]],
) -> tuple[list[dict[str, object]], dict[str, int]]:
    accepted = [normalize_inventory_row(row) for row in accepted_rows]
    current = [normalize_inventory_row(row) for row in current_rows]
    validate_unique_inventory(accepted, "accepted")
    validate_unique_inventory(current, "current")

    merged = [dict(row) for row in accepted]
    accepted_by_id = {
        str(row["capture_id"]): (index, row) for index, row in enumerate(merged)
    }
    accepted_by_digest = {
        str(row["evidence_digest"]): row
        for row in merged
        if str(row["evidence_digest"])
    }
    refreshed = 0
    appended = 0

    for row in sorted(
        current,
        key=lambda item: (str(item["capture_id"]), str(item["capture_path"])),
    ):
        capture_id = str(row["capture_id"])
        capture_path = str(row["capture_path"])
        evidence_digest = str(row["evidence_digest"])
        digest_match = accepted_by_digest.get(evidence_digest)
        if digest_match is not None and str(digest_match["capture_id"]) != capture_id:
            fail_conflict(
                "digest",
                "accepted",
                digest_match,
                "current",
                row,
            )

        identity_match = accepted_by_id.get(capture_id)
        if identity_match is None:
            merged.append(dict(row))
            accepted_by_id[capture_id] = (len(merged) - 1, merged[-1])
            if evidence_digest:
                accepted_by_digest[evidence_digest] = merged[-1]
            appended += 1
            continue

        index, accepted_row = identity_match
        accepted_path = str(accepted_row["capture_path"])
        accepted_digest = str(accepted_row["evidence_digest"])
        if accepted_digest and accepted_digest != evidence_digest:
            fail_conflict(
                "identity",
                "accepted",
                accepted_row,
                "current",
                row,
            )
        if accepted_path != capture_path and (
            not accepted_digest or accepted_digest != evidence_digest
        ):
            fail_conflict(
                "identity",
                "accepted",
                accepted_row,
                "current",
                row,
            )

        if accepted_digest:
            accepted_by_digest.pop(accepted_digest, None)
        merged[index] = dict(row)
        accepted_by_id[capture_id] = (index, merged[index])
        if evidence_digest:
            accepted_by_digest[evidence_digest] = merged[index]
        refreshed += 1

    validate_unique_inventory(merged, "merged")
    return merged, {
        "accepted_before": len(accepted),
        "current": len(current),
        "preserved": len(accepted) - refreshed,
        "refreshed": refreshed,
        "appended": appended,
        "removed": 0,
    }


def select_current_capture_paths(
    capture_paths: list[Path],
    excluded_capture_ids: set[str],
    accepted_rows: list[dict[str, object]],
    capture_id_cutoff: str = "",
) -> list[Path]:
    if capture_id_cutoff and not CAPTURE_ID.fullmatch(capture_id_cutoff):
        raise SystemExit(
            "Invalid --capture-id-cutoff; expected YYYYMMDD-HHMMSS: "
            + capture_id_cutoff
        )
    if not excluded_capture_ids:
        explicit_exclusions: set[str] = set()
    else:
        explicit_exclusions = excluded_capture_ids
    accepted_ids = {str(row["capture_id"]) for row in accepted_rows}
    accepted_exclusions = sorted(explicit_exclusions.intersection(accepted_ids))
    if accepted_exclusions:
        raise SystemExit(
            "Refusing to exclude accepted capture identities because normal "
            "generation cannot prune: " + ",".join(accepted_exclusions)
        )
    discovered_ids = {
        capture_id_from_directory_name(path.name) for path in capture_paths
    }
    unknown_exclusions = sorted(explicit_exclusions - discovered_ids)
    if unknown_exclusions:
        raise SystemExit(
            "Excluded capture identities were not discovered: "
            + ",".join(unknown_exclusions)
        )
    cutoff_exclusions = {
        capture_id
        for capture_id in discovered_ids
        if capture_id_cutoff and capture_id > capture_id_cutoff
    }
    all_exclusions = explicit_exclusions.union(cutoff_exclusions)
    return [
        path
        for path in capture_paths
        if capture_id_from_directory_name(path.name) not in all_exclusions
    ]


def write_csv(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=INVENTORY_COLUMNS)
        writer.writeheader()
        writer.writerows(rows)


def normalize_retention_row(row: dict[str, object]) -> dict[str, object]:
    return {column: row.get(column, "") for column in RETENTION_COLUMNS}


def default_retention_row(inventory_row: dict[str, object]) -> dict[str, object]:
    return {
        "capture_id": str(inventory_row.get("capture_id", "")),
        "evidence_digest": str(inventory_row.get("evidence_digest", "")),
        "analysis_state": "unreviewed",
        "evidence_coverage": "unknown",
        "used_by": "",
        "derived_artifacts": "",
        "raw_archive_path": "",
        "raw_archive_digest": "",
        "unresolved_gaps": "Capture has not been reviewed for raw-evidence disposal.",
        "retention_state": "retain",
        "approved_by": "",
        "approved_at": "",
        "reason": "Default fail-closed retention; no discard review recorded.",
    }


def load_retention_ledger(path: Path) -> list[dict[str, object]]:
    if not path.exists():
        return []
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            reader = csv.DictReader(stream)
            fieldnames = set(reader.fieldnames or ())
            missing = sorted(set(RETENTION_COLUMNS) - fieldnames)
            unknown = sorted(fieldnames - set(RETENTION_COLUMNS))
            if missing or unknown:
                raise SystemExit(
                    "Capture retention schema conflict: missing={0} unknown={1}".format(
                        ",".join(missing) or "none",
                        ",".join(unknown) or "none",
                    )
                )
            return [normalize_retention_row(row) for row in reader]
    except (OSError, UnicodeError, csv.Error) as error:
        raise SystemExit(f"Unable to read capture retention ledger {path}: {error}") from error


def retention_record(row: dict[str, object]) -> str:
    return json.dumps(
        {column: str(row.get(column, "")) for column in RETENTION_COLUMNS},
        sort_keys=True,
    )


def tracked_paths(
    repo_root: Path,
    value: object,
    field: str,
    capture_id: str,
) -> list[str]:
    paths = [item.strip() for item in str(value).split(";") if item.strip()]
    missing = [path for path in paths if not (repo_root / path).is_file()]
    if missing:
        raise SystemExit(
            f"Capture retention {field} missing for {capture_id}: " + ",".join(missing)
        )
    return paths


def validate_retention_rows(
    rows: list[dict[str, object]],
    inventory_rows: list[dict[str, object]],
    repo_root: Path,
) -> None:
    inventory_by_id = {
        str(row.get("capture_id", "")): row for row in inventory_rows
    }
    seen: dict[str, dict[str, object]] = {}
    for raw_row in rows:
        row = normalize_retention_row(raw_row)
        capture_id = str(row["capture_id"])
        if not capture_id:
            raise SystemExit("Capture retention row has an empty capture identity.")
        if capture_id in seen:
            raise SystemExit(
                "Capture retention identity conflict: first={0} second={1}".format(
                    retention_record(seen[capture_id]),
                    retention_record(row),
                )
            )
        seen[capture_id] = row
        inventory_row = inventory_by_id.get(capture_id)
        if inventory_row is None:
            raise SystemExit(
                "Capture retention orphan conflict: retention={0}".format(
                    retention_record(row)
                )
            )
        ledger_digest = str(row["evidence_digest"])
        inventory_digest = str(inventory_row.get("evidence_digest", ""))
        if ledger_digest and inventory_digest and ledger_digest != inventory_digest:
            raise SystemExit(
                "Capture retention digest conflict: inventory={0} retention={1}".format(
                    conflict_record(inventory_row),
                    retention_record(row),
                )
            )
        if ledger_digest and not re.fullmatch(r"[0-9a-f]{64}", ledger_digest):
            raise SystemExit(
                f"Capture retention evidence digest is not SHA-256 for {capture_id}."
            )
        analysis_state = str(row["analysis_state"])
        evidence_coverage = str(row["evidence_coverage"])
        retention_state = str(row["retention_state"])
        if analysis_state not in ANALYSIS_STATES:
            raise SystemExit(
                f"Unknown capture analysis state for {capture_id}: {analysis_state}"
            )
        if evidence_coverage not in EVIDENCE_COVERAGE_STATES:
            raise SystemExit(
                f"Unknown capture evidence coverage for {capture_id}: {evidence_coverage}"
            )
        if retention_state not in RETENTION_STATES:
            raise SystemExit(
                f"Unknown capture retention state for {capture_id}: {retention_state}"
            )
        archive_path = str(row["raw_archive_path"]).strip()
        archive_digest = str(row["raw_archive_digest"]).strip()
        if bool(archive_path) != bool(archive_digest):
            raise SystemExit(
                f"Capture retention archive path/digest pair is incomplete for {capture_id}."
            )
        if archive_digest and not re.fullmatch(r"[0-9a-f]{64}", archive_digest):
            raise SystemExit(
                f"Capture retention archive digest is not SHA-256 for {capture_id}."
            )
        if retention_state != "discard_approved":
            continue
        missing_requirements: list[str] = []
        if not (ledger_digest or inventory_digest):
            missing_requirements.append("evidence_digest")
        if analysis_state != "complete":
            missing_requirements.append("analysis_state=complete")
        if evidence_coverage != "complete":
            missing_requirements.append("evidence_coverage=complete")
        if not str(row["used_by"]).strip():
            missing_requirements.append("used_by")
        if not str(row["approved_by"]).strip():
            missing_requirements.append("approved_by")
        if not re.fullmatch(
            r"\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}Z)?",
            str(row["approved_at"]),
        ):
            missing_requirements.append("approved_at")
        if not str(row["reason"]).strip():
            missing_requirements.append("reason")
        used_by = tracked_paths(repo_root, row["used_by"], "used_by", capture_id)
        derived_artifacts = tracked_paths(
            repo_root,
            row["derived_artifacts"],
            "derived_artifacts",
            capture_id,
        )
        if not used_by:
            missing_requirements.append("tracked used_by")
        if not archive_path and not derived_artifacts:
            missing_requirements.append("raw archive or complete derived artifacts")
        if missing_requirements:
            raise SystemExit(
                "Capture discard approval is incomplete for {0}: {1}; record={2}".format(
                    capture_id,
                    ",".join(missing_requirements),
                    retention_record(row),
                )
            )


def merge_retention_ledger(
    existing_rows: list[dict[str, object]],
    inventory_rows: list[dict[str, object]],
    repo_root: Path,
) -> tuple[list[dict[str, object]], dict[str, int]]:
    existing = [normalize_retention_row(row) for row in existing_rows]
    validate_retention_rows(existing, inventory_rows, repo_root)
    existing_by_id = {str(row["capture_id"]): row for row in existing}
    merged: list[dict[str, object]] = []
    appended = 0
    digest_refreshed = 0
    for inventory_row in inventory_rows:
        capture_id = str(inventory_row.get("capture_id", ""))
        row = existing_by_id.get(capture_id)
        if row is None:
            row = default_retention_row(inventory_row)
            appended += 1
        else:
            row = dict(row)
            inventory_digest = str(inventory_row.get("evidence_digest", ""))
            if inventory_digest and not str(row["evidence_digest"]):
                row["evidence_digest"] = inventory_digest
                digest_refreshed += 1
        merged.append(row)
    validate_retention_rows(merged, inventory_rows, repo_root)
    return merged, {
        "preserved": len(merged) - appended,
        "appended": appended,
        "digest_refreshed": digest_refreshed,
        "discard_approved": sum(
            str(row["retention_state"]) == "discard_approved" for row in merged
        ),
    }


def write_retention_csv(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=RETENTION_COLUMNS)
        writer.writeheader()
        writer.writerows(rows)


def write_retention_markdown(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    state_counts = Counter(str(row["retention_state"]) for row in rows)
    lines = [
        "# AOSharp Capture Retention",
        "",
        "This report is generated from the tracked fail-closed retention ledger.",
        "Absence from this report never authorizes deletion; unaccepted local captures default to retain.",
        "The inventory generator does not delete or prune raw capture folders.",
        "",
        f"- Tracked captures: **{len(rows)}**",
        f"- Retain: **{state_counts.get('retain', 0)}**",
        f"- Archive required: **{state_counts.get('archive_required', 0)}**",
        f"- Discard approved: **{state_counts.get('discard_approved', 0)}**",
        "",
        "| Capture ID | Retention | Analysis | Coverage | Discardable | Used by | Unresolved gaps | Reason |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for row in rows:
        capture_id = str(row["capture_id"])
        lines.append(f"<!-- retention-capture-id: {capture_id} -->")
        lines.append(
            "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |".format(
                markdown_cell(capture_id),
                markdown_cell(row["retention_state"]),
                markdown_cell(row["analysis_state"]),
                markdown_cell(row["evidence_coverage"]),
                "YES" if str(row["retention_state"]) == "discard_approved" else "NO",
                markdown_cell(row["used_by"]),
                markdown_cell(row["unresolved_gaps"]),
                markdown_cell(row["reason"]),
            )
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def retention_markdown_ids(path: Path) -> list[str]:
    try:
        text = path.read_text(encoding="utf-8")
    except (OSError, UnicodeError) as error:
        raise SystemExit(f"Unable to read capture retention Markdown {path}: {error}") from error
    return re.findall(r"<!-- retention-capture-id: ([^ ]+) -->", text)


def validate_retention_markdown_sync(
    rows: list[dict[str, object]],
    markdown_path: Path,
) -> None:
    csv_ids = [str(row["capture_id"]) for row in rows]
    markdown_ids = retention_markdown_ids(markdown_path)
    if csv_ids != markdown_ids:
        raise SystemExit(
            "Capture retention CSV/Markdown identity conflict: csv={0} markdown={1}".format(
                json.dumps(csv_ids),
                json.dumps(markdown_ids),
            )
        )


def markdown_cell(value: object) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ")


def write_markdown(
    path: Path,
    rows: list[dict[str, object]],
    current_capture_count: int | None = None,
    discovered_capture_count: int | None = None,
    out_of_scope_count: int = 0,
    concurrently_changed_count: int = 0,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    classification_counts = Counter(str(row["classification"]) for row in rows)
    confidence_counts = Counter(str(row["confidence"]) for row in rows)
    raw_counts = Counter(str(row["raw_packet_evidence"]) for row in rows)
    subway_rows = [row for row in rows if row["classification"] in {"SUBWAY", "MIXED"}]
    subway_with_raw = [
        row for row in subway_rows if row["raw_packet_evidence"] != "none"
    ]
    unindexed_subway = [
        row for row in subway_rows if int(row["implementation_reference_count"]) == 0
    ]
    lines = [
        "# AOSharp Capture Location Inventory",
        "",
        "Generated by `tools/inventory_aosharp_captures.py` with non-destructive merge semantics. Accepted historical rows remain when their raw folders are absent; present folders refresh matching identities, and new identities append. Classification uses exact location evidence only: `enemy-dossier.resourcePlayfieldId=127`, PF127 geometry observation, capture/event playfield identities, and explicit zoning boundaries. Names, player names, folder labels, and repository references never determine location. The frozen-corpus PF127 runtime-instance map is `127`, `1187842`, `1363982`, `1388552`, and `1407006`; future runtime instances require a fresh exact resource signal. `MIXED` means the same capture contains PF127 plus an outside/zoning playfield. `UNRESOLVED` means no usable location evidence exists; it does not mean elsewhere. Raw evidence counts data rows, so a BOM-only log or header-only CSV is reported as `none`.",
        "",
        "## Summary",
        "",
        "| Metric | Count |",
        "| --- | ---: |",
        f"| Total accepted capture records | {len(rows)} |",
        f"| Discovered on-disk capture folders at snapshot | {discovered_capture_count if discovered_capture_count is not None else (current_capture_count if current_capture_count is not None else len(rows))} |",
        f"| In-scope stable current capture folders | {current_capture_count if current_capture_count is not None else len(rows)} |",
        f"| Out-of-scope discovered folders | {out_of_scope_count} |",
        f"| Concurrently changed folders skipped | {concurrently_changed_count} |",
        f"| Subway | {classification_counts['SUBWAY']} |",
        f"| Mixed including Subway | {classification_counts['MIXED']} |",
        f"| Elsewhere | {classification_counts['ELSEWHERE']} |",
        f"| Unresolved | {classification_counts['UNRESOLVED']} |",
        f"| Confirmed classifications | {confidence_counts['confirmed']} |",
        f"| Both raw packet formats with data rows | {raw_counts['both']} |",
        f"| packets.hex.log data rows only | {raw_counts['packets.hex.log']} |",
        f"| raw-packets.csv data rows only | {raw_counts['raw-packets.csv']} |",
        f"| No raw packet data rows | {raw_counts['none']} |",
        f"| Subway/mixed with raw packet data rows | {len(subway_with_raw)} |",
        f"| Subway/mixed without raw packet data rows | {len(subway_rows) - len(subway_with_raw)} |",
        f"| Subway/mixed without generated-or-runtime reference | {len(unindexed_subway)} |",
        "",
    ]

    for classification in ("SUBWAY", "MIXED", "ELSEWHERE", "UNRESOLVED"):
        selected = [row for row in rows if row["classification"] == classification]
        lines.extend(
            [
                f"## {classification.title()} ({len(selected)})",
                "",
                "| Capture | Confidence | Resource PF | Character | Validation | Raw | Indexed | Reason |",
                "| --- | --- | --- | --- | --- | --- | ---: | --- |",
            ]
        )
        for row in selected:
            lines.append(
                "| {capture} | {confidence} | {resource} | {character} | {validation} | {raw} | {indexed} | {reason} |".format(
                    capture=markdown_cell(row["capture_id"]),
                    confidence=markdown_cell(row["confidence"]),
                    resource=markdown_cell(row["resource_playfield_ids"]),
                    character=markdown_cell(row["character"]),
                    validation=markdown_cell(row["validation_status"]),
                    raw=markdown_cell(row["raw_packet_evidence"]),
                    indexed=markdown_cell(row["implementation_reference_count"]),
                    reason=markdown_cell(row["reason"]),
                )
            )
        lines.append("")

    path.write_text("\n".join(lines), encoding="utf-8")


def markdown_inventory_ids(path: Path) -> list[str]:
    result: list[str] = []
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            for line in stream:
                if not line.startswith("|"):
                    continue
                cells = [cell.strip() for cell in line.split("|")]
                if len(cells) > 2 and CAPTURE_ID.fullmatch(cells[1]):
                    result.append(cells[1])
    except OSError as error:
        raise SystemExit(f"Unable to read Markdown inventory {path}: {error}") from error
    return result


def validate_csv_markdown_sync(
    rows: list[dict[str, object]], markdown_path: Path
) -> None:
    csv_ids = [str(row["capture_id"]) for row in rows]
    markdown_ids = markdown_inventory_ids(markdown_path)
    if len(markdown_ids) != len(set(markdown_ids)):
        raise SystemExit("Markdown inventory contains duplicate capture identities.")
    if set(csv_ids) != set(markdown_ids) or len(csv_ids) != len(markdown_ids):
        missing_markdown = sorted(set(csv_ids) - set(markdown_ids))
        missing_csv = sorted(set(markdown_ids) - set(csv_ids))
        raise SystemExit(
            "CSV/Markdown inventory mismatch: missing_markdown={0} missing_csv={1}".format(
                ",".join(missing_markdown) or "none",
                ",".join(missing_csv) or "none",
            )
        )


def validate_reviewed_corpus(rows: list[dict[str, object]]) -> None:
    reviewed = {
        str(row["capture_id"]): str(row["classification"])
        for row in rows
        if str(row["capture_id"]) <= REVIEWED_CAPTURE_CUTOFF
    }
    if len(reviewed) != 298:
        return
    expected = {
        capture_id: "SUBWAY" for capture_id in EXPECTED_REVIEWED_SUBWAY_ONLY
    }
    expected.update({capture_id: "MIXED" for capture_id in EXPECTED_REVIEWED_MIXED})
    expected.update(
        {capture_id: "UNRESOLVED" for capture_id in EXPECTED_REVIEWED_INSUFFICIENT}
    )
    for capture_id in reviewed:
        expected.setdefault(capture_id, "ELSEWHERE")
    mismatches = [
        capture_id
        for capture_id in sorted(reviewed)
        if reviewed[capture_id] != expected[capture_id]
    ]
    if mismatches:
        raise SystemExit(
            "Reviewed corpus classification drift: " + ",".join(mismatches)
        )

    reviewed_subway = [
        row
        for row in rows
        if str(row["capture_id"]) <= REVIEWED_CAPTURE_CUTOFF
        and str(row["classification"]) in {"SUBWAY", "MIXED"}
    ]
    if len(reviewed_subway) != EXPECTED_REVIEWED_SUBWAY_CAPTURE_COUNT:
        raise SystemExit(
            "Reviewed Subway capture count drift: expected "
            f"{EXPECTED_REVIEWED_SUBWAY_CAPTURE_COUNT}; found {len(reviewed_subway)}"
        )
    no_raw = {
        str(row["capture_id"])
        for row in reviewed_subway
        if str(row["raw_packet_evidence"]) == "none"
    }
    if no_raw != EXPECTED_REVIEWED_SUBWAY_NO_RAW:
        raise SystemExit(
            "Reviewed Subway no-raw set drift: expected "
            f"{','.join(sorted(EXPECTED_REVIEWED_SUBWAY_NO_RAW))}; found "
            f"{','.join(sorted(no_raw)) or 'none'}"
        )
    with_raw = len(reviewed_subway) - len(no_raw)
    if with_raw != EXPECTED_REVIEWED_SUBWAY_RAW_CAPTURE_COUNT:
        raise SystemExit(
            "Reviewed Subway raw capture count drift: expected "
            f"{EXPECTED_REVIEWED_SUBWAY_RAW_CAPTURE_COUNT}; found {with_raw}"
        )


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    output_csv = (repo_root / args.output_csv).resolve()
    output_md = (repo_root / args.output_md).resolve()
    retention_ledger = (repo_root / args.retention_ledger).resolve()
    retention_md = (repo_root / args.retention_md).resolve()
    accepted_rows = load_inventory(output_csv)
    accepted_retention_rows = load_retention_ledger(retention_ledger)
    discovered_capture_paths = discover_capture_directories(repo_root)
    excluded_capture_ids = set(args.exclude_capture_id)
    capture_paths = select_current_capture_paths(
        discovered_capture_paths,
        excluded_capture_ids,
        accepted_rows,
        args.capture_id_cutoff,
    )
    initial_signatures = {
        path: capture_source_signature(path) for path in capture_paths
    }
    documented, indexed = collect_repository_references(repo_root)
    if not capture_paths and not accepted_rows:
        raise SystemExit("No accepted inventory or AOSharp capture folders were found.")
    current_rows: list[dict[str, object]] = []
    concurrently_changed: list[Path] = []
    for path in capture_paths:
        if capture_source_signature(path) != initial_signatures[path]:
            concurrently_changed.append(path)
            continue
        row = inspect_capture(repo_root, path, documented, indexed)
        if capture_source_signature(path) != initial_signatures[path]:
            concurrently_changed.append(path)
            continue
        current_rows.append(row)
    out_of_scope_count = len(discovered_capture_paths) - len(capture_paths)
    rows, merge_counts = merge_inventory(accepted_rows, current_rows)
    validate_reviewed_corpus(rows)
    if args.validate_current:
        validate_csv_markdown_sync(accepted_rows, output_md)
        retention_rows, retention_counts = merge_retention_ledger(
            accepted_retention_rows,
            accepted_rows,
            repo_root,
        )
        if retention_counts["appended"]:
            raise SystemExit(
                "Capture retention ledger is incomplete: missing accepted rows={0}. "
                "Run normal inventory regeneration to append fail-closed defaults.".format(
                    retention_counts["appended"]
                )
            )
        validate_retention_markdown_sync(retention_rows, retention_md)
        print(
            "CURRENT_CAPTURE_INVENTORY_VALID accepted={0} current={1} "
            "discovered_snapshot={2} out_of_scope={3} concurrent_skipped={4} "
            "preserved={5} refreshed={6} appended={7} removed=0 "
            "retention_tracked={8} discard_approved={9}".format(
                len(accepted_rows),
                len(current_rows),
                len(discovered_capture_paths),
                out_of_scope_count,
                len(concurrently_changed),
                merge_counts["preserved"],
                merge_counts["refreshed"],
                merge_counts["appended"],
                len(retention_rows),
                retention_counts["discard_approved"],
            )
        )
        return 0
    retention_rows, retention_counts = merge_retention_ledger(
        accepted_retention_rows,
        rows,
        repo_root,
    )
    write_csv(output_csv, rows)
    write_markdown(
        output_md,
        rows,
        len(current_rows),
        len(discovered_capture_paths),
        out_of_scope_count,
        len(concurrently_changed),
    )
    validate_csv_markdown_sync(rows, output_md)
    write_retention_csv(retention_ledger, retention_rows)
    write_retention_markdown(retention_md, retention_rows)
    validate_retention_markdown_sync(retention_rows, retention_md)
    counts = Counter(str(row["classification"]) for row in rows)
    print(
        "accepted={0} current={1} discovered_snapshot={2} out_of_scope={3} "
        "concurrent_skipped={4} preserved={5} refreshed={6} appended={7} removed=0 "
        "subway={8} mixed={9} elsewhere={10} unresolved={11} "
        "retention_appended={12} discard_approved={13}".format(
            len(rows),
            len(current_rows),
            len(discovered_capture_paths),
            out_of_scope_count,
            len(concurrently_changed),
            merge_counts["preserved"],
            merge_counts["refreshed"],
            merge_counts["appended"],
            counts["SUBWAY"],
            counts["MIXED"],
            counts["ELSEWHERE"],
            counts["UNRESOLVED"],
            retention_counts["appended"],
            retention_counts["discard_approved"],
        )
    )
    print("csv=" + str(output_csv))
    print("markdown=" + str(output_md))
    print("retention_ledger=" + str(retention_ledger))
    print("retention_markdown=" + str(retention_md))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
