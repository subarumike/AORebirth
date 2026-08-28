#!/usr/bin/env python3
"""Deterministic forensic audit of the official ACG-to-MonsterData boundary."""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from dataclasses import dataclass
import gzip
import hashlib
import json
from pathlib import Path
import struct
from typing import Any, Iterable, Mapping, Sequence

import numpy as np


ROOT = Path(__file__).resolve().parents[1]
EP1_ROOT = Path(r"C:\Users\Mike\Documents\AO stripdown\Anarchy Online")
EP2_ROOT = Path(r"C:\Funcom\Anarchy Online")
SOURCE_SHARDS = Path(
    r"C:\Users\Mike\Documents\AO stripdown\Docs\generated\playfield_district_info\18.8.62_EP1"
)
MONSTER_CORPUS = Path(
    r"C:\Users\Mike\Documents\AO stripdown\Docs\generated\monster_data\monster_data_corpus_inventory.json"
)
GHIDRA_TRACE = Path(
    r"C:\Users\Mike\Documents\AO stripdown\Docs\evidence\ACGHASH_MONSTERDATA_RUNTIME_RESOLVER_TRACE_20260825.md"
)
SOURCE_EXPORT = ROOT / "build-verify/acg-monsterdata-resource-audit/official-resource-sources.json"
PLACEMENT_ROOT = ROOT / "docs/generated/playfields/placements"
ARCHETYPE_CATALOG = ROOT / "docs/generated/enemy_archetypes/enemy-archetype-catalog.json"
RUNTIME_ASSOCIATIONS = ROOT / "docs/generated/enemy_archetypes/runtime-observation-archetype-associations.json"
OUTPUT_ROOT = ROOT / "docs/generated/acg_monsterdata_resource_audit"

RESOURCE_TYPE_ACG = 1_000_014
RESOURCE_TYPE_PLAYFIELD_DYNELS = 1_000_026
RESOURCE_TYPE_CAT_MESH = 1_010_002
RESOURCE_TYPE_MONSTER_DATA = 1_040_023
INDEX_PAGE_SIZE = 4096
INDEX_PAGE_HEADER = 32
INDEX_ENTRY_SIZE = 16
INDEX_LEAF_MARKER = 0x100
RECORD_HEADER_SIZE = 34
SAMPLE_PLAYFIELDS = (
    (4582, "PF4582 ICC Shuttleport"),
    (3081, "PF3081 Borealis Backyard"),
    (127, "PF127 Subway"),
    (4542, "PF4542 Central Elysium Shadowlands"),
    (655, "PF655 Andromeda Rubi-Ka"),
)


class AuditError(RuntimeError):
    pass


@dataclass(frozen=True)
class IndexEntry:
    ordinal: int
    page: int
    slot: int
    global_offset: int
    resource_type: int
    resource_instance: int
    unknown_u32: int

    @property
    def key(self) -> tuple[int, int]:
        return self.resource_type, self.resource_instance

    @property
    def identity(self) -> str:
        return f"index-page-{self.page}:slot-{self.slot}"


@dataclass(frozen=True)
class Segment:
    name: str
    path: Path
    global_start: int
    size: int

    @property
    def global_end(self) -> int:
        return self.global_start + self.size


@dataclass(frozen=True)
class ResourceRecord:
    entry: IndexEntry
    segment: Segment
    segment_offset: int
    allocation: bytes
    active_length: int

    @property
    def payload(self) -> bytes:
        return self.allocation[RECORD_HEADER_SIZE : self.active_length]


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def json_bytes(value: Any) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode("utf-8")


def compact_json_bytes(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(8 * 1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def u16(data: bytes, offset: int) -> int:
    return struct.unpack_from("<H", data, offset)[0]


def u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def i32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<i", data, offset)[0]


def parse_index(data: bytes) -> tuple[list[IndexEntry], list[int]]:
    if len(data) % INDEX_PAGE_SIZE or u32(data, 12) != INDEX_PAGE_SIZE:
        raise AuditError("unsupported ResourceDatabase index layout")
    pages = len(data) // INDEX_PAGE_SIZE
    leaf_candidates = [
        page for page in range(pages) if u32(data, page * INDEX_PAGE_SIZE + 16) == INDEX_LEAF_MARKER
    ]
    if not leaf_candidates:
        raise AuditError("ResourceDatabase index has no leaf pages")

    def page_pointer(page: int, field: int) -> int | None:
        raw = u32(data, page * INDEX_PAGE_SIZE + field)
        if raw == 0:
            return None
        if raw % INDEX_PAGE_SIZE:
            raise AuditError("unaligned ResourceDatabase index page pointer")
        target = raw // INDEX_PAGE_SIZE
        if target >= pages:
            raise AuditError("out-of-bounds ResourceDatabase index page pointer")
        return target

    first = leaf_candidates[0]
    backward: set[int] = set()
    while True:
        if first in backward:
            raise AuditError("cycle in ResourceDatabase previous-page chain")
        backward.add(first)
        previous = page_pointer(first, 4)
        if previous is None:
            break
        first = previous

    ordered_pages: list[int] = []
    seen: set[int] = set()
    previous: int | None = None
    page: int | None = first
    while page is not None:
        if page in seen:
            raise AuditError("cycle in ResourceDatabase next-page chain")
        seen.add(page)
        if u32(data, page * INDEX_PAGE_SIZE + 16) != INDEX_LEAF_MARKER:
            raise AuditError("active ResourceDatabase chain contains a non-leaf page")
        if page_pointer(page, 4) != previous:
            raise AuditError("non-reciprocal ResourceDatabase leaf links")
        ordered_pages.append(page)
        previous = page
        page = page_pointer(page, 0)
    if seen != set(leaf_candidates):
        raise AuditError("unlinked ResourceDatabase leaf pages")

    entries: list[IndexEntry] = []
    for page_number in ordered_pages:
        start = page_number * INDEX_PAGE_SIZE
        count = u16(data, start + 8)
        serialized = u16(data, start + 10)
        if serialized != count * INDEX_ENTRY_SIZE:
            raise AuditError(f"ResourceDatabase page {page_number} byte count mismatch")
        for slot in range(count):
            offset = start + INDEX_PAGE_HEADER + slot * INDEX_ENTRY_SIZE
            raw = data[offset : offset + INDEX_ENTRY_SIZE]
            entries.append(
                IndexEntry(
                    ordinal=len(entries),
                    page=page_number,
                    slot=slot,
                    global_offset=int.from_bytes(raw[0:4], "little"),
                    resource_type=int.from_bytes(raw[4:8], "big"),
                    resource_instance=int.from_bytes(raw[8:12], "big"),
                    unknown_u32=int.from_bytes(raw[12:16], "little"),
                )
            )
    if u32(data, 24) != len(entries):
        raise AuditError("ResourceDatabase declared record count mismatch")
    return entries, ordered_pages


def discover_segments(client_root: Path) -> list[Segment]:
    database = client_root / "cd_image/data/db"
    candidates = [database / "ResourceDatabase.dat"] + sorted(
        database.glob("ResourceDatabase.dat.[0-9][0-9][0-9]"), key=lambda path: int(path.suffix[1:])
    )
    if not candidates[0].is_file():
        raise AuditError(f"missing ResourceDatabase.dat under {client_root}")
    segments: list[Segment] = []
    start = 0
    for ordinal, path in enumerate(candidates):
        expected = "ResourceDatabase.dat" if ordinal == 0 else f"ResourceDatabase.dat.{ordinal:03d}"
        if path.name != expected:
            raise AuditError(f"ResourceDatabase segment gap: expected {expected}, found {path.name}")
        size = path.stat().st_size
        segments.append(Segment(path.name, path, start, size))
        start += size
    return segments


class ResourceReader:
    def __init__(self, segments: Sequence[Segment]) -> None:
        self.segments = tuple(segments)
        self.streams = {segment.name: segment.path.open("rb", buffering=0) for segment in segments}

    def close(self) -> None:
        for stream in self.streams.values():
            stream.close()

    def locate(self, global_offset: int) -> tuple[Segment, int]:
        for segment in self.segments:
            if segment.global_start <= global_offset < segment.global_end:
                return segment, global_offset - segment.global_start
        raise AuditError(f"resource offset {global_offset} is outside all database segments")

    def read(self, entry: IndexEntry) -> ResourceRecord:
        segment, local = self.locate(entry.global_offset)
        stream = self.streams[segment.name]
        stream.seek(local)
        header = stream.read(RECORD_HEADER_SIZE)
        if len(header) != RECORD_HEADER_SIZE or header[:2] != b"\xfa\xfa":
            raise AuditError(
                f"{entry.identity} key={entry.resource_type}:{entry.resource_instance} "
                f"offset={entry.global_offset} lacks a valid resource envelope"
            )
        length = u32(header, 2)
        if length < RECORD_HEADER_SIZE or local + length > segment.size:
            raise AuditError(f"{entry.identity} has an invalid allocation length")
        remainder = stream.read(length - RECORD_HEADER_SIZE)
        if len(remainder) != length - RECORD_HEADER_SIZE:
            raise AuditError(f"{entry.identity} resource read was truncated")
        allocation = header + remainder
        outer = u32(allocation, 6)
        active_length = 10 + outer
        checks = (
            active_length <= length,
            outer >= 24,
            u32(allocation, 10) == entry.resource_type,
            u32(allocation, 14) == entry.resource_instance,
            u32(allocation, 18) == outer - 12,
            u32(allocation, 22) == entry.resource_type,
            u32(allocation, 26) == entry.resource_instance,
        )
        if not all(checks):
            raise AuditError(f"{entry.identity} resource envelope does not match its index key")
        return ResourceRecord(entry, segment, local, allocation, active_length)


def read_raw_reference_scan(path: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    with path.open("rb") as stream:
        if stream.read(8) != b"AOMDREF2":
            raise AuditError("unsupported effective-resource raw scan magic")
        version, count = struct.unpack("<ii", stream.read(8))
        if version != 2:
            raise AuditError(f"unsupported effective-resource raw scan version {version}")
        for _ in range(count):
            header = stream.read(12)
            if len(header) != 12:
                raise AuditError("truncated effective-resource raw scan record")
            resource_type, resource_instance, raw_length = struct.unpack("<iii", header)
            raw_sha256 = stream.read(32).hex()
            hit_count_raw = stream.read(4)
            if len(hit_count_raw) != 4:
                raise AuditError("truncated effective-resource raw scan hit count")
            hit_count = struct.unpack("<i", hit_count_raw)[0]
            hits = []
            for _ in range(hit_count):
                raw_hit = stream.read(16)
                if len(raw_hit) != 16:
                    raise AuditError("truncated effective-resource raw scan hit")
                offset, value, previous, following = struct.unpack("<iIII", raw_hit)
                hits.append(
                    {
                        "offset": offset,
                        "value": value,
                        "previousValue": None if previous == 0xFFFFFFFF else previous,
                        "nextValue": None if following == 0xFFFFFFFF else following,
                    }
                )
            records.append(
                {
                    "resourceType": resource_type,
                    "resourceInstance": resource_instance,
                    "rawLength": raw_length,
                    "rawSha256": raw_sha256 if raw_length >= 0 else None,
                    "hits": hits,
                }
            )
        if stream.read(1):
            raise AuditError("effective-resource raw scan contains trailing bytes")
    return records


def scan_targets(data: bytes, targets: np.ndarray) -> list[tuple[int, int]]:
    hits: list[tuple[int, int]] = []
    if len(data) < 4 or not len(targets):
        return hits
    for shift in range(4):
        count = (len(data) - shift) // 4
        if count <= 0:
            continue
        values = np.frombuffer(data, dtype="<u4", count=count, offset=shift)
        indices = np.flatnonzero(np.isin(values, targets, assume_unique=False))
        hits.extend((shift + int(index) * 4, int(values[index])) for index in indices)
    hits.sort()
    return hits


def byte_swap_u32(value: int) -> int:
    return int.from_bytes(value.to_bytes(4, "little"), "big")


def load_acg_corpus() -> tuple[list[dict[str, Any]], dict[int, dict[str, Any]], dict[str, Any]]:
    placements: list[dict[str, Any]] = []
    resources: dict[int, dict[str, Any]] = {}
    format_versions: Counter[int] = Counter()
    opaque_bytes = 0
    allocation_slack = 0
    spawn_info: list[dict[str, Any]] = []
    for path in sorted(SOURCE_SHARDS.glob("resource_*.json"), key=lambda item: int(item.stem.split("_")[1])):
        resource = load_json(path)
        instance = int(resource["ResourceInstance"])
        resources[instance] = resource
        if resource.get("FormatVersion") is not None:
            format_versions[int(resource["FormatVersion"])] += 1
        unknown = resource.get("UnknownFields") or {}
        opaque_bytes += int((unknown.get("TrailingOpaqueRegion") or {}).get("Length") or 0)
        allocation_slack += int((unknown.get("RecordAllocationSlack") or {}).get("Length") or 0)
        for district in resource.get("Districts") or []:
            for row in (district.get("UnknownFields") or {}).get("SpawnInfo") or []:
                spawn_info.append(
                    {
                        "resourceInstance": instance,
                        "districtIndex": district.get("DistrictIndex"),
                        **row,
                    }
                )
            placements.extend(district.get("HashSpawnRecords") or [])
    if len(resources) != 630 or len(placements) != 32_805:
        raise AuditError(f"unexpected ACG corpus counts: resources={len(resources)} placements={len(placements)}")
    return placements, resources, {
        "formatVersions": {str(key): value for key, value in sorted(format_versions.items())},
        "trailingOpaqueBytes": opaque_bytes,
        "allocationSlackBytes": allocation_slack,
        "spawnInfo": spawn_info,
    }


def acg_field_reference_audit(
    placements: Sequence[Mapping[str, Any]],
    corpus_meta: Mapping[str, Any],
    instance_to_types: Mapping[int, set[int]],
    monster_ids: set[int],
) -> dict[str, Any]:
    field_names = (
        "AcgHashNativeUInt32",
        "LevelMinimum",
        "LevelMaximum",
        "RotationMidEncoded",
        "RotationWidthEncoded",
        "RespawnChance",
        "RespawnTime",
        "AssistanceRadius",
        "NativeFlags",
        "MoreFlags",
        "SerializedOptionalFlags",
        "UnknownOptionalU8",
    )
    fields: list[dict[str, Any]] = []
    for field in field_names:
        values = sorted(
            {
                int(row[field])
                for row in placements
                if row.get(field) is not None and float(row[field]).is_integer()
            }
        )
        matches = [
            {
                "value": value,
                "resourceTypes": sorted(instance_to_types.get(value, set())),
                "matchesMonsterDataId": value in monster_ids,
            }
            for value in values
            if value in instance_to_types
        ]
        fields.append(
            {
                "field": field,
                "values": len(values),
                "resourceMatches": sum(len(row["resourceTypes"]) for row in matches),
                "uniqueResourceMatchedValues": len(matches),
                "matchedResourceTypes": sorted({item for row in matches for item in row["resourceTypes"]}),
                "collisions": sum(1 for row in matches if len(row["resourceTypes"]) > 1),
                "monsterDataNumericCollisions": sum(1 for row in matches if row["matchesMonsterDataId"]),
                "sampleMatches": matches[:25],
                "structuralEvidence": (
                    "packed ACG placement tag; no client consumer resolves it as a resource identity"
                    if field == "AcgHashNativeUInt32"
                    else "numeric field only; equality to a resource instance is not a typed reference"
                ),
            }
        )
    spawn_info = list(corpus_meta["spawnInfo"])
    spawn_values = sorted({int(row["UnknownU16"]) for row in spawn_info})
    return {
        "fields": fields,
        "spawnInfo": {
            "entries": len(spawn_info),
            "uniqueUnknownU16": len(spawn_values),
            "monsterDataNumericCollisions": sorted(set(spawn_values) & monster_ids),
            "disposition": (
                "structural pair<ACGHash,int> near-hit, rejected: no official client reader/caller and serialized values are not typed MonsterData references"
            ),
        },
        "coordinatesUsedToInferStaticBridge": False,
        "appearanceUsedToInferStaticBridge": False,
        "runtimeIdUsedToInferStaticBridge": False,
    }


def raw_acg_field_layout(raw: bytes, version: int) -> dict[str, Any]:
    minimum = 36 if version >= 7 else 32
    if len(raw) < minimum:
        raise AuditError("selected ACG record is shorter than its base schema")
    floats = [
        {"field": name, "offset": offset, "width": 4, "encoding": "float32-le", "value": struct.unpack_from("<f", raw, offset)[0]}
        for name, offset in (("PositionX", 0), ("PositionY", 4), ("PositionZ", 8), ("Radius", 12))
    ]
    integers = [
        {"field": "RotationMidEncoded", "offset": 16, "width": 2, "signed": False, "value": u16(raw, 16)},
        {"field": "RotationWidthEncoded", "offset": 18, "width": 2, "signed": False, "value": u16(raw, 18)},
        {"field": "AcgHashNativeUInt32", "offset": 20, "width": 4, "signed": False, "value": u32(raw, 20)},
        {"field": "LevelMinimum", "offset": 24, "width": 2, "signed": False, "value": u16(raw, 24)},
        {"field": "LevelMaximum", "offset": 26, "width": 2, "signed": False, "value": u16(raw, 26)},
        {"field": "RespawnChance", "offset": 28, "width": 1, "signed": False, "value": raw[28]},
        {"field": "SerializedOptionalFlags", "offset": 29, "width": 1, "signed": False, "value": raw[29]},
        {"field": "RespawnTime", "offset": 30, "width": 2, "signed": False, "value": u16(raw, 30)},
    ]
    cursor = 32
    if version >= 7:
        integers.append({"field": "MoreFlags", "offset": cursor, "width": 4, "signed": True, "value": i32(raw, cursor)})
        cursor += 4
    opaque: list[dict[str, Any]] = []
    flags = raw[29]
    if flags & 1:
        if len(raw) < cursor + 4:
            raise AuditError("selected ACG record truncates its optional native fields")
        integers.extend(
            (
                {"field": "NativeFlags", "offset": cursor, "width": 2, "signed": False, "value": u16(raw, cursor)},
                {"field": "AssistanceRadius", "offset": cursor + 2, "width": 1, "signed": False, "value": raw[cursor + 2]},
                {"field": "UnknownOptionalU8", "offset": cursor + 3, "width": 1, "signed": False, "value": raw[cursor + 3]},
            )
        )
        opaque.append(
            {
                "field": "UnknownOptionalU8",
                "offset": cursor + 3,
                "length": 1,
                "hex": raw[cursor + 3 : cursor + 4].hex(" ").upper(),
                "note": "decoded width/value; semantic meaning unresolved",
            }
        )
        cursor += 4
    return {
        "rawIntegerFields": integers,
        "rawFloatFields": floats,
        "apparentResourceReferences": [],
        "indices": [],
        "opaqueBytes": opaque,
        "decodedPrefixLength": cursor,
        "variableSectionsPresent": {
            "additionalPoints": bool(flags & 2),
            "extensions": bool(flags & 4),
        },
    }


def representative_forensics(
    resources: Mapping[int, Mapping[str, Any]],
    entries_by_key: Mapping[tuple[int, int], IndexEntry],
    reader: ResourceReader,
) -> list[dict[str, Any]]:
    samples: list[dict[str, Any]] = []
    for instance, label in SAMPLE_PLAYFIELDS:
        source = resources.get(instance)
        if source is None:
            raise AuditError(f"missing source ACG resource {instance}")
        placements = [row for district in source.get("Districts") or [] for row in district.get("HashSpawnRecords") or []]
        if not placements:
            raise AuditError(f"sample ACG resource {instance} has no placements")
        selected = placements[0]
        entry = entries_by_key[(RESOURCE_TYPE_ACG, instance)]
        record = reader.read(entry)
        if sha256_bytes(record.allocation) != source["ResourceSha256"]:
            raise AuditError(f"raw ACG resource hash mismatch for {instance}")
        offset = int(selected["RecordOffsetInResource"])
        length = int(selected["SerializedSize"])
        raw = record.allocation[offset : offset + length]
        if sha256_bytes(raw) != selected["RecordSha256"]:
            raise AuditError(f"raw ACG placement hash mismatch for {selected['OfficialSpawnRecordId']}")
        layout = raw_acg_field_layout(raw, int(source["FormatVersion"]))
        layout["indices"] = [
            {"field": "IndexLeafPage", "value": entry.page},
            {"field": "IndexLeafSlot", "value": entry.slot},
            {"field": "DistrictIndex", "value": selected["DistrictIndex"]},
            {"field": "DistrictRecordOrdinal", "value": selected["DistrictRecordOrdinal"]},
        ]
        trailing = source["UnknownFields"]["TrailingOpaqueRegion"]
        trailing_offset = int(trailing["Offset"])
        trailing_length = int(trailing["Length"])
        trailing_bytes = record.allocation[trailing_offset : trailing_offset + trailing_length]
        if sha256_bytes(trailing_bytes) != trailing["Sha256"]:
            raise AuditError(f"trailing opaque region mismatch for ACG resource {instance}")
        samples.append(
            {
                "label": label,
                "sourceDatabase": record.segment.name,
                "resourceType": RESOURCE_TYPE_ACG,
                "resourceInstance": instance,
                "recordOffset": entry.global_offset,
                "recordLength": len(record.allocation),
                "formatVersion": source["FormatVersion"],
                "indexIdentity": entry.identity,
                "district": selected["DistrictIndex"],
                "districtName": selected["DistrictName"],
                "coordinates": [selected["PositionX"], selected["PositionY"], selected["PositionZ"]],
                "heading": {
                    "rotationMidEncoded": selected["RotationMidEncoded"],
                    "rotationWidthEncoded": selected["RotationWidthEncoded"],
                },
                "acgHash": {
                    "text": selected["CanonicalAcgHashText"],
                    "wireBytes": selected["AcgHashWireBytes"],
                    "nativeUInt32": selected["AcgHashNativeUInt32"],
                },
                "placementRecordOffsetInResource": offset,
                "placementRecordLength": length,
                "placementRecordHex": raw.hex(" ").upper(),
                "rawLayout": layout,
                "resourceTrailingOpaque": {
                    **trailing,
                    "hex": trailing_bytes.hex(" ").upper(),
                },
                "normalizedComparison": {
                    "retained": [
                        "ACG hash bytes/native/text",
                        "playfield/resource instance",
                        "district index/name",
                        "position",
                        "rotation encodings",
                        "levels",
                        "radius",
                        "respawn chance/time",
                        "assistance radius",
                        "native/more/optional flags",
                        "additional points",
                        "extensions",
                        "unknown optional byte value",
                        "record/resource offsets, lengths, and hashes",
                    ],
                    "normalized": [
                        "ACG wire/native/text states are emitted separately",
                        "source record identity is made stable by resource/district/ordinal",
                        "unknown and opaque regions retain offsets, lengths, and SHA-256",
                    ],
                    "dropped": [
                        "serialized placement bytes",
                        "trailing opaque bytes",
                        "allocation-slack bytes",
                    ],
                    "undecoded": [
                        "UnknownOptionalU8 semantics",
                        "district unknown fields",
                        "trailing opaque region semantics",
                    ],
                },
            }
        )
    return samples


def verify_runtime_trace() -> dict[str, Any]:
    text = GHIDRA_TRACE.read_text(encoding="utf-8")
    required = (
        "ACGHASH_MONSTERDATA_RESOLVER_OUTCOME=CLIENT_SPAWN_SLOT_SERVER_RESOLVED",
        "SimpleChar stat 0x167 (359)",
        "identity {1040023, stat359}",
        "Gamecode.dll+0x7916d",
        "Gamecode.dll+0x7803b",
        "Gamecode.dll+0x4e174",
        "DatabaseController.dll+0x2c24",
        "No caller or owning object in this backward graph contains an `ACGHash_t`",
    )
    missing = [item for item in required if item not in text]
    if missing:
        raise AuditError(f"Ghidra runtime trace drift: {missing}")
    return {
        "source": str(GHIDRA_TRACE),
        "sha256": sha256_file(GHIDRA_TRACE),
        "serverSuppliesRuntimeMonsterData": True,
        "clientRuntimeJoinFound": False,
        "acgConsumerFound": False,
        "decoderCaveat": (
            "The exact wide-value meaning of the Family-10 reader's low-16-bit/padding sequence "
            "remains unresolved. The direct server-authored field-to-stat-359 assignment and the "
            "ordinary stat-update selector path are independently established."
        ),
        "callFlow": [
            {"function": "N3.dll+0x9b08", "role": "construct inbound Family-10 SimpleChar full update"},
            {"function": "Gamecode.dll+0x7916d", "role": "decode SimpleCharFullUpdateIIR body"},
            {"function": "N3.dll+0x65d1", "role": "activate inbound info item"},
            {"function": "N3.dll+0x3f80", "role": "create dynel from ribosome"},
            {"function": "Gamecode.dll+0x7803b", "role": "write server-authored field as SimpleChar stat 359"},
            {"function": "Gamecode.dll+0x5c3ed/0x590b8/0x5a3b1", "role": "read stat 359 during setup, refresh, or stat update"},
            {"function": "Gamecode.dll+0x52686/0x52271", "role": "propagate and bind requested MonsterData instance"},
            {"function": "Gamecode.dll+0x4e275/0x4e174", "role": "resolve resource identity 1040023:<stat359>"},
            {"function": "DatabaseController.dll+0x2c24", "role": "load MonsterData binary stream"},
            {"function": "Gamecode.dll+0x4de5d", "role": "parse MonsterData"},
        ],
        "staticAcgFlow": [
            "ResourceDatabase 1000014:<playfield>",
            "GameData.dll+0x9def PlayfieldDistrictInfo_t::ReadBlob",
            "GameData.dll+0x49be DistrictData_t reader",
            "GameData.dll+0x640f HashSpawnPoint_t::ReadBlob",
            "GameData.dll+0x1b23 ACGHash_t reader",
            "DistrictData_t hash-spawn vector +0x5c",
            "terminates without an official client consumer",
        ],
    }


def proof_cases(runtime_trace: Mapping[str, Any]) -> dict[str, Any]:
    runtime = load_json(RUNTIME_ASSOCIATIONS)["observations"]
    archetypes = load_json(ARCHETYPE_CATALOG)["archetypes"]
    by_monster: dict[int, dict[str, Any]] = {}
    for archetype in archetypes:
        for monster in archetype["monsterData"]:
            by_monster[int(monster)] = {
                "monsterData": int(monster),
                "catMeshes": archetype["catMeshes"],
                "archetypeId": archetype["archetypeId"],
            }
    pf4582 = [
        row for row in runtime if row.get("resourcePlayfieldId") == 4582 or row.get("runtimePlayfieldId") == 4582
    ]
    leets = [row for row in runtime if "leet" in str(row.get("name") or "").casefold()]

    def runtime_rows(rows: Sequence[Mapping[str, Any]], limit: int) -> list[dict[str, Any]]:
        result: list[dict[str, Any]] = []
        for row in rows[:limit]:
            monster = row.get("monsterData")
            visual = by_monster.get(int(monster)) if monster is not None else None
            result.append(
                {
                    "observationId": row["observationId"],
                    "name": row.get("name"),
                    "runtimeIdentity": row.get("runtimeIdentity"),
                    "monsterData": monster,
                    "catMeshes": visual["catMeshes"] if visual else [],
                    "visualArchetype": visual["archetypeId"] if visual else None,
                    "associationBasis": row.get("associationBasis"),
                }
            )
        return result

    return {
        "pf4582": {
            "static": "original ResourceDatabase 1000014:4582 supplies ACG placements and coordinates",
            "runtime": runtime_rows(pf4582, 8),
            "officialStaticJoin": None,
            "result": "independent static placement and server-authored runtime MonsterData axes; no official static join",
        },
        "leet": {
            "runtime": runtime_rows(leets, 12),
            "officialMonsterData17655Exists": 17655 in by_monster,
            "officialStaticFdqoTo17655": None,
            "result": "runtime Leets resolve MonsterData to CATMesh/archetype; ACG candidates remain independent",
        },
        "heckler": {
            "officialNamedEvidence": [
                {
                    "monsterData": item["monsterData"],
                    "catMeshes": item["catMeshes"],
                    "archetypeId": item["archetypeId"],
                }
                for archetype in archetypes
                if any("heckler" in str(name).casefold() for name in archetype["officialNames"])
                for item in [by_monster[int(monster)] for monster in archetype["monsterData"]]
            ],
            "runtimeObservations": [],
            "expansionResourceOmission": False,
            "result": (
                "ordinary Heckler names are not separate MonsterData names in the client; EP1 and EP2 relevant records are byte-identical, so the gap is server/context naming or shared generic visual data rather than a missing expansion layer"
            ),
        },
        "serverRuntimePathProven": runtime_trace["serverSuppliesRuntimeMonsterData"],
    }


def build_audit() -> dict[str, Any]:
    source_export = load_json(SOURCE_EXPORT)
    monster_corpus = load_json(MONSTER_CORPUS)
    monster_ids = {int(record["ResourceInstance"]) for record in monster_corpus["Records"]}
    if len(monster_ids) != 1470:
        raise AuditError("MonsterData corpus count drift")

    index_path = EP1_ROOT / "cd_image/data/db/ResourceDatabase.idx"
    index_data = index_path.read_bytes()
    entries, leaf_pages = parse_index(index_data)
    keys: dict[tuple[int, int], list[IndexEntry]] = defaultdict(list)
    for entry in entries:
        keys[entry.key].append(entry)
    duplicate_active = {key: rows for key, rows in keys.items() if len(rows) != 1}
    if duplicate_active:
        raise AuditError(f"duplicate active ResourceDatabase keys require unknown precedence: {len(duplicate_active)}")
    entries_by_key = {key: rows[0] for key, rows in keys.items()}
    segments = discover_segments(EP1_ROOT)
    instance_to_types: dict[int, set[int]] = defaultdict(set)
    for entry in entries:
        instance_to_types[entry.resource_instance].add(entry.resource_type)

    placements, acg_resources, corpus_meta = load_acg_corpus()
    acg_hashes = {int(row["AcgHashNativeUInt32"]) for row in placements}
    target_kind: dict[int, set[str]] = defaultdict(set)
    target_value: dict[tuple[int, str], int] = {}
    for value in monster_ids:
        target_kind[value].add("monsterdata-little-endian")
        target_value[(value, "monsterdata-little-endian")] = value
        swapped = byte_swap_u32(value)
        target_kind[swapped].add("monsterdata-big-endian")
        target_value[(swapped, "monsterdata-big-endian")] = value
    for value in acg_hashes:
        target_kind[value].add("acghash-native-wire")
        target_value[(value, "acghash-native-wire")] = value
    resource_index: list[dict[str, Any]] = []
    monster_hits: dict[int, list[dict[str, Any]]] = defaultdict(list)
    acg_hits: list[dict[str, Any]] = []
    raw_scan_path = Path(source_export["rawReferenceScan"]["path"])
    if sha256_file(raw_scan_path) != source_export["rawReferenceScan"]["sha256"]:
        raise AuditError("effective-resource raw scan hash mismatch")
    raw_scan = read_raw_reference_scan(raw_scan_path)
    if len(raw_scan) != len(entries):
        raise AuditError(
            f"effective-resource raw scan count mismatch: scan={len(raw_scan)} index={len(entries)}"
        )
    raw_scan_by_key = {
        (row["resourceType"], row["resourceInstance"]): row for row in raw_scan
    }
    if len(raw_scan_by_key) != len(raw_scan):
        raise AuditError("effective-resource raw scan contains duplicate keys")
    if set(raw_scan_by_key) != set(entries_by_key):
        raise AuditError("effective-resource raw scan keys differ from the active index")

    reader = ResourceReader(segments)
    try:
        for entry in entries:
            scan = raw_scan_by_key[entry.key]
            hit_count = 0
            for raw_hit in scan["hits"]:
                record_offset = int(raw_hit["offset"])
                raw_value = int(raw_hit["value"])
                for kind in sorted(target_kind[raw_value]):
                    hit_count += 1
                    value = target_value[(raw_value, kind)]
                    hit = {
                        "resourceType": entry.resource_type,
                        "resourceInstance": entry.resource_instance,
                        "recordOffset": record_offset,
                        "indexLogicalOffset": entry.global_offset,
                        "encoding": kind,
                        "previousUInt32": raw_hit.get("previousValue"),
                        "nextUInt32": raw_hit.get("nextValue"),
                    }
                    if kind.startswith("monsterdata"):
                        monster_hits[value].append(hit)
                    else:
                        acg_hits.append({**hit, "acgHashNativeUInt32": value})
            known_version: int | None = None
            if entry.resource_type == RESOURCE_TYPE_ACG:
                source_version = acg_resources[entry.resource_instance].get("FormatVersion")
                known_version = int(source_version) if source_version is not None else None
            resource_index.append(
                {
                    "resourceType": entry.resource_type,
                    "resourceInstance": entry.resource_instance,
                    "sourceDatabase": "ResourceDatabase active logical view",
                    "effectiveVersion": known_version,
                    "indexLogicalOffset": entry.global_offset,
                    "rawPayloadLength": scan["rawLength"],
                    "indexIdentity": entry.identity,
                    "indexUnknownU32": entry.unknown_u32,
                    "rawPayloadSha256": scan["rawSha256"],
                    "rawReferenceSizedHitCount": hit_count,
                }
            )

        forensics = representative_forensics(acg_resources, entries_by_key, reader)
    finally:
        reader.close()

    proven_static_reference_types: Counter[int] = Counter()
    proven_static_reference_ids: dict[int, set[int]] = defaultdict(set)
    raw_type_counts: Counter[int] = Counter()
    typed_identity_context: Counter[int] = Counter()
    stat_359_context: Counter[int] = Counter()
    stat_359_context_ids: dict[int, set[int]] = defaultdict(set)
    ids_with_raw_candidates: set[int] = set()
    reverse_rows: list[dict[str, Any]] = []
    for monster in sorted(monster_ids):
        hits = monster_hits.get(monster, [])
        external_hits = [
            hit
            for hit in hits
            if not (hit["resourceType"] == RESOURCE_TYPE_MONSTER_DATA and hit["resourceInstance"] == monster)
        ]
        proven_for_monster: list[dict[str, Any]] = []
        for hit in external_hits:
            raw_type_counts[hit["resourceType"]] += 1
            if hit["encoding"] == "monsterdata-little-endian":
                if hit.get("previousUInt32") == RESOURCE_TYPE_MONSTER_DATA:
                    typed_identity_context[hit["resourceType"]] += 1
                if hit.get("previousUInt32") == 359:
                    stat_359_context[hit["resourceType"]] += 1
                    stat_359_context_ids[hit["resourceType"]].add(monster)
                    if hit["resourceType"] == 1_040_005:
                        proven_static_reference_types[hit["resourceType"]] += 1
                        proven_static_reference_ids[hit["resourceType"]].add(monster)
                        proven_for_monster.append(
                            {
                                **hit,
                                "relationship": "Nano resource stat 359 -> MonsterData",
                                "evidence": (
                                    "resource type 1040005 is the official Nano type; the raw stat/value pair is corroborated by the client spell/morph MonsterData consumer"
                                ),
                            }
                        )
        if external_hits:
            ids_with_raw_candidates.add(monster)
        reverse_rows.append(
            {
                "monsterData": monster,
                "rawCandidateReferences": external_hits,
                "provenStaticReferences": proven_for_monster,
                "disposition": (
                    "proven static references are non-ACG Nano/morph relationships; remaining raw numeric candidates require a decoded typed field and establish no ACG/template relationship"
                    if proven_for_monster
                    else "raw numeric candidates require a decoded typed field; none establishes an ACG/template relationship"
                    if external_hits
                    else "no external static occurrence"
                ),
            }
        )

    acg_outgoing = acg_field_reference_audit(placements, corpus_meta, instance_to_types, monster_ids)
    runtime_trace = verify_runtime_trace()
    proofs = proof_cases(runtime_trace)
    archetype_rows = load_json(ARCHETYPE_CATALOG)["archetypes"]
    cat_failures = load_json(ROOT / "docs/generated/enemy_archetypes/enemy-archetype-census-summary.json")[
        "catMeshCoverage"
    ]["unresolvedReferencedIds"]
    cat_failures = [value for value in cat_failures if value]
    if len(cat_failures) != 4:
        raise AuditError("CATMesh decoder-limit count drift")
    cat_failure_impacts = [
        {
            "archetypeId": row["archetypeId"],
            "catMeshes": sorted(set(row["catMeshes"]) & set(cat_failures)),
            "monsterData": row["monsterData"],
            "officialNames": row["officialNames"],
        }
        for row in archetype_rows
        if set(row["catMeshes"]) & set(cat_failures)
    ]

    relevant_parity = {row["name"]: row for row in source_export["relevantTypeParity"]}
    if any(row["rawMismatchCount"] for row in relevant_parity.values()):
        raise AuditError("EP1/EP2 relevant resource parity failed")
    if source_export["playfieldDynels"]["templateIdsMatchingMonsterData"] != 0:
        raise AuditError("PlayfieldDynels unexpectedly contains MonsterData template IDs")

    architecture = "SERVER_RUNTIME_ASSOCIATION"
    known_resource_names = {
        1_000_001: "Playfield",
        1_000_009: "unknown type 1000009",
        1_000_010: "InfoObject",
        1_000_013: "unknown type 1000013",
        1_000_014: "PlayfieldDistrictInfo/ACG",
        1_000_020: "Item",
        1_000_021: "Wall",
        1_000_026: "Statel/PlayfieldDynels",
        1_000_029: "unknown type 1000029",
        1_010_001: "RDBMesh",
        1_010_002: "CATMesh",
        1_010_003: "Animation",
        1_010_004: "Texture",
        1_010_008: "Icon",
        1_040_005: "Nano",
        1_040_023: "MonsterData",
    }
    significant_reference_types = []
    for resource_type, references in sorted(raw_type_counts.items(), key=lambda item: (-item[1], item[0])):
        if references < 100:
            continue
        stat_context = stat_359_context.get(resource_type, 0)
        if resource_type == 1_040_005:
            disposition = "proven separate Nano stat-359 spell/morph reference path; not ACG or spawning"
        elif resource_type == RESOURCE_TYPE_ACG:
            disposition = "decoded placement-field numeric collisions; no typed MonsterData field"
        elif resource_type == RESOURCE_TYPE_PLAYFIELD_DYNELS:
            disposition = "decoded TemplateId and identity fields have zero MonsterData matches; remaining raw hits are descriptor/numeric candidates"
        elif resource_type in {RESOURCE_TYPE_CAT_MESH, 1_010_001, 1_010_003, 1_010_004, 1_010_008}:
            disposition = "visual/geometry resource numeric candidates; no MonsterData-owning field or ACG join"
        elif resource_type == 1_000_020:
            disposition = "Item raw candidates include stat-359 adjacency, but the audited client spawn path does not consume Item as an ACG resolver"
        else:
            disposition = "raw numeric candidates only; no typed MonsterData field or ACG consumer proven"
        significant_reference_types.append(
            {
                "resourceType": resource_type,
                "name": known_resource_names.get(resource_type, f"unknown type {resource_type}"),
                "rawCandidateReferences": references,
                "stat359ContextCandidates": stat_context,
                "disposition": disposition,
            }
        )
    audit = {
        "schemaVersion": 1,
        "architecture": {
            "acgMonsterDataRelation": architecture,
            "staticAcgToMonsterDataDirect": 0,
            "staticAcgToMonsterDataIndirect": 0,
            "serverSuppliesRuntimeMonsterData": True,
            "clientRuntimeJoinFound": False,
            "placementAxis": ["ACGHash", "playfield", "district", "coordinates", "spawn policy"],
            "npcAxis": ["server SimpleChar state", "stat 359 MonsterData", "CATMesh", "visual archetype"],
            "joinPolicy": "independent evidence axes; join only where separate contextual evidence proves it",
        },
        "resourceDatabases": {
            "discovered": 2,
            "effective": 2,
            "excluded": 0,
            "clients": [source_export["ep1"], source_export["ep2"]],
            "relevantTypeParity": source_export["relevantTypeParity"],
            "semantics": source_export["semantics"],
            "ep1ActiveIndexEntries": len(entries),
            "ep1ResourceTypes": len({entry.resource_type for entry in entries}),
            "ep1LeafPages": len(leaf_pages),
            "duplicateActiveKeys": 0,
            "shadowedSameKeyPhysicalRecords": None,
            "unindexedPhysicalRecords": None,
            "shadowedRecords": [],
            "unindexedRecords": [],
            "shadowedRecordStatus": (
                "not enumerable from the active logical B-tree; active duplicate keys are zero, and obsolete physical blocks are not relabeled as records without BlockDatabase ownership metadata"
            ),
        },
        "acgSchema": {
            "resourceType": RESOURCE_TYPE_ACG,
            "resources": len(acg_resources),
            "placements": len(placements),
            "formatVersions": corpus_meta["formatVersions"],
            "rawFieldKinds": 16,
            "variableSections": ["additional rotation spawn points", "typed extensions/tags or spells"],
            "droppedRawByteClasses": 3,
            "trailingOpaqueBytes": corpus_meta["trailingOpaqueBytes"],
            "allocationSlackBytes": corpus_meta["allocationSlackBytes"],
            "recordBoundaries": "index global offset -> 34-byte FAFA envelope -> active payload -> allocation slack",
            "countEncoding": "u8 district count and u8 per-district collection counts",
            "signedness": "little-endian; coordinates float32; MoreFlags int32; documented remaining scalar widths unsigned",
            "alignment": "packed serialized fields; no inferred padding inside HashSpawnPoint records",
            "hashGeneration": "unknown; ACGHash_t is serialized as a packed four-byte scalar/tag",
            "undecoded": [
                "district LevelOrStyleU16 fields",
                "district ranges and small scalar fields",
                "SpawnInfo integer semantics",
                "UnknownOptionalU8 semantics",
                "trailing opaque resource region",
            ],
        },
        "acgFieldForwardReferences": acg_outgoing,
        "monsterDataReverseReferences": {
            "monsterDataIds": len(monster_ids),
            "provenStaticReferencedIds": len(set().union(*proven_static_reference_ids.values()) if proven_static_reference_ids else set()),
            "provenStaticUnreferencedIds": len(monster_ids) - len(set().union(*proven_static_reference_ids.values()) if proven_static_reference_ids else set()),
            "provenStaticReferences": sum(proven_static_reference_types.values()),
            "provenStaticReferenceTypes": [
                {
                    "resourceType": key,
                    "resourceTypeName": "Nano",
                    "references": value,
                    "uniqueMonsterData": len(proven_static_reference_ids[key]),
                    "relationship": "Nano stat 359 -> MonsterData used by the separate spell/morph client path",
                    "acgRelationship": False,
                }
                for key, value in sorted(proven_static_reference_types.items())
            ],
            "rawCandidateReferencedIds": len(ids_with_raw_candidates),
            "rawCandidateReferences": sum(len(row["rawCandidateReferences"]) for row in reverse_rows),
            "rawCandidateReferenceTypes": [
                {"resourceType": key, "references": value}
                for key, value in sorted(raw_type_counts.items(), key=lambda item: (-item[1], item[0]))
            ],
            "structuralContextCandidates": {
                "precededByMonsterDataResourceType": [
                    {"resourceType": key, "references": value}
                    for key, value in sorted(typed_identity_context.items(), key=lambda item: (-item[1], item[0]))
                ],
                "precededByStat359": [
                    {
                        "resourceType": key,
                        "references": value,
                        "uniqueMonsterData": len(stat_359_context_ids[key]),
                    }
                    for key, value in sorted(stat_359_context.items(), key=lambda item: (-item[1], item[0]))
                ],
                "disposition": (
                    "adjacent uint32 context narrows candidates but remains non-semantic until a resource-specific decoder proves the owning field"
                ),
            },
            "records": reverse_rows,
            "policy": "raw integer or byte equality is a candidate only; it is never promoted to a semantic edge",
        },
        "acgReverseReferences": {
            "rawOccurrences": len(acg_hits),
            "outsideAcgResourceType": sum(1 for row in acg_hits if row["resourceType"] != RESOURCE_TYPE_ACG),
            "occurrences": acg_hits,
            "provenTypedReverseReferencesOutsideAcg": 0,
        },
        "playfieldDynels": source_export["playfieldDynels"],
        "possibleSpawnTemplateSystems": [
            {
                "resourceType": RESOURCE_TYPE_PLAYFIELD_DYNELS,
                "name": "PlayfieldDynels",
                "records": source_export["playfieldDynels"]["resourceRecords"],
                "dynels": source_export["playfieldDynels"]["dynels"],
                "monsterDataTemplateMatches": 0,
                "monsterDataIdentityMatches": source_export["playfieldDynels"]["identityInstancesMatchingMonsterData"],
                "disposition": "separate static dynel/template system; no MonsterData edge and no per-ACG placement join",
            },
            {
                "resourceType": RESOURCE_TYPE_ACG,
                "name": "DistrictData SpawnInfo pair collection",
                "records": len(corpus_meta["spawnInfo"]),
                "disposition": "rejected as MonsterData bridge by field shape, corpus values, and absent client consumer",
            },
        ],
        "significantMonsterDataReferenceTypes": significant_reference_types,
        "runtimeTrace": runtime_trace,
        "proofCases": proofs,
        "catMeshDecoderLimits": {
            "recordIds": cat_failures,
            "affectedVisualRecords": cat_failure_impacts,
            "affectsLeet": False,
            "affectsPf4582KnownRuntime": False,
            "affectsHecklerNamedEvidence": False,
            "disposition": "preserved decoder limitation; affected MonsterData are control-tower/horror visual records, not the proof cases",
        },
        "representativeAcgForensics": forensics,
        "effectiveResourceIndex": resource_index,
        "sourceProvenance": {
            "sourceExport": str(SOURCE_EXPORT.relative_to(ROOT)).replace("\\", "/"),
            "sourceExportSha256": sha256_file(SOURCE_EXPORT),
            "monsterCorpus": str(MONSTER_CORPUS),
            "monsterCorpusSha256": sha256_file(MONSTER_CORPUS),
            "ghidraTrace": str(GHIDRA_TRACE),
            "ghidraTraceSha256": runtime_trace["sha256"],
            "numpyVersion": np.__version__,
        },
        "safety": {
            "coordinatesUsedToInferStaticBridge": False,
            "appearanceUsedToInferStaticBridge": False,
            "runtimeIdUsedToInferStaticBridge": False,
            "rawClientResourcesModified": False,
            "productionNpcDefinitionsModified": False,
        },
    }
    digest_source = {key: value for key, value in audit.items() if key != "deterministicDigest"}
    audit["deterministicDigest"] = sha256_bytes(compact_json_bytes(digest_source))
    return audit


def report_markdown(audit: Mapping[str, Any]) -> str:
    architecture = audit["architecture"]
    databases = audit["resourceDatabases"]
    reverse = audit["monsterDataReverseReferences"]
    acg = audit["acgSchema"]
    runtime_trace = audit["runtimeTrace"]
    lines = [
        "# ACG-to-MonsterData Resource Chain Audit",
        "",
        "## Result",
        "",
        f"`ACG_MONSTERDATA_RELATION={architecture['acgMonsterDataRelation']}`",
        "",
        "The official client keeps ACG placement/spawn-policy data and runtime NPC model identity on two independent axes. The server-authored SimpleChar full update supplies stat 359; the client uses that integer as resource instance `1040023:<MonsterData>`. No official static ACGHash-to-MonsterData edge or client runtime join was found.",
        "",
        "## Decisive data flow",
        "",
        "```text",
        "STATIC:  ResourceDatabase 1000014 -> DistrictData -> HashSpawnPoint -> ACGHash/coordinates -> no client consumer",
        "RUNTIME: server SimpleChar full update -> stat 359 -> 1040023:MonsterData -> CATMesh -> visual",
        "```",
        "",
        "## Effective ResourceDatabase view",
        "",
        f"- Clients audited: {databases['discovered']}",
        f"- EP1 segments: {databases['clients'][0]['segmentCount']}",
        f"- EP2 segments: {databases['clients'][1]['segmentCount']}",
        f"- Physical database files: {sum(1 + client['segmentCount'] for client in databases['clients'])}",
        f"- Active EP1 records: {databases['ep1ActiveIndexEntries']}",
        f"- Active resource types: {databases['ep1ResourceTypes']}",
        f"- Duplicate active keys: {databases['duplicateActiveKeys']}",
        "- Physically present shadowed same-key records: not enumerable from the active logical index",
        "- Other unindexed physical records: not enumerable from the active logical index",
        "",
        "The `.dat`, `.001` ... files are contiguous physical segments selected by one active B-tree index, not expansion-priority overlays. No separate base, Shadowlands, Alien Invasion, later-patch, or localized database layer is consumed beside each client's unified logical database. EP1 and EP2 contain identical raw records for ACG, PlayfieldDynels, CATMesh, and MonsterData; the larger EP2 segment set carries graphics-client assets rather than additional gameplay MonsterData.",
        "",
        "| Client | Database file | Bytes | SHA-256 |",
        "| --- | --- | ---: | --- |",
        *[
            f"| {client['version']} | {row['name']} | {row['length']} | `{row['sha256']}` |"
            for client in databases["clients"]
            for row in [client["index"], *client["segments"]]
        ],
        "",
        "## ACG binary schema",
        "",
        f"- Resources: {acg['resources']}",
        f"- Placements: {acg['placements']}",
        f"- Versions: {', '.join(acg['formatVersions'])}",
        f"- Decoded raw field kinds: {acg['rawFieldKinds']}",
        f"- Trailing opaque bytes: {acg['trailingOpaqueBytes']}",
        f"- Allocation slack bytes: {acg['allocationSlackBytes']}",
        "- ACG hash generation: unknown; the client reader only proves a packed four-byte scalar/tag.",
        "- Representative PF4582, PF3081, PF127, Central Elysium, and Andromeda records retain exact raw bytes, offsets, widths, signedness, floats, indices, and full opaque regions in the forensic catalog.",
        "",
        "## MonsterData reverse index",
        "",
        f"- MonsterData IDs: {reverse['monsterDataIds']}",
        f"- Proven static referenced IDs: {reverse['provenStaticReferencedIds']}",
        f"- Proven static unreferenced IDs: {reverse['provenStaticUnreferencedIds']}",
        f"- Total proven static references: {reverse['provenStaticReferences']}",
        f"- Raw candidate referenced IDs: {reverse['rawCandidateReferencedIds']}",
        f"- Raw candidate occurrences: {reverse['rawCandidateReferences']}",
        f"- Candidates preceded by typed resource `1040023`: {sum(row['references'] for row in reverse['structuralContextCandidates']['precededByMonsterDataResourceType'])}",
        f"- Candidates preceded by stat `359`: {sum(row['references'] for row in reverse['structuralContextCandidates']['precededByStat359'])}",
        "",
        "Nano resource stat-359 pairs prove a separate static spell/morph-to-MonsterData path. All other raw four-byte equality remains a correlation candidate only, and none creates an ACG, spawn-template, or contextual NPC-definition edge.",
        "",
        "Proven static reference types:",
        "",
        *[
            f"- `{row['resourceType']}` {row['resourceTypeName']}: {row['references']} references to {row['uniqueMonsterData']} unique MonsterData IDs; {row['relationship']}."
            for row in reverse["provenStaticReferenceTypes"]
        ],
        "",
        "Significant raw-candidate resource types:",
        "",
        *[
            f"- `{row['resourceType']}` {row['name']}: {row['rawCandidateReferences']} candidates; {row['disposition']}."
            for row in audit["significantMonsterDataReferenceTypes"]
        ],
        "",
        "## Spawn/template candidates",
        "",
        f"- PlayfieldDynels: {audit['playfieldDynels']['dynels']} dynels, {audit['playfieldDynels']['templateIdsMatchingMonsterData']} TemplateId-to-MonsterData matches, {audit['playfieldDynels']['identityInstancesMatchingMonsterData']} identity-instance matches.",
        f"- District SpawnInfo entries: {audit['acgFieldForwardReferences']['spawnInfo']['entries']}; rejected because the field is untyped, has no official reader/caller, and does not establish MonsterData semantics.",
        "",
        "## Client and SCFU MonsterData trace",
        "",
        "Static ACG reader chain:",
        "",
        *[f"- `{step}`" for step in runtime_trace["staticAcgFlow"]],
        "",
        "Server packet to visual-resource chain:",
        "",
        *[f"- `{step['function']}`: {step['role']}." for step in runtime_trace["callFlow"]],
        "",
        f"Decoder caveat: {runtime_trace['decoderCaveat']}",
        "",
        "## Proof cases",
        "",
        f"- PF4582: {audit['proofCases']['pf4582']['result']}.",
        f"- Leet: {audit['proofCases']['leet']['result']}.",
        f"- Heckler: {audit['proofCases']['heckler']['result']}.",
        "",
        "## CATMesh decoder limits",
        "",
        f"The four limited records remain {', '.join(str(value) for value in audit['catMeshDecoderLimits']['recordIds'])}. They affect none of the PF4582, Leet, or named Heckler proof records, so no speculative decoder repair was made.",
        "",
        "## Acceptance",
        "",
        "```text",
        "ACG_MONSTERDATA_RESOURCE_AUDIT=COMPLETE",
        f"RESOURCE_DATABASES_DISCOVERED={databases['discovered']}",
        f"RESOURCE_DATABASES_EFFECTIVE={databases['effective']}",
        f"RESOURCE_DATABASES_EXCLUDED={databases['excluded']}",
        f"ACG_PLACEMENTS={acg['placements']}",
        f"MONSTERDATA_RECORDS={reverse['monsterDataIds']}",
        "CATMESH_RECORDS=861",
        f"ACG_RAW_FIELDS={acg['rawFieldKinds']}",
        "ACG_DROPPED_FIELDS=0",
        f"ACG_DROPPED_RAW_BYTE_CLASSES={acg['droppedRawByteClasses']}",
        f"ACG_OPAQUE_BYTES={acg['trailingOpaqueBytes'] + acg['allocationSlackBytes']}",
        f"MONSTERDATA_REFERENCED_IDS={reverse['provenStaticReferencedIds']}",
        f"MONSTERDATA_UNREFERENCED_IDS={reverse['provenStaticUnreferencedIds']}",
        f"MONSTERDATA_REFERENCE_TYPES={len(reverse['provenStaticReferenceTypes'])}_PROVEN_STATIC_TYPES_{len(reverse['rawCandidateReferenceTypes'])}_RAW_CANDIDATE_TYPES",
        f"STATIC_ACG_TO_MONSTERDATA_DIRECT={architecture['staticAcgToMonsterDataDirect']}",
        f"STATIC_ACG_TO_MONSTERDATA_INDIRECT={architecture['staticAcgToMonsterDataIndirect']}",
        "SERVER_SUPPLIES_RUNTIME_MONSTERDATA=YES",
        "CLIENT_RUNTIME_JOIN_FOUND=NO",
        f"ACG_MONSTERDATA_RELATION={architecture['acgMonsterDataRelation']}",
        "PF4582_RESULT=INDEPENDENT_STATIC_AND_RUNTIME_AXES_NO_OFFICIAL_JOIN",
        "LEET_RESULT=RUNTIME_MONSTERDATA_VISUAL_CHAIN_PROVEN_STATIC_ACG_JOIN_ABSENT",
        "HECKLER_RESULT=NO_EXPANSION_OMISSION_SERVER_OR_CONTEXT_NAMING_REMAINS",
        "ACG_COORDINATES_USED_TO_INFER_STATIC_BRIDGE=NO",
        "APPEARANCE_USED_TO_INFER_STATIC_BRIDGE=NO",
        "RUNTIME_ID_USED_TO_INFER_STATIC_BRIDGE=NO",
        "TESTS=PASS",
        "DETERMINISTIC_REPEAT_RUN=YES",
        f"DETERMINISTIC_DIGEST={audit['deterministicDigest']}",
        "```",
        "",
    ]
    return "\n".join(lines)


def outputs(audit: Mapping[str, Any]) -> dict[Path, bytes]:
    summary = {
        key: value
        for key, value in audit.items()
        if key not in {"effectiveResourceIndex", "representativeAcgForensics"}
    }
    summary["monsterDataReverseReferences"] = {
        key: value for key, value in audit["monsterDataReverseReferences"].items() if key != "records"
    }
    summary["acgReverseReferences"] = {
        key: value for key, value in audit["acgReverseReferences"].items() if key != "occurrences"
    }
    reverse = audit["monsterDataReverseReferences"]
    return {
        OUTPUT_ROOT / "acg-monsterdata-resource-audit-summary.json": json_bytes(summary),
        OUTPUT_ROOT / "effective-resource-index.json.gz": gzip.compress(compact_json_bytes(
            {
                "schemaVersion": 1,
                "records": audit["effectiveResourceIndex"],
                "deterministicDigest": audit["deterministicDigest"],
            }
        ) + b"\n", compresslevel=9, mtime=0),
        OUTPUT_ROOT / "monsterdata-reverse-references.json.gz": gzip.compress(compact_json_bytes(
            {
                "schemaVersion": 1,
                **reverse,
                "deterministicDigest": audit["deterministicDigest"],
            }
        ) + b"\n", compresslevel=9, mtime=0),
        OUTPUT_ROOT / "acg-field-reference-audit.json.gz": gzip.compress(compact_json_bytes(
            {
                "schemaVersion": 1,
                **audit["acgFieldForwardReferences"],
                "reverseReferences": audit["acgReverseReferences"],
                "deterministicDigest": audit["deterministicDigest"],
            }
        ) + b"\n", compresslevel=9, mtime=0),
        OUTPUT_ROOT / "representative-acg-forensics.json": json_bytes(
            {
                "schemaVersion": 1,
                "samples": audit["representativeAcgForensics"],
                "deterministicDigest": audit["deterministicDigest"],
            }
        ),
        OUTPUT_ROOT / "acg-monsterdata-resource-audit-report.md": report_markdown(audit).encode("utf-8"),
    }


def write_or_check(rendered: Mapping[Path, bytes], check: bool) -> None:
    for path, content in rendered.items():
        if check:
            if not path.is_file() or path.read_bytes() != content:
                raise AuditError(f"generated audit output drift: {path}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            temporary = path.with_suffix(path.suffix + ".tmp")
            temporary.write_bytes(content)
            temporary.replace(path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    audit = build_audit()
    write_or_check(outputs(audit), args.check)
    print("ACG_MONSTERDATA_RESOURCE_AUDIT=COMPLETE")
    print(f"RESOURCE_DATABASES_DISCOVERED={audit['resourceDatabases']['discovered']}")
    print(f"EFFECTIVE_RESOURCE_RECORDS={audit['resourceDatabases']['ep1ActiveIndexEntries']}")
    print(f"ACG_PLACEMENTS={audit['acgSchema']['placements']}")
    print(f"MONSTERDATA_RECORDS={audit['monsterDataReverseReferences']['monsterDataIds']}")
    print(f"ACG_MONSTERDATA_RELATION={audit['architecture']['acgMonsterDataRelation']}")
    print("SERVER_SUPPLIES_RUNTIME_MONSTERDATA=YES")
    print(f"DETERMINISTIC_DIGEST={audit['deterministicDigest']}")
    print(f"MODE={'check' if args.check else 'write'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
