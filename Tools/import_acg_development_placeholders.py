#!/usr/bin/env python3

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import sys
import zipfile
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = REPOSITORY_ROOT / "docs/generated/acg_development_placeholders"
DEFAULT_VISUAL_EVIDENCE = (
    REPOSITORY_ROOT / "docs/reference/acg-development-visual-evidence.json"
)
PACKAGE_ROOT = "AO_ACG_Spawn_Capture_Atlas_18.8.62_EP1_20260829"
EXPECTED_PACKAGE_SHA256 = "379e39cf3a2a697b5613316ff2a7da66a9d5f0ecc30d1b75efe0a4dffc7d093e"
EXPECTED_BUILD_ID = "18.8.62_EP1"
EXPECTED_RESOURCE_TYPE = 1000014
EXPECTED_ENUMERATED_RESOURCES = 630
EXPECTED_PARSED_RESOURCES = 627
EXPECTED_MALFORMED_RESOURCES = (103, 615, 4805)
EXPECTED_PRIMARY_RECORDS = 32805
EXPECTED_ADDITIONAL_POINTS = 32737
EXPECTED_TOTAL_COORDINATES = 65542
EXPECTED_UNIQUE_ACGS = 4016
EXPECTED_PLAYFIELDS_WITH_PLACEMENTS = 459
EXPECTED_CAPTURE_PLAN_PLAYFIELDS = 238
EXPECTED_VISUAL_COUNTS = {
    "ExactOfficial": 1,
    "CaptureCorrelated": 3,
    "CaptureCorrelatedMultipleVariants": 1,
    "Unresolved": 4011,
}

PACKAGE_FILES = {
    "placements": "data/all_acg_spawn_placements.csv",
    "locations": "data/all_acg_spawn_locations_expanded.csv",
    "targets": "data/unique_acg_capture_targets.csv",
    "summary": "data/atlas_summary.json",
    "visuals": "data/known_visual_evidence.csv",
    "manifest": "manifest.json",
    "provenance": "docs/provenance.json",
}

STATUS_TO_GRADE = {
    "exact_official_server_bridge": "ExactOfficial",
    "capture_correlated_base_body": "CaptureCorrelated",
    "capture_correlated_multiple_variants": "CaptureCorrelatedMultipleVariants",
    "unresolved": "Unresolved",
}


class ImportError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ImportError(message)


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=True, sort_keys=True, separators=(",", ":"))
        + "\n"
    ).encode("utf-8")


def read_json_bytes(value: bytes, label: str) -> Any:
    try:
        return json.loads(value.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ImportError(f"{label} is not valid UTF-8 JSON: {exc}") from exc


def read_csv_bytes(value: bytes, label: str) -> list[dict[str, str]]:
    try:
        text = value.decode("utf-8-sig")
    except UnicodeError as exc:
        raise ImportError(f"{label} is not valid UTF-8 CSV: {exc}") from exc
    reader = csv.DictReader(io.StringIO(text, newline=""))
    require(reader.fieldnames is not None, f"{label} has no CSV header")
    rows = list(reader)
    require(all(None not in row for row in rows), f"{label} contains an over-wide row")
    return rows


def package_entry(archive: zipfile.ZipFile, relative_path: str) -> bytes:
    exact = f"{PACKAGE_ROOT}/{relative_path}"
    try:
        return archive.read(exact)
    except KeyError as exc:
        raise ImportError(f"portable package is missing {exact}") from exc


def validate_portable_package(package_path: Path) -> dict[str, bytes]:
    require(package_path.is_file(), f"portable package is missing: {package_path}")
    actual_package_sha256 = sha256_file(package_path)
    require(
        actual_package_sha256 == EXPECTED_PACKAGE_SHA256,
        "portable package SHA-256 mismatch: "
        f"expected {EXPECTED_PACKAGE_SHA256}, found {actual_package_sha256}",
    )

    with zipfile.ZipFile(package_path, "r") as archive:
        require(archive.testzip() is None, "portable package contains a corrupt ZIP member")
        package_manifest_bytes = package_entry(archive, PACKAGE_FILES["manifest"])
        package_manifest = read_json_bytes(package_manifest_bytes, "portable manifest")
        require(package_manifest.get("SchemaVersion") == 1, "portable manifest schema drifted")
        require(
            package_manifest.get("PackageName") == PACKAGE_ROOT,
            "portable manifest package name drifted",
        )
        declared_files = package_manifest.get("Files")
        require(isinstance(declared_files, list), "portable manifest Files is missing")
        for declared in declared_files:
            require(isinstance(declared, dict), "portable manifest contains a non-object file row")
            relative = declared.get("Path")
            require(isinstance(relative, str), "portable manifest file path is missing")
            payload = package_entry(archive, relative)
            require(len(payload) == declared.get("Bytes"), f"portable file size drifted: {relative}")
            require(
                sha256_bytes(payload) == declared.get("Sha256"),
                f"portable file SHA-256 drifted: {relative}",
            )

        return {
            key: package_entry(archive, relative)
            for key, relative in PACKAGE_FILES.items()
        }


def int_field(row: dict[str, str], field: str, label: str) -> int:
    try:
        return int(row[field], 10)
    except (KeyError, TypeError, ValueError) as exc:
        raise ImportError(f"{label}.{field} is not an integer") from exc


def float_field(row: dict[str, str], field: str, label: str) -> float:
    try:
        value = float(row[field])
    except (KeyError, TypeError, ValueError) as exc:
        raise ImportError(f"{label}.{field} is not numeric") from exc
    require(value == value and abs(value) != float("inf"), f"{label}.{field} is not finite")
    return value


def optional_int(row: dict[str, str], field: str, label: str) -> int | None:
    value = row.get(field, "")
    return None if value == "" else int_field(row, field, label)


def normalize_wire_bytes(value: str, label: str) -> str:
    parts = value.split(" ")
    require(len(parts) == 4, f"{label} does not contain four wire bytes")
    for part in parts:
        require(len(part) == 2 and all(c in "0123456789ABCDEF" for c in part),
                f"{label} contains an invalid wire byte")
    return value


def native_from_wire_bytes(value: str) -> int:
    wire = bytes(int(part, 16) for part in value.split(" "))
    return int.from_bytes(wire, byteorder="little", signed=False)


def evidence_grade(row: dict[str, str], label: str) -> str:
    status = row.get("VisualMappingStatus", "")
    require(status in STATUS_TO_GRADE, f"{label} has unsupported visual status {status!r}")
    return STATUS_TO_GRADE[status]


def build_visual_registry(
    placement_rows: list[dict[str, str]],
    visual_evidence_source: dict[str, Any],
) -> tuple[list[dict[str, Any]], dict[int, dict[str, Any]]]:
    by_native: dict[int, dict[str, Any]] = {}
    for index, row in enumerate(placement_rows):
        label = f"placements[{index}]"
        native = int_field(row, "AcgHashNativeUInt32", label)
        wire = normalize_wire_bytes(row.get("AcgHashWireBytes", ""), f"{label}.AcgHashWireBytes")
        require(native == native_from_wire_bytes(wire), f"{label} native ACG and wire bytes differ")
        grade = evidence_grade(row, label)
        candidate = {
            "AcgHashNativeUInt32": native,
            "AcgHashNativeUInt32Hex": f"0x{native:08X}",
            "AcgHashText": row.get("AcgHashText", ""),
            "AcgHashDisplay": row.get("AcgHashDisplay", ""),
            "AcgHashWireBytes": wire,
            "EvidenceGrade": grade,
            "KnownVisualEvidence": row.get("KnownVisualEvidence", ""),
            "VisualEvidenceNote": row.get("VisualEvidenceNote", ""),
            "AppearanceIds": [],
            "MeshResourceIds": [],
            "AdditionalVariantUnresolved": False,
            "ServerTemplateId": None,
            "ServerTemplateHash": None,
            "MonsterDataType": None,
            "MonsterDataInstance": None,
            "ExactMeshType": None,
            "ExactMeshInstance": None,
        }
        existing = by_native.get(native)
        if existing is None:
            by_native[native] = candidate
        else:
            for field in (
                "AcgHashText",
                "AcgHashDisplay",
                "AcgHashWireBytes",
                "EvidenceGrade",
                "KnownVisualEvidence",
                "VisualEvidenceNote",
            ):
                require(existing[field] == candidate[field], f"ACG {native:#010x} {field} drifted")

    require(len(by_native) == EXPECTED_UNIQUE_ACGS, "unique native ACG count drifted")

    require(visual_evidence_source.get("SchemaVersion") == 1,
            "visual evidence source schema drifted")
    require(visual_evidence_source.get("BuildId") == EXPECTED_BUILD_ID,
            "visual evidence source build drifted")
    require(visual_evidence_source.get("ResourceType") == EXPECTED_RESOURCE_TYPE,
            "visual evidence source resource type drifted")
    evidence_entries = visual_evidence_source.get("Entries")
    require(isinstance(evidence_entries, list) and len(evidence_entries) == 5,
            "visual evidence source must contain the five governed mappings")
    governed_keys: set[int] = set()
    for source in evidence_entries:
        require(isinstance(source, dict), "visual evidence source row is not an object")
        native = source.get("AcgHashNativeUInt32")
        require(isinstance(native, int) and native in by_native,
                "visual evidence source native key is invalid")
        require(native not in governed_keys, "visual evidence source contains a duplicate native key")
        governed_keys.add(native)
        entry = by_native[native]
        require(source.get("EvidenceGrade") == entry["EvidenceGrade"],
                f"visual evidence grade conflicts with portable evidence for 0x{native:08X}")
        for field in (
            "AppearanceIds",
            "MeshResourceIds",
            "AdditionalVariantUnresolved",
            "ServerTemplateId",
            "ServerTemplateHash",
            "MonsterDataType",
            "MonsterDataInstance",
            "ExactMeshType",
            "ExactMeshInstance",
        ):
            if field in source:
                entry[field] = source[field]

    require(governed_keys == {0x30315631, 0x4644514F, 0x52504F46, 0x55494755, 0x56415754},
            "visual evidence source native-key set drifted")

    exact = by_native[0x4644514F]
    require(exact["AcgHashText"] == "FDQO", "FDQO native ACG key drifted")
    require(exact["EvidenceGrade"] == "ExactOfficial", "FDQO evidence grade drifted")
    require(exact["ServerTemplateId"] == 43296, "FDQO server template id drifted")
    require(exact["ServerTemplateHash"] == "A004", "FDQO template hash drifted")
    require(exact["MonsterDataType"] == 1040023, "FDQO MonsterData type drifted")
    require(exact["MonsterDataInstance"] == 17655, "FDQO MonsterData drifted")
    require(exact["ExactMeshType"] == 1010002, "FDQO exact mesh type drifted")
    require(exact["ExactMeshInstance"] == 15222, "FDQO exact mesh drifted")

    required_correlations = {
        0x30315631: ([1576, 1896], ["1010002:5907", "1010002:5941"], True),
        0x52504F46: ([1576], ["1010002:5907"], False),
        0x55494755: ([1578], ["1010002:5907"], False),
        0x56415754: ([1576], ["1010002:5907"], False),
    }
    for native, expected in required_correlations.items():
        entry = by_native[native]
        require(entry["AppearanceIds"] == expected[0],
                f"correlated appearance ids drifted for 0x{native:08X}")
        require(entry["MeshResourceIds"] == expected[1],
                f"correlated mesh ids drifted for 0x{native:08X}")
        require(entry["AdditionalVariantUnresolved"] is expected[2],
                f"correlated variant boundary drifted for 0x{native:08X}")

    grade_counts = Counter(entry["EvidenceGrade"] for entry in by_native.values())
    require(dict(grade_counts) == EXPECTED_VISUAL_COUNTS, f"visual evidence counts drifted: {grade_counts}")
    require(0x20202020 in by_native, "native ACG 0x20202020 is missing")
    require(0x9F9F9F9F in by_native, "native ACG 0x9F9F9F9F is missing")
    require(by_native[0x20202020] is not by_native[0x9F9F9F9F], "non-printable ACG keys collapsed")

    ordered = [by_native[key] for key in sorted(by_native)]
    return ordered, by_native


def build_generated_files(
    package_path: Path,
    visual_evidence_path: Path,
) -> dict[str, bytes]:
    payloads = validate_portable_package(package_path)
    summary = read_json_bytes(payloads["summary"], "atlas summary")
    provenance = read_json_bytes(payloads["provenance"], "package provenance")
    placement_rows = read_csv_bytes(payloads["placements"], "all placements")
    location_rows = read_csv_bytes(payloads["locations"], "expanded locations")
    target_rows = read_csv_bytes(payloads["targets"], "capture targets")
    known_visual_rows = read_csv_bytes(payloads["visuals"], "known visuals")

    require(summary.get("SchemaVersion") == 1, "atlas summary schema drifted")
    require(summary.get("SourceBuild") == EXPECTED_BUILD_ID, "atlas build id drifted")
    require(provenance.get("Source", {}).get("ResourceType") == EXPECTED_RESOURCE_TYPE,
            "atlas resource type drifted")
    counts = provenance.get("Counts", {})
    expected_counts = {
        "AllEnumeratedPlayfieldResources": EXPECTED_ENUMERATED_RESOURCES,
        "ParsedPlayfieldResources": EXPECTED_PARSED_RESOURCES,
        "PlayfieldsWithDecodedPlacements": EXPECTED_PLAYFIELDS_WITH_PLACEMENTS,
        "DecodedStaticHashSpawnRecords": EXPECTED_PRIMARY_RECORDS,
        "DecodedPrimaryCoordinatePoints": EXPECTED_PRIMARY_RECORDS,
        "DecodedAdditionalCoordinatePoints": EXPECTED_ADDITIONAL_POINTS,
        "DecodedCoordinatePointsTotal": EXPECTED_TOTAL_COORDINATES,
        "UniqueAcgTags": EXPECTED_UNIQUE_ACGS,
        "GreedyVisitPlanPlayfields": EXPECTED_CAPTURE_PLAN_PLAYFIELDS,
    }
    for field, expected in expected_counts.items():
        require(counts.get(field) == expected, f"package count drifted for {field}")

    malformed = summary.get("MalformedResourcesExcluded")
    require(isinstance(malformed, list), "malformed-resource boundary is missing")
    malformed_ids = tuple(sorted(int(row["ResourceInstance"]) for row in malformed))
    require(malformed_ids == EXPECTED_MALFORMED_RESOURCES, "malformed-resource ids drifted")
    require(len(placement_rows) == EXPECTED_PRIMARY_RECORDS, "primary placement row count drifted")
    require(len(location_rows) == EXPECTED_TOTAL_COORDINATES, "expanded coordinate row count drifted")
    require(len(target_rows) == EXPECTED_UNIQUE_ACGS, "capture target row count drifted")
    require(len(known_visual_rows) == 5, "known visual evidence row count drifted")

    visual_evidence_source = read_json_bytes(
        visual_evidence_path.read_bytes(),
        "visual evidence source",
    )
    visual_registry, visual_by_native = build_visual_registry(
        placement_rows,
        visual_evidence_source,
    )
    target_ids: dict[str, int] = {}
    target_native_keys: set[int] = set()
    target_playfields: set[int] = set()
    for index, row in enumerate(target_rows):
        label = f"targets[{index}]"
        record_id = row.get("OfficialSpawnRecordId", "")
        native = int_field(row, "AcgHashNativeUInt32", label)
        require(record_id and record_id not in target_ids, f"{label} duplicate capture target id")
        require(native not in target_native_keys, f"{label} duplicate capture target ACG")
        target_ids[record_id] = native
        target_native_keys.add(native)
        target_playfields.add(int_field(row, "PlayfieldResourceInstance", label))
    require(target_native_keys == set(visual_by_native), "capture targets do not cover every native ACG")
    require(len(target_playfields) == EXPECTED_CAPTURE_PLAN_PLAYFIELDS, "capture-plan playfield count drifted")

    expanded_primary = Counter()
    expanded_additional = Counter()
    for index, row in enumerate(location_rows):
        label = f"locations[{index}]"
        record_id = row.get("OfficialSpawnRecordId", "")
        kind = row.get("LocationKind")
        ordinal = int_field(row, "LocationOrdinalWithinSpawnRecord", label)
        if kind == "Primary":
            require(ordinal == 0, f"{label} primary ordinal drifted")
            expanded_primary[record_id] += 1
        elif kind == "AdditionalPoint":
            require(ordinal > 0, f"{label} additional ordinal drifted")
            expanded_additional[record_id] += 1
        else:
            raise ImportError(f"{label} has unsupported location kind {kind!r}")
    require(sum(expanded_primary.values()) == EXPECTED_PRIMARY_RECORDS,
            "expanded primary coordinate count drifted")
    require(sum(expanded_additional.values()) == EXPECTED_ADDITIONAL_POINTS,
            "expanded additional coordinate count drifted")
    require(all(value == 1 for value in expanded_primary.values()),
            "expanded primary coordinates do not map one-to-one to source records")

    records_by_playfield: dict[int, list[dict[str, Any]]] = defaultdict(list)
    source_ids: set[str] = set()
    coordinate_duplicate_counts: Counter[tuple[Any, ...]] = Counter()
    fdqo_pf4582_count = 0
    ncnn_pf4582_present = False
    total_additional = 0

    for index, row in enumerate(placement_rows):
        label = f"placements[{index}]"
        record_id = row.get("OfficialSpawnRecordId", "")
        require(record_id and record_id not in source_ids, f"{label} duplicate stable source id")
        source_ids.add(record_id)
        playfield = int_field(row, "PlayfieldResourceInstance", label)
        native = int_field(row, "AcgHashNativeUInt32", label)
        require(native in visual_by_native, f"{label} visual registry key is missing")
        require(target_ids.get(record_id, native) == native, f"{label} capture target native key drifted")
        try:
            additional = json.loads(row.get("AdditionalPointsJson", ""))
        except json.JSONDecodeError as exc:
            raise ImportError(f"{label}.AdditionalPointsJson is invalid: {exc}") from exc
        require(isinstance(additional, list), f"{label}.AdditionalPointsJson is not an array")
        require(expanded_additional[record_id] == len(additional),
                f"{label} expanded additional-point count drifted")

        additional_points = []
        for ordinal, point in enumerate(additional, 1):
            require(isinstance(point, dict), f"{label} additional point is not an object")
            additional_points.append(
                {
                    "Ordinal": ordinal,
                    "PositionX": float(point["PositionX"]),
                    "PositionY": float(point["PositionY"]),
                    "PositionZ": float(point["PositionZ"]),
                    "Radius": float(point["Radius"]),
                    "RotationMidEncoded": int(point["RotationMidEncoded"]),
                    "RotationWidthEncoded": int(point["RotationWidthEncoded"]),
                    "RecordOffset": int(point["RecordOffset"]),
                }
            )
        total_additional += len(additional_points)

        primary_x = float_field(row, "PositionX", label)
        primary_y = float_field(row, "PositionY", label)
        primary_z = float_field(row, "PositionZ", label)
        coordinate_duplicate_counts[(playfield, native, primary_x, primary_y, primary_z)] += 1
        if playfield == 4582 and native == 0x4644514F:
            fdqo_pf4582_count += 1
        if playfield == 4582 and row.get("AcgHashText") == "NCNN":
            require(evidence_grade(row, label) == "Unresolved", "NCNN was promoted")
            ncnn_pf4582_present = True

        record = {
            "OfficialSpawnRecordId": record_id,
            "BuildId": EXPECTED_BUILD_ID,
            "ResourceType": EXPECTED_RESOURCE_TYPE,
            "ResourceInstance": playfield,
            "PlayfieldName": row.get("PlayfieldName", ""),
            "DistrictIndex": int_field(row, "DistrictIndex", label),
            "DistrictName": row.get("DistrictName", ""),
            "AcgHashNativeUInt32": native,
            "AcgHashText": row.get("AcgHashText", ""),
            "AcgHashDisplay": row.get("AcgHashDisplay", ""),
            "AcgHashWireBytes": normalize_wire_bytes(
                row.get("AcgHashWireBytes", ""), f"{label}.AcgHashWireBytes"
            ),
            "EvidenceGrade": visual_by_native[native]["EvidenceGrade"],
            "KnownVisualEvidence": row.get("KnownVisualEvidence", ""),
            "VisualEvidenceNote": row.get("VisualEvidenceNote", ""),
            "CapturePlanTarget": record_id in target_ids,
            "LevelMinimum": int_field(row, "LevelMinimum", label),
            "LevelMaximum": int_field(row, "LevelMaximum", label),
            "RespawnChanceRaw": int_field(row, "RespawnChancePercent", label),
            "RespawnTimeRaw": float_field(row, "RespawnTimeSeconds", label),
            "AssistanceRadius": optional_int(row, "AssistanceRadius", label),
            "NativeFlags": optional_int(row, "NativeFlags", label),
            "MoreFlags": optional_int(row, "MoreFlags", label),
            "UnknownOptionalU8": optional_int(row, "UnknownOptionalU8", label),
            "RecordOffsetInDatabase": int_field(row, "RecordOffsetInDatabase", label),
            "RecordOffsetInResource": int_field(row, "RecordOffsetInResource", label),
            "RecordSha256": row.get("RecordSha256", ""),
            "Primary": {
                "PositionX": primary_x,
                "PositionY": primary_y,
                "PositionZ": primary_z,
                "Radius": float_field(row, "Radius", label),
                "RotationMidEncoded": int_field(row, "RotationMidEncoded", label),
                "RotationWidthEncoded": int_field(row, "RotationWidthEncoded", label),
            },
            "AdditionalPoints": additional_points,
        }
        require(record["LevelMinimum"] <= record["LevelMaximum"], f"{label} level range inverted")
        require(len(record["RecordSha256"]) == 64, f"{label} record SHA-256 is invalid")
        records_by_playfield[playfield].append(record)

    require(len(source_ids) == EXPECTED_PRIMARY_RECORDS, "source stable id count drifted")
    require(total_additional == EXPECTED_ADDITIONAL_POINTS, "decoded additional-point count drifted")
    require(len(records_by_playfield) == EXPECTED_PLAYFIELDS_WITH_PLACEMENTS,
            "playfields-with-placements count drifted")
    require(fdqo_pf4582_count == 9, f"PF4582 FDQO count drifted: {fdqo_pf4582_count}")
    require(ncnn_pf4582_present, "PF4582 NCNN unresolved placement is missing")
    require(sum(1 for row in placement_rows if int(row["PlayfieldResourceInstance"]) == 4582) == 207,
            "PF4582 placement count drifted")

    duplicate_primary_rows = sum(count - 1 for count in coordinate_duplicate_counts.values() if count > 1)
    require(duplicate_primary_rows > 0, "source duplicate primary placement rows disappeared")

    generated: dict[str, bytes] = {}
    shard_entries = []
    for playfield in sorted(records_by_playfield):
        records = records_by_playfield[playfield]
        shard = {
            "SchemaVersion": 1,
            "BuildId": EXPECTED_BUILD_ID,
            "ResourceType": EXPECTED_RESOURCE_TYPE,
            "ResourceInstance": playfield,
            "PrimaryRecordCount": len(records),
            "AdditionalPointCount": sum(len(record["AdditionalPoints"]) for record in records),
            "CapturePlanTargetCount": sum(1 for record in records if record["CapturePlanTarget"]),
            "Records": records,
        }
        relative = f"playfields/pf_{playfield}.json"
        shard_bytes = canonical_json_bytes(shard)
        generated[relative] = shard_bytes
        shard_entries.append(
            {
                "ResourceInstance": playfield,
                "Path": relative,
                "Sha256": sha256_bytes(shard_bytes),
                "PrimaryRecordCount": shard["PrimaryRecordCount"],
                "AdditionalPointCount": shard["AdditionalPointCount"],
                "CapturePlanTargetCount": shard["CapturePlanTargetCount"],
            }
        )

    visual_payload = {
        "SchemaVersion": 1,
        "BuildId": EXPECTED_BUILD_ID,
        "ResourceType": EXPECTED_RESOURCE_TYPE,
        "Count": len(visual_registry),
        "EvidenceGradeCounts": EXPECTED_VISUAL_COUNTS,
        "Entries": visual_registry,
    }
    visual_bytes = canonical_json_bytes(visual_payload)
    generated["acg-visual-resolution-registry.json"] = visual_bytes

    manifest = {
        "SchemaVersion": 1,
        "CorpusVersion": "acg-development-placeholders-v1",
        "PortablePackageName": PACKAGE_ROOT,
        "PortablePackageSha256": EXPECTED_PACKAGE_SHA256,
        "BuildId": EXPECTED_BUILD_ID,
        "ResourceType": EXPECTED_RESOURCE_TYPE,
        "Metrics": {
            "EnumeratedResourceCount": EXPECTED_ENUMERATED_RESOURCES,
            "ParsedResourceCount": EXPECTED_PARSED_RESOURCES,
            "MalformedResourceCount": len(EXPECTED_MALFORMED_RESOURCES),
            "PlayfieldsWithPlacements": EXPECTED_PLAYFIELDS_WITH_PLACEMENTS,
            "PrimaryRecordCount": EXPECTED_PRIMARY_RECORDS,
            "AdditionalPointCount": EXPECTED_ADDITIONAL_POINTS,
            "TotalCoordinateCount": EXPECTED_TOTAL_COORDINATES,
            "UniqueAcgHashCount": EXPECTED_UNIQUE_ACGS,
            "CapturePlanPlayfieldCount": EXPECTED_CAPTURE_PLAN_PLAYFIELDS,
            "CapturePlanTargetCount": EXPECTED_UNIQUE_ACGS,
            "DuplicatePrimaryCoordinateRowCount": duplicate_primary_rows,
            "ExactOfficialCount": EXPECTED_VISUAL_COUNTS["ExactOfficial"],
            "CaptureCorrelatedCount": (
                EXPECTED_VISUAL_COUNTS["CaptureCorrelated"]
                + EXPECTED_VISUAL_COUNTS["CaptureCorrelatedMultipleVariants"]
            ),
            "UnresolvedCount": EXPECTED_VISUAL_COUNTS["Unresolved"],
            "Pf4582PrimaryRecordCount": 207,
            "Pf4582FdqoPlacementCount": 9,
        },
        "MalformedResources": malformed,
        "VisualRegistryPath": "acg-visual-resolution-registry.json",
        "VisualRegistrySha256": sha256_bytes(visual_bytes),
        "Playfields": shard_entries,
        "Policy": {
            "DefaultMode": "Off",
            "DevelopmentBuildOnly": True,
            "ProductionActivation": False,
            "RuntimeIdentityUsesSourceIdentity": False,
            "CaptureCorrelationPromotesExactIdentity": False,
            "AdditionalPointRuntimeSemanticsProven": False,
            "DefaultPlaceholderVisualSource": "items.dat Item 283862 Mesh stat 12",
            "DefaultPlaceholderItemId": 283862,
            "DefaultPlaceholderMeshId": 9013,
            "RespawnChanceFieldName": "RespawnChanceRaw",
        },
    }
    generated["acg-development-placeholder-manifest.json"] = canonical_json_bytes(manifest)
    return generated


def write_generated_files(output_root: Path, generated: dict[str, bytes]) -> None:
    output_root.mkdir(parents=True, exist_ok=True)
    expected = set(generated)
    for relative, payload in generated.items():
        path = output_root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload)
    actual = {
        path.relative_to(output_root).as_posix()
        for path in output_root.rglob("*")
        if path.is_file()
    }
    extras = sorted(actual - expected)
    require(not extras, f"generated output contains unexpected files: {extras}")


def check_generated_files(output_root: Path, generated: dict[str, bytes]) -> None:
    require(output_root.is_dir(), f"generated output is missing: {output_root}")
    expected = set(generated)
    actual = {
        path.relative_to(output_root).as_posix()
        for path in output_root.rglob("*")
        if path.is_file()
    }
    require(actual == expected, f"generated output file set drifted: missing={sorted(expected - actual)} extra={sorted(actual - expected)}")
    for relative, payload in generated.items():
        require((output_root / relative).read_bytes() == payload, f"generated output drifted: {relative}")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Import the checksum-pinned ACG capture atlas as development-only shards."
    )
    parser.add_argument("package", type=Path, help="portable atlas ZIP")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument(
        "--visual-evidence",
        type=Path,
        default=DEFAULT_VISUAL_EVIDENCE,
    )
    parser.add_argument("--check", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    try:
        generated = build_generated_files(
            args.package.resolve(),
            args.visual_evidence.resolve(),
        )
        output = args.output.resolve()
        if args.check:
            check_generated_files(output, generated)
        else:
            write_generated_files(output, generated)
    except (ImportError, OSError, zipfile.BadZipFile, KeyError, TypeError, ValueError) as exc:
        print(f"ACG_DEVELOPMENT_PLACEHOLDER_IMPORT=FAIL reason={exc}", file=sys.stderr)
        return 1

    print("ACG_DEVELOPMENT_PLACEHOLDER_IMPORT=PASS")
    print(f"PACKAGE_SHA256={EXPECTED_PACKAGE_SHA256}")
    print(f"PRIMARY_RECORDS={EXPECTED_PRIMARY_RECORDS}")
    print(f"ADDITIONAL_POINTS={EXPECTED_ADDITIONAL_POINTS}")
    print(f"TOTAL_COORDINATES={EXPECTED_TOTAL_COORDINATES}")
    print(f"UNIQUE_ACGS={EXPECTED_UNIQUE_ACGS}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
