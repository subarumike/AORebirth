#!/usr/bin/env python3
"""Build an evidence-first inventory of every AOSharp capture in the repository."""

from __future__ import annotations

import argparse
import csv
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

# The complete 294-folder corpus through this timestamp was manually reviewed
# against capture/event playfields, packet presence, PF127 artifacts, and zoning
# boundaries on 2026-07-17. Newer captures fall through to the evidence rules.
REVIEWED_CAPTURE_CUTOFF = "20260717-012651"
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
        if CAPTURE_ID.match(path.name) and CAPTURE_MARKERS.intersection(files):
            captures.append(path)
            directories[:] = []
    return sorted(captures, key=lambda path: path.relative_to(repo_root).as_posix())


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
    capture_id = capture_path.name
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
    has_packets_hex = packets_hex_bytes > 0
    has_raw_packets = raw_packets_bytes > 0
    raw_status = "both" if has_packets_hex and has_raw_packets else (
        "packets.hex.log" if has_packets_hex else (
            "raw-packets.csv" if has_raw_packets else "none"
        )
    )
    repository_refs = documented.get(capture_id, set())
    implementation_refs = indexed.get(capture_id, set())
    return {
        "capture_id": capture_id,
        "capture_path": capture_path.relative_to(repo_root).as_posix(),
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


def write_csv(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)


def markdown_cell(value: object) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ")


def write_markdown(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    classification_counts = Counter(str(row["classification"]) for row in rows)
    confidence_counts = Counter(str(row["confidence"]) for row in rows)
    raw_counts = Counter(str(row["raw_packet_evidence"]) for row in rows)
    subway_rows = [row for row in rows if row["classification"] in {"SUBWAY", "MIXED"}]
    unindexed_subway = [
        row for row in subway_rows if int(row["implementation_reference_count"]) == 0
    ]
    lines = [
        "# AOSharp Capture Location Inventory",
        "",
        "Generated by `tools/inventory_aosharp_captures.py`. Classification uses exact location evidence only: `enemy-dossier.resourcePlayfieldId=127`, PF127 geometry observation, capture/event playfield identities, and explicit zoning boundaries. Names, player names, and repository references never determine location. The frozen-corpus PF127 runtime-instance map is `127`, `1187842`, `1363982`, `1388552`, and `1407006`; future runtime instances require a fresh exact resource signal. `MIXED` means the same capture contains PF127 plus an outside/zoning playfield. `UNRESOLVED` means no usable location evidence exists; it does not mean elsewhere.",
        "",
        "## Summary",
        "",
        "| Metric | Count |",
        "| --- | ---: |",
        f"| Total capture folders | {len(rows)} |",
        f"| Subway | {classification_counts['SUBWAY']} |",
        f"| Mixed including Subway | {classification_counts['MIXED']} |",
        f"| Elsewhere | {classification_counts['ELSEWHERE']} |",
        f"| Unresolved | {classification_counts['UNRESOLVED']} |",
        f"| Confirmed classifications | {confidence_counts['confirmed']} |",
        f"| Both raw packet sinks | {raw_counts['both']} |",
        f"| packets.hex.log only | {raw_counts['packets.hex.log']} |",
        f"| No raw packet sink | {raw_counts['none']} |",
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


def validate_reviewed_corpus(rows: list[dict[str, object]]) -> None:
    reviewed = {
        str(row["capture_id"]): str(row["classification"])
        for row in rows
        if str(row["capture_id"]) <= REVIEWED_CAPTURE_CUTOFF
    }
    if len(reviewed) != 294:
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


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    documented, indexed = collect_repository_references(repo_root)
    capture_paths = discover_capture_directories(repo_root)
    if not capture_paths:
        raise SystemExit("No AOSharp capture folders were found.")
    rows = [
        inspect_capture(repo_root, path, documented, indexed) for path in capture_paths
    ]
    validate_reviewed_corpus(rows)
    output_csv = (repo_root / args.output_csv).resolve()
    output_md = (repo_root / args.output_md).resolve()
    write_csv(output_csv, rows)
    write_markdown(output_md, rows)
    counts = Counter(str(row["classification"]) for row in rows)
    print(
        "captures={0} subway={1} mixed={2} elsewhere={3} unresolved={4}".format(
            len(rows),
            counts["SUBWAY"],
            counts["MIXED"],
            counts["ELSEWHERE"],
            counts["UNRESOLVED"],
        )
    )
    print("csv=" + str(output_csv))
    print("markdown=" + str(output_md))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
