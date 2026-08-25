#!/usr/bin/env python3
"""Generate the current PF4582 official structural bridge evidence report."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable

import reconcile_pf4582_official_source as official


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = REPOSITORY_ROOT / "docs/reference/pf4582/PlayfieldDistrictInfo.json"
DEFAULT_PRIOR_REPORT = REPOSITORY_ROOT / "docs/generated/pf4582_template_hash_resolution_report.json"
DEFAULT_RUNTIME_SOURCE = REPOSITORY_ROOT / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs"
DEFAULT_EVIDENCE = REPOSITORY_ROOT / "docs/reference/pf4582/template-identity-bridge-evidence.json"
DEFAULT_OFFICIAL_RECORDS = official.DEFAULT_OFFICIAL_RECORDS
DEFAULT_OFFICIAL_SEARCH_REPORT = official.DEFAULT_OFFICIAL_SEARCH_REPORT
DEFAULT_OFFICIAL_EVIDENCE_MANIFEST = official.DEFAULT_EVIDENCE_MANIFEST
DEFAULT_REPORT = REPOSITORY_ROOT / "docs/generated/pf4582_template_identity_bridge_report.json"
DEFAULT_SEARCH_MANIFEST = REPOSITORY_ROOT / "docs/generated/pf4582_template_identity_bridge_search_manifest.json"
DEFAULT_MARKDOWN = REPOSITORY_ROOT / "docs/evidence/PF4582_TEMPLATE_IDENTITY_BRIDGE_DISCOVERY_20260824.md"
DEFAULT_OFFICIAL_RESOURCE_ROOT = None
DEFAULT_OFFICIAL_RUNTIME_ROOT = None

CURRENT_OUTCOME = "STRUCTURAL_SOURCE_AND_CONSUMER_FOUND"
PRIOR_OUTCOME = "NO_BRIDGE_LOCATED"
OUTCOMES = {
    "STATIC_BRIDGE_PROVEN",
    "RUNTIME_CAPTURE_READY",
    "NO_BRIDGE_LOCATED",
    CURRENT_OUTCOME,
}
BRIDGE_STRENGTHS = {
    "DIRECT_STATIC",
    "DIRECT_RUNTIME_READY",
    "CORROBORATING_ONLY",
    "CONTRADICTED",
    "NO_BRIDGE",
}
SEARCH_REPRESENTATIONS = [
    "UNSIGNED_DECIMAL_TEXT",
    "EIGHT_DIGIT_HEX_TEXT",
    "LITTLE_ENDIAN_BYTES",
    "BIG_ENDIAN_BYTES",
    "FOUR_CHARACTER_TEXT",
    "SOURCE_NPCID_WHERE_STRUCTURALLY_USEFUL",
]
BASELINE_TAGS = {
    "OITA", "CNTD", "NERE", "ONRE", "ISRE", "EQVE", "ADAF",
    "SHMG", "ICBI", "CLFI", "CCRI", "OMUI", "BNTO", "ICST",
}
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


class BridgeAnalysisError(ValueError):
    pass


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise BridgeAnalysisError(message)


def _load_json(path: Path) -> Any:
    _require(path.is_file(), f"required input is missing: {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise BridgeAnalysisError(f"could not read governed JSON {path}: {exc}") from exc


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _repository_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPOSITORY_ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def _ordered_unique(values: Iterable[str]) -> list[str]:
    return sorted({value for value in values if value})


def parse_uint32(value: int | str) -> int:
    try:
        parsed = int(value, 0) if isinstance(value, str) else int(value)
    except (TypeError, ValueError) as exc:
        raise BridgeAnalysisError(f"invalid uint32 value: {value!r}") from exc
    _require(0 <= parsed <= 0xFFFFFFFF, f"uint32 value out of range: {value!r}")
    return parsed


def hash_hex(value: int | str) -> str:
    return f"0x{parse_uint32(value):08X}"


def hash_little_endian(value: int | str) -> bytes:
    return parse_uint32(value).to_bytes(4, "little", signed=False)


def hash_big_endian(value: int | str) -> bytes:
    return parse_uint32(value).to_bytes(4, "big", signed=False)


def format_bytes(value: bytes) -> str:
    return " ".join(f"{byte:02X}" for byte in value)


def hash_ascii(value: int | str) -> str:
    parsed = parse_uint32(value)
    try:
        return official.accepted_uint32_to_canonical_text(parsed)
    except official.ReconciliationError as exc:
        raise BridgeAnalysisError(str(exc)) from exc


def roundtrip_hash(value: int | str) -> bool:
    parsed = parse_uint32(value)
    return official.roundtrip_dual_encoding(parsed)


def load_source_records(path: Path) -> list[dict[str, Any]]:
    payload = _load_json(path)
    _require(isinstance(payload, dict) and set(payload) == {"4582"}, "placement source must contain only playfield 4582")
    records = payload["4582"].get("Spawns")
    _require(isinstance(records, list) and len(records) == 206, "placement source must contain exactly 206 spawns")
    npc_ids = []
    for index, record in enumerate(records):
        _require(isinstance(record, dict), f"spawn {index} is not an object")
        for field in ("Name", "NpcId", "TemplateHash", "SpawnHash"):
            _require(field in record, f"spawn {index} is missing {field}")
        npc_id = record["NpcId"]
        _require(isinstance(npc_id, int) and npc_id >= 0, f"spawn {index} has invalid NpcId")
        _require(parse_uint32(record["TemplateHash"]) == parse_uint32(record["SpawnHash"]), f"spawn SourceNpcId {npc_id} has divergent legacy hash fields")
        npc_ids.append(npc_id)
    _require(len(set(npc_ids)) == 206, "source NpcId values must be unique")
    return records


def load_prior_records(path: Path) -> tuple[dict[str, Any], dict[int, dict[str, Any]]]:
    payload = _load_json(path)
    records = payload.get("HashRecords")
    _require(isinstance(records, list) and len(records) == 38, "prior TemplateHash audit must contain exactly 38 HashRecords")
    indexed = {parse_uint32(record.get("TemplateHashOriginal")): record for record in records}
    _require(len(indexed) == 38, "prior TemplateHash audit contains duplicate keys")
    metrics = payload.get("Metrics", {})
    _require(metrics.get("PF4582_RUNTIME_ACTIVE_AFTER") == 25, "prior audit active-placement invariant drifted")
    _require(metrics.get("PF4582_RUNTIME_BLOCKED_AFTER") == 181, "prior audit blocked-placement invariant drifted")
    return payload, indexed


def _validate_source_fingerprint(source: dict[str, Any]) -> None:
    label = source.get("LogicalSourceLabel")
    _require(isinstance(label, str) and label, "official source lacks logical label")
    _require(isinstance(source.get("Sha256"), str) and SHA256_PATTERN.fullmatch(source["Sha256"]) is not None, f"official source {label} lacks SHA-256")
    _require(isinstance(source.get("FileSize"), int) and source["FileSize"] >= 0, f"official source {label} lacks file size")
    _require(source.get("InspectionCompleted") is True, f"official source {label} lacks completed inspection")
    _require(source.get("KeysSearched") == 38, f"official source {label} did not cover all 38 keys")
    _require(source.get("SearchMethods") == SEARCH_REPRESENTATIONS, f"official source {label} search methods drifted")


def validate_evidence(evidence: dict[str, Any], source_keys: set[int]) -> None:
    _require(evidence.get("SchemaVersion") == 2, "bridge evidence schema version must be 2")
    _require(evidence.get("PriorOutcome") == PRIOR_OUTCOME, "prior bridge outcome is not preserved")
    _require(evidence.get("CurrentOutcome") == CURRENT_OUTCOME, "current bridge outcome is invalid")
    _require(evidence.get("Superseded") is True, "prior bridge outcome is not marked superseded")
    _require(evidence.get("SupersessionReason") == "OFFICIAL_EP1_SOURCE_AND_NATIVE_PARSER_CONSUMER_LOCATED", "bridge supersession reason drifted")
    _require(len(source_keys) == 38, "bridge evidence requires 38 accepted ACGHash keys")
    _require(evidence.get("OfficialBuild") == "18.8.62_EP1", "official build drifted")
    _require(evidence.get("OfficialResourceType") == 1000014, "official resource type drifted")
    _require(evidence.get("OfficialResourceInstance") == 4582, "official resource instance drifted")
    _require(evidence.get("OfficialRecordCount") == 207, "official record count drifted")
    _require(evidence.get("AcceptedRecordCount") == 206, "accepted record count drifted")
    _require(evidence.get("AdditionalOfficialKeys") == ["NCNN"], "additional official key drifted")
    _require(evidence.get("AcgHashOfficialType") == "PACKED_FOUR_BYTE_ACGHASH_SCALAR_TAG", "official ACGHash type drifted")
    _require(evidence.get("OfficialStructuralSourceStatus") == "PROVEN", "official structural source is not proven")
    _require(evidence.get("OfficialAcgHashTypeStatus") == "PROVEN", "official ACGHash type is not proven")
    _require(evidence.get("OfficialParserConsumerStatus") == "PROVEN", "official parser consumer is not proven")
    _require(evidence.get("TerminalIdentityStatus") == "UNRESOLVED", "terminal identity must remain unresolved")
    _require(evidence.get("StaticMappingsExtracted") == 0, "static mob mappings must remain zero")
    _require(evidence.get("RuntimeJoinStatus") == "UNRESOLVED", "runtime join must remain unresolved")
    claims = evidence.get("Claims", {})
    _require(claims.get("OfficialStructuralSourceFound") is True, "structural source claim missing")
    _require(claims.get("OfficialParserConsumerFound") is True, "parser consumer claim missing")
    _require(claims.get("StaticTerminalIdentityFound") is False, "terminal identity is overstated")
    _require(claims.get("DirectStaticBridge") is False and claims.get("DirectRuntimeReadyBridge") is False, "terminal bridge is overstated")
    _require(claims.get("RuntimeCaptureImplemented") is False, "runtime capture is overstated")
    _require(bool(evidence.get("MissingEvidence")), "unresolved terminal identity requires blockers")
    inputs = evidence.get("InputDigests")
    _require(isinstance(inputs, dict) and len(inputs) == 6, "governed input digest set is incomplete")
    _require(all(isinstance(value, str) and SHA256_PATTERN.fullmatch(value) for value in inputs.values()), "governed input digest is invalid")
    sources = evidence.get("OfficialSources")
    _require(isinstance(sources, list) and len(sources) == 3, "three imported structured sources are required")
    labels = set()
    for source in sources:
        _validate_source_fingerprint(source)
        _require(source["LogicalSourceLabel"] not in labels, "official source labels must be unique")
        labels.add(source["LogicalSourceLabel"])


def verify_governed_inputs(evidence: dict[str, Any], inputs: Iterable[Path]) -> None:
    expected = evidence["InputDigests"]
    for path in inputs:
        label = _repository_path(path)
        _require(label in expected, f"evidence ledger does not pin {label}")
        _require(sha256_file(path) == expected[label], f"governed input digest drifted: {label}")


def verify_official_sources(evidence: dict[str, Any], official_roots: dict[str, Path] | None = None) -> None:
    del official_roots
    for source in evidence["OfficialSources"]:
        path = REPOSITORY_ROOT / source["RelativePath"]
        _require(path.is_file(), f"imported official evidence is missing: {source['LogicalSourceLabel']}")
        _require(path.stat().st_size == source["FileSize"], f"imported official evidence size drifted: {source['LogicalSourceLabel']}")
        _require(sha256_file(path) == source["Sha256"], f"imported official evidence digest drifted: {source['LogicalSourceLabel']}")


def evidence_strength(
    *,
    official: bool,
    direct_source_key: bool,
    consumer: bool,
    terminal_identity: bool,
    same_runtime_context: bool = False,
    capture_implemented: bool = False,
    contradicted: bool = False,
) -> str:
    if contradicted:
        return "CONTRADICTED"
    if not official:
        return "CORROBORATING_ONLY"
    if direct_source_key and consumer and terminal_identity:
        return "DIRECT_STATIC"
    if direct_source_key and terminal_identity and same_runtime_context and capture_implemented:
        return "DIRECT_RUNTIME_READY"
    return "CORROBORATING_ONLY"


def _propagation_scope(prior: dict[str, Any]) -> str:
    if prior.get("DynamicNamesPresent"):
        return "DYNAMIC_OR_VARIANT"
    if prior.get("BaselineMappingState") == "MAPPED":
        return "NPCID_SPECIFIC"
    return "PROPAGATION_UNPROVEN"


def _contradictions(prior: dict[str, Any]) -> list[Any]:
    values = prior.get("ContradictoryEvidence", [])
    return values if isinstance(values, list) else [values]


def _public_official_source(source: dict[str, Any]) -> dict[str, Any]:
    return dict(source)


def build_model(
    source_path: Path = DEFAULT_SOURCE,
    prior_report_path: Path = DEFAULT_PRIOR_REPORT,
    runtime_source_path: Path = DEFAULT_RUNTIME_SOURCE,
    evidence_path: Path = DEFAULT_EVIDENCE,
    official_resource_root: Path | None = DEFAULT_OFFICIAL_RESOURCE_ROOT,
    official_runtime_root: Path | None = DEFAULT_OFFICIAL_RUNTIME_ROOT,
    verify_official: bool = True,
    official_records_path: Path = DEFAULT_OFFICIAL_RECORDS,
    official_search_report_path: Path = DEFAULT_OFFICIAL_SEARCH_REPORT,
    official_evidence_manifest_path: Path = DEFAULT_OFFICIAL_EVIDENCE_MANIFEST,
) -> dict[str, Any]:
    del official_resource_root, official_runtime_root, verify_official
    source_records = load_source_records(source_path)
    prior_payload, prior_records = load_prior_records(prior_report_path)
    source_groups: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for record in source_records:
        source_groups[parse_uint32(record["TemplateHash"])].append(record)
    _require(len(source_groups) == 38 and set(source_groups) == set(prior_records), "source and prior-audit ACGHash keys differ")

    evidence = _load_json(evidence_path)
    validate_evidence(evidence, set(source_groups))
    verify_governed_inputs(
        evidence,
        (
            source_path,
            prior_report_path,
            runtime_source_path,
            official_records_path,
            official_evidence_manifest_path,
            official_search_report_path,
        ),
    )
    verify_official_sources(evidence)

    official_payload = _load_json(official_records_path)
    try:
        official_records = official._flatten_official(official_payload)
    except official.ReconciliationError as exc:
        raise BridgeAnalysisError(str(exc)) from exc
    official_groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for record in official_records:
        if record.get("accepted_manifest_match") is True:
            official_groups[record["acghash_get_hash_as_text"]].append(record)
    _require(len(official_groups) == 38, "official source does not structurally contain all 38 accepted keys")
    _require(sum(len(records) for records in official_groups.values()) == 206, "official accepted-key occurrence count must be 206")

    search_report = _load_json(official_search_report_path)
    search_metrics = search_report.get("metrics", {})
    _require(search_metrics.get("PF4582_ACGHASH_SEARCH_OUTCOME") == CURRENT_OUTCOME, "official search report outcome drifted")
    _require(search_metrics.get("PF4582_KEYS_REACHING_TERMINAL_IDENTITY") == 0, "official search report overstates terminal identity")
    _require(search_metrics.get("PF4582_STATIC_MAPPINGS_EXTRACTED") == 0, "official search report overstates static mappings")

    evidence_labels = sorted(source["LogicalSourceLabel"] for source in evidence["OfficialSources"])
    evidence_digests = {source["LogicalSourceLabel"]: source["Sha256"] for source in evidence["OfficialSources"]}
    function_records = sorted(evidence["NativeFunctions"], key=lambda item: (item["Module"], int(item["Rva"], 16), item["Function"]))
    function_labels = [f"{item['Module']}!{item['Function']} RVA {item['Rva']}" for item in function_records]

    hash_records = []
    per_key_results = []
    for key in sorted(source_groups, key=lambda value: hash_ascii(value)):
        source_group = source_groups[key]
        prior = prior_records[key]
        canonical = hash_ascii(key)
        occurrences = sorted(official_groups[canonical], key=lambda item: item["official_record_index"])
        _require(len(occurrences) == len(source_group), f"official occurrence count differs for {canonical}")
        official_native = official.canonical_text_to_official_native_uint32(canonical)
        official_wire = format_bytes(official.canonical_text_to_official_wire_bytes(canonical))
        _require(all(item["official_scalar_uint32"] == official_native for item in occurrences), f"official native scalar differs for {canonical}")
        _require(all(item["acghash_raw_bytes_hex"] == official_wire for item in occurrences), f"official wire bytes differ for {canonical}")
        structural = [
            {
                "OfficialRecordIdentity": item["official_record_identity"],
                "OfficialDistrictIndex": item["district_index"],
                "OfficialDistrictName": item["district_name"],
                "OfficialRecordOrdinal": item["spawn_index"],
                "OfficialRecordOffset": item["record_relative_offset_hex"],
                "ConsumerStatus": "OFFICIAL_PARSER_AND_NATIVE_ACCESSOR_PROVEN_TERMINAL_IDENTITY_UNRESOLVED",
            }
            for item in occurrences
        ]
        blockers = _ordered_unique(list(prior.get("RemainingBlockers", [])) + list(evidence["MissingEvidence"]))
        hash_records.append({
            "LegacyTemplateHash": key,
            "AcceptedSourceUInt32": key,
            "AcceptedSourceHex": hash_hex(key),
            "AcceptedSourceLittleEndianBytes": format_bytes(hash_little_endian(key)),
            "CanonicalAcgHashText": canonical,
            "OfficialWireBytes": official_wire,
            "OfficialNativeUInt32": official_native,
            "OfficialNativeUInt32Hex": f"0x{official_native:08X}",
            "OfficialGetHashAsText": canonical,
            "TemplateHashOriginal": str(key),
            "TemplateHashUInt32": key,
            "TemplateHashHex": hash_hex(key),
            "TemplateHashLittleEndianBytes": format_bytes(hash_little_endian(key)),
            "TemplateHashBigEndianBytes": format_bytes(hash_big_endian(key)),
            "TemplateHashAscii": canonical,
            "PlacementCount": len(source_group),
            "NpcIds": sorted(record["NpcId"] for record in source_group),
            "SourceNames": sorted({record["Name"] for record in source_group}),
            "DynamicNamesPresent": list(prior.get("DynamicNamesPresent", [])),
            "BaselineState": prior.get("BaselineMappingState", "UNRESOLVED"),
            "PriorAuditClassification": prior.get("Classification", "NO_EVIDENCE"),
            "OfficialOccurrences": structural,
            "OfficialLookupRecords": [],
            "StaticTerminalIdentities": [],
            "RuntimeFieldLocated": True,
            "RuntimeNpcIdLocated": False,
            "CandidateAoRebirthProfiles": list(prior.get("CandidateAoRebirthProfiles", [])),
            "DirectBridgeStatus": "STRUCTURAL_SOURCE_AND_PARSER_CONSUMER_ONLY",
            "PropagationScope": _propagation_scope(prior),
            "NewProofClassification": "OFFICIAL_STRUCTURAL_SOURCE_AND_PARSER_CONSUMER",
            "EvidenceSources": evidence_labels,
            "EvidenceOffsets": sorted({item["record_relative_offset_hex"] for item in occurrences} | {evidence["SerializedFieldOffset"], evidence["ParsedFieldOffset"]}),
            "EvidenceFunctions": function_labels,
            "EvidenceDigests": evidence_digests,
            "Contradictions": _contradictions(prior),
            "RemainingBlockers": blockers,
            "RuntimeActivationAllowed": False,
        })
        per_key_results.append({
            "CanonicalAcgHashText": canonical,
            "AcceptedSourceUInt32": key,
            "OfficialWireBytes": official_wire,
            "OfficialNativeUInt32": official_native,
            "SearchMethods": SEARCH_REPRESENTATIONS,
            "OfficialRecordCount": len(occurrences),
            "OfficialRecordIdentities": [item["official_record_identity"] for item in occurrences],
            "ConsumerStatus": "PARSER_PROVEN_TERMINAL_IDENTITY_UNRESOLVED",
        })

    _require(sum(item["PlacementCount"] for item in hash_records) == 206, "per-key placement accounting drifted")
    _require(len({npc_id for item in hash_records for npc_id in item["NpcIds"]}) == 206, "SourceNpcId accounting drifted")
    _require(all(roundtrip_hash(item["AcceptedSourceUInt32"]) for item in hash_records), "dual encoding roundtrip failed")
    _require({item["CanonicalAcgHashText"] for item in hash_records if item["BaselineState"] == "MAPPED"} == BASELINE_TAGS, "baseline mapped key set drifted")

    metrics = {
        "PF4582_PRIOR_BRIDGE_OUTCOME": PRIOR_OUTCOME,
        "PF4582_BRIDGE_OUTCOME": CURRENT_OUTCOME,
        "PF4582_PRIOR_OUTCOME_SUPERSEDED": "YES",
        "PF4582_SUPERSESSION_REASON": evidence["SupersessionReason"],
        "PF4582_OFFICIAL_BUILD": evidence["OfficialBuild"],
        "PF4582_OFFICIAL_RESOURCE_TYPE": evidence["OfficialResourceType"],
        "PF4582_OFFICIAL_RESOURCE_INSTANCE": evidence["OfficialResourceInstance"],
        "PF4582_OFFICIAL_RESOURCE_RECORDS": 207,
        "PF4582_ACCEPTED_SOURCE_RECORDS": 206,
        "PF4582_OFFICIAL_ADDITIONAL_RECORDS": 1,
        "PF4582_OFFICIAL_STRUCTURAL_SOURCE_PROVEN": "YES",
        "PF4582_ACGHASH_OFFICIAL_TYPE_PROVEN": "YES",
        "PF4582_ACGHASH_PARSER_CONSUMER_PROVEN": "YES",
        "PF4582_TERMINAL_IDENTITY_BRIDGE": "UNRESOLVED",
        "PF4582_STATIC_MOB_MAPPINGS_EXTRACTED": 0,
        "PF4582_SOURCE_PLACEMENTS": 206,
        "PF4582_TEMPLATE_KEYS_TOTAL": 38,
        "PF4582_TEMPLATE_KEYS_ROUNDTRIP": 38,
        "PF4582_SOURCE_NPC_IDS": 206,
        "PF4582_SOURCE_NPCID_STABLE_FOR_AOREBIRTH": "YES",
        "PF4582_SOURCE_NPCID_PROVEN_NATIVE_FUNCOM_FIELD": "NO",
        "PF4582_TEMPLATE_FIELD_OFFICIAL_NAME_PROVEN": "NO",
        "PF4582_STRUCTURAL_KEY_OCCURRENCES": 206,
        "PF4582_FALSE_POSITIVE_OCCURRENCES_REJECTED": search_metrics["PF4582_FALSE_POSITIVE_HITS_REJECTED"],
        "PF4582_STATIC_PARSER_FOUND": "YES",
        "PF4582_STATIC_LOOKUP_CONSUMER_FOUND": "NO",
        "PF4582_STATIC_TERMINAL_IDENTITY_FOUND": "NO",
        "PF4582_STATIC_BRIDGED_HASHES": 0,
        "PF4582_STATIC_BRIDGED_NPC_IDS": 0,
        "PF4582_STATIC_BASELINE_MATCH": 0,
        "PF4582_STATIC_BASELINE_PARTIAL": 14,
        "PF4582_STATIC_BASELINE_CONFLICT": 0,
        "PF4582_STATIC_BASELINE_NOT_REACHED": 0,
        "PF4582_RUNTIME_TEMPLATE_FIELD_FOUND": "YES",
        "PF4582_RUNTIME_NPCID_FIELD_FOUND": "NO",
        "PF4582_RUNTIME_DYNEL_JOIN_FOUND": "NO",
        "PF4582_RUNTIME_CAPTURE_IMPLEMENTED": "NO",
        "PF4582_RUNTIME_CAPTURE_READY": "NO",
        "PF4582_RUNTIME_CAPTURE_LIVE_VALIDATED": "NO",
        "PF4582_NEW_DIRECT_HASH_BRIDGES": 0,
        "PF4582_NEW_DIRECT_NPCID_BRIDGES": 0,
        "PF4582_NEWLY_PROVEN_PROFILE_IDENTITIES": 0,
        "PF4582_SAME_HASH_PROPAGATION_PROVEN": "NO",
        "PF4582_ISRE_BLOCKED_PROPAGATION_PROVEN": "NO",
        "PF4582_RUNTIME_ACTIVE_BEFORE": 25,
        "PF4582_RUNTIME_ACTIVE_AFTER": 25,
        "PF4582_RUNTIME_BLOCKED_BEFORE": 181,
        "PF4582_RUNTIME_BLOCKED_AFTER": 181,
        "PF4582_RUNTIME_ACTIVATION_CHANGED": "NO",
    }
    status = {
        "OfficialStructuralSourceStatus": "PROVEN",
        "OfficialAcgHashTypeStatus": "PROVEN",
        "OfficialParserConsumerStatus": "PROVEN",
        "TerminalMobIdentityStatus": "UNRESOLVED",
        "StaticMobMappingsExtracted": 0,
        "RuntimeHashToDynelJoinStatus": "UNRESOLVED",
    }
    safety = {
        "OFFICIAL_BINARY_COPIED_TO_AOREBIRTH": "NO",
        "OFFICIAL_BINARY_MODIFIED": "NO",
        "NAME_ONLY_JOIN_ACCEPTED": "NO",
        "COORDINATE_JOIN_ACCEPTED": "NO",
        "LEVEL_ONLY_JOIN_ACCEPTED": "NO",
        "TERMINAL_IDENTITY_INFERRED": "NO",
        "RUNTIME_ACTIVATION_CHANGED": "NO",
        "LIVE_CLIENT_STARTED": "NO",
        "LIVE_CAPTURE_PERFORMED": "NO",
        "PRODUCTION_OPERATION_PERFORMED": "NO",
        "DATABASE_OPERATION_PERFORMED": "NO",
        "COMMIT_CREATED": "NO",
        "PUSH_PERFORMED": "NO",
    }
    source_provenance = {
        "Classification": "OFFICIAL_RESOURCE_EXTRACT",
        "OriginalOfficialInputFound": "YES",
        "ExtractionToolFound": "YES",
        "DeliveredDatasetSha256": sha256_file(source_path),
        "OfficialRecordSnapshotSha256": sha256_file(official_records_path),
        "Conclusion": "Later official EP1 research located and parsed the exact PF4582 type-1000014 instance-4582 resource. The 207-record HashSpawnPoint_t structure, packed ACGHash_t field, parser, native field location, vector, and accessors are proven. No terminal mob identity mapping was extracted.",
    }
    official_semantics = {
        "TemplateFieldOfficialNameProven": "NO",
        "LegacyAoRebirthFieldName": "TemplateHash",
        "OfficialType": "packed four-byte ACGHash_t scalar/tag",
        "TemplateFieldSemantics": "OFFICIAL_PACKED_ACGHASH_SCALAR_TAG_TERMINAL_IDENTITY_UNRESOLVED",
        "TemplateFieldByteOrder": "ACCEPTED_UINT32_LITTLE_ENDIAN_ASCII_CANONICAL_TEXT_OFFICIAL_WIRE_REVERSED_NATIVE_UINT32",
        "NpcIdSemantics": "STABLE_AOREBIRTH_SOURCE_PLACEMENT_KEY_NOT_PROVEN_NATIVE_FUNCOM_FIELD",
        "SerializedFieldOffset": evidence["SerializedFieldOffset"],
        "ParsedFieldOffset": evidence["ParsedFieldOffset"],
        "Conclusion": "ACGHash_t is an official packed four-byte scalar/tag, not a cryptographic hash or a proven mob-template, resource, visual, or terminal runtime identity. TemplateHash remains only as a legacy AORebirth field name.",
    }
    report = {
        "SchemaVersion": 2,
        "PriorOutcome": PRIOR_OUTCOME,
        "Outcome": CURRENT_OUTCOME,
        "PriorOutcomeSuperseded": True,
        "SupersessionReason": evidence["SupersessionReason"],
        **status,
        "Metrics": metrics,
        "Safety": safety,
        "RequiredFinalInvariants": {
            "PRIOR_STALE_OUTCOME_PRESERVED_AS_HISTORY": "YES",
            "CURRENT_BRIDGE_OUTCOME_CORRECTED": "YES",
            "ALL_38_ACCEPTED_KEYS_FOUND_STRUCTURALLY": "YES",
            "ACGHASH_PARSER_CONSUMER_PROVEN": "YES",
            "TERMINAL_MOB_IDENTITY_BRIDGE_PROVEN": "NO",
            "STATIC_MOB_MAPPINGS_EXTRACTED": 0,
            "RUNTIME_ACTIVATION_CHANGED": "NO",
            "UNPROVEN_BEHAVIOR_INVENTED": "NO",
        },
        "SourceProvenance": source_provenance,
        "OfficialSemantics": official_semantics,
        "OfficialBuilds": [{"BuildLabel": "OFFICIAL_EP1", "ProductBuildVersion": "18.8.62_EP1"}],
        "OfficialSources": [_public_official_source(source) for source in evidence["OfficialSources"]],
        "SearchCoverageSummary": {
            "OfficialFilesInspected": search_metrics.get("PF4582_OFFICIAL_FILES_INSPECTED"),
            "OfficialBytesScanned": search_metrics.get("PF4582_OFFICIAL_BYTES_SCANNED"),
            "KeysFoundStructurally": 38,
            "KeysReachingTerminalIdentity": 0,
        },
        "StaticBridgeAnalysis": {
            "Conclusion": "The official EP1 parser consumes PF4582 HashSpawnPoint_t and its ACGHash_t field. This is a structural parser/native consumer, not a terminal mob identity resolver.",
            "Chain": [
                "ResourceDatabase.dat type 1000014 / instance 4582",
                "GameData.dll!PlayfieldDistrictInfo_t::ReadBlob RVA 0x9DEF",
                "GameData.dll!operator>>(DistrictData_t) RVA 0x49BE",
                "GameData.dll!HashSpawnPoint_t::ReadBlob RVA 0x640F",
                "GameData.dll!operator>>(ACGHash_t) RVA 0x1B23",
                "HashSpawnPoint_t parsed ACGHash_t field +0x24",
                "DistrictData_t hash-spawn vector +0x5C",
                "GameData.dll!HashSpawnPoint_t::GetHash RVA 0x4459",
                "GameData.dll!DistrictData_t::GetHashSpawnPoints RVA 0x44F0",
                "terminal mob identity unresolved",
            ],
        },
        "RuntimeBridgeAnalysis": {
            "Conclusion": "No same-context official ACGHash_t-to-dynel or ACGHash_t-to-MonsterData join is proven; runtime capture is not ready.",
            "Chain": ["official structural record", "official parser/native accessor", "terminal identity unresolved"],
        },
        "StructureEvidence": evidence["NativeFunctions"],
        "BaselineControls": [
            {
                "CanonicalAcgHashText": item["CanonicalAcgHashText"],
                "Result": "STATIC_BASELINE_PARTIAL",
                "Reason": "Official structural source and parser consumer proven; terminal identity unresolved.",
            }
            for item in hash_records if item["BaselineState"] == "MAPPED"
        ],
        "SameHashPropagation": {
            "Conclusion": "The official structural parser path does not prove global, PF4582-wide, or same-hash mob identity propagation. Existing SourceNpcId-specific runtime authority is unchanged."
        },
        "DeadEnds": [
            "GetHash and GetHashSpawnPoints expose parsed structural values but do not return terminal mob identities.",
            "Printable four-byte text, names, coordinates, levels, and candidate MonsterData remain non-terminal evidence.",
        ],
        "MissingEvidence": evidence["MissingEvidence"],
        "RequiredNextEvidence": evidence["RequiredNextEvidence"],
        "HashRecords": hash_records,
        "InputDigests": evidence["InputDigests"],
        "PriorAuditMetrics": prior_payload["Metrics"],
    }
    search_manifest = {
        "SchemaVersion": 2,
        "PriorOutcome": PRIOR_OUTCOME,
        "Outcome": CURRENT_OUTCOME,
        "PriorOutcomeSuperseded": True,
        "SearchMethods": SEARCH_REPRESENTATIONS,
        "KeysSearched": [
            {
                "CanonicalAcgHashText": item["CanonicalAcgHashText"],
                "AcceptedSourceUInt32": item["AcceptedSourceUInt32"],
                "OfficialWireBytes": item["OfficialWireBytes"],
                "OfficialNativeUInt32": item["OfficialNativeUInt32"],
                "NpcIds": item["NpcIds"],
            }
            for item in hash_records
        ],
        "OfficialSources": report["OfficialSources"],
        "PerKeyResults": per_key_results,
        "StructuralOccurrencesRetained": 206,
        "OfficialRecordCount": 207,
        "AdditionalOfficialKey": "NCNN",
        "FalsePositiveOccurrencesRejected": metrics["PF4582_FALSE_POSITIVE_OCCURRENCES_REJECTED"],
        "ParserConsumerReferences": function_labels,
        "CoverageLimitations": evidence["MissingEvidence"],
        "InputDigests": evidence["InputDigests"],
    }
    return {"Report": report, "SearchManifest": search_manifest}


def render_json(value: dict[str, Any]) -> str:
    return json.dumps(value, indent=2, ensure_ascii=False) + "\n"


def render_markdown(report: dict[str, Any]) -> str:
    metrics = report["Metrics"]
    lines = [
        "# PF4582 official ACGHash structural bridge discovery",
        "",
        f"Current outcome: `{report['Outcome']}`.",
        "",
        f"Historical outcome `{report['PriorOutcome']}` is preserved and explicitly superseded because the later official EP1 investigation located the source resource, 207-record `HashSpawnPoint_t` structure, packed `ACGHash_t` field, parser, native storage, vector, and accessors.",
        "",
        "The correction is structural only. The terminal mob identity, static mob mappings, and same-context runtime dynel join remain unresolved. This report activates no placement.",
        "",
        "## Required metrics",
        "",
        "```text",
    ]
    lines.extend(f"{key}={value}" for key, value in metrics.items())
    lines.extend([
        "```",
        "",
        "## Official semantics",
        "",
        report["OfficialSemantics"]["Conclusion"],
        "",
        "The accepted legacy integer is decoded from little-endian bytes to canonical four-character text. Official wire bytes reverse those canonical bytes, and the official native scalar interprets the wire bytes as little-endian. The accepted integer and official native integer are not compared directly.",
        "",
        "## Official parser and native accessor path",
        "",
        report["StaticBridgeAnalysis"]["Conclusion"],
        "",
        "```text",
    ])
    lines.extend(report["StaticBridgeAnalysis"]["Chain"])
    lines.extend([
        "```",
        "",
        "## Status boundary",
        "",
        "```text",
        f"OfficialStructuralSourceStatus={report['OfficialStructuralSourceStatus']}",
        f"OfficialAcgHashTypeStatus={report['OfficialAcgHashTypeStatus']}",
        f"OfficialParserConsumerStatus={report['OfficialParserConsumerStatus']}",
        f"TerminalMobIdentityStatus={report['TerminalMobIdentityStatus']}",
        f"StaticMobMappingsExtracted={report['StaticMobMappingsExtracted']}",
        f"RuntimeHashToDynelJoinStatus={report['RuntimeHashToDynelJoinStatus']}",
        "```",
        "",
        "## Per-key structural result",
        "",
        "| Canonical ACGHash | Accepted uint32 | Accepted LE bytes | Official wire | Official native | Placements | Terminal identity |",
        "|---|---:|---|---|---|---:|---|",
    ])
    for item in report["HashRecords"]:
        lines.append(
            f"| `{item['CanonicalAcgHashText']}` | {item['AcceptedSourceUInt32']} | `{item['AcceptedSourceLittleEndianBytes']}` | `{item['OfficialWireBytes']}` | `{item['OfficialNativeUInt32Hex']}` | {item['PlacementCount']} | unresolved |"
        )
    lines.extend(["", "## Exact evidence still required", ""])
    lines.extend(f"- {item}" for item in report["RequiredNextEvidence"])
    lines.extend(["", "## Safety invariants", "", "```text"])
    lines.extend(f"{key}={value}" for key, value in report["Safety"].items())
    lines.extend(["```", ""])
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
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--prior-report", type=Path, default=DEFAULT_PRIOR_REPORT)
    parser.add_argument("--runtime-source", type=Path, default=DEFAULT_RUNTIME_SOURCE)
    parser.add_argument("--evidence", type=Path, default=DEFAULT_EVIDENCE)
    parser.add_argument("--official-records", type=Path, default=DEFAULT_OFFICIAL_RECORDS)
    parser.add_argument("--official-search-report", type=Path, default=DEFAULT_OFFICIAL_SEARCH_REPORT)
    parser.add_argument("--official-evidence-manifest", type=Path, default=DEFAULT_OFFICIAL_EVIDENCE_MANIFEST)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--search-manifest", type=Path, default=DEFAULT_SEARCH_MANIFEST)
    parser.add_argument("--markdown", type=Path, default=DEFAULT_MARKDOWN)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    try:
        model = build_model(
            source_path=args.source,
            prior_report_path=args.prior_report,
            runtime_source_path=args.runtime_source,
            evidence_path=args.evidence,
            official_records_path=args.official_records,
            official_search_report_path=args.official_search_report,
            official_evidence_manifest_path=args.official_evidence_manifest,
        )
        _write_or_check(args.report, render_json(model["Report"]), args.check)
        _write_or_check(args.search_manifest, render_json(model["SearchManifest"]), args.check)
        _write_or_check(args.markdown, render_markdown(model["Report"]), args.check)
        for key, value in model["Report"]["Metrics"].items():
            print(f"{key}={value}")
        return 0
    except (BridgeAnalysisError, OSError) as exc:
        print(f"PF4582 bridge analysis failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
