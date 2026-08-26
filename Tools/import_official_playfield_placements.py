#!/usr/bin/env python3
"""Verify and import the official database-wide static playfield placement corpus."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Mapping


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE_ROOT = Path(r"C:\Users\Mike\Documents\AO stripdown")

SOURCE_CLIENT_VARIANT = "EP1_OLD_GRAPHICS_CLIENT"
SOURCE_CLIENT_BUILD = "18.8.62_EP1"
RESOURCE_TYPE = 1000014
SCHEMA_VERSION = 1
SHARD_SCHEMA_VERSION = 2
CORPUS_VERSION = "18.8.62_EP1-static-placement-v2"

EXPECTED_RESOURCE_COUNT = 630
EXPECTED_PARSED_RESOURCE_COUNT = 627
EXPECTED_MALFORMED_RESOURCE_COUNT = 3
EXPECTED_DISTRICT_COUNT = 4146
EXPECTED_RECORD_COUNT = 32805
EXPECTED_UNIQUE_ACGHASH_COUNT = 4016
EXPECTED_SOURCE_SHARD_BYTES = 86019468
EXPECTED_DUPLICATE_POSITION_RECORDS = 7395
EXPECTED_DUPLICATE_POSITION_GROUPS = 2869
EXPECTED_EXACT_DUPLICATE_RECORDS = 2552
EXPECTED_EXACT_DUPLICATE_GROUPS = 1095
EXPECTED_CROSS_DISTRICT_DUPLICATE_GROUPS = 1085
EXPECTED_MALFORMED_PLAYFIELDS = (103, 615, 4805)

EXPECTED_DATABASE_SHA256 = {
    "ResourceDatabase.dat": "3cabdede7b9b2468ed22f10f536fb2f7083ea05ed9483e2d96b22cf080d736a6",
    "ResourceDatabase.dat.001": "f8884a2c382ce7c95f20b4423567f176ed40675ba9ce8362527288712871ba73",
    "ResourceDatabase.dat.002": "2024021f966c3c8a8c083e01cbad2335ba33c19a1661a148060391755a608cc1",
    "ResourceDatabase.idx": "ba152f59096d5358f4d1b6511d3a3d264999e0a59f1ab7bf3a7cc18a4888c273",
}

SOURCE_INPUT_MANIFEST_RELATIVE = PurePosixPath(
    "Docs/reference/playfield_district_info/official_input_manifest.json"
)
EXPECTED_SOURCE_INPUT_MANIFEST_SHA256 = (
    "871a364d181e998ad40417a50941dd9de413b52e9b43a182a2665b47392d374a"
)
SOURCE_GENERATED_ROOT = PurePosixPath("Docs/generated/playfield_district_info")
SOURCE_SHARD_ROOT = SOURCE_GENERATED_ROOT / SOURCE_CLIENT_BUILD

EXPECTED_GLOBAL_ARTIFACTS = {
    "ResourceInventory": (
        SOURCE_GENERATED_ROOT / "playfield_district_resource_inventory.json",
        "b61dc4d1c9a44493f33181d38cf2cd2bfa78bc37f969843d5dbb29fb5ddfd579",
    ),
    "CorpusSummary": (
        SOURCE_GENERATED_ROOT / "playfield_district_corpus_summary.json",
        "c7c746093cbfbbbbaf7d9066e69b8e83d7bb3390a9abe3a7f3798cd4259069d2",
    ),
    "ImportIndex": (
        SOURCE_GENERATED_ROOT / "playfield_district_import_index.json",
        "d6bd940a108c948fe6bc95b7e47d9aef41fe6e7e78608a5710ac9eda226d92a7",
    ),
    "AcgHashInventory": (
        SOURCE_GENERATED_ROOT / "acghash_global_inventory.json",
        "7875578ff0074d14f829ab0de9070f025219574cf2794fe4161bf13e46b03734",
    ),
    "FormatInventory": (
        SOURCE_GENERATED_ROOT / "format_version_inventory.json",
        "f95a3862f71e336693664cf640d10ca4b5571a6cddd9d9dbd4aa9d6700792ab3",
    ),
    "CoverageReport": (
        SOURCE_GENERATED_ROOT / "extraction_coverage_report.json",
        "6a9428330c1008111092ddc528276f6ebf0752db5c032e8af3f1288cf1a93abf",
    ),
}

PF4582_ARTIFACTS = {
    "OfficialOverlay": (
        PurePosixPath("docs/generated/pf4582_official_placement_overlay.json"),
        "10830619c5cc995937ceb64310266b90e6a87d94929a4cdc15137e6616561f30",
    ),
    "AuthoritativeReport": (
        PurePosixPath("docs/generated/pf4582_authoritative_placement_report.json"),
        "05bf246e47e31205f99aa3a46cc4af5f3d525d3fec2a9014a5fa2712e7954d86",
    ),
    "RuntimeEvidenceMap": (
        PurePosixPath("docs/reference/pf4582/runtime-evidence-map.json"),
        "02a1b167b97d1caa223aeaa60eaebbaf0a1e99ce6ddce753f7d54eae1f716869",
    ),
    "AcceptedPlacementSource": (
        PurePosixPath("docs/reference/pf4582/PlayfieldDistrictInfo.json"),
        "b747aea145cb36e3f9be5b2cacc7aaebca3d24017a14540ac1f29f4bd1296b32",
    ),
    "OfficialCatalogGenerated": (
        PurePosixPath(
            "AORebirth/Server/ZoneEngine/Core/Playfields/"
            "IccShuttleportOfficialPlacementCatalog.g.cs"
        ),
        "57cf172124f95463ff9e78c11f3e85f9fbc86a5de2d8235470b6c4e54d881164",
    ),
    "OfficialCatalogModel": (
        PurePosixPath(
            "AORebirth/Server/ZoneEngine/Core/Playfields/"
            "IccShuttleportOfficialPlacementCatalog.cs"
        ),
        "2070784a9d894b7b52f77e4639bc4602e3f5b166deefa2a8ed755c3015ebf0b5",
    ),
}

OUTPUT_SOURCE_MANIFEST = PurePosixPath(
    "docs/reference/playfields/official-placement-source-manifest.json"
)
OUTPUT_ROOT = PurePosixPath("docs/generated/playfields")
OUTPUT_PLACEMENT_ROOT = OUTPUT_ROOT / "placements"
OUTPUT_INDEX = OUTPUT_ROOT / "official-placement-index.json"
OUTPUT_SUMMARY = OUTPUT_ROOT / "official-placement-summary.json"
OUTPUT_ACGHASH = OUTPUT_ROOT / "official-acghash-inventory.json"
OUTPUT_CORPUS_MANIFEST = OUTPUT_ROOT / "official-placement-corpus-manifest.json"

OFFICIAL_ID_PATTERN = re.compile(
    r"^18\.8\.62_EP1:1000014:(?P<playfield>\d+):district-(?P<district>\d+):record-(?P<record>\d+)$"
)
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
WIRE_BYTES_PATTERN = re.compile(r"^[0-9A-F]{2}(?: [0-9A-F]{2}){3}$")


class PlacementImportError(ValueError):
    """The governed source or generated cohort failed a closed validation."""


@dataclass(frozen=True)
class SourceCorpus:
    source_root: Path
    artifacts: Mapping[str, Any]
    artifact_hashes: Mapping[str, str]
    resources: tuple[Mapping[str, Any], ...]
    source_records: tuple[Mapping[str, Any], ...]
    source_shard_bytes: int


@dataclass(frozen=True)
class ImportModel:
    source_manifest: Mapping[str, Any]
    placement_shards: Mapping[int, Mapping[str, Any]]
    index: Mapping[str, Any]
    summary: Mapping[str, Any]
    acghash_inventory: Mapping[str, Any]
    corpus_manifest: Mapping[str, Any]
    source_shard_bytes: int
    normalized_shard_bytes: int


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise PlacementImportError(message)


def _path(root: Path, relative: PurePosixPath) -> Path:
    return root.joinpath(*relative.parts)


def _sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _load_json(path: Path) -> Any:
    _require(path.is_file(), f"required input is missing: {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise PlacementImportError(f"cannot read governed JSON {path}: {exc}") from exc


def _json_bytes(value: Mapping[str, Any], *, compact: bool) -> bytes:
    try:
        if compact:
            text = json.dumps(
                value,
                ensure_ascii=False,
                allow_nan=False,
                sort_keys=True,
                separators=(",", ":"),
            )
        else:
            text = json.dumps(
                value,
                ensure_ascii=False,
                allow_nan=False,
                sort_keys=True,
                indent=2,
            )
    except (TypeError, ValueError) as exc:
        raise PlacementImportError(f"generated model is not canonical JSON: {exc}") from exc
    return (text + "\n").encode("utf-8")


def _require_int(value: Any, label: str, *, nullable: bool = False) -> None:
    if nullable and value is None:
        return
    _require(type(value) is int, f"{label} must be an integer")


def _require_number(value: Any, label: str, *, nullable: bool = False) -> None:
    if nullable and value is None:
        return
    _require(type(value) in (int, float), f"{label} must be numeric")


def _require_text(value: Any, label: str, *, nullable: bool = False) -> None:
    if nullable and value is None:
        return
    _require(type(value) is str, f"{label} must be text")


def _validate_input_manifest(manifest: Any) -> None:
    _require(isinstance(manifest, dict), "official input manifest must be an object")
    _require(manifest.get("SchemaVersion") == 1, "official input manifest schema drift")
    builds = manifest.get("Builds")
    _require(isinstance(builds, list) and len(builds) == 1, "official input manifest build count drift")
    build = builds[0]
    _require(build.get("LogicalBuildId") == SOURCE_CLIENT_BUILD, "official source build drift")
    _require(build.get("VersionId") == SOURCE_CLIENT_BUILD, "official source version drift")
    _require(build.get("ExpansionOrDataSet") == "EP1", "official source provenance label drift")
    segments = build.get("DataSegments")
    _require(isinstance(segments, list) and len(segments) == 3, "database segment manifest drift")
    observed = {
        Path(item["RepoRelativePath"]).name: item["Sha256"]
        for item in segments
    }
    index = build.get("Index")
    _require(isinstance(index, dict), "database index manifest is missing")
    observed[Path(index["RepoRelativePath"]).name] = index["Sha256"]
    _require(observed == EXPECTED_DATABASE_SHA256, "official database provenance hashes drifted")
    _require(index.get("ExpectedResourceRecordCount") == 460193, "official index record count drift")
    _require(
        manifest.get("ClientBuildsNotPresent") == [{"BuildFamily": "EP2", "Status": "NOT_PRESENT"}],
        "official input EP2 availability boundary drifted",
    )


def _validate_summary(summary: Any) -> None:
    _require(isinstance(summary, dict), "source corpus summary must be an object")
    metrics = summary.get("Metrics")
    _require(isinstance(metrics, dict), "source corpus metrics are missing")
    expected = {
        "TYPE_1000014_RESOURCES_DISCOVERED": EXPECTED_RESOURCE_COUNT,
        "TYPE_1000014_UNIQUE_RESOURCE_INSTANCES": EXPECTED_RESOURCE_COUNT,
        "TYPE_1000014_RESOURCES_PARSED": EXPECTED_PARSED_RESOURCE_COUNT,
        "TYPE_1000014_RESOURCES_MALFORMED": EXPECTED_MALFORMED_RESOURCE_COUNT,
        "OFFICIAL_DISTRICTS_TOTAL": EXPECTED_DISTRICT_COUNT,
        "OFFICIAL_HASH_SPAWN_RECORDS_TOTAL": EXPECTED_RECORD_COUNT,
        "OFFICIAL_UNIQUE_ACGHASH_TAGS": EXPECTED_UNIQUE_ACGHASH_COUNT,
        "VALIDATED_INSTANCE_CONTROLS": EXPECTED_RESOURCE_COUNT,
        "VALIDATED_INSTANCE_MATCHES": EXPECTED_RESOURCE_COUNT,
        "VALIDATED_INSTANCE_CONFLICTS": 0,
        "OFFICIAL_DUPLICATE_POSITION_RECORDS": EXPECTED_DUPLICATE_POSITION_RECORDS,
        "OFFICIAL_DUPLICATE_POSITION_GROUPS": EXPECTED_DUPLICATE_POSITION_GROUPS,
        "OFFICIAL_EXACT_DUPLICATE_RECORDS": EXPECTED_EXACT_DUPLICATE_RECORDS,
        "OFFICIAL_EXACT_DUPLICATE_GROUPS": EXPECTED_EXACT_DUPLICATE_GROUPS,
        "OFFICIAL_CROSS_DISTRICT_DUPLICATE_GROUPS": EXPECTED_CROSS_DISTRICT_DUPLICATE_GROUPS,
    }
    for key, value in expected.items():
        _require(metrics.get(key) == value, f"source corpus metric drift: {key}")
    _require(
        metrics.get("RESOURCE_INSTANCE_PLAYFIELD_RELATIONSHIP_STATUS")
        == "PROVEN_FOR_ALL_VALIDATED_CONTROLS",
        "ResourceInstance/playfield relationship is not governed",
    )
    regression = summary.get("PF4582Regression")
    _require(isinstance(regression, dict), "PF4582 source regression is missing")
    _require(regression.get("PF4582_REGRESSION_PASS") is True, "PF4582 source regression failed")
    _require(regression.get("PF4582_REGRESSION_RECORDS") == 207, "PF4582 source count drift")


def _validate_source_record(
    record: Mapping[str, Any],
    *,
    shard: Mapping[str, Any],
    district: Mapping[str, Any],
) -> None:
    required = {
        "AcgHashFieldOffsetInResource",
        "AcgHashNativeUInt32",
        "AcgHashNativeUInt32Hex",
        "AcgHashWireBytes",
        "AdditionalPoints",
        "AssistanceRadius",
        "CanonicalAcgHashText",
        "DistrictIndex",
        "DistrictName",
        "DistrictRecordOrdinal",
        "Extensions",
        "FieldPresence",
        "LevelMaximum",
        "LevelMinimum",
        "MoreFlags",
        "NativeFlags",
        "OfficialResourceId",
        "OfficialSpawnRecordId",
        "ParserVersion",
        "PositionX",
        "PositionY",
        "PositionZ",
        "Radius",
        "RecordOffsetInDatabase",
        "RecordOffsetInResource",
        "RecordSha256",
        "RespawnChance",
        "RespawnTime",
        "RotationMidEncoded",
        "RotationWidthEncoded",
        "SerializedOptionalFlags",
        "SerializedSize",
        "UnknownFields",
        "UnknownOptionalU8",
    }
    _require(required <= set(record), f"source record schema drift: {record.get('OfficialSpawnRecordId')}")
    identity = record["OfficialSpawnRecordId"]
    _require_text(identity, "OfficialSpawnRecordId")
    match = OFFICIAL_ID_PATTERN.fullmatch(identity)
    _require(match is not None, f"invalid OfficialSpawnRecordId: {identity}")
    _require(int(match.group("playfield")) == shard["ResourceInstance"], f"playfield identity mismatch: {identity}")
    _require(int(match.group("district")) == district["DistrictIndex"], f"district identity mismatch: {identity}")
    _require(int(match.group("record")) == record["DistrictRecordOrdinal"], f"record identity mismatch: {identity}")
    _require(record["DistrictIndex"] == district["DistrictIndex"], f"record district mismatch: {identity}")
    _require(record["DistrictName"] == district["DistrictName"], f"record district name mismatch: {identity}")
    _require(record["OfficialResourceId"] == shard["OfficialResourceId"], f"resource identity mismatch: {identity}")

    canonical = record["CanonicalAcgHashText"]
    wire = record["AcgHashWireBytes"]
    native = record["AcgHashNativeUInt32"]
    _require_text(canonical, f"{identity}.CanonicalAcgHashText")
    _require(len(canonical) == 4, f"canonical ACGHash width drift: {identity}")
    _require_text(wire, f"{identity}.AcgHashWireBytes")
    _require(WIRE_BYTES_PATTERN.fullmatch(wire) is not None, f"wire ACGHash shape drift: {identity}")
    _require_int(native, f"{identity}.AcgHashNativeUInt32")
    _require(0 <= native <= 0xFFFFFFFF, f"native ACGHash is outside uint32: {identity}")
    wire_bytes = bytes.fromhex(wire)
    _require(wire_bytes[::-1].decode("latin-1") == canonical, f"ACGHash text/wire mismatch: {identity}")
    _require(int.from_bytes(wire_bytes, "little") == native, f"ACGHash wire/native mismatch: {identity}")
    _require(record["AcgHashNativeUInt32Hex"] == f"0x{native:08X}", f"ACGHash native hex mismatch: {identity}")

    for key in (
        "DistrictIndex",
        "DistrictRecordOrdinal",
        "AcgHashFieldOffsetInResource",
        "RecordOffsetInDatabase",
        "RecordOffsetInResource",
        "SerializedSize",
    ):
        _require_int(record[key], f"{identity}.{key}")
    for key in ("PositionX", "PositionY", "PositionZ", "Radius", "RespawnTime"):
        _require_number(record[key], f"{identity}.{key}")
    for key in (
        "LevelMinimum",
        "LevelMaximum",
        "RotationMidEncoded",
        "RotationWidthEncoded",
        "RespawnChance",
        "AssistanceRadius",
        "NativeFlags",
        "MoreFlags",
        "SerializedOptionalFlags",
        "UnknownOptionalU8",
    ):
        _require_int(record[key], f"{identity}.{key}", nullable=True)
    _require(isinstance(record["FieldPresence"], dict), f"FieldPresence must be an object: {identity}")
    _require(isinstance(record["UnknownFields"], dict), f"UnknownFields must be an object: {identity}")
    _require(isinstance(record["AdditionalPoints"], list), f"AdditionalPoints must be a list: {identity}")
    _require(isinstance(record["Extensions"], list), f"Extensions must be a list: {identity}")


def load_source_corpus(source_root: Path) -> SourceCorpus:
    source_root = source_root.expanduser().resolve()
    _require(source_root.is_dir(), f"AO Stripdown source root is missing: {source_root}")
    _require(source_root != REPOSITORY_ROOT.resolve(), "AORebirth cannot be used as its own official extraction source")

    artifacts: dict[str, Any] = {}
    artifact_hashes: dict[str, str] = {}
    input_manifest_path = _path(source_root, SOURCE_INPUT_MANIFEST_RELATIVE)
    observed_manifest_hash = _sha256_file(input_manifest_path)
    _require(
        observed_manifest_hash == EXPECTED_SOURCE_INPUT_MANIFEST_SHA256,
        "official input manifest SHA-256 mismatch",
    )
    artifact_hashes["OfficialInputManifest"] = observed_manifest_hash
    artifacts["OfficialInputManifest"] = _load_json(input_manifest_path)
    _validate_input_manifest(artifacts["OfficialInputManifest"])

    for role, (relative, expected_hash) in EXPECTED_GLOBAL_ARTIFACTS.items():
        path = _path(source_root, relative)
        observed_hash = _sha256_file(path)
        _require(observed_hash == expected_hash, f"{role} SHA-256 mismatch")
        artifact_hashes[role] = observed_hash
        artifacts[role] = _load_json(path)

    _validate_summary(artifacts["CorpusSummary"])
    index = artifacts["ImportIndex"]
    inventory = artifacts["ResourceInventory"]
    _require(isinstance(index, dict) and index.get("SchemaVersion") == 1, "source import index schema drift")
    _require(isinstance(inventory, dict) and inventory.get("SchemaVersion") == 1, "source resource inventory schema drift")
    index_rows = index.get("Resources")
    inventory_rows = inventory.get("Resources")
    _require(isinstance(index_rows, list) and len(index_rows) == EXPECTED_RESOURCE_COUNT, "source import index count drift")
    _require(isinstance(inventory_rows, list) and len(inventory_rows) == EXPECTED_RESOURCE_COUNT, "source resource inventory count drift")

    inventory_by_id = {row.get("OfficialResourceId"): row for row in inventory_rows}
    _require(len(inventory_by_id) == EXPECTED_RESOURCE_COUNT, "source resource inventory identities are not unique")
    source_shard_dir = _path(source_root, SOURCE_SHARD_ROOT)
    actual_shards = sorted(source_shard_dir.glob("resource_*.json"))
    _require(len(actual_shards) == EXPECTED_RESOURCE_COUNT, "source shard file count drift")
    actual_shard_relatives = {
        path.relative_to(source_root).as_posix() for path in actual_shards
    }
    source_shard_bytes = sum(path.stat().st_size for path in actual_shards)
    _require(source_shard_bytes == EXPECTED_SOURCE_SHARD_BYTES, "source shard byte count drift")

    resources: list[Mapping[str, Any]] = []
    records: list[Mapping[str, Any]] = []
    indexed_paths: set[str] = set()
    resource_instances: set[int] = set()
    official_resource_ids: set[str] = set()
    official_record_ids: set[str] = set()
    parse_statuses: Counter[str] = Counter()
    district_count = 0
    record_count = 0

    for index_row in index_rows:
        _require(isinstance(index_row, dict), "source import index row must be an object")
        relative_text = index_row.get("GeneratedArtifactPath")
        _require_text(relative_text, "source GeneratedArtifactPath")
        relative = PurePosixPath(relative_text)
        _require(not relative.is_absolute() and ".." not in relative.parts, "source shard path escapes source root")
        _require(relative.parts[: len(SOURCE_SHARD_ROOT.parts)] == SOURCE_SHARD_ROOT.parts, "source shard path is outside governed shard directory")
        indexed_paths.add(relative.as_posix())
        shard_path = _path(source_root, relative)
        expected_shard_hash = index_row.get("GeneratedArtifactSha256")
        _require_text(expected_shard_hash, "source shard SHA-256")
        _require(SHA256_PATTERN.fullmatch(expected_shard_hash) is not None, "source shard SHA-256 shape drift")
        _require(_sha256_file(shard_path) == expected_shard_hash, f"source shard SHA-256 mismatch: {relative}")
        shard = _load_json(shard_path)
        _require(isinstance(shard, dict) and shard.get("SchemaVersion") == 1, f"source shard schema drift: {relative}")

        shared = (
            "BuildId",
            "DatabaseSha256",
            "DistrictCount",
            "FormatVersion",
            "HashSpawnRecordCount",
            "OfficialResourceId",
            "ParseStatus",
        )
        for key in shared:
            _require(shard.get(key) == index_row.get(key), f"index/shard mismatch {relative}: {key}")
        official_resource_id = shard.get("OfficialResourceId")
        inventory_row = inventory_by_id.get(official_resource_id)
        _require(isinstance(inventory_row, dict), f"source inventory row missing: {official_resource_id}")
        for key in (
            "BuildId",
            "DistrictCount",
            "FormatVersion",
            "GeneratedArtifactPath",
            "GeneratedArtifactSha256",
            "HashSpawnRecordCount",
            "OfficialResourceId",
            "ParseStatus",
        ):
            _require(inventory_row.get(key) == index_row.get(key), f"index/inventory mismatch {official_resource_id}: {key}")

        _require(shard.get("BuildId") == SOURCE_CLIENT_BUILD, f"source build drift: {official_resource_id}")
        _require(shard.get("DatabaseSha256") == EXPECTED_DATABASE_SHA256["ResourceDatabase.dat"], f"database fingerprint drift: {official_resource_id}")
        _require(shard.get("ResourceType") == RESOURCE_TYPE, f"resource type drift: {official_resource_id}")
        resource_instance = shard.get("ResourceInstance")
        _require_int(resource_instance, f"{official_resource_id}.ResourceInstance")
        _require(inventory_row.get("ResourceInstance") == resource_instance, f"inventory/shard instance mismatch: {official_resource_id}")
        _require(official_resource_id == f"{SOURCE_CLIENT_BUILD}:{RESOURCE_TYPE}:{resource_instance}", f"official resource identity drift: {official_resource_id}")
        _require(resource_instance not in resource_instances, f"duplicate ResourceInstance: {resource_instance}")
        _require(official_resource_id not in official_resource_ids, f"duplicate OfficialResourceId: {official_resource_id}")
        resource_instances.add(resource_instance)
        official_resource_ids.add(official_resource_id)

        parse_status = shard.get("ParseStatus")
        _require(parse_status in {"PARSED_SUPPORTED", "PARSED_EMPTY", "MALFORMED_RESOURCE"}, f"unsupported source parse status: {official_resource_id}")
        parse_statuses[parse_status] += 1
        districts = shard.get("Districts")
        _require(isinstance(districts, list), f"source districts must be a list: {official_resource_id}")
        _require(shard.get("DistrictCount") == len(districts), f"source district count mismatch: {official_resource_id}")
        shard_record_count = 0
        for expected_district_index, district in enumerate(districts):
            _require(isinstance(district, dict), f"source district must be an object: {official_resource_id}")
            _require(district.get("DistrictIndex") == expected_district_index, f"source district ordering drift: {official_resource_id}")
            district_records = district.get("HashSpawnRecords")
            _require(isinstance(district_records, list), f"source district records must be a list: {official_resource_id}")
            _require(district.get("HashSpawnRecordCount") == len(district_records), f"source district record count mismatch: {official_resource_id}")
            for expected_ordinal, record in enumerate(district_records):
                _require(isinstance(record, dict), f"source record must be an object: {official_resource_id}")
                _require(record.get("DistrictRecordOrdinal") == expected_ordinal, f"source record ordering drift: {official_resource_id}")
                _validate_source_record(record, shard=shard, district=district)
                record_id = record["OfficialSpawnRecordId"]
                _require(record_id not in official_record_ids, f"duplicate OfficialSpawnRecordId: {record_id}")
                official_record_ids.add(record_id)
                records.append(record)
            shard_record_count += len(district_records)
        _require(shard.get("HashSpawnRecordCount") == shard_record_count, f"source shard record count mismatch: {official_resource_id}")
        district_count += len(districts)
        record_count += shard_record_count
        resources.append({"Index": index_row, "Inventory": inventory_row, "Shard": shard})

    _require(indexed_paths == actual_shard_relatives, "source index/shard path set mismatch")
    _require(len(resource_instances) == EXPECTED_RESOURCE_COUNT, "source ResourceInstance count drift")
    _require(parse_statuses["PARSED_SUPPORTED"] == 622, "supported parsed resource count drift")
    _require(parse_statuses["PARSED_EMPTY"] == 5, "empty parsed resource count drift")
    _require(parse_statuses["MALFORMED_RESOURCE"] == EXPECTED_MALFORMED_RESOURCE_COUNT, "malformed resource count drift")
    _require(district_count == EXPECTED_DISTRICT_COUNT, "source district count drift")
    _require(record_count == EXPECTED_RECORD_COUNT, "source record count drift")
    _require(len(official_record_ids) == EXPECTED_RECORD_COUNT, "source stable record identities are not unique")

    tag_records: dict[str, list[Mapping[str, Any]]] = defaultdict(list)
    for record in records:
        tag_records[record["CanonicalAcgHashText"]].append(record)
    _require(len(tag_records) == EXPECTED_UNIQUE_ACGHASH_COUNT, "source unique ACGHash count drift")
    source_tag_rows = artifacts["AcgHashInventory"].get("Tags")
    _require(isinstance(source_tag_rows, list) and len(source_tag_rows) == EXPECTED_UNIQUE_ACGHASH_COUNT, "source ACGHash inventory count drift")
    source_tags = {row.get("CanonicalAcgHashText"): row for row in source_tag_rows}
    _require(len(source_tags) == EXPECTED_UNIQUE_ACGHASH_COUNT, "source ACGHash inventory identities are not unique")
    _require(set(source_tags) == set(tag_records), "source ACGHash inventory tag set mismatch")
    for tag, tag_source_records in tag_records.items():
        inventory_tag = source_tags[tag]
        expected_ids = sorted(record["OfficialSpawnRecordId"] for record in tag_source_records)
        _require(inventory_tag.get("PlacementCount") == len(expected_ids), f"source ACGHash placement count mismatch: {tag!r}")
        _require(sorted(inventory_tag.get("AllOfficialRecordIds", [])) == expected_ids, f"source ACGHash record identities mismatch: {tag!r}")

    malformed = tuple(
        sorted(
            item["Shard"]["ResourceInstance"]
            for item in resources
            if item["Shard"]["ParseStatus"] == "MALFORMED_RESOURCE"
        )
    )
    _require(malformed == EXPECTED_MALFORMED_PLAYFIELDS, "malformed resource identity set drift")

    resources.sort(key=lambda item: item["Shard"]["ResourceInstance"])
    return SourceCorpus(
        source_root=source_root,
        artifacts=artifacts,
        artifact_hashes=artifact_hashes,
        resources=tuple(resources),
        source_records=tuple(records),
        source_shard_bytes=source_shard_bytes,
    )


def _load_pf4582_artifacts(repo_root: Path) -> tuple[dict[str, Any], dict[str, str]]:
    loaded: dict[str, Any] = {}
    hashes: dict[str, str] = {}
    for role, (relative, expected_hash) in PF4582_ARTIFACTS.items():
        path = _path(repo_root, relative)
        observed_hash = _sha256_file(path)
        _require(observed_hash == expected_hash, f"PF4582 {role} SHA-256 mismatch")
        hashes[role] = observed_hash
        if path.suffix.lower() == ".json":
            loaded[role] = _load_json(path)
    return loaded, hashes


def _compare_pf4582_record(source: Mapping[str, Any], overlay: Mapping[str, Any]) -> None:
    identity = source["OfficialSpawnRecordId"]
    _require(overlay.get("OfficialRecordIdentity") == identity, f"PF4582 stable identity mismatch: {identity}")
    direct_pairs = {
        "OfficialDistrictIndex": "DistrictIndex",
        "OfficialDistrictName": "DistrictName",
        "OfficialRecordOrdinal": "DistrictRecordOrdinal",
        "CanonicalAcgHashText": "CanonicalAcgHashText",
        "OfficialWireBytes": "AcgHashWireBytes",
        "OfficialNativeUInt32": "AcgHashNativeUInt32",
    }
    for overlay_key, source_key in direct_pairs.items():
        _require(overlay.get(overlay_key) == source.get(source_key), f"PF4582 full-record mismatch {identity}: {source_key}")
    fields = overlay.get("OfficialFields")
    _require(isinstance(fields, dict), f"PF4582 overlay fields missing: {identity}")
    field_pairs = {
        "database_offset": "RecordOffsetInDatabase",
        "record_relative_offset": "RecordOffsetInResource",
        "acghash_field_record_relative_offset": "AcgHashFieldOffsetInResource",
        "min_level": "LevelMinimum",
        "max_level": "LevelMaximum",
        "assistance_radius": "AssistanceRadius",
        "native_flags": "NativeFlags",
        "more_flags": "MoreFlags",
        "serialized_optional_flags": "SerializedOptionalFlags",
        "serialized_size": "SerializedSize",
        "respawn_chance": "RespawnChance",
        "respawn_time": "RespawnTime",
        "unknown_optional_u8": "UnknownOptionalU8",
        "spawn_index": "DistrictRecordOrdinal",
    }
    for overlay_key, source_key in field_pairs.items():
        _require(fields.get(overlay_key) == source.get(source_key), f"PF4582 full-record mismatch {identity}: {source_key}")
    rotation = fields.get("rotation_spawn_point")
    _require(isinstance(rotation, dict), f"PF4582 rotation evidence missing: {identity}")
    _require(rotation.get("centre") == [source["PositionX"], source["PositionY"], source["PositionZ"]], f"PF4582 position mismatch: {identity}")
    _require(rotation.get("radius") == source["Radius"], f"PF4582 radius mismatch: {identity}")
    _require(rotation.get("rotation_mid_encoded") == source["RotationMidEncoded"], f"PF4582 rotation-mid mismatch: {identity}")
    _require(rotation.get("rotation_width_encoded") == source["RotationWidthEncoded"], f"PF4582 rotation-width mismatch: {identity}")


def build_pf4582_crosswalk(
    repo_root: Path,
    pf4582_records: Iterable[Mapping[str, Any]],
) -> tuple[dict[str, Mapping[str, Any]], Mapping[str, str]]:
    artifacts, hashes = _load_pf4582_artifacts(repo_root)
    overlay = artifacts["OfficialOverlay"]
    report = artifacts["AuthoritativeReport"]
    runtime_map = artifacts["RuntimeEvidenceMap"]
    _require(isinstance(overlay, dict) and overlay.get("SchemaVersion") == 1, "PF4582 overlay schema drift")
    overlay_records = overlay.get("Records")
    _require(isinstance(overlay_records, list) and len(overlay_records) == 207, "PF4582 overlay count drift")
    overlay_by_id = {row.get("OfficialRecordIdentity"): row for row in overlay_records}
    _require(len(overlay_by_id) == 207, "PF4582 overlay identities are not unique")
    source_by_id = {row["OfficialSpawnRecordId"]: row for row in pf4582_records}
    _require(len(source_by_id) == 207, "PF4582 source shard count drift")
    _require(set(source_by_id) == set(overlay_by_id), "PF4582 source/overlay stable identity set mismatch")
    for identity, source_record in source_by_id.items():
        _compare_pf4582_record(source_record, overlay_by_id[identity])

    _require(report.get("PF4582_RUNTIME_ELIGIBLE") == 25, "PF4582 runtime-eligible count drift")
    _require(report.get("PF4582_RUNTIME_BLOCKED") == 181, "PF4582 accepted runtime-blocked count drift")
    eligible_ids = report.get("runtimeEligibleNpcIds")
    _require(isinstance(eligible_ids, list) and len(eligible_ids) == 25, "PF4582 runtime-eligible identity set drift")
    _require(len(set(eligible_ids)) == 25, "PF4582 runtime-eligible identities are not unique")
    runtime_mappings = runtime_map.get("runtimeMappings")
    _require(isinstance(runtime_mappings, list) and len(runtime_mappings) == 25, "PF4582 runtime evidence mapping count drift")
    profiles = {row.get("npcId"): row.get("runtimeProfile") for row in runtime_mappings}
    _require(len(profiles) == 25 and set(profiles) == set(eligible_ids), "PF4582 profile/runtime-active crosswalk drift")
    _require(all(isinstance(value, str) and value for value in profiles.values()), "PF4582 runtime profile is invalid")

    source_npc_ids = [row.get("SourceNpcId") for row in overlay_records if row.get("SourceNpcId") is not None]
    _require(len(source_npc_ids) == 206 and len(set(source_npc_ids)) == 206, "PF4582 SourceNpcId crosswalk drift")
    crosswalk: dict[str, Mapping[str, Any]] = {}
    for identity, row in overlay_by_id.items():
        source_npc_id = row.get("SourceNpcId")
        active = source_npc_id in profiles if source_npc_id is not None else False
        profile = profiles.get(source_npc_id)
        crosswalk[identity] = {
            "SourceNpcId": source_npc_id,
            "ExistingAoRebirthProfile": profile,
            "CurrentRuntimeActive": active,
            "ReconciliationState": row.get("ReconciliationState"),
        }
    ncnn_identity = f"{SOURCE_CLIENT_BUILD}:{RESOURCE_TYPE}:4582:district-1:record-50"
    ncnn = crosswalk.get(ncnn_identity)
    _require(ncnn is not None, "PF4582 NCNN stable identity is missing")
    _require(ncnn["SourceNpcId"] is None and ncnn["ExistingAoRebirthProfile"] is None, "PF4582 NCNN identity/profile was fabricated")
    _require(ncnn["CurrentRuntimeActive"] is False, "PF4582 NCNN was activated")
    return crosswalk, hashes


def _source_parse_error_text(value: Any) -> str | None:
    if value is None:
        return None
    _require(isinstance(value, dict), "source ParseError must be an object or null")
    code = value.get("Code")
    detail = value.get("Detail")
    _require_text(code, "source ParseError.Code")
    _require_text(detail, "source ParseError.Detail")
    return f"{code}: {detail}"


def _resource_unknown_fields(shard: Mapping[str, Any]) -> Mapping[str, Any]:
    return {
        "DatabaseGlobalOffset": shard.get("DatabaseGlobalOffset"),
        "DatabaseSha256": shard.get("DatabaseSha256"),
        "DuplicateResourceKeyStatus": shard.get("DuplicateResourceKeyStatus"),
        "IndexRecordIdentity": shard.get("IndexRecordIdentity"),
        "OfficialResourceId": shard.get("OfficialResourceId"),
        "ParseWarnings": shard.get("ParseWarnings"),
        "ParserVersion": shard.get("ParserVersion"),
        "ResourceFile": shard.get("ResourceFile"),
        "ResourceLength": shard.get("ResourceLength"),
        "ResourceOffset": shard.get("ResourceOffset"),
        "ResourceSha256": shard.get("ResourceSha256"),
        "SourceParseError": shard.get("ParseError"),
        "SourceUnknownFields": shard.get("UnknownFields"),
    }


def _normalize_district(district: Mapping[str, Any]) -> Mapping[str, Any]:
    """Retain one typed district envelope without duplicating it in UnknownFields."""
    return {
        "DistrictIndex": district.get("DistrictIndex"),
        "DistrictName": district.get("DistrictName"),
        "DistrictRecordOffset": district.get("DistrictRecordOffset"),
        "DistrictSerializedSize": district.get("DistrictSerializedSize"),
        "HashSpawnRecordCount": district.get("HashSpawnRecordCount"),
        "OfficialDistrictId": district.get("OfficialDistrictId"),
        "OfficialResourceId": district.get("OfficialResourceId"),
        "OtherCollectionCountsWhereDecoded": district.get(
            "OtherCollectionCountsWhereDecoded"
        ),
        "RecordSha256": district.get("RecordSha256"),
        "UnknownFields": district.get("UnknownFields"),
    }


def _record_unknown_fields(
    record: Mapping[str, Any],
) -> Mapping[str, Any]:
    return {
        "AcgHashFieldOffsetInResource": record.get("AcgHashFieldOffsetInResource"),
        "AdditionalPoints": record.get("AdditionalPoints"),
        "Extensions": record.get("Extensions"),
        "FieldPresence": record.get("FieldPresence"),
        "OfficialResourceId": record.get("OfficialResourceId"),
        "ParserVersion": record.get("ParserVersion"),
        "RecordOffsetInDatabase": record.get("RecordOffsetInDatabase"),
        "RecordSha256": record.get("RecordSha256"),
        "SourceUnknownFields": record.get("UnknownFields"),
    }


def _normalize_record(
    shard: Mapping[str, Any],
    district: Mapping[str, Any],
    record: Mapping[str, Any],
    pf4582_crosswalk: Mapping[str, Mapping[str, Any]],
) -> Mapping[str, Any]:
    playfield_id = shard["ResourceInstance"]
    identity = record["OfficialSpawnRecordId"]
    crosswalk = pf4582_crosswalk.get(identity) if playfield_id == 4582 else None
    source_npc_id = crosswalk["SourceNpcId"] if crosswalk is not None else None
    existing_profile = crosswalk["ExistingAoRebirthProfile"] if crosswalk is not None else None
    current_active = crosswalk["CurrentRuntimeActive"] if crosswalk is not None else None
    runtime_authorized = current_active is True and existing_profile is not None
    _require(
        current_active is not True or runtime_authorized,
        f"active PF4582 placement lacks an existing AORebirth profile: {identity}",
    )
    if existing_profile is not None:
        identity_status = "EXISTING_AOREBIRTH_PROFILE_RECONCILED"
        behavior_readiness = "EXISTING_RUNTIME_BEHAVIOR_RETAINED"
    elif source_npc_id is not None:
        identity_status = "SOURCE_PLACEMENT_RECONCILED_IDENTITY_UNRESOLVED"
        behavior_readiness = "UNPROVEN"
    else:
        identity_status = "UNRESOLVED"
        behavior_readiness = "UNPROVEN"

    return {
        "AssistanceRadius": record.get("AssistanceRadius"),
        "BehaviorReady": runtime_authorized,
        "BehaviorReadiness": behavior_readiness,
        "CanonicalAcgHashText": record["CanonicalAcgHashText"],
        "CurrentRuntimeActive": current_active,
        "DistrictIndex": record["DistrictIndex"],
        "DistrictName": record["DistrictName"],
        "DistrictRecordOrdinal": record["DistrictRecordOrdinal"],
        "ExistingAoRebirthProfile": existing_profile,
        "IdentityResolved": runtime_authorized,
        "IdentityResolutionStatus": identity_status,
        "LevelMaximum": record.get("LevelMaximum"),
        "LevelMinimum": record.get("LevelMinimum"),
        "MobTemplateEvidenceSource": None,
        "MobTemplateResolutionStatus": "UNRESOLVED",
        "MoreFlags": record.get("MoreFlags"),
        "NativeFlags": record.get("NativeFlags"),
        "OfficialAcgHashNativeUInt32": record["AcgHashNativeUInt32"],
        "OfficialAcgHashWireBytes": record["AcgHashWireBytes"],
        "OfficialSpawnRecordId": identity,
        "ParseStatus": "PARSED",
        "PlacementKnown": True,
        "PlayfieldId": playfield_id,
        "PositionX": record["PositionX"],
        "PositionY": record["PositionY"],
        "PositionZ": record["PositionZ"],
        "Radius": record.get("Radius"),
        "RecordOffset": record["RecordOffsetInResource"],
        "ResolvedMobTemplateHash": None,
        "ResolvedMobTemplateId": None,
        "ResolvedMobTemplateName": None,
        "ResolvedMonsterData": None,
        "ResourceInstance": shard["ResourceInstance"],
        "ResourceOffset": shard.get("ResourceOffset"),
        "ResourceType": RESOURCE_TYPE,
        "RespawnChance": record.get("RespawnChance"),
        "RespawnTime": record.get("RespawnTime"),
        "RotationMidEncoded": record.get("RotationMidEncoded"),
        "RotationWidthEncoded": record.get("RotationWidthEncoded"),
        "RuntimeActivationAuthorized": runtime_authorized,
        "SerializedOptionalFlags": record.get("SerializedOptionalFlags"),
        "SerializedSize": record["SerializedSize"],
        "SourceClientBuild": SOURCE_CLIENT_BUILD,
        "SourceClientVariant": SOURCE_CLIENT_VARIANT,
        "SourceNpcId": source_npc_id,
        "UnknownFields": _record_unknown_fields(record),
        "UnknownOptionalU8": record.get("UnknownOptionalU8"),
    }


def _build_source_manifest(
    corpus: SourceCorpus,
    pf4582_hashes: Mapping[str, str],
) -> Mapping[str, Any]:
    source_artifacts = [
        {
            "RelativePath": SOURCE_INPUT_MANIFEST_RELATIVE.as_posix(),
            "Role": "OfficialInputManifest",
            "Sha256": corpus.artifact_hashes["OfficialInputManifest"],
        }
    ]
    for role, (relative, _) in EXPECTED_GLOBAL_ARTIFACTS.items():
        source_artifacts.append(
            {
                "RelativePath": relative.as_posix(),
                "Role": role,
                "Sha256": corpus.artifact_hashes[role],
            }
        )
    pf_artifacts = []
    for role, (relative, _) in PF4582_ARTIFACTS.items():
        pf_artifacts.append(
            {"RelativePath": relative.as_posix(), "Role": role, "Sha256": pf4582_hashes[role]}
        )
    return {
        "ImportPolicy": {
            "AcgHashSemantics": "OFFICIAL_PACKED_FOUR_BYTE_SCALAR_TAG",
            "CurrentRuntimeActiveOutsideEnumeratedPf4582": None,
            "ExistingRuntimeBehaviorChanged": False,
            "IdentityRequiredForPlacementImport": False,
            "NewRuntimeSpawnsActivated": 0,
            "PlacementEvidenceOnly": True,
            "RuntimeActivationRequiresSeparateAuthorization": True,
        },
        "OffsetMapping": {
            "RecordOffset": "source HashSpawnRecord.RecordOffsetInResource",
            "RecordOffsetInDatabasePreservedAt": "Record.UnknownFields.RecordOffsetInDatabase",
            "ResourceOffset": "source resource shard ResourceOffset, local to ResourceFile",
            "ResourceDatabaseGlobalOffsetPreservedAt": "Shard.UnknownFields.DatabaseGlobalOffset",
        },
        "PF4582ReconciliationArtifacts": pf_artifacts,
        "ResourceDatabaseSha256": EXPECTED_DATABASE_SHA256,
        "ResourceInstanceRelationship": {
            "PlayfieldIdDerivedFromValidatedInstance": True,
            "ResourceInstanceRetained": True,
            "Status": "PROVEN_FOR_ALL_VALIDATED_CONTROLS",
            "ValidatedInstanceConflicts": 0,
            "ValidatedInstanceControls": EXPECTED_RESOURCE_COUNT,
            "ValidatedInstanceMatches": EXPECTED_RESOURCE_COUNT,
        },
        "ResourceType": RESOURCE_TYPE,
        "SchemaVersion": SCHEMA_VERSION,
        "SourceArtifacts": source_artifacts,
        "SourceClientBuild": SOURCE_CLIENT_BUILD,
        "SourceClientVariant": SOURCE_CLIENT_VARIANT,
        "SourceCorpusBoundary": {
            "MalformedResources": list(EXPECTED_MALFORMED_PLAYFIELDS),
            "ParsedResources": EXPECTED_PARSED_RESOURCE_COUNT,
            "ResourceShardBytes": corpus.source_shard_bytes,
            "ResourceShards": EXPECTED_RESOURCE_COUNT,
            "StaticHashSpawnRecords": EXPECTED_RECORD_COUNT,
        },
        "SourceRepositoryRole": "READ_ONLY_DETERMINISTIC_EXTRACTION_SOURCE",
        "Terminology": {
            "ClientVariantRetainedForProvenance": True,
            "Ep1Ep2AreNotPlayfieldContentBoundaries": True,
            "Ep1Ep2AreNotSpawnContentBoundaries": True,
        },
    }


def build_import_model(
    source_root: Path = DEFAULT_SOURCE_ROOT,
    *,
    repo_root: Path = REPOSITORY_ROOT,
) -> ImportModel:
    repo_root = repo_root.resolve()
    corpus = load_source_corpus(source_root)
    source_pf4582_records = [
        record
        for item in corpus.resources
        if item["Shard"]["ResourceInstance"] == 4582
        for district in item["Shard"]["Districts"]
        for record in district["HashSpawnRecords"]
    ]
    pf4582_crosswalk, pf4582_hashes = build_pf4582_crosswalk(repo_root, source_pf4582_records)
    source_manifest = _build_source_manifest(corpus, pf4582_hashes)
    source_manifest_bytes = _json_bytes(source_manifest, compact=False)
    source_manifest_hash = _sha256_bytes(source_manifest_bytes)

    placement_shards: dict[int, Mapping[str, Any]] = {}
    normalized_records: list[Mapping[str, Any]] = []
    index_rows: list[Mapping[str, Any]] = []
    shard_bytes_by_playfield: dict[int, bytes] = {}
    for item in corpus.resources:
        shard = item["Shard"]
        playfield_id = shard["ResourceInstance"]
        malformed = shard["ParseStatus"] == "MALFORMED_RESOURCE"
        normalized_parse_status = (
            "MALFORMED_FOR_CURRENT_EXTRACTOR" if malformed else "PARSED"
        )
        records = [
            _normalize_record(shard, district, record, pf4582_crosswalk)
            for district in shard["Districts"]
            for record in district["HashSpawnRecords"]
        ]
        districts = [_normalize_district(district) for district in shard["Districts"]]
        _require(len(records) == shard["HashSpawnRecordCount"], f"normalized record count drift: {playfield_id}")
        _require(
            malformed or len(districts) == shard["DistrictCount"],
            f"normalized district count drift: {playfield_id}",
        )
        normalized = {
            "DistrictCount": None if malformed else shard["DistrictCount"],
            "Districts": districts,
            "FormatVersion": shard["FormatVersion"],
            "OfficialSpawnCount": None if malformed else shard["HashSpawnRecordCount"],
            "ParseError": _source_parse_error_text(shard["ParseError"]),
            "ParseStatus": normalized_parse_status,
            "PlayfieldId": playfield_id,
            "Records": records,
            "ResourceInstance": shard["ResourceInstance"],
            "ResourceType": RESOURCE_TYPE,
            "SchemaVersion": SHARD_SCHEMA_VERSION,
            "SourceClientBuild": SOURCE_CLIENT_BUILD,
            "SourceClientVariant": SOURCE_CLIENT_VARIANT,
            "UnknownFields": _resource_unknown_fields(shard),
        }
        placement_shards[playfield_id] = normalized
        normalized_records.extend(records)
        payload = _json_bytes(normalized, compact=True)
        shard_bytes_by_playfield[playfield_id] = payload
        path = (OUTPUT_PLACEMENT_ROOT / f"pf_{playfield_id}.json").as_posix()
        index_rows.append(
            {
                "DistrictCount": normalized["DistrictCount"],
                "FormatVersion": shard["FormatVersion"],
                "OfficialSpawnCount": normalized["OfficialSpawnCount"],
                "ParseStatus": normalized_parse_status,
                "Path": path,
                "PlayfieldId": playfield_id,
                "ResourceInstance": shard["ResourceInstance"],
                "Sha256": _sha256_bytes(payload),
            }
        )

    _require(len(placement_shards) == EXPECTED_RESOURCE_COUNT, "normalized playfield shard count drift")
    _require(len(normalized_records) == EXPECTED_RECORD_COUNT, "normalized placement count drift")
    normalized_ids = [record["OfficialSpawnRecordId"] for record in normalized_records]
    _require(len(set(normalized_ids)) == EXPECTED_RECORD_COUNT, "normalized stable identities are not unique")
    normalized_shard_bytes = sum(len(payload) for payload in shard_bytes_by_playfield.values())

    index = {
        "Playfields": index_rows,
        "ResourceType": RESOURCE_TYPE,
        "SchemaVersion": SCHEMA_VERSION,
        "SourceClientBuild": SOURCE_CLIENT_BUILD,
        "SourceClientVariant": SOURCE_CLIENT_VARIANT,
        "SourceManifestSha256": source_manifest_hash,
    }

    tag_rows: list[Mapping[str, Any]] = []
    by_tag: dict[str, list[Mapping[str, Any]]] = defaultdict(list)
    for record in normalized_records:
        by_tag[record["CanonicalAcgHashText"]].append(record)
    for tag in sorted(by_tag):
        records = sorted(by_tag[tag], key=lambda row: row["OfficialSpawnRecordId"])
        wires = {row["OfficialAcgHashWireBytes"] for row in records}
        natives = {row["OfficialAcgHashNativeUInt32"] for row in records}
        _require(len(wires) == 1 and len(natives) == 1, f"ACGHash encoding conflict: {tag!r}")
        tag_rows.append(
            {
                "CanonicalAcgHashText": tag,
                "FirstOfficialSpawnRecordId": records[0]["OfficialSpawnRecordId"],
                "OfficialAcgHashNativeUInt32": next(iter(natives)),
                "OfficialAcgHashWireBytes": next(iter(wires)),
                "OfficialSpawnRecordIds": [row["OfficialSpawnRecordId"] for row in records],
                "PlacementCount": len(records),
                "PlayfieldIds": sorted({row["PlayfieldId"] for row in records}),
            }
        )
    _require(len(tag_rows) == EXPECTED_UNIQUE_ACGHASH_COUNT, "normalized ACGHash inventory count drift")
    acghash_inventory = {
        "IdentityBoundary": "ACGHash_t is an official packed four-byte scalar/tag; no terminal mob identity is claimed.",
        "ResourceType": RESOURCE_TYPE,
        "SchemaVersion": SCHEMA_VERSION,
        "SourceClientBuild": SOURCE_CLIENT_BUILD,
        "SourceClientVariant": SOURCE_CLIENT_VARIANT,
        "Tags": tag_rows,
        "TotalPlacementCount": EXPECTED_RECORD_COUNT,
        "UniqueTagCount": EXPECTED_UNIQUE_ACGHASH_COUNT,
    }

    source_metrics = corpus.artifacts["CorpusSummary"]["Metrics"]
    active_pf4582 = sum(
        record["CurrentRuntimeActive"] is True
        for record in normalized_records
        if record["PlayfieldId"] == 4582
    )
    authorized = sum(record["RuntimeActivationAuthorized"] is True for record in normalized_records)
    outside_active_non_null = sum(
        record["CurrentRuntimeActive"] is not None
        for record in normalized_records
        if record["PlayfieldId"] != 4582
    )
    _require(active_pf4582 == 25, "PF4582 current runtime crosswalk count drift")
    _require(authorized == 25, "runtime activation authorization expanded")
    _require(outside_active_non_null == 0, "non-PF4582 current runtime state was inferred")

    ncnn_id = f"{SOURCE_CLIENT_BUILD}:{RESOURCE_TYPE}:4582:district-1:record-50"
    ncnn = next(record for record in normalized_records if record["OfficialSpawnRecordId"] == ncnn_id)
    _require(ncnn["CanonicalAcgHashText"] == "NCNN", "PF4582 NCNN tag drift")
    _require(ncnn["OfficialAcgHashWireBytes"] == "4E 4E 43 4E", "PF4582 NCNN wire bytes drift")
    _require(ncnn["OfficialAcgHashNativeUInt32"] == 0x4E434E4E, "PF4582 NCNN native scalar drift")
    _require(ncnn["SourceNpcId"] is None and ncnn["CurrentRuntimeActive"] is False, "PF4582 NCNN governance drift")

    summary = {
        "AcgHashSemantics": "OFFICIAL_PACKED_FOUR_BYTE_SCALAR_TAG",
        "MalformedResources": [
            {
                "ParseError": placement_shards[playfield_id]["ParseError"],
                "PlayfieldId": playfield_id,
                "SyntheticDataCreated": False,
                "UnknownFields": placement_shards[playfield_id]["UnknownFields"],
            }
            for playfield_id in EXPECTED_MALFORMED_PLAYFIELDS
        ],
        "Metrics": {
            "ExistingRuntimeBehaviorChanged": False,
            "NewRuntimeSpawnsActivated": 0,
            "NormalizedPlacementShardBytes": normalized_shard_bytes,
            "OfficialCrossDistrictDuplicateGroups": source_metrics["OFFICIAL_CROSS_DISTRICT_DUPLICATE_GROUPS"],
            "OfficialDistricts": EXPECTED_DISTRICT_COUNT,
            "OfficialDuplicatePositionGroups": source_metrics["OFFICIAL_DUPLICATE_POSITION_GROUPS"],
            "OfficialDuplicatePositionRecords": source_metrics["OFFICIAL_DUPLICATE_POSITION_RECORDS"],
            "OfficialExactDuplicateGroups": source_metrics["OFFICIAL_EXACT_DUPLICATE_GROUPS"],
            "OfficialExactDuplicateRecords": source_metrics["OFFICIAL_EXACT_DUPLICATE_RECORDS"],
            "OfficialRecordsDroppedByDeduplication": 0,
            "OfficialSpawnRecords": EXPECTED_RECORD_COUNT,
            "OfficialUniqueAcgHashTags": EXPECTED_UNIQUE_ACGHASH_COUNT,
            "ParsedResources": EXPECTED_PARSED_RESOURCE_COUNT,
            "Pf4582ExistingRuntimeActive": active_pf4582,
            "PlayfieldShards": EXPECTED_RESOURCE_COUNT,
            "ResourcesMalformed": EXPECTED_MALFORMED_RESOURCE_COUNT,
            "ResourcesParsedEmpty": 5,
            "ResourcesParsedSupported": 622,
            "SourcePlacementShardBytes": corpus.source_shard_bytes,
        },
        "Outcome": "OFFICIAL_STATIC_PLACEMENT_EVIDENCE_IMPORTED_RUNTIME_UNCHANGED",
        "PF4582Regression": {
            "DistrictRecordCounts": [142, 65],
            "Districts": 2,
            "FormatVersion": 7,
            "NcnnOfficialSpawnRecordId": ncnn_id,
            "NcnnRuntimeActivationAuthorized": False,
            "OfficialRecords": 207,
            "StableIdFullRecordReconciliation": "PASS",
        },
        "ResourceInstanceRelationship": {
            "PlayfieldIdDerivedFromValidatedInstance": True,
            "ResourceInstanceRetained": True,
            "Status": "PROVEN_FOR_ALL_VALIDATED_CONTROLS",
            "ValidatedInstanceConflicts": 0,
            "ValidatedInstanceControls": EXPECTED_RESOURCE_COUNT,
            "ValidatedInstanceMatches": EXPECTED_RESOURCE_COUNT,
        },
        "ResourceType": RESOURCE_TYPE,
        "RuntimeGovernance": {
            "CurrentRuntimeActiveOutsideEnumeratedPf4582": None,
            "ExistingPf4582RuntimeActive": 25,
            "MassRuntimeActivation": False,
            "RuntimeActivationAuthorizedRecords": authorized,
            "UnresolvedPlacementsRemainInactive": True,
        },
        "SchemaVersion": SCHEMA_VERSION,
        "SourceClientBuild": SOURCE_CLIENT_BUILD,
        "SourceClientVariant": SOURCE_CLIENT_VARIANT,
    }

    index_bytes = _json_bytes(index, compact=False)
    summary_bytes = _json_bytes(summary, compact=False)
    acghash_bytes = _json_bytes(acghash_inventory, compact=True)
    corpus_manifest = {
        "AcgHashInventorySha256": _sha256_bytes(acghash_bytes),
        "CorpusVersion": CORPUS_VERSION,
        "IndexSha256": _sha256_bytes(index_bytes),
        "Metrics": {
            "DistrictCount": EXPECTED_DISTRICT_COUNT,
            "ParsedResourceCount": EXPECTED_PARSED_RESOURCE_COUNT,
            "ParserLimitedResourceCount": EXPECTED_MALFORMED_RESOURCE_COUNT,
            "PlacementCount": EXPECTED_RECORD_COUNT,
            "ResourceCount": EXPECTED_RESOURCE_COUNT,
            "RuntimeActivationAuthorizedCount": authorized,
            "UniqueAcgHashCount": EXPECTED_UNIQUE_ACGHASH_COUNT,
        },
        "ParserLimitedPlayfieldIds": list(EXPECTED_MALFORMED_PLAYFIELDS),
        "Playfields": [
            {
                "DistrictCount": shard["DistrictCount"],
                "ParseStatus": shard["ParseStatus"],
                "Path": f"placements/pf_{playfield_id}.json",
                "PlacementCount": shard["OfficialSpawnCount"],
                "PlayfieldId": playfield_id,
                "RuntimeActivationAuthorizedCount": sum(
                    record["RuntimeActivationAuthorized"] is True
                    for record in shard["Records"]
                ),
                "ShardSha256": _sha256_bytes(shard_bytes_by_playfield[playfield_id]),
                "SourceResourceSha256": shard["UnknownFields"]["ResourceSha256"],
            }
            for playfield_id, shard in sorted(placement_shards.items())
        ],
        "Policy": {
            "ExistingRuntimeBehaviorChanged": False,
            "MassPlacementActivation": False,
            "UnresolvedAcgHashActivated": False,
        },
        "ResourceType": RESOURCE_TYPE,
        "SchemaVersion": SCHEMA_VERSION,
        "SourceClientBuild": SOURCE_CLIENT_BUILD,
        "SourceClientVariant": SOURCE_CLIENT_VARIANT,
        "SourceManifestSha256": source_manifest_hash,
        "SummarySha256": _sha256_bytes(summary_bytes),
    }

    return ImportModel(
        source_manifest=source_manifest,
        placement_shards=placement_shards,
        index=index,
        summary=summary,
        acghash_inventory=acghash_inventory,
        corpus_manifest=corpus_manifest,
        source_shard_bytes=corpus.source_shard_bytes,
        normalized_shard_bytes=normalized_shard_bytes,
    )


def build_candidate_outputs(model: ImportModel, *, repo_root: Path = REPOSITORY_ROOT) -> dict[Path, bytes]:
    outputs: dict[Path, bytes] = {
        _path(repo_root, OUTPUT_SOURCE_MANIFEST): _json_bytes(model.source_manifest, compact=False),
        _path(repo_root, OUTPUT_INDEX): _json_bytes(model.index, compact=False),
        _path(repo_root, OUTPUT_SUMMARY): _json_bytes(model.summary, compact=False),
        _path(repo_root, OUTPUT_ACGHASH): _json_bytes(model.acghash_inventory, compact=True),
        _path(repo_root, OUTPUT_CORPUS_MANIFEST): _json_bytes(
            model.corpus_manifest, compact=False
        ),
    }
    for playfield_id, shard in model.placement_shards.items():
        outputs[_path(repo_root, OUTPUT_PLACEMENT_ROOT / f"pf_{playfield_id}.json")] = _json_bytes(
            shard, compact=True
        )
    return outputs


def _unexpected_placement_shards(model: ImportModel, repo_root: Path) -> list[Path]:
    placement_root = _path(repo_root, OUTPUT_PLACEMENT_ROOT)
    if not placement_root.exists():
        return []
    expected = {f"pf_{playfield_id}.json" for playfield_id in model.placement_shards}
    return sorted(path for path in placement_root.glob("pf_*.json") if path.name not in expected)


def check_candidate_outputs(
    model: ImportModel,
    outputs: Mapping[Path, bytes],
    *,
    repo_root: Path = REPOSITORY_ROOT,
) -> None:
    issues: list[str] = []
    for path, expected in sorted(outputs.items(), key=lambda item: item[0].as_posix()):
        if not path.is_file():
            issues.append(f"missing {path.relative_to(repo_root).as_posix()}")
        elif path.read_bytes() != expected:
            issues.append(f"different {path.relative_to(repo_root).as_posix()}")
    for path in _unexpected_placement_shards(model, repo_root):
        issues.append(f"unexpected {path.relative_to(repo_root).as_posix()}")
    _require(not issues, "generated placement cohort is stale: " + "; ".join(issues[:20]))


def _validate_json_output(payload: bytes) -> None:
    value = json.loads(payload.decode("utf-8"))
    if not isinstance(value, dict):
        raise ValueError("generated JSON root must be an object")


def write_candidate_outputs(
    model: ImportModel,
    outputs: Mapping[Path, bytes],
    *,
    repo_root: Path = REPOSITORY_ROOT,
) -> str:
    unexpected = _unexpected_placement_shards(model, repo_root)
    _require(not unexpected, "unexpected generated placement shards require explicit review")
    try:
        import generated_artifact_transaction as transaction
    except ImportError as exc:
        raise PlacementImportError("shared generated-artifact transaction module is unavailable") from exc
    relative_outputs = {
        path.relative_to(repo_root).as_posix(): payload for path, payload in outputs.items()
    }
    placement_order = sorted(
        relative
        for relative in relative_outputs
        if relative.startswith(OUTPUT_PLACEMENT_ROOT.as_posix() + "/")
    )
    artifact_order = placement_order + [
        OUTPUT_ACGHASH.as_posix(),
        OUTPUT_SUMMARY.as_posix(),
        OUTPUT_SOURCE_MANIFEST.as_posix(),
        OUTPUT_CORPUS_MANIFEST.as_posix(),
        OUTPUT_INDEX.as_posix(),
    ]
    validators = {relative: _validate_json_output for relative in relative_outputs}
    try:
        with transaction.GeneratedArtifactLease(
            repo_root,
            "official-playfield-placements",
            mode="write",
            timeout_seconds=transaction.MAX_LEASE_WAIT_SECONDS,
        ) as lease:
            transaction.ArtifactTransaction.recover(lease)
            transaction_id = transaction.ArtifactTransaction.publish(
                lease,
                relative_outputs,
                validators=validators,
                artifact_order=artifact_order,
                commit_marker=OUTPUT_INDEX.as_posix(),
            )
    except transaction.GeneratedArtifactError as exc:
        raise PlacementImportError(f"transactional publication failed: {exc}") from exc
    check_candidate_outputs(model, outputs, repo_root=repo_root)
    return transaction_id


def _self_test(model: ImportModel, outputs: Mapping[Path, bytes]) -> None:
    _require(len(model.placement_shards) == EXPECTED_RESOURCE_COUNT, "self-test shard count failed")
    _require(
        sum(shard["OfficialSpawnCount"] or 0 for shard in model.placement_shards.values())
        == EXPECTED_RECORD_COUNT,
        "self-test placement count failed",
    )
    _require(len(model.acghash_inventory["Tags"]) == EXPECTED_UNIQUE_ACGHASH_COUNT, "self-test ACGHash count failed")
    _require(model.corpus_manifest["Metrics"]["RuntimeActivationAuthorizedCount"] == 25, "self-test authorized placement count failed")
    _require(model.summary["Metrics"]["OfficialRecordsDroppedByDeduplication"] == 0, "self-test dedup safety failed")
    _require(model.summary["Metrics"]["NewRuntimeSpawnsActivated"] == 0, "self-test runtime safety failed")
    _require(model.summary["Metrics"]["ExistingRuntimeBehaviorChanged"] is False, "self-test runtime mutation failed")
    malformed = [model.placement_shards[value] for value in EXPECTED_MALFORMED_PLAYFIELDS]
    _require(all(not shard["Records"] for shard in malformed), "self-test malformed synthesis failed")
    _require(all(shard["ParseError"] for shard in malformed), "self-test malformed reason retention failed")
    _require(
        all(
            shard["ParseStatus"] == "MALFORMED_FOR_CURRENT_EXTRACTOR"
            and shard["DistrictCount"] is None
            and shard["OfficialSpawnCount"] is None
            for shard in malformed
        ),
        "self-test malformed null-state failed",
    )
    ncnn_id = f"{SOURCE_CLIENT_BUILD}:{RESOURCE_TYPE}:4582:district-1:record-50"
    ncnn = next(
        record
        for record in model.placement_shards[4582]["Records"]
        if record["OfficialSpawnRecordId"] == ncnn_id
    )
    _require(ncnn["SourceNpcId"] is None, "self-test NCNN SourceNpcId failed")
    _require(ncnn["RuntimeActivationAuthorized"] is False, "self-test NCNN activation failed")
    for path, payload in outputs.items():
        _validate_json_output(payload)
        _require(payload.endswith(b"\n"), f"self-test trailing newline failed: {path}")


def _print_metrics(model: ImportModel, outputs: Mapping[Path, bytes]) -> None:
    generated_total_bytes = sum(len(payload) for payload in outputs.values())
    print("SOURCE_CORPUS_VERIFICATION=PASS")
    print(f"SOURCE_RESOURCE_SHARDS={EXPECTED_RESOURCE_COUNT}")
    print(f"SOURCE_PLACEMENT_RECORDS={EXPECTED_RECORD_COUNT}")
    print(f"SOURCE_SHARD_BYTES={model.source_shard_bytes}")
    print(f"NORMALIZED_SHARD_BYTES={model.normalized_shard_bytes}")
    print(f"GENERATED_TOTAL_BYTES={generated_total_bytes}")
    print("NEW_RUNTIME_SPAWNS_ACTIVATED=0")
    print("EXISTING_RUNTIME_BEHAVIOR_CHANGED=NO")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source-root",
        type=Path,
        default=DEFAULT_SOURCE_ROOT,
        help="explicit read-only AO Stripdown repository root",
    )
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--write", action="store_true", help="transactionally publish the verified cohort")
    action.add_argument("--check", action="store_true", help="verify tracked outputs exactly match the source")
    action.add_argument("--self-test", "--test", dest="self_test", action="store_true", help="run full source-backed importer self-tests")
    args = parser.parse_args(argv)
    try:
        model = build_import_model(args.source_root)
        outputs = build_candidate_outputs(model)
        _self_test(model, outputs)
        _print_metrics(model, outputs)
        if args.write:
            transaction_id = write_candidate_outputs(model, outputs)
            print(f"OUTPUT_WRITE=PASS transaction={transaction_id}")
        elif args.check:
            check_candidate_outputs(model, outputs)
            print("OUTPUT_CHECK=PASS")
        else:
            print("SELF_TEST=PASS")
        return 0
    except (OSError, PlacementImportError) as exc:
        print(f"Official playfield placement import failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
