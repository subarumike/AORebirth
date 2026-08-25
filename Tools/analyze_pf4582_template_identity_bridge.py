#!/usr/bin/env python3
"""Deterministic PF4582 official template-identity bridge analysis.

This tool deliberately separates official structural evidence from identity
bridge proof.  It never promotes a placement and never treats a printable
four-byte value, a name, a coordinate, a level, or MonsterData as a direct
join.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = REPOSITORY_ROOT / "docs/reference/pf4582/PlayfieldDistrictInfo.json"
DEFAULT_PRIOR_REPORT = (
    REPOSITORY_ROOT / "docs/generated/pf4582_template_hash_resolution_report.json"
)
DEFAULT_RUNTIME_SOURCE = (
    REPOSITORY_ROOT
    / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs"
)
DEFAULT_EVIDENCE = (
    REPOSITORY_ROOT / "docs/reference/pf4582/template-identity-bridge-evidence.json"
)
DEFAULT_REPORT = (
    REPOSITORY_ROOT / "docs/generated/pf4582_template_identity_bridge_report.json"
)
DEFAULT_SEARCH_MANIFEST = (
    REPOSITORY_ROOT
    / "docs/generated/pf4582_template_identity_bridge_search_manifest.json"
)
DEFAULT_MARKDOWN = (
    REPOSITORY_ROOT
    / "docs/evidence/PF4582_TEMPLATE_IDENTITY_BRIDGE_DISCOVERY_20260824.md"
)
DEFAULT_OFFICIAL_RESOURCE_ROOT = Path(r"D:\Funcom\Anarchy Online")
DEFAULT_OFFICIAL_RUNTIME_ROOT = Path(r"C:\Funcom\Anarchy Online")

OUTCOMES = {
    "STATIC_BRIDGE_PROVEN",
    "RUNTIME_CAPTURE_READY",
    "NO_BRIDGE_LOCATED",
}
BRIDGE_STRENGTHS = {
    "DIRECT_STATIC",
    "DIRECT_RUNTIME_READY",
    "CORROBORATING_ONLY",
    "CONTRADICTED",
    "NO_BRIDGE",
}
PROPAGATION_SCOPES = {
    "GLOBAL_HASH_UNIQUE",
    "PF4582_HASH_UNIQUE",
    "NPCID_SPECIFIC",
    "DYNAMIC_OR_VARIANT",
    "PROPAGATION_UNPROVEN",
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
    "OITA",
    "CNTD",
    "NERE",
    "ONRE",
    "ISRE",
    "EQVE",
    "ADAF",
    "SHMG",
    "ICBI",
    "CLFI",
    "CCRI",
    "OMUI",
    "BNTO",
    "ICST",
}
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


class BridgeAnalysisError(ValueError):
    """Raised when governed bridge evidence is incomplete or contradictory."""


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
    raw = hash_little_endian(value)
    _require(all(0x20 <= byte <= 0x7E for byte in raw),
             f"template hash is not printable ASCII: {hash_hex(value)}")
    return raw.decode("ascii")


def roundtrip_hash(value: int | str) -> bool:
    parsed = parse_uint32(value)
    return (
        int(hash_hex(parsed), 16) == parsed
        and int.from_bytes(hash_little_endian(parsed), "little") == parsed
        and int.from_bytes(hash_big_endian(parsed), "big") == parsed
        and int.from_bytes(hash_ascii(parsed).encode("ascii"), "little") == parsed
    )


def pe_metadata(path: Path) -> dict[str, Any]:
    """Read the stable PE timestamp and image base without third-party packages."""
    data = path.read_bytes()
    _require(len(data) >= 0x40 and data[:2] == b"MZ", f"not a PE file: {path}")
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    _require(pe_offset + 0x38 <= len(data), f"truncated PE header: {path}")
    _require(data[pe_offset:pe_offset + 4] == b"PE\0\0", f"invalid PE signature: {path}")
    timestamp = struct.unpack_from("<I", data, pe_offset + 8)[0]
    optional_offset = pe_offset + 24
    magic = struct.unpack_from("<H", data, optional_offset)[0]
    if magic == 0x10B:
        image_base = struct.unpack_from("<I", data, optional_offset + 28)[0]
    elif magic == 0x20B:
        image_base = struct.unpack_from("<Q", data, optional_offset + 24)[0]
    else:
        raise BridgeAnalysisError(f"unsupported PE optional-header magic in {path}")
    return {
        "PeTimestampHex": f"0x{timestamp:08x}",
        "ImageBaseHex": f"0x{image_base:x}",
    }


def load_source_records(path: Path) -> list[dict[str, Any]]:
    payload = _load_json(path)
    _require(isinstance(payload, dict) and set(payload) == {"4582"},
             "placement source must contain only playfield 4582")
    playfield = payload["4582"]
    _require(isinstance(playfield, dict) and isinstance(playfield.get("Spawns"), list),
             "placement source is missing 4582.Spawns")
    records = playfield["Spawns"]
    _require(len(records) == 206, "placement source must contain exactly 206 spawns")
    npc_ids: list[int] = []
    for index, record in enumerate(records):
        _require(isinstance(record, dict), f"spawn {index} is not an object")
        for field in ("Name", "NpcId", "TemplateHash", "SpawnHash"):
            _require(field in record, f"spawn {index} is missing {field}")
        npc_id = record["NpcId"]
        _require(isinstance(npc_id, int) and npc_id >= 0,
                 f"spawn {index} has invalid NpcId")
        template_hash = parse_uint32(record["TemplateHash"])
        spawn_hash = parse_uint32(record["SpawnHash"])
        _require(template_hash == spawn_hash,
                 f"spawn NpcId {npc_id} has divergent TemplateHash and SpawnHash")
        _require(isinstance(record["Name"], str) and record["Name"],
                 f"spawn NpcId {npc_id} has invalid Name")
        npc_ids.append(npc_id)
    _require(len(set(npc_ids)) == 206, "source NpcId values must be unique")
    return records


def load_prior_records(path: Path) -> tuple[dict[str, Any], dict[int, dict[str, Any]]]:
    payload = _load_json(path)
    records = payload.get("HashRecords")
    _require(isinstance(records, list) and len(records) == 38,
             "prior TemplateHash audit must contain exactly 38 HashRecords")
    indexed: dict[int, dict[str, Any]] = {}
    for record in records:
        key = parse_uint32(record.get("TemplateHashOriginal"))
        _require(key not in indexed, f"duplicate prior TemplateHash record: {key}")
        indexed[key] = record
    metrics = payload.get("Metrics", {})
    _require(metrics.get("PF4582_RUNTIME_ACTIVE_AFTER") == 25,
             "prior audit active-placement invariant drifted")
    _require(metrics.get("PF4582_RUNTIME_BLOCKED_AFTER") == 181,
             "prior audit blocked-placement invariant drifted")
    return payload, indexed


def _validate_source_fingerprint(source: dict[str, Any]) -> None:
    label = source.get("LogicalSourceLabel")
    _require(isinstance(label, str) and label, "official source lacks logical label")
    digest = source.get("Sha256")
    _require(isinstance(digest, str) and SHA256_PATTERN.fullmatch(digest) is not None,
             f"official source {label} lacks a valid SHA-256")
    _require(isinstance(source.get("FileSize"), int) and source["FileSize"] >= 0,
             f"official source {label} lacks a valid file size")
    _require(source.get("InspectionCompleted") is True,
             f"official source {label} cannot be claimed without completed inspection")
    _require(source.get("KeysSearched") == 38,
             f"official source {label} did not search all 38 keys")
    _require(source.get("SearchMethods") == SEARCH_REPRESENTATIONS,
             f"official source {label} search methods are incomplete or unordered")
    _require(isinstance(source.get("InspectionEvidence"), str)
             and source["InspectionEvidence"],
             f"official source {label} lacks inspection evidence")
    for field in ("StructuralOccurrencesRetained", "FalsePositiveOccurrencesRejected"):
        _require(isinstance(source.get(field), int) and source[field] >= 0,
                 f"official source {label} has invalid {field}")


def validate_evidence(
    evidence: dict[str, Any],
    source_keys: set[int],
) -> None:
    _require(evidence.get("SchemaVersion") == 1, "bridge evidence schema version must be 1")
    outcome = evidence.get("Outcome")
    _require(outcome in OUTCOMES, f"invalid bridge outcome: {outcome!r}")
    provenance = evidence.get("SourceProvenance")
    _require(isinstance(provenance, dict), "source provenance is missing")
    _require(provenance.get("Classification") in {
        "OFFICIAL_BINARY_EXTRACT",
        "OFFICIAL_RESOURCE_EXTRACT",
        "OFFICIAL_DATABASE_EXPORT",
        "MANUAL_TRANSCRIPTION",
        "DERIVED_THIRD_PARTY",
        "ORIGIN_NOT_PROVEN",
    }, "invalid source-provenance classification")
    input_digests = evidence.get("InputDigests")
    _require(isinstance(input_digests, dict) and input_digests,
             "governed input digests are missing")
    for path, digest in input_digests.items():
        _require(isinstance(path, str) and path and isinstance(digest, str)
                 and SHA256_PATTERN.fullmatch(digest) is not None,
                 "invalid governed input digest")

    official_sources = evidence.get("OfficialSources")
    _require(isinstance(official_sources, list) and official_sources,
             "at least one official source must be recorded")
    labels: set[str] = set()
    for official_source in official_sources:
        _require(isinstance(official_source, dict), "official source entry is malformed")
        _validate_source_fingerprint(official_source)
        label = official_source["LogicalSourceLabel"]
        _require(label not in labels, f"duplicate official source label: {label}")
        labels.add(label)

    search_coverage = evidence.get("SearchCoverage")
    _require(isinstance(search_coverage, dict), "search coverage is missing")
    rejected_by_hash = search_coverage.get("RejectedOccurrencesByHash")
    _require(isinstance(rejected_by_hash, dict),
             "per-hash rejected-occurrence accounting is missing")
    rejected_keys = {parse_uint32(key) for key in rejected_by_hash}
    _require(rejected_keys == source_keys,
             "rejected-occurrence accounting does not cover every source hash")
    _require(all(isinstance(value, int) and value >= 0
                 for value in rejected_by_hash.values()),
             "per-hash rejected-occurrence count is invalid")
    resource_record = evidence.get("OfficialResourceRecord")
    _require(isinstance(resource_record, dict), "official resource record is missing")
    _require(resource_record.get("SourceLabel") in labels,
             "official resource record cites an unknown source")
    _require(resource_record.get("KeyOccurrenceCount") == 206,
             "official resource key occurrence count must be 206")
    _require(isinstance(resource_record.get("SearchStartDecimal"), int)
             and resource_record["SearchStartDecimal"] >= 0,
             "official resource search start is invalid")
    _require(isinstance(resource_record.get("SearchEndExclusiveDecimal"), int)
             and resource_record["SearchEndExclusiveDecimal"]
             > resource_record["SearchStartDecimal"],
             "official resource search end is invalid")

    structure_evidence = evidence.get("StructureEvidence")
    _require(isinstance(structure_evidence, list), "structure evidence must be a list")
    for item in structure_evidence:
        _require(item.get("SourceLabel") in labels,
                 "structure evidence cites an unknown official source")
        _require(item.get("Strength") in BRIDGE_STRENGTHS,
                 "structure evidence has an invalid strength")

    claims = evidence.get("Claims")
    _require(isinstance(claims, dict), "bridge claims are missing")
    direct_static = claims.get("DirectStaticBridge") is True
    runtime_ready = claims.get("DirectRuntimeReadyBridge") is True
    if direct_static:
        for field in ("OfficialConsumer", "TerminalStableIdentity", "DeterministicScope"):
            _require(claims.get(field), f"direct static bridge lacks {field}")
    if runtime_ready:
        for field in (
            "ExactClientBuildFingerprint",
            "DirectSourceField",
            "DirectResultingIdentity",
            "SameContextCorrelation",
            "CaptureSerializationImplemented",
        ):
            _require(claims.get(field), f"runtime-ready bridge lacks {field}")
    if outcome == "STATIC_BRIDGE_PROVEN":
        _require(direct_static and not runtime_ready,
                 "STATIC_BRIDGE_PROVEN requires a direct static bridge")
    elif outcome == "RUNTIME_CAPTURE_READY":
        _require(runtime_ready and not direct_static,
                 "RUNTIME_CAPTURE_READY requires only a direct runtime-ready bridge")
    else:
        _require(not direct_static and not runtime_ready,
                 "NO_BRIDGE_LOCATED cannot contain a direct bridge")
        _require(claims.get("StaticTerminalIdentityFound") is False,
                 "NO_BRIDGE_LOCATED cannot contain a static terminal identity")
        _require(claims.get("RuntimeCaptureImplemented") is False,
                 "NO_BRIDGE_LOCATED cannot contain runtime capture support")
        _require(bool(evidence.get("MissingEvidence")),
                 "NO_BRIDGE_LOCATED requires explicit missing evidence")


def verify_governed_inputs(
    evidence: dict[str, Any],
    inputs: Iterable[Path],
) -> None:
    expected = evidence["InputDigests"]
    for path in inputs:
        label = _repository_path(path)
        _require(label in expected, f"evidence ledger does not pin {label}")
        actual = sha256_file(path)
        _require(actual == expected[label], f"governed input digest drifted: {label}")


def verify_official_sources(
    evidence: dict[str, Any],
    official_roots: dict[str, Path],
) -> None:
    verified = 0
    for source in evidence["OfficialSources"]:
        if source.get("VerifyDuringGeneration") is not True:
            continue
        root_label = source.get("RootLabel")
        _require(root_label in official_roots,
                 f"no local root was supplied for {root_label}")
        official_root = official_roots[root_label]
        _require(official_root.is_dir(), f"official AO root is missing: {official_root}")
        relative_path = source.get("RelativePath")
        _require(isinstance(relative_path, str) and relative_path,
                 f"official source {source['LogicalSourceLabel']} lacks a relative path")
        path = official_root / Path(relative_path)
        _require(path.is_file(), f"official evidence input is missing: {path}")
        _require(path.stat().st_size == source["FileSize"],
                 f"official evidence size drifted: {source['LogicalSourceLabel']}")
        _require(sha256_file(path) == source["Sha256"],
                 f"official evidence digest drifted: {source['LogicalSourceLabel']}")
        if source.get("PeTimestampHex"):
            metadata = pe_metadata(path)
            _require(metadata["PeTimestampHex"] == source["PeTimestampHex"],
                     f"official PE timestamp drifted: {source['LogicalSourceLabel']}")
            _require(metadata["ImageBaseHex"] == source["ImageBaseHex"],
                     f"official image base drifted: {source['LogicalSourceLabel']}")
        elif path.suffix.lower() in {".dll", ".exe"}:
            source.update(pe_metadata(path))
        verified += 1
    _require(verified > 0, "no official structural evidence source was verified")


def verify_official_pf4582_records(
    evidence: dict[str, Any],
    source_records: list[dict[str, Any]],
    official_roots: dict[str, Path],
) -> tuple[dict[int, list[int]], dict[int, int]]:
    primary_specification = evidence["OfficialResourceRecord"]
    source_by_label = {
        source["LogicalSourceLabel"]: source for source in evidence["OfficialSources"]
    }
    specifications = [primary_specification] + list(
        evidence.get("OfficialResourceMirrors", [])
    )
    expected_hash_counts = Counter(
        parse_uint32(record["TemplateHash"]) for record in source_records
    )
    source_keys = sorted({parse_uint32(record["TemplateHash"]) for record in source_records})
    key_patterns = {key: key.to_bytes(4, "big") for key in source_keys}
    verified_primary: dict[int, list[int]] | None = None
    primary_rejected: dict[int, int] | None = None

    for specification in specifications:
        source = source_by_label[specification["SourceLabel"]]
        root_label = source["RootLabel"]
        _require(root_label in official_roots,
                 f"no local root was supplied for official PF4582 resource {root_label}")
        path = official_roots[root_label] / Path(source["RelativePath"])
        _require(path.is_file(), f"official PF4582 resource is missing: {path}")
        start = specification["SearchStartDecimal"]
        end = specification["SearchEndExclusiveDecimal"]
        with path.open("rb") as stream:
            stream.seek(start)
            region = stream.read(end - start)
        _require(len(region) == end - start,
                 f"official PF4582 resource region is truncated: {source['LogicalSourceLabel']}")
        actual_hash_counts: Counter[int] = Counter()
        offsets_by_hash: dict[int, list[int]] = defaultdict(list)
        rejected_by_hash: dict[int, int] = {key: 0 for key in source_keys}
        for key, pattern in key_patterns.items():
            cursor = 0
            while True:
                relative_offset = region.find(pattern, cursor)
                if relative_offset < 0:
                    break
                cursor = relative_offset + 1
                actual_hash_counts[key] += 1
                offsets_by_hash[key].append(start + relative_offset)
        if actual_hash_counts != expected_hash_counts:
            missing = expected_hash_counts - actual_hash_counts
            unexpected = actual_hash_counts - expected_hash_counts
            raise BridgeAnalysisError(
                "official PF4582 hash multiset differs in "
                f"{source['LogicalSourceLabel']}: retained={sum(actual_hash_counts.values())}, "
                f"missing={sum(missing.values())}, unexpected={sum(unexpected.values())}, "
                f"first_missing={next(iter(missing), None)!r}, "
                f"first_unexpected={next(iter(unexpected), None)!r}"
            )
        _require(len(offsets_by_hash) == 38,
                 f"official PF4582 resource does not contain all 38 keys in {source['LogicalSourceLabel']}")
        for npc_id in (record["NpcId"] for record in source_records):
            _require(npc_id.to_bytes(4, "little") not in region,
                     f"source NpcId unexpectedly occurs little-endian in {source['LogicalSourceLabel']}")
            _require(npc_id.to_bytes(4, "big") not in region,
                     f"source NpcId unexpectedly occurs big-endian in {source['LogicalSourceLabel']}")
            _require(str(npc_id).encode("ascii") not in region,
                     f"source NpcId unexpectedly occurs as text in {source['LogicalSourceLabel']}")
        if specification is primary_specification:
            verified_primary = {
                key: values for key, values in sorted(offsets_by_hash.items())
            }
            primary_rejected = rejected_by_hash
    _require(verified_primary is not None and primary_rejected is not None,
             "primary official PF4582 resource was not verified")
    return verified_primary, primary_rejected


def build_search_results(
    evidence: dict[str, Any],
    offsets_by_hash: dict[int, list[int]],
    scanned_rejected_by_hash: dict[int, int],
) -> list[dict[str, Any]]:
    specification = evidence["OfficialResourceRecord"]
    rejected = evidence["SearchCoverage"]["RejectedOccurrencesByHash"]
    result: list[dict[str, Any]] = []
    for key in sorted(offsets_by_hash):
        offsets = offsets_by_hash[key]
        result.append({
            "TemplateHashUInt32": key,
            "TemplateHashHex": hash_hex(key),
            "TemplateHashAscii": hash_ascii(key),
            "SearchMethods": SEARCH_REPRESENTATIONS,
            "FoundSourceLabels": [specification["SourceLabel"]],
            "StructuralOccurrences": [{
                "SourceLabel": specification["SourceLabel"],
                "Classification": "OFFICIAL_PF4582_HASH_IN_RESOURCE_WINDOW",
                "Count": len(offsets),
                "FileOffsets": [f"0x{offset:08X}" for offset in offsets],
                "ConsumerStatus": "PARSER_PROVEN_TERMINAL_IDENTITY_NOT_FOUND",
            }],
            "RejectedOccurrenceCount": (
                rejected[str(key)] + scanned_rejected_by_hash.get(key, 0)
            ),
        })
    return result


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
    if (direct_source_key and terminal_identity and same_runtime_context
            and capture_implemented):
        return "DIRECT_RUNTIME_READY"
    return "CORROBORATING_ONLY"


def _propagation_scope(prior: dict[str, Any]) -> str:
    if prior.get("DynamicNamesPresent"):
        return "DYNAMIC_OR_VARIANT"
    if prior.get("BaselineMappingState") == "MAPPED":
        return "NPCID_SPECIFIC"
    return "PROPAGATION_UNPROVEN"


def _occurrence_offsets(search_result: dict[str, Any]) -> list[str]:
    offsets: list[str] = []
    for occurrence in search_result.get("StructuralOccurrences", []):
        for offset in occurrence.get("FileOffsets", []):
            offsets.append(str(offset))
        if occurrence.get("RecordOffset"):
            offsets.append(str(occurrence["RecordOffset"]))
    return _ordered_unique(offsets)


def _contradictions(prior: dict[str, Any]) -> list[Any]:
    values = prior.get("ContradictoryEvidence", [])
    return values if isinstance(values, list) else [values]


def _public_official_source(source: dict[str, Any]) -> dict[str, Any]:
    fields = (
        "LogicalSourceLabel",
        "ProductBuildVersion",
        "RootLabel",
        "RelativePath",
        "MediaClass",
        "FileSize",
        "Sha256",
        "PeTimestampHex",
        "ImageBaseHex",
        "ArchiveMemberPath",
        "SearchMethods",
        "KeysSearched",
        "ExactKeysFound",
        "StructuralOccurrencesRetained",
        "FalsePositiveOccurrencesRejected",
        "FalsePositiveCountingScope",
        "ParserConsumerReferences",
        "CoverageLimitations",
        "InspectionEvidence",
    )
    return {field: source[field] for field in fields if field in source}


def build_model(
    source_path: Path = DEFAULT_SOURCE,
    prior_report_path: Path = DEFAULT_PRIOR_REPORT,
    runtime_source_path: Path = DEFAULT_RUNTIME_SOURCE,
    evidence_path: Path = DEFAULT_EVIDENCE,
    official_resource_root: Path | None = DEFAULT_OFFICIAL_RESOURCE_ROOT,
    official_runtime_root: Path | None = DEFAULT_OFFICIAL_RUNTIME_ROOT,
    verify_official: bool = True,
) -> dict[str, Any]:
    source_records = load_source_records(source_path)
    prior_payload, prior_records = load_prior_records(prior_report_path)
    source_groups: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for record in source_records:
        source_groups[parse_uint32(record["TemplateHash"])].append(record)
    _require(len(source_groups) == 38, "source must contain exactly 38 template hashes")
    _require(set(source_groups) == set(prior_records),
             "source hashes and prior-audit hashes differ")

    evidence = _load_json(evidence_path)
    validate_evidence(evidence, set(source_groups))
    verify_governed_inputs(
        evidence,
        (source_path, prior_report_path, runtime_source_path),
    )
    official_roots = {
        "AO_CLIENT_EP1_INSTALL": official_resource_root,
        "AO_CLIENT_EP2_INSTALL": official_runtime_root,
    }
    typed_roots = {key: value for key, value in official_roots.items() if value is not None}
    if verify_official:
        _require(official_resource_root is not None and official_runtime_root is not None,
                 "official source verification cannot be disabled silently")
        verify_official_sources(evidence, typed_roots)
    else:
        _require(official_resource_root is not None,
                 "official resource records are required to build search offsets")
    verified_offsets, scanned_rejected = verify_official_pf4582_records(
        evidence, source_records, typed_roots
    )

    official_sources = sorted(
        evidence["OfficialSources"], key=lambda item: item["LogicalSourceLabel"]
    )
    official_source_by_label = {
        item["LogicalSourceLabel"]: item for item in official_sources
    }
    derived_search_results = build_search_results(
        evidence, verified_offsets, scanned_rejected
    )
    search_results = {
        parse_uint32(item["TemplateHashUInt32"]): item
        for item in derived_search_results
    }
    for key, offsets in verified_offsets.items():
        expected = [int(value, 16) for value in
                    search_results[key]["StructuralOccurrences"][0]["FileOffsets"]]
        _require(expected == offsets,
                 f"official structural offsets drifted for {hash_ascii(key)}")
    common_functions = _ordered_unique(
        item.get("Function", "") for item in evidence["StructureEvidence"]
    )
    common_offsets = _ordered_unique(
        item.get("AddressOrOffset", "") for item in evidence["StructureEvidence"]
    )
    common_labels = _ordered_unique(
        item.get("SourceLabel", "") for item in evidence["StructureEvidence"]
    )
    common_digests = {
        label: official_source_by_label[label]["Sha256"] for label in common_labels
    }

    hash_records: list[dict[str, Any]] = []
    for key in sorted(source_groups):
        records = source_groups[key]
        prior = prior_records[key]
        search = search_results[key]
        blockers = _ordered_unique(
            list(prior.get("RemainingBlockers", []))
            + list(evidence.get("MissingEvidence", []))
        )
        evidence_labels = _ordered_unique(
            common_labels + list(search.get("FoundSourceLabels", []))
        )
        evidence_digests = {
            label: official_source_by_label[label]["Sha256"]
            for label in evidence_labels
        }
        structural_occurrences = list(search.get("StructuralOccurrences", []))
        per_key_offsets = _ordered_unique(common_offsets + _occurrence_offsets(search))
        hash_records.append({
            "TemplateHashOriginal": str(key),
            "TemplateHashUInt32": key,
            "TemplateHashHex": hash_hex(key),
            "TemplateHashLittleEndianBytes": format_bytes(hash_little_endian(key)),
            "TemplateHashBigEndianBytes": format_bytes(hash_big_endian(key)),
            "TemplateHashAscii": hash_ascii(key),
            "PlacementCount": len(records),
            "NpcIds": sorted(record["NpcId"] for record in records),
            "SourceNames": sorted({record["Name"] for record in records}),
            "DynamicNamesPresent": list(prior.get("DynamicNamesPresent", [])),
            "BaselineState": prior.get("BaselineMappingState", "UNRESOLVED"),
            "PriorAuditClassification": prior.get("Classification", "NO_EVIDENCE"),
            "OfficialOccurrences": structural_occurrences,
            "OfficialLookupRecords": [],
            "StaticTerminalIdentities": [],
            "RuntimeFieldLocated": bool(evidence["Claims"]["RuntimeTemplateFieldFound"]),
            "RuntimeNpcIdLocated": False,
            "CandidateAoRebirthProfiles": list(
                prior.get("CandidateAoRebirthProfiles", [])
            ),
            "DirectBridgeStatus": "NO_BRIDGE",
            "PropagationScope": _propagation_scope(prior),
            "NewProofClassification": (
                "CORROBORATING_ONLY" if structural_occurrences else "NO_BRIDGE"
            ),
            "EvidenceSources": evidence_labels,
            "EvidenceOffsets": per_key_offsets,
            "EvidenceFunctions": common_functions,
            "EvidenceDigests": evidence_digests,
            "Contradictions": _contradictions(prior),
            "RemainingBlockers": blockers,
            "RuntimeActivationAllowed": False,
        })

    _require(sum(item["PlacementCount"] for item in hash_records) == 206,
             "per-hash placement accounting drifted")
    _require(len({npc_id for item in hash_records for npc_id in item["NpcIds"]}) == 206,
             "per-hash NpcId accounting drifted")
    _require(all(roundtrip_hash(item["TemplateHashUInt32"]) for item in hash_records),
             "one or more template hashes failed reversible conversion")
    _require(len({item["TemplateHashAscii"] for item in hash_records}) == 38,
             "four-character display collision detected")
    _require({item["TemplateHashAscii"] for item in hash_records
              if item["BaselineState"] == "MAPPED"} == BASELINE_TAGS,
             "baseline template-key set drifted")

    source_false_positives = sum(
        item["RejectedOccurrenceCount"] for item in derived_search_results
    )
    source_structural_occurrences = sum(
        item["StructuralOccurrencesRetained"] for item in official_sources
    )
    baseline_count = sum(item["BaselineState"] == "MAPPED" for item in hash_records)
    _require(baseline_count == 14, "baseline hash count drifted")
    outcome = evidence["Outcome"]
    claims = evidence["Claims"]
    baseline_partial = 14 if claims.get("SourceRecordStructureMatched") else 0
    baseline_not_reached = 14 - baseline_partial
    metrics = {
        "PF4582_BRIDGE_OUTCOME": outcome,
        "PF4582_SOURCE_PLACEMENTS": 206,
        "PF4582_TEMPLATE_KEYS_TOTAL": 38,
        "PF4582_TEMPLATE_KEYS_ROUNDTRIP": 38,
        "PF4582_SOURCE_NPC_IDS": 206,
        "PF4582_SOURCE_PROVENANCE_CLASS": evidence["SourceProvenance"]["Classification"],
        "PF4582_ORIGINAL_SOURCE_INPUT_FOUND": evidence["SourceProvenance"]["OriginalOfficialInputFound"],
        "PF4582_EXTRACTION_TOOL_FOUND": evidence["SourceProvenance"]["ExtractionToolFound"],
        "PF4582_TEMPLATE_FIELD_OFFICIAL_NAME_PROVEN": evidence["OfficialSemantics"]["TemplateFieldOfficialNameProven"],
        "PF4582_TEMPLATE_FIELD_SEMANTICS": evidence["OfficialSemantics"]["TemplateFieldSemantics"],
        "PF4582_TEMPLATE_FIELD_BYTE_ORDER": evidence["OfficialSemantics"]["TemplateFieldByteOrder"],
        "PF4582_NPCID_SEMANTICS": evidence["OfficialSemantics"]["NpcIdSemantics"],
        "PF4582_OFFICIAL_SOURCE_FILES_INSPECTED": evidence["NonMaterialSearchScope"]["OfficialFilesInspectedTotal"],
        "PF4582_OFFICIAL_SOURCE_BUILDS_INSPECTED": len(evidence["OfficialBuilds"]),
        "PF4582_STRUCTURAL_KEY_OCCURRENCES": source_structural_occurrences,
        "PF4582_FALSE_POSITIVE_OCCURRENCES_REJECTED": source_false_positives,
        "PF4582_STATIC_PARSER_FOUND": "YES" if claims["StaticParserFound"] else "NO",
        "PF4582_STATIC_LOOKUP_CONSUMER_FOUND": "YES" if claims["StaticLookupConsumerFound"] else "NO",
        "PF4582_STATIC_TERMINAL_IDENTITY_FOUND": "YES" if claims["StaticTerminalIdentityFound"] else "NO",
        "PF4582_STATIC_BRIDGED_HASHES": 0,
        "PF4582_STATIC_BRIDGED_NPC_IDS": 0,
        "PF4582_STATIC_BASELINE_MATCH": 0,
        "PF4582_STATIC_BASELINE_PARTIAL": baseline_partial,
        "PF4582_STATIC_BASELINE_CONFLICT": 0,
        "PF4582_STATIC_BASELINE_NOT_REACHED": baseline_not_reached,
        "PF4582_RUNTIME_TEMPLATE_FIELD_FOUND": "YES" if claims["RuntimeTemplateFieldFound"] else "NO",
        "PF4582_RUNTIME_NPCID_FIELD_FOUND": "YES" if claims["RuntimeNpcIdFieldFound"] else "NO",
        "PF4582_RUNTIME_DYNEL_JOIN_FOUND": "YES" if claims["RuntimeDynelJoinFound"] else "NO",
        "PF4582_RUNTIME_CAPTURE_IMPLEMENTED": "YES" if claims["RuntimeCaptureImplemented"] else "NO",
        "PF4582_RUNTIME_CAPTURE_READY": "YES" if outcome == "RUNTIME_CAPTURE_READY" else "NO",
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
    safety = {
        "OFFICIAL_FUNCOM_EVIDENCE_REQUIRED": "YES",
        "CELL_AO_USED_AS_AUTHORITY": "NO",
        "THIRD_PARTY_DATA_USED_AS_AUTHORITY": "NO",
        "NAME_ONLY_JOIN_ACCEPTED": "NO",
        "COORDINATE_JOIN_ACCEPTED": "NO",
        "LEVEL_ONLY_JOIN_ACCEPTED": "NO",
        "MONSTERDATA_ONLY_JOIN_ACCEPTED": "NO",
        "DYNAMIC_NAME_FORCED_RESOLVED": "NO",
        "OFFICIAL_BINARY_MODIFIED": "NO",
        "LIVE_CLIENT_STARTED": "NO",
        "LIVE_CAPTURE_PERFORMED": "NO",
        "PRODUCTION_OPERATION_PERFORMED": "NO",
        "DATABASE_OPERATION_PERFORMED": "NO",
        "RUNTIME_ACTIVATION_CHANGED": "NO",
        "COMMIT_CREATED": "NO",
        "PUSH_PERFORMED": "NO",
    }
    required_invariants = {
        "PF4582_SOURCE_PLACEMENTS": 206,
        "PF4582_RUNTIME_ACTIVE": 25,
        "PF4582_RUNTIME_BLOCKED": 181,
        "BRIDGE_OUTCOME_RECORDED": "YES",
        "ALL_38_TEMPLATE_KEYS_ANALYZED": "YES",
        "SOURCE_VALUE_INFERENCE_USED": "NO",
        "NAME_ONLY_MAPPING_ACCEPTED": "NO",
        "COORDINATE_MAPPING_ACCEPTED": "NO",
        "LEVEL_ONLY_MAPPING_ACCEPTED": "NO",
        "MONSTERDATA_ONLY_MAPPING_ACCEPTED": "NO",
        "SAME_HASH_PROPAGATION_ASSUMED": "NO",
        "DYNAMIC_NAMES_FORCED_RESOLVED": "NO",
        "OFFICIAL_EVIDENCE_PROVENANCE_RECORDED": "YES",
        "UNPROVEN_BEHAVIOR_INVENTED": "NO",
        "RUNTIME_ACTIVATION_CHANGED": "NO",
        "OFFICIAL_BINARY_MODIFIED": "NO",
        "LIVE_CLIENT_STARTED": "NO",
        "LIVE_CAPTURE_PERFORMED": "NO",
        "PRODUCTION_OPERATION_PERFORMED": "NO",
        "COMMIT_CREATED": "NO",
        "PUSH_PERFORMED": "NO",
    }

    baseline_controls = [
        {
            "TemplateHashUInt32": item["TemplateHashUInt32"],
            "TemplateHashAscii": item["TemplateHashAscii"],
            "Result": (
                "STATIC_BASELINE_PARTIAL"
                if claims.get("SourceRecordStructureMatched")
                else "STATIC_BASELINE_NOT_REACHED"
            ),
            "Reason": (
                "The official PF4582 source record and parser are reproduced, but the lookup does not reach a terminal official identity."
                if claims.get("SourceRecordStructureMatched")
                else "The official parser was identified, but the source record was not reached."
            ),
        }
        for item in hash_records if item["BaselineState"] == "MAPPED"
    ]

    report = {
        "SchemaVersion": 1,
        "Outcome": outcome,
        "Metrics": metrics,
        "Safety": safety,
        "RequiredFinalInvariants": required_invariants,
        "SourceProvenance": evidence["SourceProvenance"],
        "OfficialSemantics": evidence["OfficialSemantics"],
        "OfficialBuilds": evidence["OfficialBuilds"],
        "OfficialSources": [_public_official_source(item) for item in official_sources],
        "SearchCoverageSummary": evidence["NonMaterialSearchScope"],
        "StaticBridgeAnalysis": evidence["StaticBridgeAnalysis"],
        "RuntimeBridgeAnalysis": evidence["RuntimeBridgeAnalysis"],
        "StructureEvidence": evidence["StructureEvidence"],
        "BaselineControls": baseline_controls,
        "SameHashPropagation": evidence["SameHashPropagation"],
        "DeadEnds": evidence["DeadEnds"],
        "MissingEvidence": evidence["MissingEvidence"],
        "RequiredNextEvidence": evidence["RequiredNextEvidence"],
        "HashRecords": hash_records,
        "InputDigests": evidence["InputDigests"],
        "PriorAuditMetrics": prior_payload["Metrics"],
    }
    search_manifest = {
        "SchemaVersion": 1,
        "Outcome": outcome,
        "SearchMethods": SEARCH_REPRESENTATIONS,
        "KeysSearched": [
            {
                "TemplateHashUInt32": item["TemplateHashUInt32"],
                "TemplateHashHex": item["TemplateHashHex"],
                "TemplateHashLittleEndianBytes": item["TemplateHashLittleEndianBytes"],
                "TemplateHashBigEndianBytes": item["TemplateHashBigEndianBytes"],
                "TemplateHashAscii": item["TemplateHashAscii"],
                "NpcIds": item["NpcIds"],
            }
            for item in hash_records
        ],
        "OfficialSources": [_public_official_source(item) for item in official_sources],
        "PerKeyResults": sorted(
            derived_search_results,
            key=lambda item: parse_uint32(item["TemplateHashUInt32"]),
        ),
        "StructuralOccurrencesRetained": source_structural_occurrences,
        "FalsePositiveOccurrencesRejected": source_false_positives,
        "ParserConsumerReferences": _ordered_unique(
            reference
            for source in official_sources
            for reference in source.get("ParserConsumerReferences", [])
        ),
        "CoverageLimitations": evidence["SearchCoverage"]["CoverageLimitations"],
        "SearchCoverageSummary": evidence["NonMaterialSearchScope"],
        "InputDigests": evidence["InputDigests"],
    }
    return {"Report": report, "SearchManifest": search_manifest}


def render_json(value: dict[str, Any]) -> str:
    return json.dumps(value, indent=2, ensure_ascii=False) + "\n"


def _markdown_cell(value: Any) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ")


def render_markdown(report: dict[str, Any]) -> str:
    metrics = report["Metrics"]
    lines = [
        "# PF4582 Official Template Identity Bridge Discovery",
        "",
        f"Primary outcome: `{report['Outcome']}`.",
        "",
        "This evidence-only audit activates no PF4582 placement. Printable hash bytes, names, coordinates, levels, MonsterData, and third-party material remain non-authoritative unless an official source-key join reaches a terminal identity.",
        "",
        "## Required metrics",
        "",
        "```text",
    ]
    lines.extend(f"{key}={value}" for key, value in metrics.items())
    lines.extend(["```", "", "## Source provenance", ""])
    provenance = report["SourceProvenance"]
    lines.extend([
        f"Classification: `{provenance['Classification']}`.",
        "",
        provenance["Conclusion"],
        "",
        f"Delivered dataset SHA-256: `{provenance['DeliveredDatasetSha256']}`.",
        "",
        "## Strongest bounded field semantics",
        "",
        report["OfficialSemantics"]["Conclusion"],
        "",
        "The numeric representation remains exactly reversible: the unsigned integer and eight-digit integer hex are serialized as four little-endian bytes; those bytes are also displayed as four printable characters. That display is not itself semantic proof.",
        "",
        "## Official sources inspected",
        "",
        "| Logical source | Build | Media | Size | SHA-256 | Structural | Rejected |",
        "|---|---|---|---:|---|---:|---:|",
    ])
    for source in report["OfficialSources"]:
        lines.append(
            "| {label} | {build} | {media} | {size} | `{digest}` | {structural} | {rejected} |".format(
                label=_markdown_cell(source["LogicalSourceLabel"]),
                build=_markdown_cell(source.get("ProductBuildVersion", "UNKNOWN")),
                media=_markdown_cell(source.get("MediaClass", "UNKNOWN")),
                size=source["FileSize"],
                digest=source["Sha256"],
                structural=source["StructuralOccurrencesRetained"],
                rejected=source["FalsePositiveOccurrencesRejected"],
            )
        )
    lines.extend(["", "## Official parser and consumer trace", ""])
    lines.append(report["StaticBridgeAnalysis"]["Conclusion"])
    lines.extend(["", "```text"])
    lines.extend(report["StaticBridgeAnalysis"]["Chain"])
    lines.extend(["```", "", "## Runtime bridge trace", ""])
    lines.append(report["RuntimeBridgeAnalysis"]["Conclusion"])
    lines.extend(["", "```text"])
    lines.extend(report["RuntimeBridgeAnalysis"]["Chain"])
    lines.extend(["```", "", "## Baseline controls", ""])
    if metrics["PF4582_STATIC_BASELINE_PARTIAL"]:
        lines.append(
            "All 14 governed baseline keys are `STATIC_BASELINE_PARTIAL`: their official PF4582 source records are reproduced, but no terminal official identity was available to match or conflict with the AORebirth profile."
        )
    else:
        lines.append(
            "All 14 governed baseline keys are `STATIC_BASELINE_NOT_REACHED`; no official terminal identity was available to match, partially match, or conflict with the AORebirth profile."
        )
    lines.extend(["", "## Same-hash propagation", ""])
    lines.append(report["SameHashPropagation"]["Conclusion"])
    lines.extend([
        "",
        "## Per-hash result",
        "",
        "| UInt32 | Hex | LE bytes | Tag | Placements | Prior | Direct | Propagation |",
        "|---:|---|---|---|---:|---|---|---|",
    ])
    for record in report["HashRecords"]:
        lines.append(
            f"| {record['TemplateHashUInt32']} | `{record['TemplateHashHex']}` | `{record['TemplateHashLittleEndianBytes']}` | `{record['TemplateHashAscii']}` | {record['PlacementCount']} | {record['PriorAuditClassification']} | {record['DirectBridgeStatus']} | {record['PropagationScope']} |"
        )
    lines.extend(["", "## Dead ends", ""])
    lines.extend(f"- {item}" for item in report["DeadEnds"])
    lines.extend(["", "## Exact evidence still required", ""])
    lines.extend(f"- {item}" for item in report["RequiredNextEvidence"])
    lines.extend(["", "## Safety and no-promotion invariants", "", "```text"])
    lines.extend(f"{key}={value}" for key, value in report["Safety"].items())
    lines.extend(["```", ""])
    return "\n".join(lines)


def _write_or_check(path: Path, content: str, check: bool) -> None:
    if check:
        _require(path.is_file(), f"generated artifact is missing: {path}")
        _require(path.read_text(encoding="utf-8") == content,
                 f"generated artifact is stale: {path}")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--prior-report", type=Path, default=DEFAULT_PRIOR_REPORT)
    parser.add_argument("--runtime-source", type=Path, default=DEFAULT_RUNTIME_SOURCE)
    parser.add_argument("--evidence", type=Path, default=DEFAULT_EVIDENCE)
    parser.add_argument(
        "--official-resource-root", type=Path, default=DEFAULT_OFFICIAL_RESOURCE_ROOT
    )
    parser.add_argument(
        "--official-runtime-root", type=Path, default=DEFAULT_OFFICIAL_RUNTIME_ROOT
    )
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
            official_resource_root=args.official_resource_root,
            official_runtime_root=args.official_runtime_root,
            verify_official=True,
        )
        report_text = render_json(model["Report"])
        manifest_text = render_json(model["SearchManifest"])
        markdown_text = render_markdown(model["Report"])
        _write_or_check(args.report, report_text, args.check)
        _write_or_check(args.search_manifest, manifest_text, args.check)
        _write_or_check(args.markdown, markdown_text, args.check)
        for key, value in model["Report"]["Metrics"].items():
            print(f"{key}={value}")
        return 0
    except (BridgeAnalysisError, OSError) as exc:
        print(f"PF4582 bridge analysis failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
