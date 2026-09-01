#!/usr/bin/env python3
"""Normalize archived ARPA3/ClickSaver mission evidence without network access."""

from __future__ import annotations

import argparse
import csv
import gzip
import hashlib
import html
import io
import json
import re
import shutil
import struct
import sys
import tempfile
import zipfile
from collections import Counter, defaultdict
from dataclasses import dataclass
from decimal import Decimal, getcontext
from pathlib import Path
from typing import Any, Iterable


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
REFERENCE_ROOT = REPOSITORY_ROOT / "docs" / "reference" / "missions"
GENERATED_ROOT = REPOSITORY_ROOT / "docs" / "generated" / "missions" / "arpa3"
SOURCE_MANIFEST = REFERENCE_ROOT / "source-manifest.json"
ALL_ARCHIVE = REFERENCE_ROOT / "clicksaver" / "raw" / "cs3-all-noicons-localdb-18-8-0.zip"
TINY_ARCHIVE = REFERENCE_ROOT / "clicksaver" / "raw" / "cs310-v2.zip"
BDB_ARCHIVE = REFERENCE_ROOT / "clicksaver" / "raw" / "cs23-24-localdb-18-8-0.zip"
AO_TEMPLATE_PROJECTION = REFERENCE_ROOT / "aorebirth-item-templates.jsonl"
AO_ITEMS_DAT = REPOSITORY_ROOT / "AORebirth" / "Datafiles" / "items.dat"
ROLLABILITY_PAGE = REFERENCE_ROOT / "arpa3" / "raw" / "rollability.html"
ARPA_FIXTURE = REPOSITORY_ROOT / "Tools" / "tests" / "fixtures" / "arpa3-rollability-sunglasses-sample.html"
CLICK_LOG_FIXTURE = REPOSITORY_ROOT / "Tools" / "tests" / "fixtures" / "clicksaver-cs-res-sample.log"
REWARD_ROOT = REPOSITORY_ROOT / "AORebirth" / "Server" / "ZoneEngine" / "XML Data" / "MissionRewards"
REWARD_FILES = (
    "ItemDB_Clusters.json",
    "ItemDB_Implants.json",
    "ItemDB_Nanos.json",
    "ItemDB_Refined.json",
    "ItemDB_Rest.json",
)
EXPECTED_ITEMS_DAT_SHA256 = "4e5355f177a42fbd05b33b4a27083a53ecfee93f5fce982880f19e5461badf3c"
EXPECTED_ITEMS_DAT_BYTES = 2466207
ARPA_FIXTURE_QUERY = "sunglasses"
ARPA_FIXTURE_RETRIEVED_AT = "2026-09-01T03:16:00Z"
SCHEMA_VERSION = 1


class EvidenceError(RuntimeError):
    pass


@dataclass(frozen=True)
class ClickSaverItem:
    ordinal: int
    raw_key: int
    key_flags: int
    item_id: int
    name_offset: int
    item_name: str | None
    metadata_word_0: int
    metadata_word_1: int
    metadata_word_2: int


@dataclass(frozen=True)
class ClickSaverPlayfield:
    ordinal: int
    raw_key: int
    playfield_id: int
    name_offset: int
    playfield_name: str


@dataclass(frozen=True)
class AoTemplate:
    item_id: int
    quality_level: int
    relations: tuple[int, ...]
    item_type: int
    flags: int


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def archive_member(path: Path, member: str) -> bytes:
    with zipfile.ZipFile(path) as archive:
        candidates = [name for name in archive.namelist() if Path(name).name.casefold() == member.casefold()]
        if len(candidates) != 1:
            raise EvidenceError(f"expected one {member} in {path}, found {len(candidates)}")
        return archive.read(candidates[0])


def decode_clicksaver_7bit(encoded: bytes) -> str:
    """Decode ClickSaver 3.x's eight-characters-in-seven-bytes text."""
    decoded = bytearray()
    for start in range(0, len(encoded), 7):
        chunk = encoded[start:start + 7]
        decoded.extend(value >> 1 for value in chunk)
        if len(chunk) == 7:
            eighth = sum((value & 1) << index for index, value in enumerate(chunk))
            if eighth != 0x7F:
                decoded.append(eighth)
    try:
        return decoded.decode("ascii")
    except UnicodeDecodeError as error:
        raise EvidenceError("ClickSaver packed name is not seven-bit ASCII") from error


def encode_clicksaver_7bit(value: str) -> bytes:
    raw = value.encode("ascii")
    encoded = bytearray()
    for start in range(0, len(raw), 8):
        chunk = raw[start:start + 8]
        first = chunk[:7]
        eighth = chunk[7] if len(chunk) == 8 else 0x7F
        encoded.extend((character << 1) | ((eighth >> index) & 1) for index, character in enumerate(first))
    return bytes(encoded)


def validate_clicksaver_name(value: str, context: str, allow_missing: bool = False) -> str | None:
    if allow_missing and value == "\x00":
        return None
    if not value or any(not character.isprintable() for character in value):
        escaped = value.encode("unicode_escape").decode("ascii")
        raise EvidenceError(f"{context} decoded to an empty or non-printable name: {escaped}")
    return value


def parse_clicksaver_name_table(
    payload: bytes,
    name_count: int,
    item_table_offset: int,
) -> tuple[dict[int, bytes], int]:
    cursor = 12
    records_by_offset: dict[int, bytes] = {}
    for _ in range(name_count):
        record_offset = cursor
        if cursor + 2 > item_table_offset:
            raise EvidenceError("ClickSaver name-length field crosses the item table")
        encoded_length = struct.unpack_from("<H", payload, cursor)[0]
        cursor += 2
        end = cursor + encoded_length
        if end > item_table_offset:
            raise EvidenceError("ClickSaver packed name crosses the item table")
        records_by_offset[record_offset] = payload[cursor:end]
        cursor = end
    return records_by_offset, cursor


def parse_clicksaver_database(
    payload: bytes,
) -> tuple[list[ClickSaverItem], list[ClickSaverPlayfield], dict[str, Any]]:
    if len(payload) < 12:
        raise EvidenceError("ClickSaver database header is truncated")
    name_count, item_count, item_table_offset = struct.unpack_from("<III", payload, 0)
    if name_count == 0 or item_count == 0:
        raise EvidenceError("ClickSaver database counts must be positive")
    if item_table_offset < 12 or item_table_offset > len(payload):
        raise EvidenceError("ClickSaver item-table offset is invalid")
    if len(payload) - item_table_offset != item_count * 8:
        raise EvidenceError("ClickSaver item table is not exactly eight bytes per declared item")

    records_by_offset, names_end = parse_clicksaver_name_table(payload, name_count, item_table_offset)
    items: list[ClickSaverItem] = []
    playfields: list[ClickSaverPlayfield] = []
    seen_ids: set[int] = set()
    seen_playfield_ids: set[int] = set()
    record_flag_counts: Counter[int] = Counter()
    named_record_flag_counts: Counter[int] = Counter()
    for ordinal in range(item_count):
        raw_key, name_offset = struct.unpack_from("<II", payload, item_table_offset + ordinal * 8)
        item_id = raw_key & 0x3FFFFFFF
        key_flags = raw_key >> 30
        record_flag_counts[key_flags] += 1
        if ordinal >= name_count:
            continue
        named_record_flag_counts[key_flags] += 1
        if item_id == 0:
            raise EvidenceError(f"ClickSaver named record {ordinal} has a zero identity")
        if name_offset not in records_by_offset:
            raise EvidenceError(
                f"ClickSaver item ordinal {ordinal} ID {item_id} points to undecoded name offset {name_offset}"
            )
        record_payload = records_by_offset[name_offset]
        if key_flags == 1:
            if item_id in seen_playfield_ids:
                raise EvidenceError(f"ClickSaver database duplicates playfield ID {item_id}")
            seen_playfield_ids.add(item_id)
            playfields.append(
                ClickSaverPlayfield(
                    ordinal,
                    raw_key,
                    item_id,
                    name_offset,
                    validate_clicksaver_name(
                        decode_clicksaver_7bit(record_payload),
                        f"ClickSaver playfield {item_id}",
                    ),
                )
            )
            continue
        elif key_flags == 2 and len(record_payload) >= 12:
            if item_id in seen_ids:
                raise EvidenceError(f"ClickSaver database duplicates template ID {item_id}")
            metadata_word_0, metadata_word_1, metadata_word_2 = struct.unpack_from("<III", record_payload, 0)
            packed_name = record_payload[12:]
        else:
            raise EvidenceError(
                f"ClickSaver item ordinal {ordinal} ID {item_id} has unsupported key flags {key_flags} and payload {record_payload[:32].hex()}"
            )
        seen_ids.add(item_id)
        items.append(
            ClickSaverItem(
                ordinal,
                raw_key,
                key_flags,
                item_id,
                name_offset,
                validate_clicksaver_name(
                    decode_clicksaver_7bit(packed_name),
                    f"ClickSaver item {item_id}",
                    allow_missing=True,
                ),
                metadata_word_0,
                metadata_word_1,
                metadata_word_2,
            )
        )
    if len(items) + len(playfields) != name_count:
        raise EvidenceError(
            f"ClickSaver header declares {name_count} names but decoded {len(items)} items and {len(playfields)} playfields"
        )
    return items, playfields, {
        "byte_length": len(payload),
        "item_identity_count": len(items),
        "item_missing_name_count": sum(1 for item in items if item.item_name is None),
        "item_table_offset": item_table_offset,
        "named_record_count": name_count,
        "named_record_flag_counts": {str(key): value for key, value in sorted(named_record_flag_counts.items())},
        "opaque_payload_bytes_between_names_and_table": item_table_offset - names_end,
        "playfield_identity_count": len(playfields),
        "resource_record_count": item_count,
        "resource_record_flag_counts": {str(key): value for key, value in sorted(record_flag_counts.items())},
        "decoded_name_record_count": len(records_by_offset),
        "names_end_offset": names_end,
    }


def require_int(row: dict[str, Any], key: str, context: str) -> int:
    value = row.get(key)
    if type(value) is not int:
        raise EvidenceError(f"{context} has invalid {key}")
    return value


def load_ao_templates(path: Path) -> dict[int, AoTemplate]:
    templates: dict[int, AoTemplate] = {}
    previous_id = -1
    with path.open("r", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            try:
                row = json.loads(line)
            except json.JSONDecodeError as error:
                raise EvidenceError(f"invalid AO template JSONL at line {line_number}") from error
            item_id = require_int(row, "item_id", f"AO template line {line_number}")
            if item_id <= previous_id:
                raise EvidenceError("AO template projection is not strictly sorted by item ID")
            previous_id = item_id
            relations_raw = row.get("relations")
            if not isinstance(relations_raw, list) or any(type(value) is not int for value in relations_raw):
                raise EvidenceError(f"invalid relations at AO template line {line_number}")
            relations = tuple(relations_raw)
            if tuple(sorted(relations)) != relations or len(set(relations)) != len(relations):
                raise EvidenceError(f"relations are not sorted and unique at AO template line {line_number}")
            templates[item_id] = AoTemplate(
                item_id,
                require_int(row, "quality_level", f"AO template line {line_number}"),
                relations,
                require_int(row, "item_type", f"AO template line {line_number}"),
                require_int(row, "flags", f"AO template line {line_number}"),
            )
    if not templates:
        raise EvidenceError("AO template projection contains no rows")
    return templates


def strip_html(value: str) -> str:
    without_tags = re.sub(r"<[^>]+>", " ", value)
    return re.sub(r"\s+", " ", html.unescape(without_tags)).strip()


def parse_html_table(table_html: str) -> list[list[str]]:
    rows: list[list[str]] = []
    for row_html in re.findall(r"<tr\b[^>]*>(.*?)</tr>", table_html, flags=re.IGNORECASE | re.DOTALL):
        cells = [strip_html(cell) for cell in re.findall(
            r"<t[dh]\b[^>]*>(.*?)</t[dh]>", row_html, flags=re.IGNORECASE | re.DOTALL
        )]
        if cells:
            rows.append(cells)
    return rows


def parse_arpa3_rollability_response(payload: str, source_sha256: str) -> list[dict[str, Any]]:
    observations: list[dict[str, Any]] = []
    seen: set[tuple[str, int, str, int | None]] = set()
    table_matches = list(re.finditer(r"<table\b[^>]*>.*?</table>", payload, flags=re.IGNORECASE | re.DOTALL))
    if not table_matches:
        raise EvidenceError("ARPA3 response has no result tables")
    for table_index, table_match in enumerate(table_matches):
        prefix = strip_html(payload[max(0, table_match.start() - 800):table_match.start()])
        heading = re.search(
            r"QL\s*(\d+)\s+(.+?)\s+as\s+(mission reward|item-to-find)\s*:\s*$",
            prefix,
            flags=re.IGNORECASE,
        )
        if not heading:
            raise EvidenceError(f"ARPA3 table {table_index} has no parseable item heading")
        item_ql = int(heading.group(1))
        item_name = heading.group(2).strip()
        role = "MISSION_REWARD" if heading.group(3).casefold() == "mission reward" else "ITEM_TO_FIND"
        rows = parse_html_table(table_match.group(0))
        if not rows or rows[0] != ["Mish QL", "Found once every these items", "Average (x5) rolls"]:
            raise EvidenceError(f"ARPA3 table {table_index} has unexpected columns")
        for row_index, cells in enumerate(rows[1:], 1):
            if len(cells) != 3:
                raise EvidenceError(f"ARPA3 table {table_index} row {row_index} has {len(cells)} cells")
            mission_text, frequency_text, average_text = cells
            contributor = mission_text.startswith("(c)")
            mission_text = mission_text.removeprefix("(c)").strip()
            row_scope = "OVERALL" if mission_text.casefold() == "overall" else "MISSION_QL"
            if row_scope == "OVERALL":
                mission_ql = None
            elif mission_text.isdigit():
                mission_ql = int(mission_text)
            else:
                raise EvidenceError(f"ARPA3 table {table_index} row {row_index} has invalid mission QL")
            try:
                frequency = int(frequency_text.replace(",", ""))
                average = int(average_text.replace(",", ""))
            except ValueError as error:
                raise EvidenceError(f"ARPA3 table {table_index} row {row_index} has invalid frequency") from error
            if frequency <= 0 or average <= 0:
                raise EvidenceError("ARPA3 frequency values must be positive")
            key = (item_name.casefold(), item_ql, role, mission_ql)
            if key in seen:
                raise EvidenceError(f"ARPA3 response duplicates observation {key}")
            seen.add(key)
            getcontext().prec = 18
            observations.append({
                "average_five_mission_rolls": average,
                "contributor_log_derived": contributor,
                "evidence_level": "OBSERVED_ARPA3_RESPONSE",
                "frequency_denominator_items": frequency,
                "high_id": None,
                "item_name": item_name,
                "item_ql": item_ql,
                "location_name": None,
                "location_playfield_id": None,
                "location_x": None,
                "location_y": None,
                "low_id": None,
                "match_mode": "CONTAINS",
                "mission_ql": mission_ql,
                "mission_slot": None,
                "mission_type": None,
                "probability_per_item": format(Decimal(1) / Decimal(frequency), ".12f"),
                "probability_provenance": "DERIVED_ARITHMETIC_FROM_ONE_IN_N",
                "query": ARPA_FIXTURE_QUERY,
                "retrieved_at_utc": ARPA_FIXTURE_RETRIEVED_AT,
                "role": role,
                "row_scope": row_scope,
                "source_artifact": str(ARPA_FIXTURE.relative_to(REPOSITORY_ROOT)).replace("\\", "/"),
                "source_sha256": source_sha256,
            })
    return observations


def parse_clicksaver_log_sample(payload: str, source_sha256: str) -> list[dict[str, Any]]:
    starts = list(re.finditer(r"^\s*\*\*\* Found wanted mission, QL(\d+) #(\d+)\s*$", payload, flags=re.MULTILINE))
    if not starts:
        raise EvidenceError("ClickSaver log sample contains no wanted missions")
    records: list[dict[str, Any]] = []
    seen: set[tuple[int, int, int, float, float]] = set()
    for index, start in enumerate(starts):
        block_end = starts[index + 1].start() if index + 1 < len(starts) else len(payload)
        block = payload[start.end():block_end]
        mission_ql = int(start.group(1))
        mission_slot = int(start.group(2))
        location = re.search(r"^\s*\*{0,3}\s*loc\s+(\d+):\s+([0-9.]+)\s+([0-9.]+)\s+(.+?)\s*$", block, flags=re.MULTILINE)
        find_item = re.search(r"^\s*\*{0,3}\s*find\s+(.+?)\s*$", block, flags=re.MULTILINE)
        reward = re.search(r"^\s*\*{0,3}\s*reward\s+QL(\d+)\s+(.+?)\s+\(DB Id\s+(\d+)/(\d+)\)\s*$", block, flags=re.MULTILINE)
        if not location or (not find_item and not reward):
            raise EvidenceError(f"ClickSaver mission QL{mission_ql} slot {mission_slot} is incomplete")
        key = (mission_ql, mission_slot, int(location.group(1)), float(location.group(2)), float(location.group(3)))
        if key in seen:
            raise EvidenceError(f"ClickSaver log sample duplicates mission {key}")
        seen.add(key)
        records.append({
            "evidence_level": "DOCUMENTED_CLICKSAVER_LOG_SAMPLE",
            "find_item_name": find_item.group(1).strip() if find_item else None,
            "location_name": location.group(4).strip(),
            "location_playfield_id": int(location.group(1)),
            "location_x": float(location.group(2)),
            "location_y": float(location.group(3)),
            "mission_ql": mission_ql,
            "mission_slot": mission_slot,
            "mission_type": None,
            "reward_high_id": int(reward.group(4)) if reward else None,
            "reward_item_name": reward.group(2).strip() if reward else None,
            "reward_low_id": int(reward.group(3)) if reward else None,
            "reward_ql": int(reward.group(1)) if reward else None,
            "source_artifact": str(CLICK_LOG_FIXTURE.relative_to(REPOSITORY_ROOT)).replace("\\", "/"),
            "source_sha256": source_sha256,
        })
    return records


def parse_documented_ql_exceptions(payload: str) -> list[dict[str, Any]]:
    text_lines = [strip_html(line) for line in payload.splitlines()]
    exceptions: list[dict[str, Any]] = []
    exact_pattern = re.compile(r"^QL(\d+) (.+?) (?:rewarded/)?to-find in QL(\d+) mission \(QLdif=(\d+)\)$")
    approximate_pattern = re.compile(r"^QL(\d+) (.+?) rewarded/to-find in missions around QL(\d+)$")
    for line_number, line in enumerate(text_lines, 1):
        exact = exact_pattern.match(line)
        approximate = approximate_pattern.match(line)
        if exact:
            item_ql, item_name, mission_ql, stated_delta = exact.groups()
            exceptions.append({
                "delta": int(mission_ql) - int(item_ql),
                "delta_absolute": int(stated_delta),
                "evidence_level": "DOCUMENTED_EXACT_QL_EXCEPTION",
                "item_name": item_name,
                "item_ql": int(item_ql),
                "mission_ql": int(mission_ql),
                "source_line": line_number,
            })
        elif approximate:
            item_ql, item_name, mission_ql = approximate.groups()
            delta = int(mission_ql) - int(item_ql)
            exceptions.append({
                "delta": delta,
                "delta_absolute": abs(delta),
                "evidence_level": "DOCUMENTED_APPROXIMATE_QL_EXCEPTION",
                "item_name": item_name,
                "item_ql": int(item_ql),
                "mission_ql": int(mission_ql),
                "source_line": line_number,
            })
    if len(exceptions) != 7:
        raise EvidenceError(f"expected seven documented QL exceptions, found {len(exceptions)}")
    return exceptions


def load_reward_catalogs() -> tuple[dict[int, list[dict[str, Any]]], dict[str, list[dict[str, Any]]], int]:
    by_endpoint: dict[int, list[dict[str, Any]]] = defaultdict(list)
    by_name: dict[str, list[dict[str, Any]]] = defaultdict(list)
    row_count = 0
    for filename in REWARD_FILES:
        rows = json.loads((REWARD_ROOT / filename).read_text(encoding="utf-8-sig"))
        if not isinstance(rows, list):
            raise EvidenceError(f"reward catalog {filename} is not an array")
        for ordinal, row in enumerate(rows):
            if not isinstance(row, dict) or not isinstance(row.get("Key"), dict):
                raise EvidenceError(f"reward catalog {filename} row {ordinal} has no Key object")
            key = row["Key"]
            normalized = {
                "catalog": filename,
                "high_id": require_int(key, "HighId", f"{filename} row {ordinal}"),
                "high_ql": require_int(key, "HighQl", f"{filename} row {ordinal}"),
                "low_id": require_int(key, "LowId", f"{filename} row {ordinal}"),
                "low_ql": require_int(key, "LowQl", f"{filename} row {ordinal}"),
                "name": str(key.get("Name", "")).strip(),
                "ordinal": ordinal,
            }
            if not normalized["name"]:
                raise EvidenceError(f"reward catalog {filename} row {ordinal} has no name")
            row_count += 1
            by_endpoint[normalized["low_id"]].append(normalized)
            if normalized["high_id"] != normalized["low_id"]:
                by_endpoint[normalized["high_id"]].append(normalized)
            by_name[normalized["name"].casefold()].append(normalized)
    return by_endpoint, by_name, row_count


def resolve_clicksaver_item(
    item: ClickSaverItem,
    templates: dict[int, AoTemplate],
    reward_by_endpoint: dict[int, list[dict[str, Any]]],
    reward_by_name: dict[str, list[dict[str, Any]]],
) -> dict[str, Any]:
    template = templates.get(item.item_id)
    name_candidates = reward_by_name.get(item.item_name.casefold(), []) if item.item_name else []
    if template is None:
        unique_pairs = sorted({(row["low_id"], row["high_id"]) for row in name_candidates})
        resolution = "NAME_ONLY_CANDIDATE" if len(unique_pairs) == 1 else "AMBIGUOUS_NAME" if len(unique_pairs) > 1 else "UNRESOLVED"
        return {
            "aorebirth_flags": None,
            "aorebirth_item_type": None,
            "aorebirth_quality_level": None,
            "aorebirth_resolution": resolution,
            "group_high_id": None,
            "group_high_ql": None,
            "group_low_id": None,
            "group_low_ql": None,
            "group_relation_ids": [],
            "reward_catalog_endpoint_matches": 0,
            "reward_catalog_name_candidates": len(name_candidates),
        }
    relation_ids = template.relations if template.relations else (template.item_id,)
    resolved_relations = [templates[relation_id] for relation_id in relation_ids if relation_id in templates]
    missing_relations = [relation_id for relation_id in relation_ids if relation_id not in templates]
    if not resolved_relations:
        raise EvidenceError(f"AO template {template.item_id} has no resolvable relation members")
    low = min(resolved_relations, key=lambda value: (value.quality_level, value.item_id))
    high = max(resolved_relations, key=lambda value: (value.quality_level, value.item_id))
    endpoint_matches = reward_by_endpoint.get(template.item_id, [])
    resolution = (
        "EXACT_ID_PARTIAL_RELATION" if missing_relations
        else "EXACT_ID_AND_REWARD_ENDPOINT" if endpoint_matches
        else "EXACT_ID"
    )
    return {
        "aorebirth_flags": template.flags,
        "aorebirth_item_type": template.item_type,
        "aorebirth_quality_level": template.quality_level,
        "aorebirth_resolution": resolution,
        "group_high_id": high.item_id,
        "group_high_ql": high.quality_level,
        "group_low_id": low.item_id,
        "group_low_ql": low.quality_level,
        "group_relation_ids": list(relation_ids),
        "reward_catalog_endpoint_matches": len(endpoint_matches),
        "reward_catalog_name_candidates": len(name_candidates),
    }


def verify_acquisition_manifest() -> dict[str, Any]:
    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    policy = manifest.get("AcquisitionPolicy", {})
    if policy.get("RobotsDisallowCgi") is not True or policy.get("CgiPathsRequested") is not False:
        raise EvidenceError("source manifest does not preserve the CGI acquisition boundary")
    artifacts = manifest.get("Artifacts")
    if not isinstance(artifacts, list) or not artifacts:
        raise EvidenceError("source manifest has no artifacts")
    for artifact in artifacts:
        path = REFERENCE_ROOT / artifact["RelativePath"]
        if path.stat().st_size != artifact["ByteLength"]:
            raise EvidenceError(f"source byte length changed: {path}")
        if sha256_file(path) != artifact["Sha256"]:
            raise EvidenceError(f"source SHA-256 changed: {path}")
    return manifest


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False, sort_keys=True) + "\n", encoding="utf-8", newline="\n")


def write_jsonl(path: Path, rows: Iterable[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        for row in rows:
            stream.write(json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")


def write_gzip_jsonl(path: Path, rows: Iterable[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as raw_stream:
        with gzip.GzipFile(filename="", mode="wb", compresslevel=9, fileobj=raw_stream, mtime=0) as gzip_stream:
            with io.TextIOWrapper(gzip_stream, encoding="utf-8", newline="\n") as text_stream:
                for row in rows:
                    text_stream.write(json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not rows:
        raise EvidenceError(f"refusing to write empty CSV {path}")
    fieldnames = sorted(rows[0])
    if any(sorted(row) != fieldnames for row in rows):
        raise EvidenceError(f"CSV rows have inconsistent fields for {path}")
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            rendered = dict(row)
            for key, value in rendered.items():
                if isinstance(value, (list, dict)):
                    rendered[key] = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
            writer.writerow(rendered)


def generate(output_root: Path) -> dict[str, Any]:
    source_manifest = verify_acquisition_manifest()
    if AO_ITEMS_DAT.stat().st_size != EXPECTED_ITEMS_DAT_BYTES or sha256_file(AO_ITEMS_DAT) != EXPECTED_ITEMS_DAT_SHA256:
        raise EvidenceError("AORebirth items.dat no longer matches the projected authoritative source")
    templates = load_ao_templates(AO_TEMPLATE_PROJECTION)
    all_payload = archive_member(ALL_ARCHIVE, "All.cdb")
    tiny_payload = archive_member(TINY_ARCHIVE, "Tiny.cdb")
    bdb_payload = archive_member(BDB_ARCHIVE, "AODatabase.bdb")
    all_items, all_playfields, all_meta = parse_clicksaver_database(all_payload)
    tiny_items, tiny_playfields, tiny_meta = parse_clicksaver_database(tiny_payload)
    all_by_id = {item.item_id: item for item in all_items}
    tiny_by_id = {item.item_id: item for item in tiny_items}
    missing_tiny_ids = sorted(set(tiny_by_id) - set(all_by_id))
    name_mismatches = [
        item_id
        for item_id, item in tiny_by_id.items()
        if item_id in all_by_id and item.item_name != all_by_id[item_id].item_name
    ]

    reward_by_endpoint, reward_by_name, reward_row_count = load_reward_catalogs()
    catalog_rows: list[dict[str, Any]] = []
    resolution_counts: Counter[str] = Counter()
    for item_id in sorted(set(all_by_id) | set(tiny_by_id)):
        all_item = all_by_id.get(item_id)
        tiny_item = tiny_by_id.get(item_id)
        item = all_item or tiny_item
        assert item is not None
        resolution = resolve_clicksaver_item(item, templates, reward_by_endpoint, reward_by_name)
        resolution_counts[resolution["aorebirth_resolution"]] += 1
        catalog_rows.append({
            "clicksaver_item_id": item.item_id,
            "clicksaver_item_name": item.item_name,
            "clicksaver_item_name_present": item.item_name is not None,
            "clicksaver_all_item_name": all_item.item_name if all_item else None,
            "clicksaver_all_metadata_word_0": all_item.metadata_word_0 if all_item else None,
            "clicksaver_all_metadata_word_1": all_item.metadata_word_1 if all_item else None,
            "clicksaver_all_metadata_word_2": all_item.metadata_word_2 if all_item else None,
            "clicksaver_all_key_flags": all_item.key_flags if all_item else None,
            "clicksaver_all_name_offset": all_item.name_offset if all_item else None,
            "clicksaver_all_ordinal": all_item.ordinal if all_item else None,
            "clicksaver_all_raw_key": all_item.raw_key if all_item else None,
            "clicksaver_name_conflict": bool(all_item and tiny_item and all_item.item_name != tiny_item.item_name),
            "clicksaver_tiny_item_name": tiny_item.item_name if tiny_item else None,
            "clicksaver_tiny_metadata_word_0": tiny_item.metadata_word_0 if tiny_item else None,
            "clicksaver_tiny_metadata_word_1": tiny_item.metadata_word_1 if tiny_item else None,
            "clicksaver_tiny_metadata_word_2": tiny_item.metadata_word_2 if tiny_item else None,
            "clicksaver_tiny_key_flags": tiny_item.key_flags if tiny_item else None,
            "clicksaver_tiny_name_offset": tiny_item.name_offset if tiny_item else None,
            "clicksaver_tiny_ordinal": tiny_item.ordinal if tiny_item else None,
            "clicksaver_tiny_raw_key": tiny_item.raw_key if tiny_item else None,
            "in_all_cdb": all_item is not None,
            "in_tiny_cdb": tiny_item is not None,
            "name_authority": "ALL_CDB" if all_item else "TINY_CDB",
            **resolution,
        })

    all_playfields_by_id = {row.playfield_id: row for row in all_playfields}
    tiny_playfields_by_id = {row.playfield_id: row for row in tiny_playfields}
    playfield_rows = []
    for playfield_id in sorted(set(all_playfields_by_id) | set(tiny_playfields_by_id)):
        all_row = all_playfields_by_id.get(playfield_id)
        tiny_row = tiny_playfields_by_id.get(playfield_id)
        playfield_rows.append({
            "all_name": all_row.playfield_name if all_row else None,
            "all_name_offset": all_row.name_offset if all_row else None,
            "all_ordinal": all_row.ordinal if all_row else None,
            "all_raw_key": all_row.raw_key if all_row else None,
            "in_all_cdb": all_row is not None,
            "in_tiny_cdb": tiny_row is not None,
            "name_conflict": bool(all_row and tiny_row and all_row.playfield_name != tiny_row.playfield_name),
            "playfield_id": playfield_id,
            "tiny_name": tiny_row.playfield_name if tiny_row else None,
            "tiny_name_offset": tiny_row.name_offset if tiny_row else None,
            "tiny_ordinal": tiny_row.ordinal if tiny_row else None,
            "tiny_raw_key": tiny_row.raw_key if tiny_row else None,
        })

    arpa_fixture_bytes = ARPA_FIXTURE.read_bytes()
    roll_rows = parse_arpa3_rollability_response(arpa_fixture_bytes.decode("utf-8"), sha256_bytes(arpa_fixture_bytes))
    click_log_bytes = CLICK_LOG_FIXTURE.read_bytes()
    click_log_rows = parse_clicksaver_log_sample(click_log_bytes.decode("utf-8"), sha256_bytes(click_log_bytes))
    ql_exceptions = parse_documented_ql_exceptions(ROLLABILITY_PAGE.read_text(encoding="latin-1"))
    mission_rows = [row for row in roll_rows if row["mission_ql"] is not None]
    sample_deltas = [row["mission_ql"] - row["item_ql"] for row in mission_rows]
    sample_abs_deltas = [abs(value) for value in sample_deltas]

    write_gzip_jsonl(output_root / "clicksaver-item-catalog.jsonl.gz", catalog_rows)
    write_csv(output_root / "clicksaver-item-catalog.csv", catalog_rows)
    write_jsonl(output_root / "normalized-roll-observations.jsonl", roll_rows)
    write_csv(output_root / "normalized-roll-observations.csv", roll_rows)
    write_jsonl(output_root / "normalized-clicksaver-log-samples.jsonl", click_log_rows)
    write_json(output_root / "clicksaver-playfield-catalog.json", playfield_rows)
    write_json(output_root / "documented-ql-exceptions.json", ql_exceptions)
    write_json(output_root / "archive-member-inventory.json", [
        {
            "byte_length": len(all_payload),
            "extraction_status": "DECODED_CUSTOM_CDB",
            "member": "All.cdb",
            "parent_archive": str(ALL_ARCHIVE.relative_to(REPOSITORY_ROOT)).replace("\\", "/"),
            "sha256": sha256_bytes(all_payload),
        },
        {
            "byte_length": len(tiny_payload),
            "extraction_status": "DECODED_CUSTOM_CDB",
            "member": "Tiny.cdb",
            "parent_archive": str(TINY_ARCHIVE.relative_to(REPOSITORY_ROOT)).replace("\\", "/"),
            "sha256": sha256_bytes(tiny_payload),
        },
        {
            "byte_length": len(bdb_payload),
            "extraction_status": "OPAQUE_BERKELEY_DB_4_ARTIFACT",
            "member": "AODatabase.bdb",
            "parent_archive": str(BDB_ARCHIVE.relative_to(REPOSITORY_ROOT)).replace("\\", "/"),
            "prefix_hex": bdb_payload[:32].hex(),
            "sha256": sha256_bytes(bdb_payload),
        },
    ])
    analysis = {
        "arpa3_live_rdb_complete": False,
        "arpa3_query_backend_bulk_extraction_permitted": False,
        "clicksaver_all": all_meta,
        "clicksaver_tiny": tiny_meta,
        "clicksaver_union_item_count": len(catalog_rows),
        "clicksaver_tiny_only_item_count": len(missing_tiny_ids),
        "clicksaver_cross_version_name_conflict_count": len(name_mismatches),
        "clicksaver_playfield_union_count": len(playfield_rows),
        "clicksaver_playfield_cross_version_name_conflict_count": sum(1 for row in playfield_rows if row["name_conflict"]),
        "clicksaver_bdb_byte_length": len(bdb_payload),
        "documented_clicksaver_log_missions": len(click_log_rows),
        "documented_ql_exception_count": len(ql_exceptions),
        "documented_ql_exception_max_absolute_delta": max(row["delta_absolute"] for row in ql_exceptions),
        "documented_ql_exception_min_delta": min(row["delta"] for row in ql_exceptions),
        "documented_ql_exception_max_delta": max(row["delta"] for row in ql_exceptions),
        "exact_full_corpus_reward_composition_distribution": None,
        "exact_full_corpus_reward_type_distribution": None,
        "exact_full_corpus_per_mission_ql_counts": None,
        "exact_full_corpus_repeatability_probabilities": None,
        "observed_fixture_item_count": len({(row["item_name"], row["item_ql"], row["role"]) for row in roll_rows}),
        "observed_fixture_mission_ql_row_count": len(mission_rows),
        "observed_fixture_overall_row_count": len(roll_rows) - len(mission_rows),
        "observed_fixture_max_absolute_delta": max(sample_abs_deltas),
        "observed_fixture_min_delta": min(sample_deltas),
        "observed_fixture_max_delta": max(sample_deltas),
        "aorebirth_item_template_count": len(templates),
        "aorebirth_reward_catalog_row_count": reward_row_count,
        "aorebirth_resolution_counts": dict(sorted(resolution_counts.items())),
        "runtime_dependency_created": False,
        "runtime_mission_logic_changed": False,
    }
    write_json(output_root / "analysis-summary.json", analysis)

    generated_files = sorted(path for path in output_root.iterdir() if path.is_file() and path.name != "evidence-manifest.json")
    generated_manifest = {
        "schema_version": SCHEMA_VERSION,
        "acquisition_manifest_sha256": sha256_file(SOURCE_MANIFEST),
        "aorebirth_items_dat": {
            "byte_length": AO_ITEMS_DAT.stat().st_size,
            "relative_path": str(AO_ITEMS_DAT.relative_to(REPOSITORY_ROOT)).replace("\\", "/"),
            "sha256": sha256_file(AO_ITEMS_DAT),
        },
        "aorebirth_item_projection": {
            "byte_length": AO_TEMPLATE_PROJECTION.stat().st_size,
            "relative_path": str(AO_TEMPLATE_PROJECTION.relative_to(REPOSITORY_ROOT)).replace("\\", "/"),
            "row_count": len(templates),
            "sha256": sha256_file(AO_TEMPLATE_PROJECTION),
        },
        "corpus_boundaries": {
            "arpa3_backend_rows": "single pre-policy-discovery representative response fixture only",
            "failed_queries_are_negative_evidence": False,
            "robots_disallow_cgi": True,
            "source_artifact_count": len(source_manifest["Artifacts"]),
        },
        "files": [{
            "byte_length": path.stat().st_size,
            "relative_path": "docs/generated/missions/arpa3/" + path.name,
            "sha256": sha256_file(path),
        } for path in generated_files],
    }
    write_json(output_root / "evidence-manifest.json", generated_manifest)
    return analysis


def check_generated() -> None:
    with tempfile.TemporaryDirectory(prefix="aorebirth-arpa3-check-") as temporary:
        candidate = Path(temporary) / "generated"
        generate(candidate)
        expected_files = sorted(path.relative_to(GENERATED_ROOT) for path in GENERATED_ROOT.rglob("*") if path.is_file())
        actual_files = sorted(path.relative_to(candidate) for path in candidate.rglob("*") if path.is_file())
        if expected_files != actual_files:
            raise EvidenceError("generated mission evidence file set is stale")
        for relative in expected_files:
            if (GENERATED_ROOT / relative).read_bytes() != (candidate / relative).read_bytes():
                raise EvidenceError(f"generated mission evidence is stale: {relative}")


def build_test_cdb(names_and_ids: list[tuple[str, int]]) -> bytes:
    names = bytearray()
    offsets: list[int] = []
    for name, _ in names_and_ids:
        offsets.append(12 + len(names))
        record = struct.pack("<III", 100, 0xFFFF - 50, 42) + encode_clicksaver_7bit(name)
        names.extend(struct.pack("<H", len(record)))
        names.extend(record)
    item_offset = 12 + len(names)
    items = b"".join(struct.pack("<II", 0x80000000 | item_id, offset) for offset, (_, item_id) in zip(offsets, names_and_ids))
    return struct.pack("<III", len(names_and_ids), len(names_and_ids), item_offset) + bytes(names) + items


def self_test() -> None:
    value = "House template"
    if decode_clicksaver_7bit(encode_clicksaver_7bit(value)) != value:
        raise EvidenceError("ClickSaver seven-bit codec self-test failed")
    items, playfields, metadata = parse_clicksaver_database(build_test_cdb([("Alpha", 101), ("Eight888", 202)]))
    if playfields or [item.item_name for item in items] != ["Alpha", "Eight888"] or metadata["item_identity_count"] != 2:
        raise EvidenceError("ClickSaver database parser self-test failed")
    malformed = bytearray(build_test_cdb([("Alpha", 101)]))
    struct.pack_into("<I", malformed, 8, len(malformed) + 1)
    try:
        parse_clicksaver_database(bytes(malformed))
    except EvidenceError:
        pass
    else:
        raise EvidenceError("malformed ClickSaver offset was accepted")
    try:
        parse_clicksaver_database(build_test_cdb([("Alpha", 101), ("Beta", 101)]))
    except EvidenceError:
        pass
    else:
        raise EvidenceError("duplicate ClickSaver item ID was accepted")

    sample = """<html><body><p>QL1 Alpha as mission reward :</p><table><tr><td>Mish QL</td><td>Found once every these items</td><td>Average (x5) rolls</td></tr><tr><td><b>(c)</b> 5</td><td>2,000</td><td>400</td></tr><tr><td>Overall</td><td>3,000</td><td>600</td></tr></table></body></html>"""
    parsed = parse_arpa3_rollability_response(sample, sha256_bytes(sample.encode()))
    if len(parsed) != 2 or parsed[0]["mission_ql"] != 5 or parsed[0]["contributor_log_derived"] is not True:
        raise EvidenceError("ARPA3 response parser self-test failed")
    malformed_html = sample.replace("<td>400</td>", "")
    try:
        parse_arpa3_rollability_response(malformed_html, sha256_bytes(malformed_html.encode()))
    except EvidenceError:
        pass
    else:
        raise EvidenceError("malformed ARPA3 row was accepted")

    log = """ *** Found wanted mission, QL149 #2\n     loc 615: 1600.3 2142.4 Southern Fouls Hills\n *** find Nano Crystal (Flawed Warbot)\n     reward QL149 Senior Aero Borealis Corona (DB Id 122629/122630)\n"""
    log_rows = parse_clicksaver_log_sample(log, sha256_bytes(log.encode()))
    if log_rows[0]["reward_high_id"] != 122630 or log_rows[0]["location_playfield_id"] != 615:
        raise EvidenceError("ClickSaver log parser self-test failed")
    incomplete_log = " *** Found wanted mission, QL149 #2\n *** find Missing Location\n"
    try:
        parse_clicksaver_log_sample(incomplete_log, sha256_bytes(incomplete_log.encode()))
    except EvidenceError:
        pass
    else:
        raise EvidenceError("incomplete ClickSaver log mission was accepted")

    fake = ClickSaverItem(0, 0x80000001, 2, 1, 12, "Same", 1, 1, 1)
    candidates = {"same": [{"low_id": 10, "high_id": 11}, {"low_id": 20, "high_id": 21}]}
    if resolve_clicksaver_item(fake, {}, {}, candidates)["aorebirth_resolution"] != "AMBIGUOUS_NAME":
        raise EvidenceError("ambiguous name join did not fail closed")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_mutually_exclusive_group(required=True)
    actions.add_argument("--write", action="store_true")
    actions.add_argument("--check", action="store_true")
    actions.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        print("ARPA3_MISSION_EVIDENCE_SELF_TEST=PASS")
    elif args.write:
        if GENERATED_ROOT.exists():
            shutil.rmtree(GENERATED_ROOT)
        analysis = generate(GENERATED_ROOT)
        print(f"ARPA3_MISSION_EVIDENCE_WRITE=PASS ALL_ITEMS={analysis['clicksaver_all']['item_identity_count']} TINY_ITEMS={analysis['clicksaver_tiny']['item_identity_count']}")
    else:
        check_generated()
        print("ARPA3_MISSION_EVIDENCE_CHECK=PASS")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EvidenceError, OSError, ValueError, zipfile.BadZipFile) as error:
        print(f"ARPA3 mission evidence normalization failed: {error}", file=sys.stderr)
        raise SystemExit(1)
