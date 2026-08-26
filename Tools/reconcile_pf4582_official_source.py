#!/usr/bin/env python3
"""Deterministically reconcile AORebirth PF4582 placements to official EP1 records."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


PLAYFIELD_ID = 4582
EXPECTED_ACCEPTED_COUNT = 206
EXPECTED_OFFICIAL_COUNT = 207
EXPECTED_ACCEPTED_KEYS = 38
EXPECTED_ACCEPTED_SHA256 = "b747aea145cb36e3f9be5b2cacc7aaebca3d24017a14540ac1f29f4bd1296b32"
EXPECTED_ARTIFACT_SHA256 = {
    "records": "f19ed7fb094369f99998cd83da451839f08a4882833d6ade1b533ccc4bba3ec2",
    "search_report": "7bc10b90e5e7e7d25c7ff1f47cf7e74f8fba37bab8da9d1e7cc67d5457c290f2",
    "occurrence_manifest": "b87691b66a9f92f4e44130079bf0e749d808032a6a280ac6a9933f74b75c504c",
}
OFFICIAL_BUILD = "18.8.62_EP1"
NCNN_DISPOSITION = "INCLUDE_AS_OFFICIAL_BLOCKED_PLACEMENT"

REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ACCEPTED_SOURCE = REPOSITORY_ROOT / "docs/reference/pf4582/PlayfieldDistrictInfo.json"
DEFAULT_NORMALIZED_REPORT = REPOSITORY_ROOT / "docs/generated/pf4582_authoritative_placement_report.json"
DEFAULT_OFFICIAL_RECORDS = REPOSITORY_ROOT / "docs/reference/pf4582/official/ep1_18_8_62_pf4582_acghash_records.json"
DEFAULT_OFFICIAL_SEARCH_REPORT = REPOSITORY_ROOT / "docs/reference/pf4582/official/ep1_18_8_62_pf4582_search_report.json"
DEFAULT_OFFICIAL_OCCURRENCE_MANIFEST = REPOSITORY_ROOT / "docs/reference/pf4582/official/ep1_18_8_62_pf4582_occurrence_manifest.json"
DEFAULT_EVIDENCE_MANIFEST = REPOSITORY_ROOT / "docs/reference/pf4582/official/ep1_18_8_62_pf4582_evidence_manifest.json"
DEFAULT_GENERAL_PLACEMENT_SHARD = REPOSITORY_ROOT / "docs/generated/playfields/placements/pf_4582.json"
DEFAULT_REPORT = REPOSITORY_ROOT / "docs/generated/pf4582_official_source_reconciliation_report.json"
DEFAULT_OVERLAY = REPOSITORY_ROOT / "docs/generated/pf4582_official_placement_overlay.json"
DEFAULT_CSHARP = REPOSITORY_ROOT / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportOfficialPlacementCatalog.g.cs"
DEFAULT_MARKDOWN = REPOSITORY_ROOT / "docs/evidence/PF4582_OFFICIAL_SOURCE_RECONCILIATION_20260825.md"

MATCH_BASIS = [
    "CanonicalAcgHashText",
    "PositionRoundedToAcceptedSourcePrecision",
    "LevelMinimumValueCorrespondence",
    "LevelMaximumValueCorrespondence",
    "RadiusValueCorrespondence",
    "SpawnAngleWidthEncodedValueCorrespondence",
    "SpawnChanceValueCorrespondence",
    "SpawnTimeValueCorrespondence",
    "NativeFlagsToLegacyExFlagsValueCorrespondence",
    "SerializedOptionalFlagsToLegacyExtraDataValueCorrespondence",
]


class ReconciliationError(ValueError):
    pass


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ReconciliationError(message)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_json(path: Path) -> Any:
    _require(path.is_file(), f"required input is missing: {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ReconciliationError(f"cannot read governed JSON {path}: {exc}") from exc


def format_bytes(raw: bytes) -> str:
    return " ".join(f"{byte:02X}" for byte in raw)


def accepted_uint32_to_canonical_text(value: int) -> str:
    _require(isinstance(value, int) and not isinstance(value, bool), "accepted ACGHash value must be an integer")
    _require(0 <= value <= 0xFFFFFFFF, "accepted ACGHash integer is outside uint32")
    raw = value.to_bytes(4, "little", signed=False)
    _require(all(0x20 <= byte <= 0x7E for byte in raw), "accepted ACGHash bytes are not printable")
    return raw.decode("ascii")


def canonical_text_to_accepted_uint32(text: str) -> int:
    _require(isinstance(text, str) and len(text) == 4, "canonical ACGHash text must contain four characters")
    raw = text.encode("ascii")
    return int.from_bytes(raw, "little", signed=False)


def canonical_text_to_official_wire_bytes(text: str) -> bytes:
    _require(isinstance(text, str) and len(text) == 4, "canonical ACGHash text must contain four characters")
    return text.encode("ascii")[::-1]


def canonical_text_to_official_native_uint32(text: str) -> int:
    return int.from_bytes(canonical_text_to_official_wire_bytes(text), "little", signed=False)


def roundtrip_dual_encoding(value: int) -> bool:
    text = accepted_uint32_to_canonical_text(value)
    wire = canonical_text_to_official_wire_bytes(text)
    native = canonical_text_to_official_native_uint32(text)
    return (
        canonical_text_to_accepted_uint32(text) == value
        and int.from_bytes(wire, "little", signed=False) == native
        and wire[::-1].decode("ascii") == text
    )


def _accepted_signature(record: dict[str, Any]) -> tuple[Any, ...]:
    position = record["Position"]
    return (
        accepted_uint32_to_canonical_text(record["TemplateHash"]),
        round(float(position["X"]), 1),
        round(float(position["Y"]), 1),
        round(float(position["Z"]), 1),
        int(record["MinLevel"]),
        int(record["MaxLevel"]),
        round(float(record["SpawnRadius"]), 3),
        round(float(record["SpawnAngleW"]), 6),
        int(record["SpawnChance"]),
        round(float(record["SpawnTime"]), 6),
        int(record["ExFlags"]),
        int(record["ExtraData"]),
    )


def _official_signature(record: dict[str, Any]) -> tuple[Any, ...]:
    point = record["rotation_spawn_point"]
    centre = point["centre"]
    return (
        record["acghash_get_hash_as_text"],
        round(float(centre[0]), 1),
        round(float(centre[1]), 1),
        round(float(centre[2]), 1),
        int(record["min_level"]),
        int(record["max_level"]),
        round(float(point["radius"]), 3),
        round(float(point["rotation_width_encoded"]), 6),
        int(record["respawn_chance"]),
        round(float(record["respawn_time"]), 6),
        int(record["native_flags"]),
        int(record["serialized_optional_flags"]),
    )


def _record_identity(record: dict[str, Any]) -> str:
    return (
        f"{OFFICIAL_BUILD}:1000014:{PLAYFIELD_ID}:"
        f"district-{record['district_index']}:record-{record['spawn_index']}"
    )


def _flatten_official(payload: dict[str, Any]) -> list[dict[str, Any]]:
    structure = payload.get("structure")
    _require(isinstance(structure, dict), "official record snapshot lacks structure")
    _require(structure.get("data_format_version") == 7, "official format version must be 7")
    _require(structure.get("district_count") == 2, "official district count must be 2")
    districts = structure.get("districts")
    _require(isinstance(districts, list) and len(districts) == 2, "official districts are malformed")
    records: list[dict[str, Any]] = []
    for district in sorted(districts, key=lambda item: item["district_index"]):
        district_index = district["district_index"]
        district_name = district["name"]
        points = district.get("hash_spawn_points")
        _require(isinstance(points, list), f"district {district_index} lacks hash-spawn points")
        _require(district.get("hash_spawn_count") == len(points), f"district {district_index} count drifted")
        for expected_ordinal, raw in enumerate(points):
            _require(raw.get("district_index") == district_index, "official district index drifted")
            _require(raw.get("district_name") == district_name, "official district name drifted")
            _require(raw.get("spawn_index") == expected_ordinal, "official record ordinal drifted")
            record = dict(raw)
            record["official_record_index"] = len(records)
            record["official_record_identity"] = _record_identity(record)
            records.append(record)
    _require([len(district["hash_spawn_points"]) for district in districts] == [142, 65], "official district counts must be 142 and 65")
    _require(len(records) == EXPECTED_OFFICIAL_COUNT, "official record snapshot must contain exactly 207 records")
    _require(len({record["official_record_identity"] for record in records}) == len(records), "official record identities are not unique")
    return records


def _validate_general_placement_shard(
    general_placement_path: Path,
    official_records: list[dict[str, Any]],
) -> None:
    payload = load_json(general_placement_path)
    for key, value in {
        "SchemaVersion": 2,
        "SourceClientVariant": "EP1_OLD_GRAPHICS_CLIENT",
        "SourceClientBuild": OFFICIAL_BUILD,
        "ResourceType": 1000014,
        "ResourceInstance": PLAYFIELD_ID,
        "PlayfieldId": PLAYFIELD_ID,
        "FormatVersion": 7,
        "ParseStatus": "PARSED",
        "DistrictCount": 2,
        "OfficialSpawnCount": EXPECTED_OFFICIAL_COUNT,
    }.items():
        _require(payload.get(key) == value, f"general PF4582 shard drifted: {key}")
    general_records = payload.get("Records")
    _require(
        isinstance(general_records, list) and len(general_records) == EXPECTED_OFFICIAL_COUNT,
        "general PF4582 shard must contain exactly 207 records",
    )
    general_by_id = {record.get("OfficialSpawnRecordId"): record for record in general_records}
    _require(len(general_by_id) == EXPECTED_OFFICIAL_COUNT, "general PF4582 identities are not unique")
    _require(
        set(general_by_id) == {record["official_record_identity"] for record in official_records},
        "general and specialized PF4582 identity sets differ",
    )
    for official in official_records:
        identity = official["official_record_identity"]
        general = general_by_id[identity]
        point = official["rotation_spawn_point"]
        expected = {
            "SourceClientBuild": OFFICIAL_BUILD,
            "ResourceType": 1000014,
            "ResourceInstance": PLAYFIELD_ID,
            "PlayfieldId": PLAYFIELD_ID,
            "DistrictIndex": official["district_index"],
            "DistrictName": official["district_name"],
            "DistrictRecordOrdinal": official["spawn_index"],
            "RecordOffset": official["record_relative_offset"],
            "SerializedSize": official["serialized_size"],
            "PositionX": point["centre"][0],
            "PositionY": point["centre"][1],
            "PositionZ": point["centre"][2],
            "LevelMinimum": official["min_level"],
            "LevelMaximum": official["max_level"],
            "Radius": point["radius"],
            "RotationMidEncoded": point["rotation_mid_encoded"],
            "RotationWidthEncoded": point["rotation_width_encoded"],
            "RespawnChance": official["respawn_chance"],
            "RespawnTime": official["respawn_time"],
            "AssistanceRadius": official["assistance_radius"],
            "NativeFlags": official["native_flags"],
            "MoreFlags": official["more_flags"],
            "SerializedOptionalFlags": official["serialized_optional_flags"],
            "UnknownOptionalU8": official["unknown_optional_u8"],
            "CanonicalAcgHashText": official["acghash_get_hash_as_text"],
            "OfficialAcgHashWireBytes": official["acghash_raw_bytes_hex"],
            "OfficialAcgHashNativeUInt32": official["official_scalar_uint32"],
            "ParseStatus": "PARSED",
        }
        for key, value in expected.items():
            _require(
                general.get(key) == value,
                f"general/specialized PF4582 record mismatch: {identity} {key}",
            )
        unknown = general.get("UnknownFields")
        _require(isinstance(unknown, dict), f"general PF4582 record lacks unknown-field evidence: {identity}")
        _require(
            unknown.get("RecordOffsetInDatabase") == official["database_offset"],
            f"general/specialized PF4582 database offset mismatch: {identity}",
        )


def _validate_inputs(
    accepted_path: Path,
    normalized_report_path: Path,
    official_records_path: Path,
    official_search_report_path: Path,
    official_occurrence_manifest_path: Path,
    evidence_manifest_path: Path,
    general_placement_path: Path = DEFAULT_GENERAL_PLACEMENT_SHARD,
) -> tuple[list[dict[str, Any]], dict[str, Any], list[dict[str, Any]], dict[str, Any]]:
    _require(sha256_file(accepted_path) == EXPECTED_ACCEPTED_SHA256, "accepted PF4582 JSON digest drifted")
    actual_artifacts = {
        "records": sha256_file(official_records_path),
        "search_report": sha256_file(official_search_report_path),
        "occurrence_manifest": sha256_file(official_occurrence_manifest_path),
    }
    _require(actual_artifacts == EXPECTED_ARTIFACT_SHA256, "imported official artifact digest drifted")

    accepted_payload = load_json(accepted_path)
    _require(isinstance(accepted_payload, dict) and set(accepted_payload) == {str(PLAYFIELD_ID)}, "accepted source root is invalid")
    accepted_records = accepted_payload[str(PLAYFIELD_ID)].get("Spawns")
    _require(isinstance(accepted_records, list) and len(accepted_records) == EXPECTED_ACCEPTED_COUNT, "accepted source must contain exactly 206 records")
    source_ids = [record.get("NpcId") for record in accepted_records]
    _require(all(isinstance(value, int) for value in source_ids) and len(set(source_ids)) == EXPECTED_ACCEPTED_COUNT, "accepted SourceNpcId values must be 206 unique integers")

    normalized = load_json(normalized_report_path)
    _require(normalized.get("sourceSha256") == EXPECTED_ACCEPTED_SHA256, "normalized placement report cites another accepted source")
    for key, value in {
        "PF4582_SOURCE_PLACEMENTS": 206,
        "PF4582_RUNTIME_ACTIVE": 25,
        "PF4582_RUNTIME_BLOCKED": 181,
    }.items():
        _require(normalized.get(key) == value, f"normalized placement invariant drifted: {key}")

    official_payload = load_json(official_records_path)
    official_records = _flatten_official(official_payload)
    _validate_general_placement_shard(general_placement_path, official_records)
    _require(official_payload.get("counts", {}).get("official_hash_spawn_points") == 207, "official snapshot count metric drifted")
    _require(official_payload.get("counts", {}).get("accepted_manifest_placements") == 206, "official snapshot accepted count metric drifted")
    _require(official_payload.get("counts", {}).get("unexpected_official_placements") == 1, "official snapshot extra count metric drifted")

    search_report = load_json(official_search_report_path)
    _require(
        search_report.get("primary_outcome")
        == "PF4582_ACGHASH_SEARCH_OUTCOME=STRUCTURAL_SOURCE_AND_CONSUMER_FOUND"
        and search_report.get("metrics", {}).get("PF4582_ACGHASH_SEARCH_OUTCOME")
        == "STRUCTURAL_SOURCE_AND_CONSUMER_FOUND",
        "official search outcome drifted",
    )
    _require(search_report.get("metrics", {}).get("PF4582_KEYS_FOUND_STRUCTURAL") == 38, "official search does not contain all 38 accepted keys")
    load_json(official_occurrence_manifest_path)

    manifest = load_json(evidence_manifest_path)
    _require(manifest.get("SchemaVersion") == 1, "official evidence manifest schema drifted")
    _require(manifest.get("OfficialBuild") == OFFICIAL_BUILD, "official build must be 18.8.62_EP1")
    resource = manifest.get("OfficialResource", {})
    for key, value in {
        "Type": 1000014,
        "Instance": 4582,
        "FormatVersion": 7,
        "DistrictCount": 2,
        "HashSpawnRecordCount": 207,
    }.items():
        _require(resource.get(key) == value, f"official evidence manifest drifted: {key}")
    artifact_by_local = {item["LocalPath"]: item for item in manifest.get("Artifacts", [])}
    for path, digest in (
        (official_records_path, EXPECTED_ARTIFACT_SHA256["records"]),
        (official_search_report_path, EXPECTED_ARTIFACT_SHA256["search_report"]),
        (official_occurrence_manifest_path, EXPECTED_ARTIFACT_SHA256["occurrence_manifest"]),
    ):
        label = path.resolve().relative_to(REPOSITORY_ROOT.resolve()).as_posix()
        item = artifact_by_local.get(label)
        _require(item is not None and item.get("ExpectedSha256") == digest and item.get("ImportedSha256") == digest and item.get("ByteIdentical") is True, f"official evidence manifest does not pin {label}")
    return accepted_records, normalized, official_records, manifest


def _group_records(records: Iterable[dict[str, Any]], signature_function: Any) -> dict[tuple[Any, ...], list[dict[str, Any]]]:
    grouped: dict[tuple[Any, ...], list[dict[str, Any]]] = defaultdict(list)
    for record in records:
        grouped[signature_function(record)].append(record)
    return grouped


def require_monotonic_source_order(pairs: list[tuple[int, int]]) -> None:
    source_ids = [source_id for _, source_id in sorted(pairs)]
    _require(
        all(left < right for left, right in zip(source_ids, source_ids[1:])),
        "SourceNpcId order is not demonstrably preserved across unique official matches",
    )


def classify_ncnn(
    *,
    structurally_ordinary: bool,
    official_exclusion_rule: dict[str, Any] | None = None,
) -> str:
    if official_exclusion_rule is not None:
        _require(
            official_exclusion_rule.get("DirectOfficialConsumerEvidence") is True
            and isinstance(official_exclusion_rule.get("Rule"), str)
            and bool(official_exclusion_rule["Rule"]),
            "NCNN exclusion requires a direct official consumer rule",
        )
        return "EXCLUDE_WITH_PROVEN_OFFICIAL_RULE"
    if structurally_ordinary:
        return "INCLUDE_AS_OFFICIAL_BLOCKED_PLACEMENT"
    return "OFFICIAL_RECORD_PENDING_CLASSIFICATION"


def _official_summary(record: dict[str, Any]) -> dict[str, Any]:
    return {
        "OfficialRecordIdentity": record["official_record_identity"],
        "OfficialRecordIndex": record["official_record_index"],
        "OfficialDistrictIndex": record["district_index"],
        "OfficialDistrictName": record["district_name"],
        "OfficialRecordOrdinal": record["spawn_index"],
        "OfficialRecordOffset": record["record_relative_offset"],
        "OfficialRecordOffsetHex": record["record_relative_offset_hex"],
        "CanonicalAcgHashText": record["acghash_get_hash_as_text"],
    }


def _build_ncnn_audit(
    official_records: list[dict[str, Any]],
    accepted_keys: set[str],
    source_by_official_identity: dict[str, int],
    active_source_ids: set[int],
) -> dict[str, Any]:
    extras = [record for record in official_records if record["acghash_get_hash_as_text"] == "NCNN"]
    _require(len(extras) == 1, "official source must contain exactly one NCNN record")
    record = extras[0]
    point = record["rotation_spawn_point"]
    index = record["official_record_index"]
    adjacent = []
    for candidate_index in (index - 1, index + 1):
        if 0 <= candidate_index < len(official_records):
            adjacent.append(_official_summary(official_records[candidate_index]))
    same_district = [item for item in official_records if item["district_index"] == record["district_index"]]
    same_position = [item for item in official_records if item["rotation_spawn_point"]["centre"] == point["centre"]]
    active_records = [item for item in official_records if source_by_official_identity.get(item["official_record_identity"]) in active_source_ids]
    return {
        "OfficialRecordIdentity": record["official_record_identity"],
        "AcceptedSourceRecordPresent": False,
        "SourceNpcId": None,
        "CanonicalAcgHashText": record["acghash_get_hash_as_text"],
        "OfficialWireBytes": record["acghash_raw_bytes_hex"],
        "OfficialNativeUInt32": record["official_scalar_uint32"],
        "OfficialNativeUInt32Hex": record["official_scalar_hex"],
        "OfficialDistrictIndex": record["district_index"],
        "OfficialDistrictName": record["district_name"],
        "OfficialRecordOrdinal": record["spawn_index"],
        "OfficialRecordOffset": record["record_relative_offset"],
        "OfficialRecordOffsetHex": record["record_relative_offset_hex"],
        "Position": list(point["centre"]),
        "LevelMinimum": record["min_level"],
        "LevelMaximum": record["max_level"],
        "Radius": point["radius"],
        "SpawnAngleEncoded": point["rotation_mid_encoded"],
        "SpawnAngleWidthEncoded": point["rotation_width_encoded"],
        "SpawnChance": record["respawn_chance"],
        "SpawnTime": record["respawn_time"],
        "NativeFlags": record["native_flags"],
        "MoreFlags": record["more_flags"],
        "SerializedOptionalFlags": record["serialized_optional_flags"],
        "UnknownOptionalU8": record["unknown_optional_u8"],
        "AssistanceRadius": record["assistance_radius"],
        "SerializedSize": record["serialized_size"],
        "UnavailableAcceptedOnlyFields": ["BossMods", "Name", "SpawnPointFlags", "SpawnUnknowns"],
        "AdjacentRecords": adjacent,
        "DuplicatePositionParticipation": len(same_position) > 1,
        "DuplicatePositionRecordIdentities": [item["official_record_identity"] for item in same_position],
        "StructuralComparisons": {
            "AllOfficialRecordCount": len(official_records),
            "SameDistrictRecordCount": len(same_district),
            "SameNativeFlagsRecordCount": sum(item["native_flags"] == record["native_flags"] for item in official_records),
            "SameTimingRecordCount": sum(math.isclose(float(item["respawn_time"]), float(record["respawn_time"]), abs_tol=0.0) for item in official_records),
            "SameLevelRangeRecordCount": sum(item["min_level"] == record["min_level"] and item["max_level"] == record["max_level"] for item in official_records),
            "SameRadiusRecordCount": sum(math.isclose(float(item["rotation_spawn_point"]["radius"]), float(point["radius"]), abs_tol=1e-9) for item in official_records),
            "ActiveAoRebirthRecordCount": len(active_records),
            "ActiveRecordsWithSameNativeFlags": sum(item["native_flags"] == record["native_flags"] for item in active_records),
            "CanonicalKeyPresentInAccepted38": record["acghash_get_hash_as_text"] in accepted_keys,
        },
        "OfficialFields": {
            key: value
            for key, value in record.items()
            if key not in {"official_record_index", "official_record_identity"}
        },
        "Disposition": NCNN_DISPOSITION,
        "DispositionRationale": "NCNN is a normal 40-byte HashSpawnPoint_t in the official district vector, has the same parsed field schema as all neighboring records, and no imported official field or consumer rule proves it disabled, sentinel, editor-only, or non-runtime. Inclusion preserves official placement evidence only and grants no identity or activation authority.",
        "OfficialExclusionRuleFound": False,
        "MobIdentityResolved": False,
        "ProfileSelected": False,
        "RuntimeActivationAuthorized": False,
        "RemainingBlockers": [
            "No accepted-source SourceNpcId exists for NCNN.",
            "No official ACGHash_t-to-mob-template or ACGHash_t-to-dynel identity join is proven.",
            "No AORebirth runtime profile is authorized for NCNN.",
            "Stable-key integration into the current runtime catalog requires separate authorization.",
        ],
    }


def build_model(
    accepted_path: Path = DEFAULT_ACCEPTED_SOURCE,
    normalized_report_path: Path = DEFAULT_NORMALIZED_REPORT,
    official_records_path: Path = DEFAULT_OFFICIAL_RECORDS,
    official_search_report_path: Path = DEFAULT_OFFICIAL_SEARCH_REPORT,
    official_occurrence_manifest_path: Path = DEFAULT_OFFICIAL_OCCURRENCE_MANIFEST,
    evidence_manifest_path: Path = DEFAULT_EVIDENCE_MANIFEST,
    general_placement_path: Path = DEFAULT_GENERAL_PLACEMENT_SHARD,
) -> dict[str, Any]:
    accepted_records, normalized, official_records, manifest = _validate_inputs(
        accepted_path,
        normalized_report_path,
        official_records_path,
        official_search_report_path,
        official_occurrence_manifest_path,
        evidence_manifest_path,
        general_placement_path,
    )
    accepted_indexed = [dict(record, _accepted_record_index=index) for index, record in enumerate(accepted_records)]
    accepted_groups = _group_records(accepted_indexed, _accepted_signature)
    official_groups = _group_records(official_records, _official_signature)

    accepted_keys = {accepted_uint32_to_canonical_text(record["TemplateHash"]) for record in accepted_records}
    _require(len(accepted_keys) == EXPECTED_ACCEPTED_KEYS, "accepted source must contain exactly 38 canonical ACGHash keys")
    _require(all(roundtrip_dual_encoding(record["TemplateHash"]) for record in accepted_records), "accepted ACGHash dual encoding failed")

    missing_signatures = [signature for signature in accepted_groups if signature not in official_groups]
    _require(
        not missing_signatures,
        "accepted records are absent from official source: "
        f"{len(missing_signatures)} signatures; first={missing_signatures[:2]!r}",
    )
    count_conflicts = [signature for signature, records in accepted_groups.items() if len(records) != len(official_groups.get(signature, []))]
    _require(not count_conflicts, f"accepted/official exact-field group counts differ: {len(count_conflicts)} signatures")

    dynamic_source_ids = {
        item["npcId"] for item in normalized.get("unresolvedDynamicSourceNames", [])
    }
    unique_order_pairs = []
    for signature, accepted_group in accepted_groups.items():
        official_group = official_groups[signature]
        if len(accepted_group) == 1 and accepted_group[0]["NpcId"] not in dynamic_source_ids:
            unique_order_pairs.append((official_group[0]["official_record_index"], accepted_group[0]["NpcId"]))
    require_monotonic_source_order(unique_order_pairs)

    duplicate_signatures = sorted(
        (signature for signature, records in accepted_groups.items() if len(records) > 1),
        key=lambda signature: repr(signature),
    )
    duplicate_ids = {signature: f"DUPLICATE_EQUIVALENCE_{index:03d}" for index, signature in enumerate(duplicate_signatures, 1)}
    reconciliation: list[dict[str, Any]] = []
    used_official: set[str] = set()
    source_by_official_identity: dict[str, int] = {}
    for signature, accepted_group in sorted(accepted_groups.items(), key=lambda item: min(record["NpcId"] for record in item[1])):
        official_group = official_groups[signature]
        accepted_sorted = sorted(accepted_group, key=lambda record: record["NpcId"])
        official_sorted = sorted(official_group, key=lambda record: record["official_record_index"])
        group_identity = duplicate_ids.get(signature)
        candidates = [record["official_record_identity"] for record in official_sorted]
        for accepted, official in zip(accepted_sorted, official_sorted):
            official_identity = official["official_record_identity"]
            _require(official_identity not in used_official, "official record was assigned more than once")
            used_official.add(official_identity)
            source_by_official_identity[official_identity] = accepted["NpcId"]
            canonical = accepted_uint32_to_canonical_text(accepted["TemplateHash"])
            _require(official["acghash_get_hash_as_text"] == canonical, "canonical ACGHash text differs")
            _require(official["acghash_raw_bytes_hex"] == format_bytes(canonical_text_to_official_wire_bytes(canonical)), "official wire bytes differ from canonical conversion")
            _require(official["official_scalar_uint32"] == canonical_text_to_official_native_uint32(canonical), "official native scalar differs from canonical conversion")
            accepted_angle = round(float(accepted["SpawnAngle"]), 6)
            official_angle = round(float(official["rotation_spawn_point"]["rotation_mid_encoded"]), 6)
            angle_matches = accepted_angle == official_angle
            field_differences = [] if angle_matches else [{
                "AcceptedField": "SpawnAngle",
                "AcceptedValue": accepted_angle,
                "OfficialField": "rotation_spawn_point.rotation_mid_encoded",
                "OfficialValue": official_angle,
                "SemanticsClaimedEquivalent": False,
            }]
            reconciliation.append({
                "SourceNpcId": accepted["NpcId"],
                "AcceptedRecordIndex": accepted["_accepted_record_index"],
                "AcceptedDistrict": "NOT_PRESENT_IN_ACCEPTED_SOURCE",
                "AcceptedTemplateHashUInt32": accepted["TemplateHash"],
                "AcceptedTemplateHashHex": f"0x{accepted['TemplateHash']:08X}",
                "AcceptedSourceLittleEndianBytes": format_bytes(accepted["TemplateHash"].to_bytes(4, "little")),
                "CanonicalAcgHashText": canonical,
                "OfficialRecordIndex": official["official_record_index"],
                "OfficialRecordIdentity": official_identity,
                "OfficialDistrictIndex": official["district_index"],
                "OfficialDistrictName": official["district_name"],
                "OfficialRecordOrdinal": official["spawn_index"],
                "OfficialRecordOffset": official["record_relative_offset"],
                "OfficialRecordOffsetHex": official["record_relative_offset_hex"],
                "OfficialWireBytes": official["acghash_raw_bytes_hex"],
                "OfficialNativeUInt32": official["official_scalar_uint32"],
                "OfficialNativeUInt32Hex": official["official_scalar_hex"],
                "FieldMatchStatus": (
                    "ALL_SHARED_MATCH_FIELDS_EQUAL"
                    if angle_matches
                    else "DETERMINISTIC_RECORD_CORRESPONDENCE_WITH_LEGACY_ANGLE_VARIANCE"
                ),
                "ReconciliationState": "EXACT_DUPLICATE_GROUP_MATCH" if group_identity else "EXACT_UNIQUE_MATCH",
                "MatchBasis": MATCH_BASIS
                + (["SpawnAngleEncodedValueCorrespondence"] if angle_matches else [])
                + (["DemonstratedSourceNpcIdOrderToOfficialRecordOrder"] if group_identity else []),
                "FieldDifferences": field_differences,
                "DuplicateGroup": group_identity,
                "ConflictingOfficialCandidates": candidates if group_identity else [],
            })
    reconciliation.sort(key=lambda item: item["SourceNpcId"])

    _require(len(reconciliation) == EXPECTED_ACCEPTED_COUNT, "exact reconciliation did not produce 206 accepted rows")
    _require(len(used_official) == EXPECTED_ACCEPTED_COUNT, "exact reconciliation did not consume 206 distinct official rows")
    _require(len({item["SourceNpcId"] for item in reconciliation}) == EXPECTED_ACCEPTED_COUNT, "SourceNpcId mapping is not one-to-one")
    unmatched_official = [record for record in official_records if record["official_record_identity"] not in used_official]
    _require(len(unmatched_official) == 1, "official source must have exactly one unmatched record")
    _require(unmatched_official[0]["acghash_get_hash_as_text"] == "NCNN", "the unmatched official key must be NCNN")

    active_source_ids = set(normalized["runtimeEligibleNpcIds"])
    ncnn = _build_ncnn_audit(official_records, accepted_keys, source_by_official_identity, active_source_ids)
    _require(
        ncnn["Disposition"] == classify_ncnn(structurally_ordinary=True),
        "NCNN disposition differs from the governed structural rule",
    )
    overlay_records = []
    state_by_identity = {item["OfficialRecordIdentity"]: item["ReconciliationState"] for item in reconciliation}
    for official in official_records:
        identity = official["official_record_identity"]
        source_npc_id = source_by_official_identity.get(identity)
        overlay_records.append({
            "OfficialRecordIdentity": identity,
            "SourceNpcId": source_npc_id,
            "ReconciliationState": state_by_identity.get(identity, "OFFICIAL_RECORD_NOT_PRESENT_IN_ACCEPTED_SOURCE"),
            "OfficialRecordIndex": official["official_record_index"],
            "OfficialDistrictIndex": official["district_index"],
            "OfficialDistrictName": official["district_name"],
            "OfficialRecordOrdinal": official["spawn_index"],
            "CanonicalAcgHashText": official["acghash_get_hash_as_text"],
            "OfficialWireBytes": official["acghash_raw_bytes_hex"],
            "OfficialNativeUInt32": official["official_scalar_uint32"],
            "OfficialNativeUInt32Hex": official["official_scalar_hex"],
            "OfficialFields": {
                key: value
                for key, value in official.items()
                if key not in {"official_record_index", "official_record_identity"}
            },
        })

    input_digests = {
        "docs/reference/pf4582/PlayfieldDistrictInfo.json": sha256_file(accepted_path),
        "docs/generated/pf4582_authoritative_placement_report.json": sha256_file(normalized_report_path),
        "docs/reference/pf4582/official/ep1_18_8_62_pf4582_acghash_records.json": sha256_file(official_records_path),
        "docs/reference/pf4582/official/ep1_18_8_62_pf4582_search_report.json": sha256_file(official_search_report_path),
        "docs/reference/pf4582/official/ep1_18_8_62_pf4582_occurrence_manifest.json": sha256_file(official_occurrence_manifest_path),
        "docs/reference/pf4582/official/ep1_18_8_62_pf4582_evidence_manifest.json": sha256_file(evidence_manifest_path),
    }
    duplicate_groups = [
        {
            "DuplicateGroup": duplicate_ids[signature],
            "AcceptedSourceNpcIds": sorted(record["NpcId"] for record in accepted_groups[signature]),
            "OfficialRecordIdentities": [record["official_record_identity"] for record in sorted(official_groups[signature], key=lambda item: item["official_record_index"])],
            "PairingRule": "SourceNpcId and official record order after global monotonic order preservation was demonstrated on all unique exact-field matches",
        }
        for signature in duplicate_signatures
    ]
    report = {
        "SchemaVersion": 1,
        "Outcome": "EXACT_RECONCILIATION_WITH_ONE_OFFICIAL_ADDITIONAL_RECORD",
        "Metrics": {
            "PF4582_PRIOR_BRIDGE_OUTCOME": "NO_BRIDGE_LOCATED",
            "PF4582_BRIDGE_OUTCOME": "STRUCTURAL_SOURCE_AND_CONSUMER_FOUND",
            "PF4582_PRIOR_OUTCOME_SUPERSEDED": "YES",
            "PF4582_OFFICIAL_BUILD": OFFICIAL_BUILD,
            "PF4582_OFFICIAL_RESOURCE_TYPE": 1000014,
            "PF4582_OFFICIAL_RESOURCE_INSTANCE": 4582,
            "PF4582_OFFICIAL_RESOURCE_RECORDS": 207,
            "PF4582_ACCEPTED_SOURCE_RECORDS": 206,
            "PF4582_OFFICIAL_ADDITIONAL_RECORDS": 1,
            "PF4582_OFFICIAL_ICC_RECORDS": 142,
            "PF4582_OFFICIAL_CENTRAL_ICC_RECORDS": 65,
            "PF4582_OFFICIAL_STRUCTURAL_SOURCE_PROVEN": "YES",
            "PF4582_ACGHASH_OFFICIAL_TYPE_PROVEN": "YES",
            "PF4582_ACGHASH_PARSER_CONSUMER_PROVEN": "YES",
            "PF4582_TERMINAL_IDENTITY_BRIDGE": "UNRESOLVED",
            "PF4582_STATIC_MOB_MAPPINGS_EXTRACTED": 0,
            "PF4582_ACCEPTED_RECORDS_RECONCILED": 206,
            "PF4582_ACCEPTED_RECORDS_UNMATCHED": 0,
            "PF4582_OFFICIAL_RECORDS_UNMATCHED": 1,
            "PF4582_OFFICIAL_EXTRA_KEY": "NCNN",
            "PF4582_NCNN_DISPOSITION": NCNN_DISPOSITION,
            "PF4582_NCNN_SOURCE_NPCID_PRESENT": "NO",
            "PF4582_NCNN_PROFILE_SELECTED": "NO",
            "PF4582_NCNN_RUNTIME_ACTIVE": "NO",
            "PF4582_OFFICIAL_OVERLAY_RECORDS": 207,
            "PF4582_OFFICIAL_OVERLAY_RECONCILED_TO_SOURCE_NPCID": 206,
            "PF4582_OFFICIAL_OVERLAY_WITHOUT_SOURCE_NPCID": 1,
            "PF4582_OFFICIAL_OVERLAY_RUNTIME_CONSUMED": "NO",
            "PF4582_CURRENT_RUNTIME_CATALOG_RECORDS": 206,
            "PF4582_OFFICIAL_RECORDS_PENDING_RUNTIME_INTEGRATION": 1,
            "PF4582_ACCEPTED_JSON_SHA256_MATCH": "YES",
            "PF4582_ACCEPTED_JSON_REWRITTEN": "NO",
            "PF4582_DUAL_ENCODING_KEYS_ROUNDTRIPPED": 38,
            "PF4582_SOURCE_NPCID_STABLE_FOR_AOREBIRTH": "YES",
            "PF4582_SOURCE_NPCID_PROVEN_NATIVE_FUNCOM_FIELD": "NO",
            "PF4582_RUNTIME_ACTIVE_BEFORE": 25,
            "PF4582_RUNTIME_ACTIVE_AFTER": 25,
            "PF4582_CURRENT_RUNTIME_BLOCKED_BEFORE": 181,
            "PF4582_CURRENT_RUNTIME_BLOCKED_AFTER": 181,
            "PF4582_RUNTIME_ACTIVATION_CHANGED": "NO",
        },
        "OfficialSource": manifest,
        "EncodingModel": {
            "OfficialType": "packed four-byte ACGHash_t scalar/tag",
            "LegacyFieldName": "TemplateHash",
            "CanonicalReconciliationKey": "CanonicalAcgHashText",
            "AcceptedSourceUInt32Conversion": "decode uint32 little-endian bytes as four ASCII characters",
            "OfficialWireConversion": "reverse the canonical four ASCII bytes",
            "OfficialNativeUInt32Conversion": "interpret OfficialWireBytes as little-endian uint32",
            "AcceptedAndOfficialNativeScalarsComparedDirectly": False,
            "CimaExample": {
                "CanonicalAcgHashText": "CIMA",
                "AcceptedSourceUInt32": 1095584067,
                "AcceptedSourceHex": "0x414D4943",
                "AcceptedSourceLittleEndianBytes": "43 49 4D 41",
                "OfficialWireBytes": "41 4D 49 43",
                "OfficialNativeUInt32": 1128877377,
                "OfficialNativeUInt32Hex": "0x43494D41",
                "OfficialGetHashAsText": "CIMA",
            },
        },
        "OrderPreservationEvidence": {
            "UniqueExactFieldMatchesEvaluated": len(unique_order_pairs),
            "SourceNpcIdStrictlyIncreasesWithOfficialRecordOrder": True,
            "ExcludedDynamicSourceNpcIds": sorted(dynamic_source_ids),
            "DuplicateGroupPairingAuthorized": True,
        },
        "DuplicateEquivalenceGroups": duplicate_groups,
        "AmbiguousMatches": [],
        "AcceptedRecordsNotFoundOfficially": [],
        "OfficialRecordsNotPresentInAcceptedSource": [_official_summary(record) for record in unmatched_official],
        "ReconciliationRecords": reconciliation,
        "NcnnAudit": ncnn,
        "InputDigests": input_digests,
        "Safety": {
            "OFFICIAL_BINARY_COPIED_TO_AOREBIRTH": "NO",
            "OFFICIAL_BINARY_MODIFIED": "NO",
            "AOSTRIPDOWN_REPOSITORY_MODIFIED": "NO",
            "OFFICIAL_OVERLAY_RUNTIME_CONSUMED": "NO",
            "SOURCE_NPCID_FABRICATED_FOR_NCNN": "NO",
            "PROFILE_FABRICATED_FOR_NCNN": "NO",
            "RUNTIME_ACTIVATION_CHANGED": "NO",
            "ISRE_PROPAGATION_PERFORMED": "NO",
            "UNPROVEN_BEHAVIOR_INVENTED": "NO",
            "LIVE_CLIENT_STARTED": "NO",
            "LIVE_CAPTURE_PERFORMED": "NO",
            "PRODUCTION_OPERATION_PERFORMED": "NO",
            "DATABASE_OPERATION_PERFORMED": "NO",
            "DEPLOYMENT_OPERATION_PERFORMED": "NO",
            "COMMIT_CREATED": "NO",
            "PUSH_PERFORMED": "NO",
        },
        "RequiredFinalInvariants": {
            "PRIOR_STALE_OUTCOME_PRESERVED_AS_HISTORY": "YES",
            "CURRENT_BRIDGE_OUTCOME_CORRECTED": "YES",
            "OFFICIAL_PF4582_SOURCE_PROVEN": "YES",
            "OFFICIAL_PF4582_RECORDS": 207,
            "ACCEPTED_JSON_RECORDS": 206,
            "OFFICIAL_EXTRA_RECORDS": 1,
            "ALL_38_ACCEPTED_KEYS_FOUND_STRUCTURALLY": "YES",
            "ACGHASH_OFFICIAL_TYPE_PROVEN": "YES",
            "ACGHASH_PARSER_CONSUMER_PROVEN": "YES",
            "TERMINAL_MOB_IDENTITY_BRIDGE_PROVEN": "NO",
            "STATIC_MOB_MAPPINGS_EXTRACTED": 0,
            "ACCEPTED_JSON_REWRITTEN": "NO",
            "ALL_206_SOURCE_NPCIDS_RETAINED": "YES",
            "SOURCE_NPCID_CALLED_NATIVE_FUNCOM_ID": "NO",
            "DUAL_ENCODING_MODELED_EXPLICITLY": "YES",
            "NCNN_PRESERVED_AS_OFFICIAL_EVIDENCE": "YES",
            "NCNN_SOURCE_NPCID_FABRICATED": "NO",
            "NCNN_PROFILE_FABRICATED": "NO",
            "NCNN_RUNTIME_ACTIVATED": "NO",
            "NCNN_DISPOSITION_RECORDED": "YES",
            "OFFICIAL_OVERLAY_RECORDS": 207,
            "OFFICIAL_OVERLAY_RUNTIME_CONSUMED": "NO",
            "RUNTIME_ACTIVE_BEFORE": 25,
            "RUNTIME_ACTIVE_AFTER": 25,
            "CURRENT_RUNTIME_BLOCKED_BEFORE": 181,
            "CURRENT_RUNTIME_BLOCKED_AFTER": 181,
            "ACTIVE_NPCID_SET_CHANGED": "NO",
            "RUNTIME_ACTIVATION_CHANGED": "NO",
            "PROJECT_APPROVED_MAPPINGS_CHANGED": "NO",
            "ISRE_PROPAGATION_PERFORMED": "NO",
            "UNPROVEN_BEHAVIOR_INVENTED": "NO",
            "OFFICIAL_BINARY_COPIED": "NO",
            "OFFICIAL_BINARY_MODIFIED": "NO",
            "AOSTRIPDOWN_REPOSITORY_MODIFIED": "NO",
            "LIVE_CLIENT_STARTED": "NO",
            "LIVE_CAPTURE_PERFORMED": "NO",
            "PRODUCTION_OPERATION_PERFORMED": "NO",
            "DATABASE_OPERATION_PERFORMED": "NO",
            "COMMIT_CREATED": "NO",
            "PUSH_PERFORMED": "NO",
        },
    }
    overlay = {
        "SchemaVersion": 1,
        "Purpose": "Official PF4582 structural evidence overlay; not consumed by current runtime activation.",
        "OfficialBuild": OFFICIAL_BUILD,
        "OfficialSourceRecords": 207,
        "ReconciledToSourceNpcId": 206,
        "WithoutSourceNpcId": 1,
        "RuntimeConsumptionStatus": "NOT_CONSUMED",
        "NcnnDisposition": NCNN_DISPOSITION,
        "InputDigests": input_digests,
        "Records": overlay_records,
    }
    return {"Report": report, "Overlay": overlay, "OfficialRecords": official_records}


def render_json(value: dict[str, Any]) -> str:
    return json.dumps(value, indent=2, ensure_ascii=False) + "\n"


def _csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'


def _float_literal(value: Any) -> str:
    text = format(float(value), ".9g")
    if "e" not in text.lower() and "." not in text:
        text += ".0"
    return text + "f"


def render_csharp(model: dict[str, Any]) -> str:
    report = model["Report"]
    source_by_identity = {item["OfficialRecordIdentity"]: item["SourceNpcId"] for item in report["ReconciliationRecords"]}
    lines = [
        "// <auto-generated />",
        "// Generated by Tools/reconcile_pf4582_official_source.py.",
        "// Official structural evidence only; this catalog is not consumed by runtime spawning.",
        "namespace AORebirth.Core.Playfields",
        "{",
        "    internal static partial class IccShuttleportOfficialPlacementCatalog",
        "    {",
        "        internal const int OfficialRecordCount = 207;",
        "        internal const int ReconciledSourceNpcIdCount = 206;",
        "        internal const int WithoutSourceNpcIdCount = 1;",
        f"        internal const string SourceSha256 = \"{EXPECTED_ARTIFACT_SHA256['records']}\";",
        "",
        "        private static IccShuttleportOfficialPlacementRecord[] CreateRecords()",
        "        {",
        "            return new[]",
        "            {",
    ]
    for record in model["OfficialRecords"]:
        point = record["rotation_spawn_point"]
        centre = point["centre"]
        source_id = source_by_identity.get(record["official_record_identity"])
        source_literal = str(source_id) if source_id is not None else "null"
        values = [
            _csharp_string(record["official_record_identity"]),
            source_literal,
            str(record["official_record_index"]),
            str(record["district_index"]),
            _csharp_string(record["district_name"]),
            str(record["spawn_index"]),
            str(record["record_relative_offset"]),
            str(record["database_offset"]),
            _csharp_string(record["acghash_get_hash_as_text"]),
            _csharp_string(record["acghash_raw_bytes_hex"]),
            str(record["official_scalar_uint32"]) + "u",
            _float_literal(centre[0]),
            _float_literal(centre[1]),
            _float_literal(centre[2]),
            _float_literal(point["radius"]),
            str(point["rotation_mid_encoded"]),
            str(point["rotation_width_encoded"]),
            str(record["min_level"]),
            str(record["max_level"]),
            str(record["respawn_chance"]),
            _float_literal(record["respawn_time"]),
            str(record["assistance_radius"]),
            str(record["native_flags"]),
            str(record["more_flags"]),
            str(record["serialized_optional_flags"]),
            str(record["unknown_optional_u8"]),
            str(record["serialized_size"]),
        ]
        lines.append("                new IccShuttleportOfficialPlacementRecord(" + ", ".join(values) + "),")
    lines.extend(["            };", "        }", "    }", "}", ""])
    return "\n".join(lines)


def render_markdown(report: dict[str, Any]) -> str:
    metrics = report["Metrics"]
    ncnn = report["NcnnAudit"]
    resource = report["OfficialSource"]["OfficialResource"]
    lines = [
        "# PF4582 official source reconciliation",
        "",
        "The accepted 206-record AORebirth source reconciles one-to-one to 206 of the 207 official EP1 `HashSpawnPoint_t` records. The unmatched official record is `NCNN`. This is structural evidence only: the official terminal mob identity remains unresolved and runtime activation is unchanged.",
        "",
        "## Required metrics",
        "",
        "```text",
    ]
    lines.extend(f"{key}={value}" for key, value in metrics.items())
    lines.extend([
        "```",
        "",
        "## Official resource",
        "",
        f"Build `{OFFICIAL_BUILD}`; type `{resource['Type']}`; instance `{resource['Instance']}`; offset `{resource['OffsetHex']}`; length `{resource['Length']}`; record SHA-256 `{resource['RecordSha256']}`. Format version `{resource['FormatVersion']}` contains two districts with 142 and 65 records.",
        "",
        "The official native path is `PlayfieldDistrictInfo_t::ReadBlob -> operator>>(DistrictData_t) -> HashSpawnPoint_t::ReadBlob -> operator>>(ACGHash_t)`. `ACGHash_t` is a packed four-byte scalar/tag. The parser and native accessors are proven; no terminal mob-template or dynel identity resolver is proven.",
        "",
        "## Encoding model",
        "",
        "The legacy accepted `TemplateHash` uint32 is decoded as little-endian ASCII to `CanonicalAcgHashText`. Official wire bytes are the reversed canonical bytes, and the official native scalar is those wire bytes interpreted little-endian. Accepted and official native integers are never compared directly. All 38 accepted keys round-trip without collision; the accepted JSON remains byte-identical.",
        "",
        "## Duplicate reconciliation",
        "",
        f"`{len(report['DuplicateEquivalenceGroups'])}` exact duplicate-equivalence groups are retained. SourceNpcId/official-order pairing is used only after monotonic order preservation was demonstrated across `{report['OrderPreservationEvidence']['UniqueExactFieldMatchesEvaluated']}` unique exact-field matches. No record is collapsed or assigned twice.",
        "",
        "## NCNN audit",
        "",
        f"Official identity: `{ncnn['OfficialRecordIdentity']}`; district `{ncnn['OfficialDistrictIndex']}` `{ncnn['OfficialDistrictName']}`; ordinal `{ncnn['OfficialRecordOrdinal']}`; relative offset `{ncnn['OfficialRecordOffsetHex']}`.",
        "",
        f"Position `{ncnn['Position']}`; levels `{ncnn['LevelMinimum']}-{ncnn['LevelMaximum']}`; radius `{ncnn['Radius']}`; encoded rotation `{ncnn['SpawnAngleEncoded']}` width `{ncnn['SpawnAngleWidthEncoded']}`; chance `{ncnn['SpawnChance']}`; time `{ncnn['SpawnTime']}`; native flags `{ncnn['NativeFlags']}`; more flags `{ncnn['MoreFlags']}`; serialized optional flags `{ncnn['SerializedOptionalFlags']}`; unknown optional byte `{ncnn['UnknownOptionalU8']}`; assistance radius `{ncnn['AssistanceRadius']}`; serialized size `{ncnn['SerializedSize']}`.",
        "",
        f"Canonical text `{ncnn['CanonicalAcgHashText']}`; wire bytes `{ncnn['OfficialWireBytes']}`; native scalar `{ncnn['OfficialNativeUInt32Hex']}`. `BossMods`, `Name`, `SpawnPointFlags`, and `SpawnUnknowns` do not exist in the imported official record and are not fabricated.",
        "",
        f"Disposition: `{ncnn['Disposition']}`. {ncnn['DispositionRationale']}",
        "",
        "The disposition records an official blocked placement only. `SourceNpcId` is null, no profile is selected, and runtime activation is unauthorized.",
        "",
        "## Runtime boundary",
        "",
        "The current runtime catalog remains 206 records with 25 active and 181 blocked. The 207-record official overlay is not referenced by `IccShuttleportSpawn`; no candidate mapping or ISRE propagation is performed.",
        "",
    ])
    return "\n".join(lines)


def _write_or_check(path: Path, content: str, check: bool) -> None:
    if check:
        _require(path.is_file(), f"generated artifact is missing: {path}")
        _require(path.read_text(encoding="utf-8") == content, f"generated artifact is stale: {path}")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--accepted-source", type=Path, default=DEFAULT_ACCEPTED_SOURCE)
    parser.add_argument("--normalized-report", type=Path, default=DEFAULT_NORMALIZED_REPORT)
    parser.add_argument("--official-records", type=Path, default=DEFAULT_OFFICIAL_RECORDS)
    parser.add_argument("--official-search-report", type=Path, default=DEFAULT_OFFICIAL_SEARCH_REPORT)
    parser.add_argument("--official-occurrence-manifest", type=Path, default=DEFAULT_OFFICIAL_OCCURRENCE_MANIFEST)
    parser.add_argument("--evidence-manifest", type=Path, default=DEFAULT_EVIDENCE_MANIFEST)
    parser.add_argument("--general-placement-shard", type=Path, default=DEFAULT_GENERAL_PLACEMENT_SHARD)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--overlay", type=Path, default=DEFAULT_OVERLAY)
    parser.add_argument("--csharp", type=Path, default=DEFAULT_CSHARP)
    parser.add_argument("--markdown", type=Path, default=DEFAULT_MARKDOWN)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    try:
        model = build_model(
            args.accepted_source,
            args.normalized_report,
            args.official_records,
            args.official_search_report,
            args.official_occurrence_manifest,
            args.evidence_manifest,
            args.general_placement_shard,
        )
        _write_or_check(args.report, render_json(model["Report"]), args.check)
        _write_or_check(args.overlay, render_json(model["Overlay"]), args.check)
        _write_or_check(args.csharp, render_csharp(model), args.check)
        _write_or_check(args.markdown, render_markdown(model["Report"]), args.check)
        for key, value in model["Report"]["Metrics"].items():
            print(f"{key}={value}")
        return 0
    except (OSError, ReconciliationError) as exc:
        print(f"PF4582 official reconciliation failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
