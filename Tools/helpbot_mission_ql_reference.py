#!/usr/bin/env python3
"""Extract and verify the pinned Helpbot mission-QL reference.

The source page publishes the distinct mission QLs available at each character
level. AO's request field has eleven one-based difficulty detents, so repeated
adjacent detent values are absent from the published lists. This tool preserves
the published lists verbatim and derives the eleven runtime detents with an
integer-only rule whose de-duplicated output must match every published row.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import sys


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_REFERENCE = (
    REPOSITORY_ROOT
    / "docs"
    / "evidence"
    / "data"
    / "helpbot-mission-ql-levels-1-149.json"
)
DEFAULT_GRAPH = (
    REPOSITORY_ROOT
    / "AORebirth"
    / "Server"
    / "ZoneEngine"
    / "XML Data"
    / "MissionLevels.csv"
)

SOURCE_PAGE_URL = "https://wiki.aodb.us/wiki/Level_Parameters"
SOURCE_REVISION_URL = (
    "https://wiki.aodb.us/index.php?title=Level_Parameters&oldid=44808"
)
SOURCE_RAW_URL = SOURCE_REVISION_URL + "&action=raw"
SOURCE_REVISION_ID = 44808
SOURCE_RAW_SHA256 = (
    "f8841253af7ed9b63aa2d9d1a2d48e487239b4f8e44e57b225cc7b3855c04488"
)
RETRIEVED_UTC = "2026-09-01T11:41:39Z"

MIN_REFERENCE_LEVEL = 1
MAX_REFERENCE_LEVEL = 149
DIFFICULTY_COUNT = 11
BASE_PERCENTAGES = (70, 75, 80, 85, 90, 100, 110, 120, 130, 150)
PUBLISHED_ANOMALIES = {
    77: {2: 60, 3: 64, 7: 91},
    112: {6: 122},
    142: {9: 212},
}
ROW_PATTERN = re.compile(
    r"^#?L (\d+):.*\| Missions (\d+(?:, \d+)*)\s*$"
)


class ReferenceError(ValueError):
    pass


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def unique_adjacent(values: list[int]) -> list[int]:
    result: list[int] = []
    for value in values:
        if not result or result[-1] != value:
            result.append(value)
    return result


def parse_raw_wikitext(raw: bytes) -> dict[int, list[int]]:
    actual_hash = sha256(raw)
    if actual_hash != SOURCE_RAW_SHA256:
        raise ReferenceError(
            "Pinned raw wikitext SHA-256 mismatch: "
            f"expected {SOURCE_RAW_SHA256}, found {actual_hash}."
        )

    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ReferenceError("Pinned raw wikitext is not UTF-8.") from error

    rows: dict[int, list[int]] = {}
    for line in text.splitlines():
        match = ROW_PATTERN.match(line)
        if not match:
            continue
        level = int(match.group(1))
        if level < MIN_REFERENCE_LEVEL or level > MAX_REFERENCE_LEVEL:
            continue
        if level in rows:
            raise ReferenceError(f"Duplicate published level {level}.")
        rows[level] = [int(value) for value in match.group(2).split(", ")]

    expected_levels = list(range(MIN_REFERENCE_LEVEL, MAX_REFERENCE_LEVEL + 1))
    if list(rows) != expected_levels:
        missing = sorted(set(expected_levels) - set(rows))
        raise ReferenceError(
            "Pinned source does not contain exactly levels 1..149; "
            f"missing={missing}."
        )
    validate_published_rows(rows)
    return rows


def validate_published_rows(rows: dict[int, list[int]]) -> None:
    for level in range(MIN_REFERENCE_LEVEL, MAX_REFERENCE_LEVEL + 1):
        values = rows.get(level)
        if not values:
            raise ReferenceError(f"Published level {level} is missing or empty.")
        if any(value < 1 or value > 250 for value in values):
            raise ReferenceError(f"Published level {level} has an out-of-range QL.")
        if any(values[index] >= values[index + 1] for index in range(len(values) - 1)):
            raise ReferenceError(
                f"Published level {level} is not strictly increasing."
            )
        if level not in values:
            raise ReferenceError(
                f"Published level {level} omits its neutral mission QL."
            )


def derive_detents(level: int, published: list[int]) -> list[int]:
    detents = [max(1, (level * percentage) // 100) for percentage in BASE_PERCENTAGES]
    for index, value in PUBLISHED_ANOMALIES.get(level, {}).items():
        detents[index] = value
    detents.append(published[-1])

    if len(detents) != DIFFICULTY_COUNT:
        raise ReferenceError(f"Level {level} did not derive eleven detents.")
    if any(detents[index] > detents[index + 1] for index in range(DIFFICULTY_COUNT - 1)):
        raise ReferenceError(f"Derived detents decrease at level {level}.")
    if detents[5] != level:
        raise ReferenceError(f"Derived neutral detent is wrong at level {level}.")
    if unique_adjacent(detents) != published:
        raise ReferenceError(
            f"Derived detents do not reproduce the published list at level {level}: "
            f"published={published}, derived={detents}."
        )
    return detents


def build_reference(rows: dict[int, list[int]]) -> dict[str, object]:
    return {
        "schema_version": 1,
        "source": {
            "title": "AOWiki Level Parameters",
            "page_url": SOURCE_PAGE_URL,
            "revision_url": SOURCE_REVISION_URL,
            "raw_url": SOURCE_RAW_URL,
            "revision_id": SOURCE_REVISION_ID,
            "retrieved_utc": RETRIEVED_UTC,
            "raw_wikitext_sha256": SOURCE_RAW_SHA256,
            "provenance_statement": (
                "The page states that its level data was obtained exclusively "
                "in game with /tell helpbot level X."
            ),
        },
        "coverage": {
            "minimum_character_level": MIN_REFERENCE_LEVEL,
            "maximum_character_level": MAX_REFERENCE_LEVEL,
            "published_lists_status": "PROVEN",
            "detent_reconstruction_status": "DERIVED",
            "levels_150_220_status": "UNKNOWN_FROM_THIS_REFERENCE",
        },
        "reconstruction": {
            "difficulty_wire_values": list(range(1, DIFFICULTY_COUNT + 1)),
            "base_integer_percentages_for_wires_1_10": list(BASE_PERCENTAGES),
            "base_rounding": "floor(level * percentage / 100), minimum 1",
            "wire_11_rule": "last value in the published Helpbot list",
            "published_list_rule": "remove adjacent duplicate detent QLs",
            "authoritative_anomalies": [
                {
                    "character_level": level,
                    "difficulty_wire": index + 1,
                    "mission_ql": value,
                }
                for level, corrections in sorted(PUBLISHED_ANOMALIES.items())
                for index, value in sorted(corrections.items())
            ],
        },
        "levels": [
            {
                "character_level": level,
                "published_mission_qls": values,
                "derived_detent_qls": derive_detents(level, values),
            }
            for level, values in rows.items()
        ],
    }


def canonical_json(document: dict[str, object]) -> bytes:
    return (json.dumps(document, indent=2, ensure_ascii=True) + "\n").encode("utf-8")


def load_reference(path: Path) -> tuple[dict[str, object], dict[int, list[int]]]:
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ReferenceError(f"Cannot load reference artifact {path}: {error}") from error

    source = document.get("source", {})
    if source.get("revision_id") != SOURCE_REVISION_ID:
        raise ReferenceError("Reference artifact revision ID is not pinned.")
    if source.get("raw_wikitext_sha256") != SOURCE_RAW_SHA256:
        raise ReferenceError("Reference artifact raw-source hash is not pinned.")

    records = document.get("levels")
    if not isinstance(records, list):
        raise ReferenceError("Reference artifact levels must be an array.")

    rows: dict[int, list[int]] = {}
    for record in records:
        if not isinstance(record, dict):
            raise ReferenceError("Reference artifact contains a malformed level record.")
        level = record.get("character_level")
        published = record.get("published_mission_qls")
        derived = record.get("derived_detent_qls")
        if not isinstance(level, int) or not isinstance(published, list):
            raise ReferenceError("Reference artifact contains malformed level fields.")
        if not all(isinstance(value, int) for value in published):
            raise ReferenceError(f"Level {level} published QLs are malformed.")
        if level in rows:
            raise ReferenceError(f"Reference artifact duplicates level {level}.")
        rows[level] = published
        expected_detents = derive_detents(level, published)
        if derived != expected_detents:
            raise ReferenceError(f"Level {level} derived detents are stale.")

    expected_levels = list(range(MIN_REFERENCE_LEVEL, MAX_REFERENCE_LEVEL + 1))
    if list(rows) != expected_levels:
        raise ReferenceError("Reference artifact does not contain exactly levels 1..149.")
    validate_published_rows(rows)

    if document != build_reference(rows):
        raise ReferenceError("Reference artifact metadata or reconstruction is stale.")
    if canonical_json(document) != path.read_bytes():
        raise ReferenceError("Reference artifact JSON serialization is not canonical.")
    return document, rows


def load_graph(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    try:
        text = path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError) as error:
        raise ReferenceError(f"Cannot load mission graph {path}: {error}") from error
    if "\r" in text or not text.endswith("\n"):
        raise ReferenceError("Mission graph must use LF and end with a newline.")
    lines = text[:-1].split("\n")
    reader = csv.DictReader(lines)
    records = list(reader)
    expected_fields = ["Level", *[f"Q{index}" for index in range(11)], "Tokens"]
    if reader.fieldnames != expected_fields:
        raise ReferenceError("Mission graph header is not canonical.")
    if len(records) != 220:
        raise ReferenceError("Mission graph must contain exactly 220 level rows.")
    return lines, records


def verify_graph(path: Path, rows: dict[int, list[int]]) -> int:
    _, records = load_graph(path)
    checked_cells = 0
    for level in range(MIN_REFERENCE_LEVEL, MAX_REFERENCE_LEVEL + 1):
        record = records[level - 1]
        if int(record["Level"]) != level:
            raise ReferenceError(f"Mission graph row {level} has the wrong level key.")
        expected = derive_detents(level, rows[level])
        actual = [int(record[f"Q{index}"]) for index in range(DIFFICULTY_COUNT)]
        if actual != expected:
            raise ReferenceError(
                f"Mission graph differs from Helpbot at level {level}: "
                f"expected={expected}, actual={actual}."
            )
        checked_cells += DIFFICULTY_COUNT
    return checked_cells


def update_graph(path: Path, rows: dict[int, list[int]]) -> None:
    lines, records = load_graph(path)
    for level in range(MIN_REFERENCE_LEVEL, MAX_REFERENCE_LEVEL + 1):
        record = records[level - 1]
        expected = derive_detents(level, rows[level])
        for index, value in enumerate(expected):
            record[f"Q{index}"] = str(value)

    output = [lines[0]]
    fields = ["Level", *[f"Q{index}" for index in range(11)], "Tokens"]
    output.extend(",".join(record[field] for field in fields) for record in records)
    path.write_bytes(("\n".join(output) + "\n").encode("utf-8"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", type=Path, default=DEFAULT_REFERENCE)
    parser.add_argument("--graph", type=Path, default=DEFAULT_GRAPH)
    parser.add_argument(
        "--extract-raw",
        type=Path,
        help="Extract the pinned raw wikitext into the tracked JSON artifact.",
    )
    parser.add_argument(
        "--update-graph",
        action="store_true",
        help="Replace only levels 1..149 Q0..Q10 with derived reference detents.",
    )
    args = parser.parse_args()

    if args.extract_raw:
        rows = parse_raw_wikitext(args.extract_raw.read_bytes())
        document = build_reference(rows)
        args.reference.parent.mkdir(parents=True, exist_ok=True)
        args.reference.write_bytes(canonical_json(document))

    _, rows = load_reference(args.reference)
    if args.update_graph:
        update_graph(args.graph, rows)
    checked_cells = verify_graph(args.graph, rows)
    print(
        "Helpbot mission-QL parity PASS: "
        f"levels=149 detents={checked_cells} "
        f"source_sha256={SOURCE_RAW_SHA256}"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ReferenceError) as error:
        print(f"Helpbot mission-QL verification failed: {error}", file=sys.stderr)
        raise SystemExit(1)
