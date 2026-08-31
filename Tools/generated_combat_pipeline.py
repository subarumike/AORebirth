#!/usr/bin/env python3
"""Coordinate the capture-backed NPC combat generated-artifact cohort.

The coordinator never asks the existing generators to write a production path.
It builds a complete candidate cohort under one isolated root, proves the
active-coverage/formula cycle has reached a fixed point, and delegates the
lease and publication transaction to ``generated_artifact_transaction``.
"""

from __future__ import annotations

import argparse
import csv
import io
from collections.abc import Mapping
import contextlib
import dataclasses
import hashlib
import importlib
import json
import os
import re
import signal
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Iterator, Mapping, Sequence


PIPELINE_NAME = "capture-backed-npc-combat"
MANIFEST_SCHEMA_VERSION = 2
LEGACY_MANIFEST_SCHEMA_VERSION = 1
CAPTURE_INPUT_SNAPSHOT_SCHEMA_VERSION = 1
PORTABLE_INPUT_SNAPSHOT_SCHEMA_VERSION = 2
DEFAULT_MAX_FIXED_POINT_ROUNDS = 8
CHILD_PROCESS_TIMEOUT_SECONDS = 1800
MAX_READ_LEASE_COMMAND_TIMEOUT_SECONDS = 4 * 60 * 60
PRIMARY_AGGREGATION_MAX_ATTEMPTS = 3
GOVERNED_JSON_READ_MAX_ATTEMPTS = 3
MAX_ARTIFACT_BYTES = 512 * 1024 * 1024
GENERATOR_INVENTORY_PROJECTION_KEYS = (
    "schemaVersion",
    "authoritativeInputs",
    "attackRangeAuthority",
    "sessions",
    "profiles",
    "capturedRealmToRuntimeResource",
    "metadataGenerations",
    "summary",
)

REPO_ROOT = Path(__file__).resolve().parents[1]
PRIMARY_GENERATOR = Path(
    "tools-temp/AOSharpCaptureAnalyzer/extract_capture_backed_npc_combat.py"
)
ACTIVE_GENERATOR = Path(
    "tools-temp/AOSharpCaptureAnalyzer/generate_capture_backed_npc_active_coverage.py"
)
FORMULA_GENERATOR = Path(
    "tools-temp/AOSharpCaptureAnalyzer/analyze_enemy_combat_setup_formula.py"
)
ATTACK_RANGE_AUDIT = Path(
    "tools-temp/AOSharpCaptureAnalyzer/audit_capture_backed_npc_attack_range.py"
)
SECONDARY_EVIDENCE_AUDIT = Path(
    "tools-temp/AOSharpCaptureAnalyzer/audit_capture_backed_npc_secondary_evidence.py"
)
ITEM_DATABASE = Path("AORebirth/Datafiles/items.dat")
SCFU_ANALYZER = Path(
    "tools-temp/AOSharpCaptureAnalyzer/bin/Debug/AOSharpCaptureAnalyzer.exe"
)
SCFU_ANALYZER_PROJECT = Path(
    "tools-temp/AOSharpCaptureAnalyzer/AOSharpCaptureAnalyzer.csproj"
)
SCFU_ANALYZER_SOURCE_ROOTS = (
    Path("tools-temp/AOSharpCaptureAnalyzer"),
    Path("tools-temp/AOSharpCaptureProtocol"),
    Path("AORebirth/Libraries/Source/AORebirth.Core"),
    Path("AORebirth/Libraries/Source/AORebirth.Enums"),
    Path("AORebirth/Libraries/Source/AORebirth.Stats"),
    Path("AORebirth/Libraries/Source/Utility"),
    Path("AORebirth/Libraries/Source/msgpack-cli/src/MsgPack.Mono"),
)
SCFU_ANALYZER_SOURCE_SUFFIXES = frozenset(
    {".config", ".cs", ".csproj", ".props", ".resx", ".snk", ".targets"}
)
ITEM_TEMPLATE_PROJECTION_SOURCE = Path(
    "tools-temp/AOSharpCaptureAnalyzer/ItemTemplateProjection.cs"
)
FORMULA_STATIC_INPUTS = (
    Path(
        "docs/accepted/combat/"
        "enemy_combat_formula_packet_evidence.json"
    ),
    Path(
        "AORebirth/Server/ZoneEngine/Core/Playfields/"
        "CapturedSubwayOrdinaryContentProvider.cs"
    ),
    Path(
        "AORebirth/Server/ZoneEngine/Core/Playfields/"
        "CapturedTempleOfThreeWindsContentProvider.cs"
    ),
    Path("docs/evidence/TEMPLE_CULTIST_COMBAT_QUARANTINE_20260726.md"),
)
FORMULA_CAPTURE_SOURCE_NAMES = (
    "capture_info.json",
    "packets.hex.log",
    "raw-packets.csv",
    "scfu-appearance.csv",
)
ACTIVE_RUNTIME_SOURCE_ROOT = Path("AORebirth/Server/ZoneEngine/Core")
ARETE_ATTACK_RANGE_ITEM_TEMPLATE_IDS = (
    120910,
    120911,
    120913,
    120914,
    121038,
    121039,
    121041,
    121042,
)

# This is a private producer output: it records the exact capture-input snapshot
# used by the aggregate worker without making that snapshot a published artifact.
PRIMARY_SNAPSHOT_ARGUMENT = "--_input-snapshot-manifest"

ARTIFACT_RELATIVE_PATHS: dict[str, PurePosixPath] = {
    "inventory": PurePosixPath(
        "docs/generated/capture_backed_npc_combat_inventory.json"
    ),
    "catalog": PurePosixPath(
        "AORebirth/Server/ZoneEngine/Core/Playfields/"
        "CapturedEnemyCombatProfileCatalog.g.cs"
    ),
    "fixtures": PurePosixPath(
        "AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/"
        "SmokeLounge.AOtomation.Messaging.Tests/"
        "CapturedEnemyCombatProfileCatalogFixtures.g.cs"
    ),
    "activeCoverage": PurePosixPath(
        "docs/generated/capture_backed_npc_combat_active_coverage.json"
    ),
    "formulaDataset": PurePosixPath(
        "docs/generated/enemy_combat_setup_formula_dataset.json"
    ),
    "attackRangeAudit": PurePosixPath(
        "docs/generated/capture_backed_npc_attack_range_audit.json"
    ),
    "secondaryEvidenceAudit": PurePosixPath(
        "docs/generated/capture_backed_npc_secondary_evidence_audit.json"
    ),
}
MANIFEST_RELATIVE_PATH = PurePosixPath(
    "docs/generated/capture_backed_npc_combat_generation_manifest.json"
)
JSON_ARTIFACT_ROLES = frozenset(
    (
        "inventory",
        "activeCoverage",
        "formulaDataset",
        "attackRangeAudit",
        "secondaryEvidenceAudit",
    )
)

GENERATOR_PATHS: dict[str, PurePosixPath] = {
    "captureDiscovery": PurePosixPath("Tools/inventory_aosharp_captures.py"),
    "captureDecoder": PurePosixPath(
        "tools-temp/AOSharpLiveCapture/decode_npc_lifecycle_capture.py"
    ),
    "coordinator": PurePosixPath("Tools/generated_combat_pipeline.py"),
    "transaction": PurePosixPath("Tools/generated_artifact_transaction.py"),
    "primary": PurePosixPath(PRIMARY_GENERATOR.as_posix()),
    "activeCoverage": PurePosixPath(ACTIVE_GENERATOR.as_posix()),
    "formulaDataset": PurePosixPath(FORMULA_GENERATOR.as_posix()),
    "attackRangeAudit": PurePosixPath(ATTACK_RANGE_AUDIT.as_posix()),
    "secondaryEvidenceAudit": PurePosixPath(SECONDARY_EVIDENCE_AUDIT.as_posix()),
    "itemTemplateProjection": PurePosixPath(
        ITEM_TEMPLATE_PROJECTION_SOURCE.as_posix()
    ),
    "scfuAnalyzer": PurePosixPath(SCFU_ANALYZER_PROJECT.as_posix()),
}

LEASE_DELEGATION_ENVIRONMENT = "AO_REBIRTH_GENERATED_COMBAT_LEASE_DELEGATION"
LEASE_REPO_ROOT_ENVIRONMENT = "AO_REBIRTH_GENERATED_COMBAT_LEASE_REPO_ROOT"
PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT = (
    "AO_REBIRTH_GENERATED_COMBAT_PRIMARY_CAPTURE_REPO_ROOT"
)
PRIMARY_SCFU_ANALYZER_ENVIRONMENT = (
    "AO_REBIRTH_GENERATED_COMBAT_PRIMARY_SCFU_ANALYZER"
)
NPC_COMBAT_AUDIT_REPO_ROOT_ENVIRONMENT = (
    "AO_REBIRTH_NPC_COMBAT_AUDIT_REPO_ROOT"
)
NPC_COMBAT_AUDIT_INVENTORY_ENVIRONMENT = (
    "AO_REBIRTH_NPC_COMBAT_AUDIT_INVENTORY"
)
NPC_COMBAT_AUDIT_INVENTORY_LOGICAL_PATH_ENVIRONMENT = (
    "AO_REBIRTH_NPC_COMBAT_AUDIT_INVENTORY_LOGICAL_PATH"
)
NPC_COMBAT_SECONDARY_AUDIT_OUTPUT_ENVIRONMENT = (
    "AO_REBIRTH_NPC_COMBAT_SECONDARY_AUDIT_OUTPUT"
)


class PipelineError(RuntimeError):
    """The generated-combat cohort could not be proved or published."""


class CohortValidationError(PipelineError):
    """The currently published cohort is partial, mixed, or malformed."""


class FixedPointError(PipelineError):
    """The active-coverage/formula iteration did not converge safely."""


@dataclasses.dataclass(frozen=True)
class PairState:
    active_coverage: bytes
    formula_dataset: bytes

    @property
    def digest(self) -> str:
        digest = hashlib.sha256()
        for payload in (self.active_coverage, self.formula_dataset):
            digest.update(len(payload).to_bytes(8, "big"))
            digest.update(payload)
        return digest.hexdigest()


@dataclasses.dataclass(frozen=True)
class FixedPointResult:
    state: PairState
    rounds: int
    identities: tuple[str, ...]


@dataclasses.dataclass(frozen=True)
class CandidateCohort:
    root: Path
    artifacts: Mapping[str, Path]
    manifest_path: Path
    capture_snapshot: Mapping[str, Any]
    generation_identity: str
    input_snapshot_identity: str
    fixed_point_rounds: int


def canonical_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=True,
            indent=2,
            sort_keys=True,
            separators=(",", ": "),
        )
        + "\n"
    ).encode("utf-8")


def identity_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=True,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def artifact_descriptor(path: Path, logical_path: PurePosixPath) -> dict[str, Any]:
    if not path.is_file() or path.is_symlink():
        raise PipelineError(f"generated artifact is missing: {path}")
    byte_length = path.stat().st_size
    if byte_length <= 0 or byte_length > MAX_ARTIFACT_BYTES:
        raise PipelineError(
            f"generated artifact has an invalid byte length: {logical_path}"
        )
    return {
        "path": logical_path.as_posix(),
        "sha256": sha256_file(path),
        "byteLength": byte_length,
    }


def decode_json_text(raw: str) -> Any:
    return json.loads(raw)


def _is_transient_json_decoder_failure(error: BaseException) -> bool:
    if isinstance(error, json.JSONDecodeError):
        return True
    if (
        isinstance(error, RuntimeError)
        and "internal error in regular expression engine" in str(error)
    ):
        return True
    if not isinstance(error, (TypeError, AttributeError, RuntimeError)):
        return False
    traceback = error.__traceback__
    while traceback is not None:
        code = traceback.tb_frame.f_code
        filename = code.co_filename.replace("\\", "/").casefold()
        if code.co_name in {
            "JSONObject",
            "JSONArray",
            "py_scanstring",
            "_scan_once",
            "scan_once",
        } and (
            filename == "json/decoder.py"
            or filename == "json/scanner.py"
            or filename.endswith("/json/decoder.py")
            or filename.endswith("/json/scanner.py")
        ):
            return True
        traceback = traceback.tb_next
    return False


def _require_json_mapping(value: Any, label: str) -> Mapping[str, Any]:
    if not isinstance(value, dict):
        raise CohortValidationError(f"{label} must be one JSON object")
    return value


def _require_json_list(value: Any, label: str) -> list[Any]:
    if not isinstance(value, list):
        raise CohortValidationError(f"{label} must be one JSON array")
    return value


def _present_fields(
    source: Mapping[str, Any], fields: Sequence[str]
) -> dict[str, Any]:
    return {field: source[field] for field in fields if field in source}


def _project_active_observation(value: Any, label: str) -> dict[str, Any]:
    row = _require_json_mapping(value, label)
    return _present_fields(
        row,
        (
            "classification",
            "sourceIdentity",
            "observationCount",
            "captureSessions",
            "samplePacketIds",
            "evidenceFound",
            "missingEvidence",
            "conflicts",
            "runtimeSupport",
        ),
    )


def _project_active_variant(value: Any, label: str) -> dict[str, Any]:
    variant = _require_json_mapping(value, label)
    projected = _present_fields(
        variant,
        (
            "captureCertified",
            "captureEvidenceSafe",
            "runtimeContractReady",
            "captureSessions",
            "sourceIdentities",
            "semanticProfileId",
            "runtimeMissingEvidence",
            "representativeWifuPacketId",
            "representativeSawPacketId",
            "representativeAttackPacketId",
            "capturedAttackRangeMeters",
            "capturedAttackRangeEvidence",
        ),
    )
    if "baseSignature" in variant:
        projected["baseSignature"] = _present_fields(
            _require_json_mapping(variant["baseSignature"], f"{label}.baseSignature"),
            ("weaponContextKind",),
        )
    if "streams" in variant:
        streams = _require_json_list(variant["streams"], f"{label}.streams")
        projected_streams = []
        for index, value in enumerate(streams):
            stream = _require_json_mapping(value, f"{label}.streams[{index}]")
            projected_stream: dict[str, Any] = _present_fields(
                stream,
                (
                    "capturedTerminalHitOnly",
                    "damageObservations",
                    "attackStartDelayObservationsSeconds",
                    "firstHitDelayObservationsSeconds",
                    "initialAmmoCandidates",
                    "landedIntervalObservationsSeconds",
                    "capturedAttackRange",
                    "capturedAttackRangeEvidenceId",
                ),
            )
            if "attackInfoPacketIds" in stream:
                packet_ids = _require_json_list(
                    stream["attackInfoPacketIds"],
                    f"{label}.streams[{index}].attackInfoPacketIds",
                )
                projected_stream["attackInfoPacketIds"] = list(packet_ids)
            if "pairedFightTimingObservations" in stream:
                timings = _require_json_list(
                    stream["pairedFightTimingObservations"],
                    f"{label}.streams[{index}].pairedFightTimingObservations",
                )
                projected_stream["pairedFightTimingObservations"] = [
                    _present_fields(
                        _require_json_mapping(
                            timing,
                            f"{label}.streams[{index}].pairedFightTimingObservations[{timing_index}]",
                        ),
                        ("sourceIdentity", "attackStartDelaySeconds"),
                    )
                    for timing_index, timing in enumerate(timings)
                ]
            projected_streams.append(projected_stream)
        projected["streams"] = projected_streams
    if "rawWireVariantObservations" in variant:
        rows = _require_json_list(
            variant["rawWireVariantObservations"],
            f"{label}.rawWireVariantObservations",
        )
        projected["rawWireVariantObservations"] = [None] * len(rows)
    if "mutableSawStateObservations" in variant:
        rows = _require_json_list(
            variant["mutableSawStateObservations"],
            f"{label}.mutableSawStateObservations",
        )
        projected["mutableSawStateObservations"] = [
            _present_fields(
                _require_json_mapping(
                    row, f"{label}.mutableSawStateObservations[{index}]"
                ),
                ("sourceIdentity", "unknown5"),
            )
            for index, row in enumerate(rows)
        ]
    if "runtimeMutableWeaponStateCandidates" in variant:
        rows = _require_json_list(
            variant["runtimeMutableWeaponStateCandidates"],
            f"{label}.runtimeMutableWeaponStateCandidates",
        )
        projected["runtimeMutableWeaponStateCandidates"] = [None] * len(rows)
    return projected


def _project_active_profile(value: Any, label: str) -> dict[str, Any]:
    profile = _require_json_mapping(value, label)
    if not isinstance(profile.get("profileKey"), str):
        raise CohortValidationError(f"{label}.profileKey must be one string")
    projected = _present_fields(
        profile,
        (
            "profileKey",
            "captureSessionsSearched",
            "semanticFallbackCaptureProven",
            "status",
            "disabledCapability",
            "conflictedSourceIdentities",
        ),
    )
    if "metadata" in profile:
        metadata = profile["metadata"]
        projected["metadata"] = (
            None
            if metadata is None
            else _present_fields(
                _require_json_mapping(metadata, f"{label}.metadata"), ("level",)
            )
        )
    if "variants" in profile:
        variants = _require_json_list(profile["variants"], f"{label}.variants")
        projected["variants"] = [
            _project_active_variant(row, f"{label}.variants[{index}]")
            for index, row in enumerate(variants)
        ]
    for field in (
        "incompleteObservations",
        "nonNormalObservations",
        "unsupportedSequences",
    ):
        if field not in profile:
            continue
        rows = _require_json_list(profile[field], f"{label}.{field}")
        projected[field] = [
            _project_active_observation(row, f"{label}.{field}[{index}]")
            for index, row in enumerate(rows)
        ]
    return projected


def _project_active_metadata(value: Any, label: str) -> dict[str, Any]:
    metadata = _require_json_mapping(value, label)
    return _present_fields(
        metadata,
        (
            "capturedRealmId",
            "monsterData",
            "level",
            "name",
            "capture",
            "generationKey",
            "sourceIdentity",
            "sequence",
            "packetSha256",
            "projection",
        ),
    )


def build_generator_inventory_projection(
    inventory: Mapping[str, Any],
) -> dict[str, Any]:
    missing = [key for key in GENERATOR_INVENTORY_PROJECTION_KEYS if key not in inventory]
    if missing:
        raise CohortValidationError(
            "primary inventory is missing generator projection keys: "
            + ", ".join(missing)
        )
    sessions = _require_json_list(inventory["sessions"], "primary sessions")
    profiles = _require_json_list(inventory["profiles"], "primary profiles")
    metadata = _require_json_list(
        inventory["metadataGenerations"], "primary metadata generations"
    )
    authoritative_inputs = _require_json_list(
        inventory["authoritativeInputs"], "primary authoritative inputs"
    )
    realm_map = _require_json_mapping(
        inventory["capturedRealmToRuntimeResource"], "primary captured realm map"
    )
    summary = _require_json_mapping(inventory["summary"], "primary summary")
    summary_fields = (
        "captureCertifiedProfiles",
        "captureCertifiedSemanticDefinitions",
        "runtimeReadyProfiles",
    )
    missing_summary = [field for field in summary_fields if field not in summary]
    if missing_summary:
        raise CohortValidationError(
            "primary summary is missing active projection keys: "
            + ", ".join(missing_summary)
        )
    return {
        "schemaVersion": inventory["schemaVersion"],
        "authoritativeInputs": authoritative_inputs,
        "attackRangeAuthority": inventory["attackRangeAuthority"],
        "sessions": [
            _present_fields(
                _require_json_mapping(row, f"primary sessions[{index}]"),
                ("capture",),
            )
            for index, row in enumerate(sessions)
        ],
        "profiles": [
            _project_active_profile(row, f"primary profiles[{index}]")
            for index, row in enumerate(profiles)
        ],
        "capturedRealmToRuntimeResource": dict(realm_map),
        "metadataGenerations": [
            _project_active_metadata(row, f"primary metadata generations[{index}]")
            for index, row in enumerate(metadata)
        ],
        "summary": {field: summary[field] for field in summary_fields},
    }


FORMULA_COMPACT_OBSERVATION_FIELDS = (
    "messageType",
    "classification",
    "sourceIdentity",
    "attackerIdentity",
    "defenderIdentity",
    "n3SourceIdentity",
    "n3Unknown",
    "unknown1",
    "unknown2",
    "unknown5",
    "hitTypeWire",
    "damageTypeWire",
    "packetOrderProven",
    "observationCount",
    "captureSessions",
    "missingEvidence",
    "evidenceFound",
)


def _profile_resource(profile_key: str) -> int | None:
    match = re.search(r"(?:^|\|)resource=(\d+)(?:\||$)", profile_key)
    return int(match.group(1)) if match else None


def _project_formula_variant(
    value: Any, label: str, *, scope_profile: bool, raw_chain_profile: bool
) -> dict[str, Any]:
    variant = _require_json_mapping(value, label)
    projected = _present_fields(variant, ("semanticProfileId", "baseSignature"))
    if scope_profile:
        projected.update(
            _present_fields(
                variant,
                (
                    "captureEvidenceSafe",
                    "runtimeContractReady",
                    "runtimeMissingEvidence",
                    "streams",
                    "mutableSawStateObservations",
                ),
            )
        )
    if raw_chain_profile and "rawWireVariantObservations" in variant:
        rows = _require_json_list(
            variant["rawWireVariantObservations"],
            f"{label}.rawWireVariantObservations",
        )
        projected["rawWireVariantObservations"] = [
            _present_fields(
                _require_json_mapping(
                    row, f"{label}.rawWireVariantObservations[{index}]"
                ),
                (
                    "sourceIdentity",
                    "weaponItemFullUpdatePacketId",
                    "specialAttackWeaponPacketId",
                    "attackPacketId",
                    "attackInfoPacketId",
                    "terminalHit",
                ),
            )
            for index, row in enumerate(rows)
        ]
    return projected


def _project_formula_profile(value: Any, label: str) -> dict[str, Any]:
    profile = _require_json_mapping(value, label)
    profile_key = profile.get("profileKey")
    if not isinstance(profile_key, str):
        raise CohortValidationError(f"{label}.profileKey must be one string")
    resource = _profile_resource(profile_key)
    scope_profile = resource in (127, 1931)
    metadata = profile.get("metadata")
    metadata_map = (
        _require_json_mapping(metadata, f"{label}.metadata")
        if metadata is not None
        else {}
    )
    raw_chain_profile = (
        resource == 127 and "|md=203739|" in profile_key
    ) or (
        metadata_map.get("monsterData") == 203747
        and metadata_map.get("name") == "Melded Patterns"
    )
    projected: dict[str, Any] = {"profileKey": profile_key}
    if "metadata" in profile:
        projected["metadata"] = metadata
    variants = _require_json_list(profile.get("variants", []), f"{label}.variants")
    projected["variants"] = [
        _project_formula_variant(
            row,
            f"{label}.variants[{index}]",
            scope_profile=scope_profile,
            raw_chain_profile=raw_chain_profile,
        )
        for index, row in enumerate(variants)
    ]
    if scope_profile:
        projected.update(
            _present_fields(
                profile,
                ("status", "normalCompleteChainCount", "unsupportedNpcSequenceCount"),
            )
        )
        for field in ("unsupportedSequences", "incompleteObservations"):
            rows = _require_json_list(profile.get(field, []), f"{label}.{field}")
            projected[field] = [
                _present_fields(
                    _require_json_mapping(row, f"{label}.{field}[{index}]"),
                    FORMULA_COMPACT_OBSERVATION_FIELDS,
                )
                for index, row in enumerate(rows)
            ]
    return projected


def build_formula_inventory_projection(
    inventory: Mapping[str, Any],
) -> dict[str, Any]:
    if "profiles" not in inventory:
        raise CohortValidationError(
            "primary inventory is missing formula projection key: profiles"
        )
    profiles = _require_json_list(inventory["profiles"], "primary profiles")
    return {
        "profiles": [
            _project_formula_profile(row, f"primary profiles[{index}]")
            for index, row in enumerate(profiles)
        ]
    }


def collect_formula_template_ids(value: Any) -> set[int]:
    result: set[int] = set()
    if isinstance(value, dict):
        for key, child in value.items():
            normalized = key.lower()
            if normalized.endswith("template") and isinstance(child, int):
                result.add(child)
            elif normalized.endswith("templates") and isinstance(child, list):
                result.update(item for item in child if isinstance(item, int))
            result.update(collect_formula_template_ids(child))
    elif isinstance(value, list):
        for child in value:
            result.update(collect_formula_template_ids(child))
    return result


def collect_referenced_formula_template_ids(
    formula_inventory_projection: Mapping[str, Any],
) -> set[int]:
    profiles = _require_json_list(
        formula_inventory_projection.get("profiles"),
        "formula inventory profiles",
    )
    scoped_profiles = []
    for index, value in enumerate(profiles):
        profile = _require_json_mapping(value, f"formula profiles[{index}]")
        match = re.search(
            r"(?:^|\|)resource=(\d+)(?:\||$)",
            str(profile.get("profileKey", "")),
        )
        if match and int(match.group(1)) in (127, 1931):
            scoped_profiles.append(profile)
    return collect_formula_template_ids(scoped_profiles)


def primary_output_signature(
    artifacts: Mapping[str, Path], snapshot_descriptor: Mapping[str, Any]
) -> str:
    return sha256_bytes(
        canonical_json_bytes(
            primary_output_signature_descriptor(artifacts, snapshot_descriptor)
        )
    )


def primary_output_signature_descriptor(
    artifacts: Mapping[str, Path], snapshot_descriptor: Mapping[str, Any]
) -> dict[str, Any]:
    paths = {
        "inventory": artifacts["inventory"],
        "catalog": artifacts["catalog"],
        "fixtures": artifacts["fixtures"],
    }
    descriptors: dict[str, dict[str, Any]] = {}
    for role, path in paths.items():
        if not path.is_file() or path.is_symlink():
            raise CohortValidationError(
                f"primary {role} output is missing or is not a regular file"
            )
        descriptors[role] = {
            "byteLength": path.stat().st_size,
            "sha256": sha256_file(path),
        }
    descriptors["captureSnapshot"] = {
        key: snapshot_descriptor[key]
        for key in (
            "schemaVersion",
            "captureSchemaVersion",
            "captureSnapshotIdentity",
            "captureManifestSha256",
            "captureManifestByteLength",
        )
    }
    return descriptors


def load_json_object(
    path: Path,
    label: str,
    *,
    expected_sha256: str | None = None,
    expected_byte_length: int | None = None,
) -> dict[str, Any]:
    if not path.is_file() or path.is_symlink():
        raise CohortValidationError(f"{label} is missing or is not a regular file")
    if (expected_sha256 is None) != (expected_byte_length is None):
        raise ValueError("JSON integrity expectations must be supplied together")
    try:
        payload = path.read_bytes()
    except OSError as error:
        raise CohortValidationError(
            f"{label} is not valid UTF-8 JSON: {error}"
        ) from error
    if len(payload) > MAX_ARTIFACT_BYTES:
        raise CohortValidationError(f"{label} has an invalid byte length")
    if expected_byte_length is not None and (
        len(payload) != expected_byte_length
        or sha256_bytes(payload) != expected_sha256
    ):
        raise CohortValidationError(f"{label} is stale or mixed")
    try:
        raw = payload.decode("utf-8")
    except UnicodeError as error:
        raise CohortValidationError(
            f"{label} is not valid UTF-8 JSON: {error}"
        ) from error
    del payload
    failure_detail = ""
    for attempt in range(1, GOVERNED_JSON_READ_MAX_ATTEMPTS + 1):
        try:
            value = decode_json_text(raw)
        except (json.JSONDecodeError, TypeError, AttributeError, RuntimeError) as error:
            if not _is_transient_json_decoder_failure(error):
                raise
            if isinstance(error, json.JSONDecodeError):
                failure_detail = (
                    f"{error.msg}: line {error.lineno} column {error.colno} "
                    f"(char {error.pos})"
                )
            else:
                failure_detail = f"{type(error).__name__}: {error}"
            if attempt < GOVERNED_JSON_READ_MAX_ATTEMPTS:
                continue
            break
        except ValueError as error:
            raise CohortValidationError(
                f"{label} is not valid UTF-8 JSON: {error}"
            ) from error
        if not isinstance(value, dict):
            raise CohortValidationError(f"{label} must contain one JSON object")
        return value
    raise CohortValidationError(
        f"{label} is not valid UTF-8 JSON after "
        f"{GOVERNED_JSON_READ_MAX_ATTEMPTS} stable-input parse attempts: "
        f"{failure_detail}"
    )


def _require_nonnegative_int(value: Any, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise CohortValidationError(f"{label} must be a nonnegative integer")
    return value


def extract_acceptance_counts(
    inventory: Mapping[str, Any],
    active_coverage: Mapping[str, Any],
    formula_dataset: Mapping[str, Any],
) -> dict[str, int]:
    totals = active_coverage.get("totals")
    if not isinstance(totals, dict):
        raise CohortValidationError("active-coverage totals are missing")
    accepted = _require_nonnegative_int(totals.get("certified"), "certified count")
    quarantined = _require_nonnegative_int(
        totals.get("unresolved"), "unresolved count"
    )
    initial = _require_nonnegative_int(
        totals.get("initialActorCount"), "initial actor count"
    )
    if accepted + quarantined != initial:
        raise CohortValidationError(
            "accepted and quarantined counts do not cover the active actor inventory"
        )

    summary = inventory.get("summary")
    if not isinstance(summary, dict):
        raise CohortValidationError("primary inventory summary is missing")
    runtime_ready = _require_nonnegative_int(
        summary.get("runtimeReadyProfiles"), "runtime-ready profile count"
    )
    unresolved_profiles = _require_nonnegative_int(
        summary.get("unresolvedProfiles"), "unresolved profile count"
    )
    profiles = formula_dataset.get("profiles")
    if not isinstance(profiles, list):
        raise CohortValidationError("formula dataset profiles are missing")
    formula_binding_sections = (
        "acceptedFormula",
        "stimFiendFormula",
        "meldedPatternsFormula",
        "fragmentedSoulFormula",
        "incompleteRebuildFormula",
        "molestedMoleculesFormula",
        "fixedScopeSelectorBindings",
    )
    formula_bindings = 0
    for section_name in formula_binding_sections:
        section = formula_dataset.get(section_name, {})
        if not isinstance(section, dict):
            raise CohortValidationError(
                f"formula dataset section is invalid: {section_name}"
            )
        bindings = section.get("activeBindings", [])
        if not isinstance(bindings, list):
            raise CohortValidationError(
                f"formula active bindings are invalid: {section_name}"
            )
        formula_bindings += len(bindings)
    return {
        "captureSessions": _require_nonnegative_int(
            summary.get("captureSessionsDiscovered"), "capture session count"
        ),
        "canonicalSessions": _require_nonnegative_int(
            summary.get("canonicalValidSessions"), "canonical session count"
        ),
        "completeAttackChains": _require_nonnegative_int(
            summary.get("completeAttackInfoChains"), "complete attack chain count"
        ),
        "certifiedProfiles": _require_nonnegative_int(
            summary.get("captureCertifiedProfiles"), "certified profile count"
        ),
        "accepted": accepted,
        "quarantined": quarantined,
        "initialActors": initial,
        "runtimeReadyProfiles": runtime_ready,
        "semanticDefinitions": _require_nonnegative_int(
            summary.get("captureCertifiedSemanticDefinitions"),
            "semantic definition count",
        ),
        "runtimeReadyDefinitions": _require_nonnegative_int(
            summary.get("runtimeReadyGeneratedSemanticDefinitions"),
            "runtime-ready definition count",
        ),
        "unresolvedProfiles": unresolved_profiles,
        "formulaProfiles": len(profiles),
        "formulaBindings": formula_bindings,
        "generatorErrors": _require_nonnegative_int(
            summary.get("decodeOrProjectionErrors"), "generator error count"
        ),
    }


def validate_audit_inventory_bindings(
    inventory_path: Path,
    attack_range_audit: Mapping[str, Any],
    secondary_evidence_audit: Mapping[str, Any],
) -> None:
    logical_path = ARTIFACT_RELATIVE_PATHS["inventory"].as_posix()
    inventory_sha256 = sha256_file(inventory_path)
    inventory_byte_length = inventory_path.stat().st_size

    if attack_range_audit.get("inventory") != logical_path:
        raise CohortValidationError(
            "attack-range audit is not bound to the governed inventory path"
        )
    if attack_range_audit.get("inventorySha256") != inventory_sha256:
        raise CohortValidationError(
            "attack-range audit is not bound to the governed inventory SHA-256"
        )

    secondary_input = secondary_evidence_audit.get("combatInventoryInput")
    if not isinstance(secondary_input, dict):
        raise CohortValidationError(
            "secondary-evidence audit inventory binding is missing"
        )
    expected_secondary_binding = {
        "path": logical_path,
        "exists": True,
        "sizeBytes": inventory_byte_length,
        "hashStatus": "content-sha256",
        "sha256": inventory_sha256,
    }
    for key, expected in expected_secondary_binding.items():
        if secondary_input.get(key) != expected:
            raise CohortValidationError(
                "secondary-evidence audit is not bound to the governed inventory "
                f"{key}"
            )


def _normalized_capture_snapshot_document(
    snapshot: Mapping[str, Any], schema_version: int
) -> dict[str, Any]:
    captures = [
        {
            "capture": entry["capture"],
            "captureId": entry["captureId"],
            "sourceFiles": entry["sourceFiles"],
            "sessionState": entry["sessionState"],
        }
        for entry in snapshot["captures"]
    ]
    core = {
        "schemaVersion": schema_version,
        "planIdentity": snapshot["planIdentity"],
        "generatorSources": snapshot["generatorSources"],
        "captures": captures,
    }
    return {
        **core,
        "snapshotIdentity": sha256_bytes(identity_json_bytes(core)),
    }


def _portable_snapshot_descriptor(
    snapshot: Mapping[str, Any], auxiliary_input_identity: str
) -> dict[str, Any]:
    expected_fields = {
        "schemaVersion",
        "planIdentity",
        "snapshotIdentity",
        "generatorSources",
        "captures",
    }
    if not isinstance(snapshot, dict) or set(snapshot) != expected_fields:
        raise CohortValidationError("capture input snapshot fields are invalid")
    schema_version = snapshot["schemaVersion"]
    if schema_version != CAPTURE_INPUT_SNAPSHOT_SCHEMA_VERSION:
        raise CohortValidationError("capture input snapshot schemaVersion is unsupported")
    for identity_name in ("planIdentity", "snapshotIdentity"):
        if not isinstance(snapshot[identity_name], str) or not re.fullmatch(
            r"[0-9a-f]{64}", snapshot[identity_name]
        ):
            raise CohortValidationError(
                f"capture input snapshot {identity_name} is invalid"
            )

    def validate_relative_path(value: Any, label: str) -> str:
        if (
            not isinstance(value, str)
            or not value
            or "\\" in value
            or ":" in value
            or value.startswith("/")
            or any(part in ("", ".", "..") for part in value.split("/"))
        ):
            raise CohortValidationError(f"{label} path is invalid")
        return value

    def validate_source_descriptor(value: Any, label: str) -> dict[str, Any]:
        if not isinstance(value, dict) or type(value.get("exists")) is not bool:
            raise CohortValidationError(f"{label} descriptor is invalid")
        expected = (
            {"path", "exists", "byteLength", "sha256"}
            if value["exists"]
            else {"path", "exists"}
        )
        if set(value) != expected:
            raise CohortValidationError(f"{label} descriptor fields are invalid")
        validate_relative_path(value["path"], label)
        if value["exists"]:
            _require_nonnegative_int(value["byteLength"], f"{label} byte length")
            if not isinstance(value["sha256"], str) or not re.fullmatch(
                r"[0-9a-f]{64}", value["sha256"]
            ):
                raise CohortValidationError(f"{label} SHA-256 is invalid")
        return value

    generator_sources = snapshot["generatorSources"]
    captures = snapshot["captures"]
    if not isinstance(generator_sources, list) or not isinstance(captures, list):
        raise CohortValidationError("capture input snapshot collections are invalid")
    for index, descriptor in enumerate(generator_sources):
        validate_source_descriptor(descriptor, f"generator source {index}")
    generator_paths = [descriptor["path"] for descriptor in generator_sources]
    if generator_paths != sorted(generator_paths) or len(generator_paths) != len(
        set(generator_paths)
    ):
        raise CohortValidationError(
            "capture input snapshot generator sources are not sorted and unique"
        )

    capture_keys: list[str] = []
    plan_captures: list[dict[str, Any]] = []
    session_fields = {
        "disposition",
        "capabilityStatus",
        "canonicalValid",
        "recaptureRequired",
        "captureComplete",
        "positiveEvidenceOnly",
        "absenceInferenceAllowed",
        "canonicalPackets",
        "conflictCount",
    }
    for index, entry in enumerate(captures):
        if not isinstance(entry, dict) or set(entry) != {
            "capture",
            "captureId",
            "sourceFiles",
            "sessionState",
            "shard",
        }:
            raise CohortValidationError(
                f"capture input snapshot entry {index} is invalid"
            )
        capture = validate_relative_path(entry["capture"], f"capture {index}")
        if entry["captureId"] != PurePosixPath(capture).name:
            raise CohortValidationError(
                f"capture input snapshot captureId is invalid: {capture}"
            )
        sources = entry["sourceFiles"]
        if not isinstance(sources, list):
            raise CohortValidationError(
                f"capture input snapshot source list is invalid: {capture}"
            )
        for source_index, descriptor in enumerate(sources):
            validate_source_descriptor(
                descriptor, f"capture {capture} source {source_index}"
            )
            if not descriptor["path"].startswith(capture + "/"):
                raise CohortValidationError(
                    f"capture input snapshot source escaped its capture: {capture}"
                )
        source_paths = [descriptor["path"] for descriptor in sources]
        if source_paths != sorted(source_paths) or len(source_paths) != len(
            set(source_paths)
        ):
            raise CohortValidationError(
                f"capture input snapshot sources are not sorted and unique: {capture}"
            )
        session = entry["sessionState"]
        if not isinstance(session, dict) or set(session) != session_fields:
            raise CohortValidationError(
                f"capture input snapshot session state is invalid: {capture}"
            )
        boolean_fields = (
            "canonicalValid",
            "recaptureRequired",
            "captureComplete",
            "positiveEvidenceOnly",
            "absenceInferenceAllowed",
        )
        if any(type(session[name]) is not bool for name in boolean_fields):
            raise CohortValidationError(
                f"capture input snapshot session flags are invalid: {capture}"
            )
        if (
            not isinstance(session["capabilityStatus"], str)
            or not session["capabilityStatus"]
            or session["disposition"]
            != ("accepted" if session["canonicalValid"] else "quarantined")
        ):
            raise CohortValidationError(
                f"capture input snapshot session disposition is invalid: {capture}"
            )
        _require_nonnegative_int(
            session["canonicalPackets"], f"capture {capture} canonical packets"
        )
        _require_nonnegative_int(
            session["conflictCount"], f"capture {capture} conflict count"
        )
        shard = entry["shard"]
        if not isinstance(shard, dict) or set(shard) != {
            "path",
            "byteLength",
            "sha256",
        }:
            raise CohortValidationError(
                f"capture input snapshot shard is invalid: {capture}"
            )
        shard_path = validate_relative_path(shard["path"], f"capture {capture} shard")
        if not re.fullmatch(r"capture-shards/capture-[0-9]{6}\.json", shard_path):
            raise CohortValidationError(
                f"capture input snapshot shard path is invalid: {capture}"
            )
        _require_nonnegative_int(
            shard["byteLength"], f"capture {capture} shard byte length"
        )
        if not isinstance(shard["sha256"], str) or not re.fullmatch(
            r"[0-9a-f]{64}", shard["sha256"]
        ):
            raise CohortValidationError(
                f"capture input snapshot shard SHA-256 is invalid: {capture}"
            )
        capture_keys.append(capture)
        plan_captures.append({"capture": capture, "sourceFiles": sources})
    if capture_keys != sorted(capture_keys) or len(capture_keys) != len(
        set(capture_keys)
    ):
        raise CohortValidationError(
            "capture input snapshot captures are not sorted and unique"
        )
    plan_core = {
        "schemaVersion": schema_version,
        "generatorSources": generator_sources,
        "captures": plan_captures,
    }
    if snapshot["planIdentity"] != sha256_bytes(identity_json_bytes(plan_core)):
        raise CohortValidationError(
            "capture input snapshot plan identity does not match its content"
        )
    snapshot_core = {
        "schemaVersion": schema_version,
        "planIdentity": snapshot["planIdentity"],
        "generatorSources": generator_sources,
        "captures": captures,
    }
    if snapshot["snapshotIdentity"] != sha256_bytes(
        identity_json_bytes(snapshot_core)
    ):
        raise CohortValidationError(
            "capture input snapshot identity does not match its content"
        )
    normalized_snapshot = _normalized_capture_snapshot_document(
        snapshot, schema_version
    )
    canonical = identity_json_bytes(normalized_snapshot)
    canonical_sha256 = sha256_bytes(canonical)
    provided_identity = normalized_snapshot["snapshotIdentity"]
    if not isinstance(auxiliary_input_identity, str) or not re.fullmatch(
        r"[0-9a-f]{64}", auxiliary_input_identity
    ):
        raise CohortValidationError("auxiliary input snapshot identity is invalid")
    combined_core = {
        "schemaVersion": PORTABLE_INPUT_SNAPSHOT_SCHEMA_VERSION,
        "captureSchemaVersion": schema_version,
        "captureSnapshotIdentity": provided_identity,
        "captureManifestSha256": canonical_sha256,
        "auxiliarySnapshotIdentity": auxiliary_input_identity,
    }
    return {
        "schemaVersion": PORTABLE_INPUT_SNAPSHOT_SCHEMA_VERSION,
        "identity": sha256_bytes(identity_json_bytes(combined_core)),
        "captureSchemaVersion": schema_version,
        "captureSnapshotIdentity": provided_identity,
        "captureManifestSha256": canonical_sha256,
        "captureManifestByteLength": len(canonical),
        "auxiliarySnapshotIdentity": auxiliary_input_identity,
    }


def runtime_descriptor(executable: Path | None = None) -> dict[str, Any]:
    # Generated bytes must be identical on Windows and Linux. The concrete
    # interpreter is an execution detail, not an accepted-state input.
    del executable
    return {
        "contract": "python3-cross-platform-deterministic-v1",
        "encoding": "utf-8-lf",
    }


def generator_descriptors(repo_root: Path) -> dict[str, dict[str, Any]]:
    descriptors: dict[str, dict[str, Any]] = {}
    for name, logical_path in sorted(GENERATOR_PATHS.items()):
        descriptors[name] = artifact_descriptor(
            repo_root / Path(logical_path), logical_path
        )
    return descriptors


def auxiliary_input_paths(
    repo_root: Path, *, require_capture_evidence: bool = False
) -> tuple[str, ...]:
    excluded = {
        relative.as_posix().casefold()
        for relative in ARTIFACT_RELATIVE_PATHS.values()
    }
    values = {
        logical.as_posix() for logical in GENERATOR_PATHS.values()
    }
    values.add(ITEM_DATABASE.as_posix())
    values.update(path.as_posix() for path in FORMULA_STATIC_INPUTS)
    formula_source_texts = [
        (repo_root / FORMULA_GENERATOR).read_text(encoding="utf-8")
    ]
    formula_source_texts.extend(
        path.read_text(encoding="utf-8")
        for relative in FORMULA_STATIC_INPUTS
        if (path := repo_root / relative).is_file()
    )
    capture_ids = set(
        re.findall(
            r"\b20[0-9]{6}-[0-9]{6}\b", "\n".join(formula_source_texts)
        )
    )
    missing_capture_roots: list[str] = []
    for capture_id in sorted(capture_ids) if require_capture_evidence else ():
        capture_root = (
            repo_root
            / "tools-temp"
            / "AOSharpLiveCapture"
            / "bin"
            / "Debug"
            / "captures"
            / capture_id
        )
        found_capture_source = False
        for source_name in FORMULA_CAPTURE_SOURCE_NAMES:
            source = capture_root / source_name
            if source.is_file():
                found_capture_source = True
                values.add(source.relative_to(repo_root).as_posix())
        if require_capture_evidence and not found_capture_source:
            missing_capture_roots.append(capture_root.relative_to(repo_root).as_posix())
    if missing_capture_roots:
        raise PipelineError(
            "Required capture evidence is unavailable: "
            + ", ".join(missing_capture_roots)
        )
    active_runtime_root = repo_root / ACTIVE_RUNTIME_SOURCE_ROOT
    if not active_runtime_root.is_dir():
        raise PipelineError(
            "active-coverage runtime source root is missing: "
            f"{ACTIVE_RUNTIME_SOURCE_ROOT.as_posix()}"
        )
    for source in active_runtime_root.rglob("*.cs"):
        if source.is_file() and not source.is_symlink():
            relative = source.relative_to(repo_root).as_posix()
            if relative.casefold() not in excluded:
                values.add(relative)
    for logical_root in SCFU_ANALYZER_SOURCE_ROOTS:
        source_root = repo_root / logical_root
        if not source_root.is_dir():
            raise PipelineError(
                "SCFU analyzer source dependency root is missing: "
                f"{logical_root.as_posix()}"
            )
        for source in source_root.rglob("*"):
            local_parts = source.relative_to(source_root).parts
            if any(part.casefold() in {"bin", "obj"} for part in local_parts):
                continue
            if (
                source.is_file()
                and not source.is_symlink()
                and source.suffix.casefold() in SCFU_ANALYZER_SOURCE_SUFFIXES
            ):
                values.add(source.relative_to(repo_root).as_posix())
    return tuple(sorted(values))


def capture_auxiliary_inputs(
    lease: Any, repo_root: Path, *, require_capture_evidence: bool = False
) -> Any:
    transaction = _load_transaction_module()
    return transaction.InputSnapshot.capture(
        lease,
        auxiliary_input_paths(
            repo_root, require_capture_evidence=require_capture_evidence
        ),
    )


def revalidate_auxiliary_inputs(
    snapshot: Any, repo_root: Path, *, require_capture_evidence: bool = False
) -> None:
    snapshot.revalidate(
        auxiliary_input_paths(
            repo_root, require_capture_evidence=require_capture_evidence
        )
    )


def scfu_analyzer_runtime_paths(repo_root: Path) -> tuple[str, ...]:
    analyzer = repo_root / SCFU_ANALYZER
    runtime_root = analyzer.parent
    if not analyzer.is_file() or not runtime_root.is_dir():
        raise PipelineError(
            "SCFU analyzer executable is missing; build it with the documented "
            "AOSharpCaptureAnalyzer MSBuild command before --check or --write"
        )
    values = []
    for source in runtime_root.rglob("*"):
        if source.is_file() and not source.is_symlink():
            values.append(source.relative_to(repo_root).as_posix())
    return tuple(sorted(values))


def capture_scfu_analyzer_runtime(lease: Any, repo_root: Path) -> Any:
    transaction = _load_transaction_module()
    return transaction.InputSnapshot.capture(
        lease,
        scfu_analyzer_runtime_paths(repo_root),
    )


def revalidate_scfu_analyzer_runtime(snapshot: Any, repo_root: Path) -> None:
    snapshot.revalidate(scfu_analyzer_runtime_paths(repo_root))


_ABSOLUTE_WINDOWS_PATH = re.compile(r"^[A-Za-z]:[\\/]")
_PATH_BOUNDARY_CHARACTERS = frozenset(
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_+.-"
)
_VOLATILE_MANIFEST_KEYS = frozenset(
    {
        "pid",
        "processid",
        "starttime",
        "endtime",
        "timestamp",
        "generatedat",
        "createdat",
        "username",
        "user",
        "temppath",
        "absolutepath",
    }
)


def _contains_absolute_windows_path_text(text: str) -> bool:
    if "\\\\" in text:
        return True
    for delimiter in (":\\", ":/"):
        start = 0
        while True:
            index = text.find(delimiter, start)
            if index < 0:
                break
            if index > 0 and text[index - 1].isalpha() and (
                index == 1 or text[index - 2] not in _PATH_BOUNDARY_CHARACTERS
            ):
                return True
            start = index + len(delimiter)
    return False


def assert_manifest_is_path_independent(value: Any, location: str = "manifest") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            if not isinstance(key, str):
                raise CohortValidationError(f"{location} contains a non-string key")
            if key.casefold() in _VOLATILE_MANIFEST_KEYS:
                raise CohortValidationError(
                    f"{location} contains forbidden volatile field {key!r}"
                )
            assert_manifest_is_path_independent(child, f"{location}.{key}")
        return
    if isinstance(value, list):
        for index, child in enumerate(value):
            assert_manifest_is_path_independent(child, f"{location}[{index}]")
        return
    if isinstance(value, str):
        if _ABSOLUTE_WINDOWS_PATH.match(value) or value.startswith(("/", "\\\\")):
            raise CohortValidationError(
                f"{location} contains an absolute or machine-specific path"
            )


def assert_generated_value_is_path_independent(
    value: Any, location: str
) -> None:
    stack: list[Any] = [value]
    while stack:
        current = stack.pop()
        if isinstance(current, dict):
            stack.extend(current.values())
            continue
        if isinstance(current, list):
            stack.extend(current)
            continue
        if isinstance(current, str) and _contains_absolute_windows_path_text(current):
            raise CohortValidationError(
                f"{location} contains an absolute repository-location-dependent path"
            )


def _manifest_identity_payload(manifest: Mapping[str, Any]) -> dict[str, Any]:
    return {
        "schemaVersion": manifest["schemaVersion"],
        "pipeline": manifest["pipeline"],
        "inputSnapshot": manifest["inputSnapshot"],
        "generators": manifest["generators"],
        "runtime": manifest["runtime"],
        "counts": manifest["counts"],
        "artifacts": manifest["artifacts"],
    }


def build_generation_manifest(
    *,
    cohort_root: Path,
    artifacts: Mapping[str, Path],
    input_snapshot: Mapping[str, Any],
    auxiliary_input_identity: str,
    generators: Mapping[str, Mapping[str, Any]],
    runtime: Mapping[str, Any],
    input_snapshot_is_portable: bool = False,
) -> tuple[dict[str, Any], bytes]:
    if set(artifacts) != set(ARTIFACT_RELATIVE_PATHS):
        raise PipelineError("candidate cohort does not contain the exact artifact roles")

    inventory = load_json_object(artifacts["inventory"], "primary inventory")
    active = load_json_object(artifacts["activeCoverage"], "active coverage")
    formula = load_json_object(artifacts["formulaDataset"], "formula dataset")
    attack_range_audit = load_json_object(
        artifacts["attackRangeAudit"], "attack-range audit"
    )
    secondary_evidence_audit = load_json_object(
        artifacts["secondaryEvidenceAudit"], "secondary-evidence audit"
    )
    assert_generated_value_is_path_independent(inventory, "primary inventory")
    assert_generated_value_is_path_independent(active, "active coverage")
    assert_generated_value_is_path_independent(formula, "formula dataset")
    assert_generated_value_is_path_independent(
        attack_range_audit, "attack-range audit"
    )
    assert_generated_value_is_path_independent(
        secondary_evidence_audit, "secondary-evidence audit"
    )
    validate_audit_inventory_bindings(
        artifacts["inventory"], attack_range_audit, secondary_evidence_audit
    )
    counts = extract_acceptance_counts(inventory, active, formula)

    artifact_rows = []
    for role, logical_path in ARTIFACT_RELATIVE_PATHS.items():
        row = artifact_descriptor(artifacts[role], logical_path)
        row["role"] = role
        artifact_rows.append(row)

    manifest: dict[str, Any] = {
        "schemaVersion": MANIFEST_SCHEMA_VERSION,
        "pipeline": PIPELINE_NAME,
        "inputSnapshot": (
            dict(input_snapshot)
            if input_snapshot_is_portable
            else _portable_snapshot_descriptor(
                input_snapshot, auxiliary_input_identity
            )
        ),
        "generators": {
            key: dict(value) for key, value in sorted(generators.items())
        },
        "runtime": dict(runtime),
        "counts": counts,
        "artifacts": artifact_rows,
    }
    assert_manifest_is_path_independent(manifest)
    manifest["generationIdentity"] = sha256_bytes(
        identity_json_bytes(_manifest_identity_payload(manifest))
    )
    rendered = canonical_json_bytes(manifest)
    # The root is deliberately unused in the identity. Keeping it in the API
    # makes location-invariance explicit and testable.
    del cohort_root
    return manifest, rendered


def iterate_pair_to_fixed_point(
    transition: Callable[[PairState, int], PairState],
    initial: PairState,
    *,
    max_rounds: int = DEFAULT_MAX_FIXED_POINT_ROUNDS,
) -> FixedPointResult:
    if max_rounds < 1:
        raise ValueError("max_rounds must be positive")
    previous = initial
    seen: dict[str, PairState] = {previous.digest: previous}
    identities = [previous.digest]
    for round_number in range(1, max_rounds + 1):
        current = transition(previous, round_number)
        if not isinstance(current, PairState):
            raise TypeError("fixed-point transition must return PairState")
        identity = current.digest
        identities.append(identity)
        if current == previous:
            return FixedPointResult(current, round_number, tuple(identities))
        seen_state = seen.get(identity)
        if seen_state is not None:
            if seen_state != current:
                raise FixedPointError("active/formula pair SHA-256 collision detected")
            raise FixedPointError(
                "active/formula generation entered a deterministic cycle before convergence"
            )
        seen[identity] = current
        previous = current
    raise FixedPointError(
        f"active/formula generation did not converge within {max_rounds} rounds"
    )


def _process_output_detail(stdout: str, stderr: str) -> str:
    sections = []
    for label, value in (("stdout", stdout), ("stderr", stderr)):
        if value.strip():
            sections.append(f"{label}:\n{value.strip()}")
    return "\n".join(sections)


def _bounded_detail(detail: str) -> str:
    head_characters = 2000
    tail_characters = 3500
    if len(detail) <= head_characters + tail_characters:
        return detail
    omitted = len(detail) - head_characters - tail_characters
    return (
        f"{detail[:head_characters]}\n"
        f"... {omitted} characters omitted ...\n"
        f"{detail[-tail_characters:]}"
    )


def _bounded_process_detail(completed: subprocess.CompletedProcess[str]) -> str:
    return _bounded_detail(
        _process_output_detail(completed.stdout, completed.stderr)
    )


def _is_transient_interpreter_failure(return_code: int, detail: str) -> bool:
    if return_code in {-signal.SIGINT, -signal.SIGTERM}:
        return False
    normalized = return_code & 0xFFFFFFFF
    if normalized == 0xC000013A:
        return False
    if return_code < 0 or 0xC0000000 <= normalized <= 0xCFFFFFFF:
        return True
    if any(
        marker in detail
        for marker in (
            "Windows fatal exception: access violation",
            "Windows fatal exception: stack overflow",
            "SystemError:",
            "AttributeError: 'datetime.timezone' object has no attribute 'astimezone'",
        )
    ):
        return True
    json_decoder_failure = (
        "ValueError: invalid literal for int() with base 10:" in detail
        and ("json\\decoder.py" in detail or "json/decoder.py" in detail)
    )
    governed_json_parse_failure = (
        "json.decoder.JSONDecodeError:" in detail and "decode_json_text" in detail
    )
    governed_json_internal_failure = (
        ("TypeError:" in detail or "AttributeError:" in detail)
        and "decode_json_text" in detail
        and ("json\\decoder.py" in detail or "json/decoder.py" in detail)
        and ("json\\scanner.py" in detail or "json/scanner.py" in detail)
    )
    verified_item_database_failure = (
        "verified item-template decode failure:" in detail
    )
    capture_decoder_internal_failure = (
        ("TypeError:" in detail or "AttributeError:" in detail)
        and "extract_capture_backed_npc_combat.py" in detail
        and ("parse_capture" in detail or "decode_" in detail)
    )
    aggregate_worker_state_corruption = (
        "extract_capture_backed_npc_combat.py" in detail
        and "TypeError:" in detail
        and "object is not iterable" in detail
        and "context_candidates" in detail
    )
    return (
        json_decoder_failure
        or governed_json_parse_failure
        or governed_json_internal_failure
        or verified_item_database_failure
        or capture_decoder_internal_failure
        or aggregate_worker_state_corruption
    )


def _terminate_process_tree(
    process: subprocess.Popen[str], *, grace_seconds: float = 30.0
) -> tuple[str, str, str]:
    cleanup_detail = ""
    if os.name == "nt":
        try:
            terminated = subprocess.run(
                ("taskkill", "/PID", str(process.pid), "/T", "/F"),
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                encoding="utf-8",
                errors="replace",
                check=False,
                timeout=grace_seconds,
            )
            if terminated.returncode != 0:
                cleanup_detail = (
                    f"taskkillExit={terminated.returncode} "
                    f"taskkillError={terminated.stderr.strip()[-1000:]}"
                )
        except (OSError, subprocess.TimeoutExpired) as cleanup_error:
            cleanup_detail = f"taskkillFailure={type(cleanup_error).__name__}"
        if process.poll() is None:
            process.kill()
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except (OSError, ProcessLookupError) as cleanup_error:
            cleanup_detail = f"killpgFailure={type(cleanup_error).__name__}"
            if process.poll() is None:
                process.kill()
    try:
        stdout, stderr = process.communicate(timeout=grace_seconds)
    except subprocess.TimeoutExpired:
        stdout, stderr = "", ""
        cleanup_detail = (cleanup_detail + " outputPipesDidNotClose").strip()
    return stdout, stderr, cleanup_detail


def run_checked(
    command: Sequence[str],
    *,
    repo_root: Path,
    lease: Any,
    label: str = "child",
    environment_overrides: Mapping[str, str] | None = None,
    retry_interpreter_failures: bool = False,
) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    environment[LEASE_DELEGATION_ENVIRONMENT] = json.dumps(
        lease.delegation(), sort_keys=True, separators=(",", ":")
    )
    environment[LEASE_REPO_ROOT_ENVIRONMENT] = str(lease.repo_root)
    if environment_overrides:
        environment.update(environment_overrides)
    max_attempts = 3 if retry_interpreter_failures else 1
    for attempt in range(1, max_attempts + 1):
        process = subprocess.Popen(
            list(command),
            cwd=repo_root,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            creationflags=(
                subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
            ),
            start_new_session=os.name != "nt",
        )
        try:
            stdout, stderr = process.communicate(
                timeout=CHILD_PROCESS_TIMEOUT_SECONDS
            )
        except subprocess.TimeoutExpired as error:
            stdout, stderr, cleanup_detail = _terminate_process_tree(process)
            detail = _process_output_detail(stdout, stderr)
            if cleanup_detail:
                detail = "\n".join(
                    value for value in (detail, cleanup_detail) if value
                )
            detail = _bounded_detail(detail)
            suffix = f": {detail}" if detail else ""
            raise PipelineError(
                f"generated-combat {label} timed out "
                f"after {CHILD_PROCESS_TIMEOUT_SECONDS}s pid={process.pid}{suffix}"
            ) from error
        completed = subprocess.CompletedProcess(
            list(command),
            process.returncode,
            stdout,
            stderr,
        )
        if completed.returncode == 0:
            return completed
        full_detail = _process_output_detail(completed.stdout, completed.stderr)
        if (
            retry_interpreter_failures
            and attempt < max_attempts
            and _is_transient_interpreter_failure(completed.returncode, full_detail)
        ):
            continue
        detail = _bounded_detail(full_detail)
        suffix = f": {detail}" if detail else ""
        attempt_suffix = (
            f" on attempt {attempt}/{max_attempts}"
            if retry_interpreter_failures
            else ""
        )
        raise PipelineError(
            f"generated-combat {label} failed with exit code "
            f"{completed.returncode}{attempt_suffix}{suffix}"
        )
    raise AssertionError("generated child retry loop exited unexpectedly")


def _candidate_artifact_paths(candidate_root: Path) -> dict[str, Path]:
    paths = {
        role: candidate_root / Path(logical_path)
        for role, logical_path in ARTIFACT_RELATIVE_PATHS.items()
    }
    for path in paths.values():
        path.parent.mkdir(parents=True, exist_ok=True)
    return paths


def _write_verified_private_input(path: Path, payload: bytes, label: str) -> None:
    with path.open("xb") as writer:
        writer.write(payload)
        writer.flush()
        os.fsync(writer.fileno())
    if path.read_bytes() != payload:
        raise PipelineError(f"{label} failed exact readback verification")


def _read_verified_private_input(
    source: Path,
    *,
    expected_sha256: str,
    expected_byte_length: int,
    label: str,
) -> bytes:
    failures: list[str] = []
    for attempt in range(1, 4):
        try:
            payload = source.read_bytes()
        except OSError as error:
            failures.append(f"attempt {attempt}: {type(error).__name__}")
            continue
        actual_sha256 = sha256_bytes(payload)
        if len(payload) != expected_byte_length or actual_sha256 != expected_sha256:
            failures.append(
                f"attempt {attempt}: expected {expected_byte_length}/"
                f"{expected_sha256}, found {len(payload)}/{actual_sha256}"
            )
            continue
        return payload
    raise PipelineError(f"{label} read failed: {'; '.join(failures)}")


def _write_round_seed(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=False)
    _write_verified_private_input(path, payload, "active/formula round seed")


def _stage_short_scfu_analyzer(analyzer: Path, staging_root: Path) -> Path:
    source_root = analyzer.parent.resolve(strict=True)
    destination_root = staging_root.resolve(strict=True) / "a"
    destination_root.mkdir()
    copied = 0
    for source in sorted(source_root.rglob("*"), key=lambda path: path.as_posix()):
        if source.is_dir():
            continue
        if not source.is_file() or source.is_symlink():
            raise PipelineError("frozen SCFU analyzer member is not regular")
        destination = destination_root / source.relative_to(source_root)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        if (
            destination.stat().st_size != source.stat().st_size
            or sha256_file(destination) != sha256_file(source)
        ):
            raise PipelineError("short SCFU analyzer snapshot changed during copy")
        copied += 1
    executable = destination_root / analyzer.name
    if copied == 0 or not executable.is_file():
        raise PipelineError("short SCFU analyzer snapshot is incomplete")
    return executable


def _build_item_template_projection(
    *,
    repo_root: Path,
    frozen_repo_root: Path,
    analyzer: Path,
    template_ids: Sequence[int],
    projection_name: str,
    item_database_path: Path,
    item_database_sha256: str,
    item_database_byte_length: int,
    lease: Any,
) -> tuple[bytes, Path]:
    template_ids = sorted(set(template_ids))
    if not template_ids:
        raise PipelineError("item-template projection references no templates")
    if not re.fullmatch(r"[a-z0-9-]+", projection_name):
        raise PipelineError("item-template projection name is invalid")
    if not analyzer.is_file():
        raise PipelineError(
            "SCFU analyzer executable is missing; build it with the documented "
            "AOSharpCaptureAnalyzer MSBuild command before --check or --write"
        )
    output = (
        frozen_repo_root
        / "_item-template-projection"
        / (projection_name + ".json")
    )
    output.parent.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="aor-scfu-projection-") as runtime_name:
        staged_analyzer = _stage_short_scfu_analyzer(
            analyzer, Path(runtime_name)
        )
        completed = run_checked(
            (
                str(staged_analyzer),
                "--project-item-templates",
                str(item_database_path),
                item_database_sha256,
                str(item_database_byte_length),
                ",".join(str(template_id) for template_id in template_ids),
                str(output),
            ),
            repo_root=repo_root,
            lease=lease,
            label="item-template projection",
        )
    if not output.is_file():
        detail = _bounded_process_detail(completed)
        suffix = f": {detail}" if detail else ""
        raise PipelineError(f"item-template projector omitted its output{suffix}")
    document = load_json_object(output, "item-template projection")
    raw_templates = document.get("templates")
    if not isinstance(raw_templates, dict):
        raise PipelineError("item-template projection root is invalid")
    actual_ids: set[int] = set()
    try:
        actual_ids = {int(value) for value in raw_templates}
    except (TypeError, ValueError) as error:
        raise PipelineError("item-template projection contains an invalid ID") from error
    if actual_ids != set(template_ids):
        raise PipelineError("item-template projection membership is incomplete")
    payload = canonical_json_bytes(document)
    try:
        output.unlink()
    except OSError as error:
        raise PipelineError("item-template projection could not be replaced") from error
    _write_verified_private_input(output, payload, "item-template projection")
    return payload, output


def _run_candidate_inventory_audits(
    *,
    repo_root: Path,
    artifacts: Mapping[str, Path],
    auxiliary_snapshot: Any,
    lease: Any,
) -> None:
    inventory_logical_path = ARTIFACT_RELATIVE_PATHS["inventory"].as_posix()
    run_checked(
        (
            sys.executable,
            "-B",
            "-I",
            "-u",
            "-X",
            "faulthandler",
            str(auxiliary_snapshot.path_for(ATTACK_RANGE_AUDIT.as_posix())),
            "--inventory",
            str(artifacts["inventory"]),
            "--inventory-logical-path",
            inventory_logical_path,
            "--output",
            str(artifacts["attackRangeAudit"]),
            "--write",
            "--summary-only",
        ),
        repo_root=repo_root,
        lease=lease,
        label="attack-range audit",
        environment_overrides={
            NPC_COMBAT_AUDIT_REPO_ROOT_ENVIRONMENT: str(repo_root)
        },
        retry_interpreter_failures=True,
    )
    load_json_object(artifacts["attackRangeAudit"], "attack-range audit")

    run_checked(
        (
            sys.executable,
            "-B",
            "-I",
            "-u",
            "-X",
            "faulthandler",
            str(
                auxiliary_snapshot.path_for(
                    SECONDARY_EVIDENCE_AUDIT.as_posix()
                )
            ),
            "--write",
        ),
        repo_root=repo_root,
        lease=lease,
        label="secondary-evidence audit",
        environment_overrides={
            NPC_COMBAT_AUDIT_REPO_ROOT_ENVIRONMENT: str(repo_root),
            NPC_COMBAT_AUDIT_INVENTORY_ENVIRONMENT: str(artifacts["inventory"]),
            NPC_COMBAT_AUDIT_INVENTORY_LOGICAL_PATH_ENVIRONMENT: inventory_logical_path,
            NPC_COMBAT_SECONDARY_AUDIT_OUTPUT_ENVIRONMENT: str(
                artifacts["secondaryEvidenceAudit"]
            ),
        },
        retry_interpreter_failures=True,
    )
    load_json_object(
        artifacts["secondaryEvidenceAudit"], "secondary-evidence audit"
    )


def _run_active_formula_fixed_point(
    *,
    repo_root: Path,
    frozen_repo_root: Path,
    active_inventory_payload: bytes,
    formula_inventory_payload: bytes,
    authoritative_inventory_path: Path,
    item_template_projection_payload: bytes,
    lease: Any,
    max_rounds: int,
) -> FixedPointResult:
    rounds_root = frozen_repo_root / "_active-formula-rounds"
    rounds_root.mkdir()
    initial_payload = canonical_json_bytes({})
    initial = PairState(initial_payload, initial_payload)
    active_inventory_sha256 = sha256_bytes(active_inventory_payload)
    active_inventory_byte_length = len(active_inventory_payload)
    formula_inventory_sha256 = sha256_bytes(formula_inventory_payload)
    formula_inventory_byte_length = len(formula_inventory_payload)
    item_template_projection_sha256 = sha256_bytes(
        item_template_projection_payload
    )
    item_template_projection_byte_length = len(item_template_projection_payload)
    memoized_identity_transition: PairState | None = None

    def transition(previous: PairState, round_number: int) -> PairState:
        nonlocal memoized_identity_transition
        if memoized_identity_transition is not None:
            if memoized_identity_transition != previous:
                raise PipelineError("fixed-point memoization state is stale")
            return memoized_identity_transition
        round_root = rounds_root / f"round-{round_number:02d}"
        active_inventory_path = (
            round_root / "active-input" / "combat-inventory.json"
        )
        formula_inventory_path = (
            round_root / "formula-input" / "combat-inventory.json"
        )
        formula_item_projection_path = (
            round_root / "formula-items" / "item-template-projection.json"
        )
        formula_seed = round_root / "formula-seed" / "formula-dataset.json"
        _write_round_seed(active_inventory_path, active_inventory_payload)
        _write_round_seed(formula_seed, previous.formula_dataset)
        active_output = round_root / "output" / "active-coverage.json"
        active_output.parent.mkdir(parents=True)
        active_completed = run_checked(
            (
                sys.executable,
                "-B",
                "-I",
                "-u",
                "-X",
                "faulthandler",
                str(frozen_repo_root / ACTIVE_GENERATOR),
                "--write",
                "--repo-root",
                str(frozen_repo_root),
                "--combat-inventory",
                str(active_inventory_path),
                "--combat-inventory-descriptor",
                str(authoritative_inventory_path),
                "--combat-inventory-sha256",
                active_inventory_sha256,
                "--combat-inventory-byte-length",
                str(active_inventory_byte_length),
                "--formula-dataset",
                str(formula_seed),
                "--output",
                str(active_output),
            ),
            repo_root=repo_root,
            lease=lease,
            label=f"active-coverage round {round_number}",
            retry_interpreter_failures=True,
        )
        if not active_output.is_file():
            detail = _bounded_process_detail(active_completed)
            suffix = f": {detail}" if detail else ""
            raise PipelineError(
                f"active-coverage generator omitted its staged output{suffix}"
            )
        active_payload = active_output.read_bytes()
        if (
            round_number > 1
            and active_payload == previous.active_coverage
        ):
            return PairState(active_payload, previous.formula_dataset)
        _write_round_seed(formula_inventory_path, formula_inventory_payload)
        _write_round_seed(
            formula_item_projection_path, item_template_projection_payload
        )
        formula_output = round_root / "output" / "formula-dataset.json"
        formula_completed = run_checked(
            (
                sys.executable,
                "-B",
                "-I",
                "-u",
                "-X",
                "faulthandler",
                str(frozen_repo_root / FORMULA_GENERATOR),
                "--write",
                "--inventory",
                str(formula_inventory_path),
                "--inventory-sha256",
                formula_inventory_sha256,
                "--inventory-byte-length",
                str(formula_inventory_byte_length),
                "--item-template-projection",
                str(formula_item_projection_path),
                "--item-template-projection-sha256",
                item_template_projection_sha256,
                "--item-template-projection-byte-length",
                str(item_template_projection_byte_length),
                "--active-coverage",
                str(active_output),
                "--output",
                str(formula_output),
            ),
            repo_root=repo_root,
            lease=lease,
            label=f"formula round {round_number}",
            retry_interpreter_failures=True,
        )
        if not formula_output.is_file():
            detail = _bounded_process_detail(formula_completed)
            suffix = f": {detail}" if detail else ""
            raise PipelineError(
                f"formula generator omitted its staged output{suffix}"
            )
        formula_payload = formula_output.read_bytes()
        current = PairState(active_payload, formula_payload)
        if formula_payload == previous.formula_dataset:
            # current.active = f(previous.formula) and current.formula =
            # g(current.active). Equal formula bytes therefore prove the next
            # transition is exactly current without launching either child.
            memoized_identity_transition = current
        return current

    return iterate_pair_to_fixed_point(
        transition,
        initial,
        max_rounds=max_rounds,
    )


def build_candidate_cohort(
    repo_root: Path,
    candidate_root: Path,
    *,
    auxiliary_snapshot: Any,
    scfu_analyzer_snapshot: Any,
    lease: Any,
    max_rounds: int = DEFAULT_MAX_FIXED_POINT_ROUNDS,
) -> CandidateCohort:
    repo_root = repo_root.resolve(strict=True)
    candidate_root = candidate_root.resolve(strict=True)
    try:
        candidate_root.relative_to(repo_root)
    except ValueError as error:
        raise PipelineError("candidate root must stay inside the repository") from error

    artifacts = _candidate_artifact_paths(candidate_root)
    generators_before = generator_descriptors(repo_root)
    runtime_before = runtime_descriptor()
    frozen_repo_root = auxiliary_snapshot.snapshot_root
    frozen_scfu_analyzer = scfu_analyzer_snapshot.path_for(SCFU_ANALYZER.as_posix())
    item_database_record = next(
        (
            record
            for record in auxiliary_snapshot.records
            if record.relative_path == ITEM_DATABASE.as_posix()
        ),
        None,
    )
    if item_database_record is None:
        raise PipelineError("frozen item database descriptor is missing")
    _read_verified_private_input(
        auxiliary_snapshot.path_for(ITEM_DATABASE.as_posix()),
        expected_sha256=item_database_record.sha256,
        expected_byte_length=item_database_record.size,
        label="frozen item database",
    )
    (
        arete_range_item_projection_payload,
        arete_range_item_projection_path,
    ) = _build_item_template_projection(
        repo_root=repo_root,
        frozen_repo_root=frozen_repo_root,
        analyzer=frozen_scfu_analyzer,
        template_ids=ARETE_ATTACK_RANGE_ITEM_TEMPLATE_IDS,
        projection_name="arete-attack-range",
        item_database_path=auxiliary_snapshot.path_for(ITEM_DATABASE.as_posix()),
        item_database_sha256=item_database_record.sha256,
        item_database_byte_length=item_database_record.size,
        lease=lease,
    )
    arete_range_projection_sha256 = sha256_bytes(
        arete_range_item_projection_payload
    )
    arete_range_projection_byte_length = len(
        arete_range_item_projection_payload
    )
    with tempfile.TemporaryDirectory(
        prefix="aorebirth-generated-combat-input-snapshot-"
    ) as snapshot_root_name:
        snapshot_path = Path(snapshot_root_name) / "capture-input-snapshot.json"
        accepted_primary_signatures: set[str] = set()
        observed_primary_signatures: list[str] = []
        observed_primary_descriptors: list[dict[str, Any]] = []
        for primary_attempt in range(1, PRIMARY_AGGREGATION_MAX_ATTEMPTS + 1):
            snapshot_path.unlink(missing_ok=True)
            run_checked(
                (
                    sys.executable,
                    "-B",
                    "-I",
                    "-u",
                    "-X",
                    "faulthandler",
                    str(auxiliary_snapshot.path_for(PRIMARY_GENERATOR.as_posix())),
                    "--write",
                    "--output",
                    str(artifacts["inventory"]),
                    "--catalog-output",
                    str(artifacts["catalog"]),
                    "--fixture-output",
                    str(artifacts["fixtures"]),
                    "--item-template-projection",
                    str(arete_range_item_projection_path),
                    "--item-template-projection-sha256",
                    arete_range_projection_sha256,
                    "--item-template-projection-byte-length",
                    str(arete_range_projection_byte_length),
                    "--item-database-sha256",
                    item_database_record.sha256,
                    "--item-database-byte-length",
                    str(item_database_record.size),
                    PRIMARY_SNAPSHOT_ARGUMENT,
                    str(snapshot_path),
                ),
                repo_root=repo_root,
                lease=lease,
                label="primary aggregation",
                environment_overrides={
                    PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT: str(repo_root),
                    PRIMARY_SCFU_ANALYZER_ENVIRONMENT: str(frozen_scfu_analyzer),
                },
                retry_interpreter_failures=True,
            )
            try:
                if not snapshot_path.is_file():
                    raise CohortValidationError(
                        "capture input snapshot is missing or is not a regular file"
                    )
                inventory = load_json_object(artifacts["inventory"], "primary inventory")
                active_inventory_projection = build_generator_inventory_projection(
                    inventory
                )
                formula_inventory_projection = build_formula_inventory_projection(
                    inventory
                )
                snapshot = load_json_object(snapshot_path, "capture input snapshot")
                snapshot_descriptor = _portable_snapshot_descriptor(
                    snapshot, auxiliary_snapshot.identity
                )
                signature = primary_output_signature(
                    artifacts, snapshot_descriptor
                )
                descriptor = primary_output_signature_descriptor(
                    artifacts, snapshot_descriptor
                )
            except CohortValidationError as error:
                if primary_attempt < PRIMARY_AGGREGATION_MAX_ATTEMPTS:
                    continue
                raise PipelineError(
                    "primary aggregation output validation failed on attempt "
                    f"{primary_attempt}/{PRIMARY_AGGREGATION_MAX_ATTEMPTS}: {error}"
                ) from error
            observed_primary_signatures.append(signature)
            observed_primary_descriptors.append(descriptor)
            if signature in accepted_primary_signatures:
                break
            accepted_primary_signatures.add(signature)
        else:
            raise PipelineError(
                "primary aggregation did not produce two matching validated outputs "
                f"in {PRIMARY_AGGREGATION_MAX_ATTEMPTS} attempts: "
                + ", ".join(observed_primary_signatures)
                + "; descriptors="
                + json.dumps(
                    observed_primary_descriptors,
                    sort_keys=True,
                    separators=(",", ":"),
                )
            )

    for role in ("inventory", "catalog", "fixtures"):
        frozen_path = frozen_repo_root / Path(ARTIFACT_RELATIVE_PATHS[role])
        frozen_path.parent.mkdir(parents=True, exist_ok=True)
        frozen_path.write_bytes(artifacts[role].read_bytes())

    authoritative_inventory_path = frozen_repo_root / Path(
        ARTIFACT_RELATIVE_PATHS["inventory"]
    )
    active_inventory_projection_bytes = canonical_json_bytes(
        active_inventory_projection
    )
    if (
        decode_json_text(active_inventory_projection_bytes.decode("utf-8"))
        != active_inventory_projection
    ):
        raise PipelineError("private active inventory projection is not canonical")
    formula_inventory_projection_bytes = canonical_json_bytes(
        formula_inventory_projection
    )
    if (
        decode_json_text(formula_inventory_projection_bytes.decode("utf-8"))
        != formula_inventory_projection
    ):
        raise PipelineError("private formula inventory projection is not canonical")

    item_template_projection_payload, _ = _build_item_template_projection(
        repo_root=repo_root,
        frozen_repo_root=frozen_repo_root,
        analyzer=frozen_scfu_analyzer,
        template_ids=sorted(
            collect_referenced_formula_template_ids(formula_inventory_projection)
        ),
        projection_name="formula",
        item_database_path=auxiliary_snapshot.path_for(ITEM_DATABASE.as_posix()),
        item_database_sha256=item_database_record.sha256,
        item_database_byte_length=item_database_record.size,
        lease=lease,
    )

    fixed_point = _run_active_formula_fixed_point(
        repo_root=repo_root,
        frozen_repo_root=frozen_repo_root,
        active_inventory_payload=active_inventory_projection_bytes,
        formula_inventory_payload=formula_inventory_projection_bytes,
        authoritative_inventory_path=authoritative_inventory_path,
        item_template_projection_payload=item_template_projection_payload,
        lease=lease,
        max_rounds=max_rounds,
    )
    artifacts["activeCoverage"].write_bytes(fixed_point.state.active_coverage)
    artifacts["formulaDataset"].write_bytes(fixed_point.state.formula_dataset)

    _run_candidate_inventory_audits(
        repo_root=repo_root,
        artifacts=artifacts,
        auxiliary_snapshot=auxiliary_snapshot,
        lease=lease,
    )

    generators_after = generator_descriptors(repo_root)
    runtime_after = runtime_descriptor()
    if generators_after != generators_before or runtime_after != runtime_before:
        raise PipelineError("generator or Python runtime changed during candidate generation")

    manifest, rendered = build_generation_manifest(
        cohort_root=candidate_root,
        artifacts=artifacts,
        input_snapshot=snapshot,
        auxiliary_input_identity=auxiliary_snapshot.identity,
        generators=generators_before,
        runtime=runtime_before,
    )
    manifest_path = candidate_root / Path(MANIFEST_RELATIVE_PATH)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_bytes(rendered)
    validate_cohort(candidate_root, verify_toolchain=False)
    return CandidateCohort(
        root=candidate_root,
        artifacts=artifacts,
        manifest_path=manifest_path,
        capture_snapshot=snapshot,
        generation_identity=manifest["generationIdentity"],
        input_snapshot_identity=manifest["inputSnapshot"]["identity"],
        fixed_point_rounds=fixed_point.rounds,
    )


def build_accepted_candidate_cohort(
    repo_root: Path,
    candidate_root: Path,
    *,
    accepted_manifest: Mapping[str, Any],
    auxiliary_snapshot: Any,
    scfu_analyzer_snapshot: Any,
    lease: Any,
    max_rounds: int = DEFAULT_MAX_FIXED_POINT_ROUNDS,
) -> CandidateCohort:
    """Regenerate every derived artifact from promoted repository state."""
    repo_root = repo_root.resolve(strict=True)
    candidate_root = candidate_root.resolve(strict=True)
    try:
        candidate_root.relative_to(repo_root)
    except ValueError as error:
        raise PipelineError("candidate root must stay inside the repository") from error

    artifacts = _candidate_artifact_paths(candidate_root)
    generators_before = generator_descriptors(repo_root)
    runtime_before = runtime_descriptor()
    frozen_repo_root = auxiliary_snapshot.snapshot_root
    frozen_scfu_analyzer = scfu_analyzer_snapshot.path_for(SCFU_ANALYZER.as_posix())
    item_database_record = next(
        (
            record
            for record in auxiliary_snapshot.records
            if record.relative_path == ITEM_DATABASE.as_posix()
        ),
        None,
    )
    if item_database_record is None:
        raise PipelineError("frozen item database descriptor is missing")
    _read_verified_private_input(
        auxiliary_snapshot.path_for(ITEM_DATABASE.as_posix()),
        expected_sha256=item_database_record.sha256,
        expected_byte_length=item_database_record.size,
        label="frozen item database",
    )

    accepted_inventory_path = repo_root / Path(ARTIFACT_RELATIVE_PATHS["inventory"])
    accepted_inventory_payload = accepted_inventory_path.read_bytes()
    inventory_row = next(
        (
            row
            for row in accepted_manifest["artifacts"]
            if row.get("role") == "inventory"
        ),
        None,
    )
    if inventory_row is None:
        raise PipelineError("accepted manifest inventory descriptor is missing")
    if (
        len(accepted_inventory_payload) != inventory_row["byteLength"]
        or sha256_bytes(accepted_inventory_payload) != inventory_row["sha256"]
    ):
        raise PipelineError("accepted inventory does not match its committed descriptor")
    artifacts["inventory"].write_bytes(accepted_inventory_payload)

    run_checked(
        (
            sys.executable,
            "-B",
            "-I",
            "-u",
            "-X",
            "faulthandler",
            str(auxiliary_snapshot.path_for(PRIMARY_GENERATOR.as_posix())),
            "--render-accepted-inventory",
            str(artifacts["inventory"]),
            "--catalog-output",
            str(artifacts["catalog"]),
            "--fixture-output",
            str(artifacts["fixtures"]),
        ),
        repo_root=repo_root,
        lease=lease,
        label="accepted-inventory rendering",
        retry_interpreter_failures=True,
    )

    inventory = load_json_object(artifacts["inventory"], "accepted inventory")
    active_inventory_projection = build_generator_inventory_projection(inventory)
    formula_inventory_projection = build_formula_inventory_projection(inventory)
    for role in ("inventory", "catalog", "fixtures"):
        frozen_path = frozen_repo_root / Path(ARTIFACT_RELATIVE_PATHS[role])
        frozen_path.parent.mkdir(parents=True, exist_ok=True)
        frozen_path.write_bytes(artifacts[role].read_bytes())

    active_inventory_projection_bytes = canonical_json_bytes(
        active_inventory_projection
    )
    formula_inventory_projection_bytes = canonical_json_bytes(
        formula_inventory_projection
    )
    item_template_projection_payload, _ = _build_item_template_projection(
        repo_root=repo_root,
        frozen_repo_root=frozen_repo_root,
        analyzer=frozen_scfu_analyzer,
        template_ids=sorted(
            collect_referenced_formula_template_ids(formula_inventory_projection)
        ),
        projection_name="formula",
        item_database_path=auxiliary_snapshot.path_for(ITEM_DATABASE.as_posix()),
        item_database_sha256=item_database_record.sha256,
        item_database_byte_length=item_database_record.size,
        lease=lease,
    )
    fixed_point = _run_active_formula_fixed_point(
        repo_root=repo_root,
        frozen_repo_root=frozen_repo_root,
        active_inventory_payload=active_inventory_projection_bytes,
        formula_inventory_payload=formula_inventory_projection_bytes,
        authoritative_inventory_path=(
            frozen_repo_root / Path(ARTIFACT_RELATIVE_PATHS["inventory"])
        ),
        item_template_projection_payload=item_template_projection_payload,
        lease=lease,
        max_rounds=max_rounds,
    )
    artifacts["activeCoverage"].write_bytes(fixed_point.state.active_coverage)
    artifacts["formulaDataset"].write_bytes(fixed_point.state.formula_dataset)
    for role in ("attackRangeAudit", "secondaryEvidenceAudit"):
        source = repo_root / Path(ARTIFACT_RELATIVE_PATHS[role])
        artifacts[role].write_bytes(source.read_bytes())

    generators_after = generator_descriptors(repo_root)
    runtime_after = runtime_descriptor()
    if generators_after != generators_before or runtime_after != runtime_before:
        raise PipelineError("generator contract changed during canonical regeneration")
    manifest, rendered = build_generation_manifest(
        cohort_root=candidate_root,
        artifacts=artifacts,
        input_snapshot=accepted_manifest["inputSnapshot"],
        auxiliary_input_identity=accepted_manifest["inputSnapshot"][
            "auxiliarySnapshotIdentity"
        ],
        generators=generators_before,
        runtime=runtime_before,
        input_snapshot_is_portable=True,
    )
    manifest_path = candidate_root / Path(MANIFEST_RELATIVE_PATH)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_bytes(rendered)
    validate_cohort(candidate_root, verify_toolchain=False)
    return CandidateCohort(
        root=candidate_root,
        artifacts=artifacts,
        manifest_path=manifest_path,
        capture_snapshot={},
        generation_identity=manifest["generationIdentity"],
        input_snapshot_identity=manifest["inputSnapshot"]["identity"],
        fixed_point_rounds=fixed_point.rounds,
    )


def _validate_descriptor_shape(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise CohortValidationError(f"{label} descriptor is invalid")
    if set(value) != {"path", "sha256", "byteLength"}:
        raise CohortValidationError(f"{label} descriptor fields are invalid")
    if not isinstance(value["path"], str) or not value["path"]:
        raise CohortValidationError(f"{label} path is invalid")
    if not isinstance(value["sha256"], str) or not re.fullmatch(
        r"[0-9a-f]{64}", value["sha256"]
    ):
        raise CohortValidationError(f"{label} SHA-256 is invalid")
    _require_nonnegative_int(value["byteLength"], f"{label} byte length")
    return value


def validate_cohort(cohort_root: Path, *, verify_toolchain: bool) -> dict[str, Any]:
    cohort_root = cohort_root.resolve(strict=True)
    manifest_path = cohort_root / Path(MANIFEST_RELATIVE_PATH)
    manifest = load_json_object(manifest_path, "generated-combat generation manifest")
    if manifest_path.read_bytes() != canonical_json_bytes(manifest):
        raise CohortValidationError("generation manifest is not canonical JSON")
    expected_fields = {
        "schemaVersion",
        "pipeline",
        "inputSnapshot",
        "generators",
        "runtime",
        "counts",
        "artifacts",
        "generationIdentity",
    }
    if set(manifest) != expected_fields:
        raise CohortValidationError("generation manifest fields are invalid")
    if manifest["schemaVersion"] not in {
        LEGACY_MANIFEST_SCHEMA_VERSION,
        MANIFEST_SCHEMA_VERSION,
    }:
        raise CohortValidationError("generation manifest schema version is unsupported")
    if manifest["pipeline"] != PIPELINE_NAME:
        raise CohortValidationError("generation manifest pipeline identity is invalid")
    assert_manifest_is_path_independent(manifest)
    input_snapshot = manifest["inputSnapshot"]
    if not isinstance(input_snapshot, dict) or set(input_snapshot) != {
        "schemaVersion",
        "identity",
        "captureSchemaVersion",
        "captureSnapshotIdentity",
        "captureManifestSha256",
        "captureManifestByteLength",
        "auxiliarySnapshotIdentity",
    }:
        raise CohortValidationError("generation manifest input snapshot is invalid")
    _require_nonnegative_int(
        input_snapshot["schemaVersion"], "input snapshot descriptor schema version"
    )
    if (
        input_snapshot["schemaVersion"]
        != PORTABLE_INPUT_SNAPSHOT_SCHEMA_VERSION
    ):
        raise CohortValidationError("input snapshot descriptor schema is unsupported")
    _require_nonnegative_int(
        input_snapshot["captureSchemaVersion"], "capture snapshot schema version"
    )
    if (
        input_snapshot["captureSchemaVersion"]
        != CAPTURE_INPUT_SNAPSHOT_SCHEMA_VERSION
    ):
        raise CohortValidationError("capture snapshot schema is unsupported")
    _require_nonnegative_int(
        input_snapshot["captureManifestByteLength"],
        "capture snapshot manifest byte length",
    )
    for key in (
        "identity",
        "captureSnapshotIdentity",
        "captureManifestSha256",
        "auxiliarySnapshotIdentity",
    ):
        if not isinstance(input_snapshot[key], str) or not re.fullmatch(
            r"[0-9a-f]{64}", input_snapshot[key]
        ):
            raise CohortValidationError(f"input snapshot {key} is invalid")
    combined_snapshot_core = {
        "schemaVersion": input_snapshot["schemaVersion"],
        "captureSchemaVersion": input_snapshot["captureSchemaVersion"],
        "captureSnapshotIdentity": input_snapshot["captureSnapshotIdentity"],
        "captureManifestSha256": input_snapshot["captureManifestSha256"],
        "auxiliarySnapshotIdentity": input_snapshot["auxiliarySnapshotIdentity"],
    }
    if input_snapshot["identity"] != sha256_bytes(
        identity_json_bytes(combined_snapshot_core)
    ):
        raise CohortValidationError("combined input snapshot identity is invalid")
    runtime = manifest["runtime"]
    if manifest["schemaVersion"] == LEGACY_MANIFEST_SCHEMA_VERSION:
        if not isinstance(runtime, dict) or set(runtime) != {
            "implementation",
            "version",
            "executableSha256",
            "executableByteLength",
        }:
            raise CohortValidationError("legacy runtime descriptor is invalid")
        if not isinstance(runtime["implementation"], str) or not runtime["implementation"]:
            raise CohortValidationError("legacy runtime implementation is invalid")
        if not isinstance(runtime["version"], str) or not runtime["version"]:
            raise CohortValidationError("legacy runtime version is invalid")
        if not isinstance(runtime["executableSha256"], str) or not re.fullmatch(
            r"[0-9a-f]{64}", runtime["executableSha256"]
        ):
            raise CohortValidationError("legacy runtime executable SHA-256 is invalid")
        _require_nonnegative_int(
            runtime["executableByteLength"], "legacy runtime executable byte length"
        )
    elif runtime != runtime_descriptor():
        raise CohortValidationError("runtime determinism contract is invalid")
    expected_identity = sha256_bytes(
        identity_json_bytes(_manifest_identity_payload(manifest))
    )
    if not isinstance(manifest["generationIdentity"], str) or not re.fullmatch(
        r"[0-9a-f]{64}", manifest["generationIdentity"]
    ):
        raise CohortValidationError("generation identity is invalid")
    if manifest["generationIdentity"] != expected_identity:
        raise CohortValidationError("generation identity does not match manifest content")

    rows = manifest["artifacts"]
    if not isinstance(rows, list) or len(rows) != len(ARTIFACT_RELATIVE_PATHS):
        raise CohortValidationError("generation manifest artifact cohort is incomplete")
    expected_roles = list(ARTIFACT_RELATIVE_PATHS)
    if [row.get("role") if isinstance(row, dict) else None for row in rows] != expected_roles:
        raise CohortValidationError("generation manifest artifact order or roles are invalid")
    parsed_json_artifacts: dict[str, dict[str, Any]] = {}
    for row, role in zip(rows, expected_roles):
        if set(row) != {"role", "path", "sha256", "byteLength"}:
            raise CohortValidationError(f"artifact descriptor fields are invalid: {role}")
        logical_path = ARTIFACT_RELATIVE_PATHS[role]
        if row["path"] != logical_path.as_posix():
            raise CohortValidationError(f"artifact target is invalid: {role}")
        path = cohort_root / Path(logical_path)
        if role in JSON_ARTIFACT_ROLES:
            value = load_json_object(
                path,
                f"{role} artifact",
                expected_sha256=row["sha256"],
                expected_byte_length=row["byteLength"],
            )
            parsed_json_artifacts[role] = value
            assert_generated_value_is_path_independent(value, f"{role} artifact")
        else:
            actual = artifact_descriptor(path, logical_path)
            if any(actual[key] != row[key] for key in actual):
                raise CohortValidationError(
                    f"artifact is stale or mixed: {logical_path}"
                )
            if _contains_absolute_windows_path_text(path.read_text(encoding="utf-8")):
                raise CohortValidationError(
                    "artifact contains an absolute repository-location-dependent "
                    f"path: {logical_path}"
                )

    inventory = parsed_json_artifacts["inventory"]
    active = parsed_json_artifacts["activeCoverage"]
    formula = parsed_json_artifacts["formulaDataset"]
    validate_audit_inventory_bindings(
        cohort_root / Path(ARTIFACT_RELATIVE_PATHS["inventory"]),
        parsed_json_artifacts["attackRangeAudit"],
        parsed_json_artifacts["secondaryEvidenceAudit"],
    )
    if manifest["counts"] != extract_acceptance_counts(inventory, active, formula):
        raise CohortValidationError("generation manifest counts are stale")

    generators = manifest["generators"]
    if not isinstance(generators, dict) or set(generators) != set(GENERATOR_PATHS):
        raise CohortValidationError("generation manifest generator set is invalid")
    for name, logical_path in GENERATOR_PATHS.items():
        descriptor = _validate_descriptor_shape(generators[name], f"generator {name}")
        if descriptor["path"] != logical_path.as_posix():
            raise CohortValidationError(f"generator path is invalid: {name}")
    if verify_toolchain:
        if generators != generator_descriptors(cohort_root):
            raise CohortValidationError("published cohort generator hashes are stale")
        if (
            manifest["schemaVersion"] == MANIFEST_SCHEMA_VERSION
            and manifest["runtime"] != runtime_descriptor()
        ):
            raise CohortValidationError("published cohort Python runtime hash is stale")
    return manifest


def cohort_differences(candidate_root: Path, published_root: Path) -> list[str]:
    differences: list[str] = []
    logical_paths = list(ARTIFACT_RELATIVE_PATHS.values()) + [MANIFEST_RELATIVE_PATH]
    for logical_path in logical_paths:
        candidate = candidate_root / Path(logical_path)
        published = published_root / Path(logical_path)
        if not candidate.is_file() or not published.is_file():
            differences.append(logical_path.as_posix())
            continue
        if candidate.stat().st_size != published.stat().st_size:
            differences.append(logical_path.as_posix())
            continue
        if sha256_file(candidate) != sha256_file(published):
            differences.append(logical_path.as_posix())
    return differences


def _load_transaction_module() -> Any:
    try:
        return importlib.import_module("Tools.generated_artifact_transaction")
    except ModuleNotFoundError:
        try:
            return importlib.import_module("generated_artifact_transaction")
        except ModuleNotFoundError as error:
            raise PipelineError(
                "shared generated-artifact transaction module is unavailable"
            ) from error


def _validate_delegated_lease(repo_root: Path, required_mode: str | None) -> None:
    transaction = _load_transaction_module()
    raw = os.environ.get(LEASE_DELEGATION_ENVIRONMENT)
    if raw is None:
        raise PipelineError("generated-combat lease delegation is missing")
    try:
        delegation = json.loads(raw)
        record = transaction.GeneratedArtifactLease.validate_delegation(
            repo_root, delegation, required_mode=required_mode
        )
    except (json.JSONDecodeError, transaction.GeneratedArtifactError) as error:
        raise PipelineError("generated-combat lease delegation is invalid") from error
    if record.get("domain") != PIPELINE_NAME:
        raise PipelineError("generated-combat lease delegation domain is invalid")


# Adapter boundary. This deliberately contains no fallback lock or publication
# implementation: all interprocess semantics must come from the shared module.
@contextlib.contextmanager
def _shared_lease(repo_root: Path, mode: str) -> Iterator[Any]:
    transaction = _load_transaction_module()
    try:
        with transaction.GeneratedArtifactLease(
            repo_root,
            PIPELINE_NAME,
            mode=mode,
            timeout_seconds=transaction.MAX_LEASE_WAIT_SECONDS,
        ) as lease:
            if mode == "write":
                transaction.ArtifactTransaction.recover(lease)
            else:
                transaction.ArtifactTransaction.assert_readable(lease)
            yield lease
    except transaction.GeneratedArtifactError as error:
        raise PipelineError(f"generated-artifact transaction failed: {error}") from error


def _validate_json_bytes(payload: bytes) -> None:
    text = payload.decode("utf-8")
    value = json.loads(text)
    if not isinstance(value, dict):
        raise ValueError("generated JSON root must be an object")
    assert_generated_value_is_path_independent(value, "generated JSON")


def _validate_utf8_bytes(payload: bytes) -> None:
    text = payload.decode("utf-8")
    if _contains_absolute_windows_path_text(text):
        raise ValueError("generated text contains an absolute Windows path")


def _validate_manifest_bytes(payload: bytes) -> None:
    value = json.loads(payload.decode("utf-8"))
    if not isinstance(value, dict) or payload != canonical_json_bytes(value):
        raise ValueError("generation manifest must be canonical JSON")
    if value.get("generationIdentity") != sha256_bytes(
        identity_json_bytes(_manifest_identity_payload(value))
    ):
        raise ValueError("generation manifest identity is invalid")


def _freeze_candidate_outputs(
    lease: Any, candidate: CandidateCohort
) -> dict[str, bytes]:
    outputs = {
        ARTIFACT_RELATIVE_PATHS[role].as_posix(): path.read_bytes()
        for role, path in candidate.artifacts.items()
    }
    outputs[MANIFEST_RELATIVE_PATH.as_posix()] = candidate.manifest_path.read_bytes()
    frozen_root = lease.new_staging_directory("publish-freeze")
    for relative, payload in outputs.items():
        destination = frozen_root / Path(*relative.split("/"))
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open("xb") as writer:
            writer.write(payload)
            writer.flush()
            os.fsync(writer.fileno())
    manifest = validate_cohort(frozen_root, verify_toolchain=False)
    if manifest["generationIdentity"] != candidate.generation_identity:
        raise PipelineError("frozen candidate generation identity changed before publish")
    if manifest["inputSnapshot"]["identity"] != candidate.input_snapshot_identity:
        raise PipelineError("frozen candidate input identity changed before publish")
    return outputs


def revalidate_candidate_inputs(
    auxiliary_snapshot: Any,
    candidate: CandidateCohort,
    repo_root: Path,
    lease: Any,
    *,
    require_capture_evidence: bool = False,
) -> None:
    revalidate_auxiliary_inputs(
        auxiliary_snapshot,
        repo_root,
        require_capture_evidence=require_capture_evidence,
    )
    with tempfile.TemporaryDirectory(
        prefix="aorebirth-generated-combat-publication-validation-"
    ) as validation_root_name:
        snapshot_path = Path(validation_root_name) / "capture-input-snapshot.json"
        snapshot_path.write_bytes(canonical_json_bytes(candidate.capture_snapshot))
        run_checked(
            (
                sys.executable,
                "-B",
                "-I",
                "-u",
                "-X",
                "faulthandler",
                str(auxiliary_snapshot.path_for(PRIMARY_GENERATOR.as_posix())),
                "--_validate-exported-input-snapshot",
                str(snapshot_path),
            ),
            repo_root=repo_root,
            lease=lease,
            label="primary input revalidation",
            environment_overrides={
                PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT: str(repo_root)
            },
        )


def _shared_publish(
    lease: Any,
    candidate: CandidateCohort,
    validation_callback: Callable[[str], Any],
) -> str:
    transaction = _load_transaction_module()
    outputs = _freeze_candidate_outputs(lease, candidate)
    artifact_order = [
        relative.as_posix() for relative in ARTIFACT_RELATIVE_PATHS.values()
    ] + [MANIFEST_RELATIVE_PATH.as_posix()]
    validators = {
        ARTIFACT_RELATIVE_PATHS[role].as_posix(): (
            _validate_json_bytes if role in JSON_ARTIFACT_ROLES else _validate_utf8_bytes
        )
        for role in ARTIFACT_RELATIVE_PATHS
    }
    validators[MANIFEST_RELATIVE_PATH.as_posix()] = _validate_manifest_bytes
    return transaction.ArtifactTransaction.publish(
        lease,
        outputs,
        validators=validators,
        artifact_order=artifact_order,
        commit_marker=MANIFEST_RELATIVE_PATH.as_posix(),
        validation_callback=validation_callback,
    )


GOVERNANCE_STATE_LEGACY_ACCEPTED_RAW_UNAVAILABLE = "LEGACY_ACCEPTED_RAW_UNAVAILABLE"
GOVERNANCE_STATE_RAW_REVALIDATABLE = "RAW_REVALIDATABLE"
GOVERNANCE_STATE_NEW_RAW_VERIFIED = "NEW_RAW_VERIFIED"
GOVERNANCE_STATE_BLOCKED_INSUFFICIENT_EVIDENCE = "BLOCKED_INSUFFICIENT_EVIDENCE"
GOVERNANCE_CATEGORY_ORDINARY_HOSTILE = "ORDINARY_HOSTILE_COMBAT_OBSERVED"
GOVERNANCE_CATEGORY_GUARD_COMBAT = "GUARD_COMBAT_OBSERVED"
GOVERNANCE_CATEGORY_COMBAT_CAPABLE_UNRESOLVED = "COMBAT_CAPABLE_ROLE_UNRESOLVED"
GOVERNANCE_CATEGORY_SOCIAL_NONCOMBAT = "NONCOMBAT_SOCIAL_OBSERVED"
GOVERNANCE_CATEGORY_VENDOR_NONCOMBAT = "VENDOR_NONCOMBAT_OBSERVED"
GOVERNANCE_CATEGORY_NO_COMBAT = "NO_COMBAT_EVIDENCE"
GOVERNANCE_CATEGORY_AMBIGUOUS = "AMBIGUOUS"
GOVERNANCE_COMBAT_CANDIDATE_CATEGORIES = frozenset(
    {
        GOVERNANCE_CATEGORY_ORDINARY_HOSTILE,
        GOVERNANCE_CATEGORY_GUARD_COMBAT,
        GOVERNANCE_CATEGORY_COMBAT_CAPABLE_UNRESOLVED,
        GOVERNANCE_CATEGORY_AMBIGUOUS,
    }
)
GOVERNANCE_RUNTIME_REQUIRED_FIELDS = (
    "actorIdentity",
    "monsterData",
    "level",
    "maxHealth",
    "minDamage",
    "maxDamage",
    "damageType",
    "defaultAttackType",
    "attackDelay",
    "rechargeDelay",
    "attackRange",
    "followChaseBehavior",
    "catMesh",
    "hitType",
    "attackSlot",
    "nanoOrSpecialBehavior",
    "factionAlignment",
    "naturalOrWeaponMode",
    "archetypeLinkage",
)
GOVERNANCE_BASIC_COMBAT_REQUIRED_FIELDS = (
    "actorIdentity",
    "monsterData",
    "level",
    "maxHealth",
    "minDamage",
    "maxDamage",
    "damageType",
    "defaultAttackType",
    "attackDelay",
    "rechargeDelay",
    "attackRange",
    "followChaseBehavior",
    "hitType",
    "attackSlot",
    "naturalOrWeaponMode",
    "archetypeLinkage",
)
GOVERNANCE_OPTIONAL_OBSERVATION_FIELDS = ("nanoOrSpecialBehavior",)
GOVERNANCE_SEPARATE_SPAWN_CONCERN_FIELDS = ("catMesh", "factionAlignment")
GOVERNANCE_RUNTIME_FIELD_CLASSES = {
    **{field: "BASIC_COMBAT_REQUIRED" for field in GOVERNANCE_BASIC_COMBAT_REQUIRED_FIELDS},
    **{field: "OPTIONAL_OBSERVATION" for field in GOVERNANCE_OPTIONAL_OBSERVATION_FIELDS},
    **{field: "SEPARATE_SPAWN_CONCERN" for field in GOVERNANCE_SEPARATE_SPAWN_CONCERN_FIELDS},
}
GOVERNANCE_FIELD_RESOLUTION_DIRECT_CAPTURE = "DIRECTLY_PROVABLE_FROM_CAPTURE"
GOVERNANCE_FIELD_RESOLUTION_DERIVED_RULE = "DERIVABLE_BY_GOVERNED_RULE"
GOVERNANCE_FIELD_RESOLUTION_NOT_REQUIRED = "NOT_REQUIRED_FOR_BASIC_RUNTIME_COMBAT"
GOVERNANCE_FIELD_RESOLUTION_REQUIRED_NOT_PROTOCOL_PROVEN = (
    "REQUIRED_BUT_NOT_PROTOCOL_PROVEN"
)
GOVERNANCE_FINAL_STATUS_PROVEN = "PROVEN"
GOVERNANCE_FINAL_STATUS_DERIVED_GOVERNED = "DERIVED_GOVERNED"
GOVERNANCE_FINAL_STATUS_NOT_REQUIRED = "NOT_REQUIRED"
GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN = "NOT_PROTOCOL_PROVEN"
GOVERNANCE_FINAL_STATUS_MISSING_PROTOCOL_PROVABLE = "MISSING_PROTOCOL_PROVABLE"
GOVERNANCE_FINAL_AUTHORITY_DIRECT_PACKET = "DIRECT_PACKET"
GOVERNANCE_FINAL_AUTHORITY_DERIVED_GOVERNED = "DERIVED_GOVERNED"
GOVERNANCE_FINAL_AUTHORITY_NOT_REQUIRED = "NOT_REQUIRED_BASIC_COMBAT"
GOVERNANCE_FINAL_AUTHORITY_REQUIRED_NOT_PROTOCOL_PROVEN = (
    "REQUIRED_NOT_PROTOCOL_PROVEN"
)
GOVERNANCE_FINAL_AUTHORITY_MORE_CAPTURE = (
    "REQUIRED_PROTOCOL_PROVABLE_NEEDS_MORE_CAPTURE"
)
GOVERNANCE_REQUIRED_NOT_PROTOCOL_PROVEN_FIELDS = frozenset(
    {
        "attackDelay",
        "attackRange",
        "naturalOrWeaponMode",
        "rechargeDelay",
    }
)
GOVERNANCE_NORMAL_HIT_TYPE_WIRE = 3
GOVERNANCE_BASIC_COMBAT_MODEL = "CAPTURE_BACKED_BASIC_ORDINARY_MELEE_DRY_RUN"
GOVERNANCE_BASIC_COMBAT_PROTOCOL_FIELDS = (
    "actorIdentity",
    "monsterData",
    "level",
    "maxHealth",
    "damageType",
    "defaultAttackType",
    "followChaseBehavior",
    "hitType",
    "attackSlot",
    "archetypeLinkage",
)
GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED = "RUNTIME_EXECUTION_REQUIRED"
GOVERNANCE_FIELD_LEGACY_GENERATOR_REQUIRED_ONLY = (
    "LEGACY_GENERATOR_REQUIRED_ONLY"
)
GOVERNANCE_FIELD_OPTIONAL = "OPTIONAL"
GOVERNANCE_FIELD_DERIVABLE_FROM_OTHER_PROVEN_RUNTIME_STATE = (
    "DERIVABLE_FROM_OTHER_PROVEN_RUNTIME_STATE"
)
GOVERNANCE_FIELD_NOT_ACTUALLY_USED = "NOT_ACTUALLY_USED"
GOVERNANCE_FIELD_RUNTIME_EXECUTION_CLASSIFICATIONS = {
    "actorIdentity": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
    "monsterData": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
    "level": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
    "maxHealth": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
    "minDamage": GOVERNANCE_FIELD_NOT_ACTUALLY_USED,
    "maxDamage": GOVERNANCE_FIELD_NOT_ACTUALLY_USED,
    "damageType": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
    "defaultAttackType": GOVERNANCE_FIELD_DERIVABLE_FROM_OTHER_PROVEN_RUNTIME_STATE,
    "attackDelay": GOVERNANCE_FIELD_LEGACY_GENERATOR_REQUIRED_ONLY,
    "rechargeDelay": GOVERNANCE_FIELD_DERIVABLE_FROM_OTHER_PROVEN_RUNTIME_STATE,
    "attackRange": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
    "followChaseBehavior": GOVERNANCE_FIELD_DERIVABLE_FROM_OTHER_PROVEN_RUNTIME_STATE,
    "catMesh": GOVERNANCE_FIELD_OPTIONAL,
    "hitType": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
    "attackSlot": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
    "nanoOrSpecialBehavior": GOVERNANCE_FIELD_OPTIONAL,
    "factionAlignment": GOVERNANCE_FIELD_OPTIONAL,
    "naturalOrWeaponMode": GOVERNANCE_FIELD_NOT_ACTUALLY_USED,
    "archetypeLinkage": GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED,
}
GOVERNANCE_FIX_GUIDANCE_CAPTURE_MORE = "CAPTURE_MORE"
GOVERNANCE_FIX_GUIDANCE_ENGINEERING_REQUIRED = "ENGINEERING_REQUIRED"
GOVERNANCE_FIX_GUIDANCE_OPTIONAL = "OPTIONAL"
GOVERNANCE_FIX_GUIDANCE_RUNTIME_POLICY = "RUNTIME_POLICY"
GOVERNANCE_FIX_GUIDANCE_RESOLVED = "RESOLVED"
GOVERNANCE_OBSERVED_DAMAGE_BASIC_MODEL_FIELDS = frozenset(
    {
        "maxDamage",
        "minDamage",
    }
)
GOVERNANCE_DIRECT_ATTACK_INFO_PACKET_TYPE = 0x46002F16
GOVERNANCE_SIMPLE_CHAR_TYPE = 50000
GOVERNANCE_CAPTURE_READINESS = (
    {
        "field": "attackDelay",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "SCFU_STAT",
        "evidencePath": (
            "owner-linked WeaponItemFullUpdate stat 294 is accepted for equipped "
            "weapon contracts only; passive hit timing remains observation data, "
            "not an NPC stat source"
        ),
    },
    {
        "field": "attackRange",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "SCFU_STAT",
        "evidencePath": (
            "accepted historical path is ItemDb/WeaponItemFullUpdate AttackRange "
            "stat 287 with template authority; chase, follow, and attack-start "
            "distance are explicitly not attackRange evidence"
        ),
    },
    {
        "field": "attackSlot",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "DIRECT_PACKET_FIELD",
        "evidencePath": (
            "AttackInfo.weaponSlot decoded from raw AttackInfo packet and "
            "cross-linked to owner WeaponItemFullUpdate where equipped weapon "
            "contracts are accepted"
        ),
    },
    {
        "field": "catMesh",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "SCFU_STAT",
        "evidencePath": (
            "SCFU/corpse visual decoding can prove corpse cat mesh when a "
            "non-sentinel visual id is captured; sentinel 1234567890 remains "
            "promotion-blocking"
        ),
    },
    {
        "field": "damageType",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "DIRECT_PACKET_FIELD",
        "evidencePath": (
            "AttackInfo.damageTypeWire decoded from raw AttackInfo packet; "
            "Unknown1/Unknown2/Unknown3 CSV columns are not contract evidence"
        ),
    },
    {
        "field": "defaultAttackType",
        "runtimeStatus": "DERIVABLE",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "KNOWN_GENERATOR_CONSTANT_WITH_PROVEN_SCOPE",
        "evidencePath": (
            "runtime uses the governed normal AttackInfo hit type constant only "
            "inside already proven normal-hit packet contracts; sentinel dossier "
            "values are not evidence"
        ),
    },
    {
        "field": "factionAlignment",
        "runtimeStatus": "NOT_PROTOCOL_PROVEN",
        "captureStatus": "NOT_PROTOCOL_PROVEN",
        "analyzerStatus": "NOT_PROTOCOL_PROVEN",
        "historicalProvenance": "UNRESOLVED_HISTORICAL_ASSUMPTION",
        "evidencePath": (
            "current audited capture outputs prove behavior and identity, not "
            "authoritative faction/alignment semantics; names, species, location, "
            "or hostility are not accepted"
        ),
    },
    {
        "field": "followChaseBehavior",
        "runtimeStatus": "DERIVABLE",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "OBSERVED_EVENT_VALUE",
        "evidencePath": (
            "enemy movement and target/follow rows prove follow/chase behavior "
            "for classification only; the value is intentionally separate from "
            "attackRange"
        ),
    },
    {
        "field": "hitType",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "DIRECT_PACKET_FIELD",
        "evidencePath": (
            "AttackInfo.hitTypeWire decoded from raw AttackInfo packet and "
            "accepted only when the packet contract is otherwise complete"
        ),
    },
    {
        "field": "maxDamage",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "SCFU_STAT",
        "evidencePath": (
            "owner-linked WeaponItemFullUpdate weapon max-damage stat is accepted "
            "for equipped weapon contracts; observed maximum hit is only an "
            "observation and is not authoritative maxDamage"
        ),
    },
    {
        "field": "minDamage",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "SCFU_STAT",
        "evidencePath": (
            "owner-linked WeaponItemFullUpdate weapon min-damage stat is accepted "
            "for equipped weapon contracts; observed minimum hit is only an "
            "observation and is not authoritative minDamage"
        ),
    },
    {
        "field": "nanoOrSpecialBehavior",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "DIRECT_PACKET_FIELD",
        "evidencePath": (
            "positive SpecialAttackWeapon, CastNanoSpell, alternate AttackInfo, "
            "and related action packets are observable; absence remains "
            "NOT_OBSERVED unless a separate governed coverage rule exists"
        ),
    },
    {
        "field": "naturalOrWeaponMode",
        "runtimeStatus": "DERIVABLE",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "MULTI_EVENT_DERIVATION",
        "evidencePath": (
            "mode is derived only from packet-complete attack chains and "
            "owner-linked WeaponItemFullUpdate evidence; visual unarmed "
            "appearance alone is not evidence"
        ),
    },
    {
        "field": "rechargeDelay",
        "runtimeStatus": "DIRECTLY_CAPTURED",
        "captureStatus": "CAPTURE_READY",
        "analyzerStatus": "ANALYZER_READY",
        "historicalProvenance": "SCFU_STAT",
        "evidencePath": (
            "owner-linked WeaponItemFullUpdate stat 210 is accepted for equipped "
            "weapon contracts only; repeated hit interval is not authoritative "
            "rechargeDelay"
        ),
    },
)
GOVERNANCE_CAPTURE_READY_STATUSES = frozenset({"CAPTURE_READY"})
GOVERNANCE_ANALYZER_READY_STATUSES = frozenset({"ANALYZER_READY"})

GOVERNANCE_REQUIRED_RAW_FILES = (
    "capture_info.json",
    "packets.hex.log",
    "raw-packets.csv",
    "scfu-appearance.csv",
)
GOVERNANCE_SENTINEL_TEXT = "1234567890"
GOVERNANCE_SENTINEL_FIELDS = (
    "attackDelay",
    "catMesh",
    "defaultAttackType",
    "maxDamage",
    "minDamage",
    "rechargeDelay",
)
GOVERNANCE_LEGACY_BASELINE = Path(
    "docs/project/CAPTURE_BACKED_COMBAT_LEGACY_BASELINE.json"
)
GOVERNANCE_CAPTURE_ID_PATTERN = re.compile(r"20\d{6}-\d{6}")
GOVERNANCE_LEGACY_CAPTURE_ROOT = Path(
    "tools-temp/AOSharpLiveCapture/bin/Debug/captures"
)


def _governance_manifest_artifacts(manifest: Mapping[str, Any]) -> dict[str, Any]:
    artifacts: dict[str, Any] = {}
    for artifact in manifest.get("artifacts", []):
        role = artifact.get("role")
        if isinstance(role, str):
            artifacts[role] = artifact
    return artifacts


def _governance_capture_id(value: object) -> str | None:
    match = GOVERNANCE_CAPTURE_ID_PATTERN.search(str(value))
    if match:
        return match.group(0)
    return None


def _governance_required_capture_ids(repo_root: Path) -> list[str]:
    source_paths = [FORMULA_GENERATOR, *FORMULA_STATIC_INPUTS]
    capture_ids: set[str] = set()
    for source_path in source_paths:
        text = (repo_root / source_path).read_text(encoding="utf-8")
        capture_ids.update(GOVERNANCE_CAPTURE_ID_PATTERN.findall(text))
    return sorted(capture_ids)


def _governance_missing_raw_files(capture_root: Path) -> list[str]:
    return [
        file_name
        for file_name in GOVERNANCE_REQUIRED_RAW_FILES
        if not (capture_root / file_name).is_file()
    ]


def _governance_raw_file_descriptors(capture_root: Path) -> list[dict[str, Any]]:
    descriptors: list[dict[str, Any]] = []
    for file_name in GOVERNANCE_REQUIRED_RAW_FILES:
        path = capture_root / file_name
        descriptors.append(
            {
                "path": str(path),
                "byteLength": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )
    return descriptors


def _governance_load_inventory(repo_root: Path) -> dict[str, Any]:
    return load_json_object(
        repo_root / ARTIFACT_RELATIVE_PATHS["inventory"],
        "capture-backed NPC combat inventory",
    )


def _governance_profile_items(inventory: Mapping[str, Any]) -> list[tuple[str, Any]]:
    profiles = inventory.get("profiles")
    if isinstance(profiles, Mapping):
        return [(str(key), value) for key, value in profiles.items()]
    if isinstance(profiles, list):
        items: list[tuple[str, Any]] = []
        for index, profile in enumerate(profiles):
            key = f"index={index}"
            if isinstance(profile, Mapping):
                for key_name in ("profileKey", "key", "identity"):
                    candidate = profile.get(key_name)
                    if isinstance(candidate, str) and candidate:
                        key = candidate
                        break
            items.append((key, profile))
        return items
    return []


def _governance_mentions_capture(value: Any, capture_id: str) -> bool:
    if isinstance(value, str):
        return capture_id in value
    if isinstance(value, Mapping):
        return any(_governance_mentions_capture(child, capture_id) for child in value.values())
    if isinstance(value, list):
        return any(_governance_mentions_capture(child, capture_id) for child in value)
    return False


def _governance_runtime_variant_count(profile: Any) -> int:
    if not isinstance(profile, Mapping):
        return 0
    for key in (
        "runtimeReadyVariantCount",
        "runtimeReadyVariants",
        "runtimeGeneratedSemanticDefinitionCount",
    ):
        value = profile.get(key)
        if isinstance(value, int) and value > 0:
            return value
        if isinstance(value, list):
            return len(value)
    value = profile.get("runtimeReady")
    if isinstance(value, bool) and value:
        return 1
    value = profile.get("runtimeEnabled")
    if isinstance(value, bool) and value:
        return 1
    return 0


def _governance_capture_runtime_rows(
    inventory: Mapping[str, Any], capture_id: str
) -> tuple[int, int]:
    profiles = 0
    rows = 0
    for _profile_key, profile in _governance_profile_items(inventory):
        if not _governance_mentions_capture(profile, capture_id):
            continue
        variant_count = _governance_runtime_variant_count(profile)
        if variant_count <= 0:
            continue
        profiles += 1
        rows += variant_count
    return profiles, rows


def _governance_casefold_lookup(mapping: Mapping[str, Any], key: str) -> Any:
    wanted = key.casefold()
    for actual_key, value in mapping.items():
        if str(actual_key).casefold() == wanted:
            return value
    return None


def _governance_is_sentinel(value: Any) -> bool:
    if value is None:
        return False
    if isinstance(value, bool):
        return False
    if isinstance(value, int):
        return value == int(GOVERNANCE_SENTINEL_TEXT)
    if isinstance(value, float):
        return value == float(GOVERNANCE_SENTINEL_TEXT)
    return str(value).strip() == GOVERNANCE_SENTINEL_TEXT


def _governance_sentinel_fields(value: Any) -> list[str]:
    found: set[str] = set()
    canonical_by_folded = {
        field.casefold(): field for field in GOVERNANCE_SENTINEL_FIELDS
    }

    def visit(node: Any) -> None:
        if isinstance(node, Mapping):
            for key, child in node.items():
                folded = str(key).casefold()
                canonical = canonical_by_folded.get(folded)
                if canonical is not None and _governance_is_sentinel(child):
                    found.add(canonical)
                visit(child)
        elif isinstance(node, list):
            for child in node:
                visit(child)

    visit(value)
    return sorted(found)


def _governance_csv_rows(path: Path) -> list[dict[str, str]]:
    if not path.is_file():
        return []
    text = _governance_read_text(path)
    return list(csv.DictReader(io.StringIO(text)))


def _governance_read_text(path: Path) -> str:
    payload = path.read_bytes()
    encoding = (
        "utf-16"
        if payload[:2] in (b"\xff\xfe", b"\xfe\xff")
        or payload[:512].count(b"\x00") > 16
        else "utf-8-sig"
    )
    return payload.decode(encoding, errors="replace")


def _governance_text_lines(path: Path) -> list[str]:
    if not path.is_file():
        return []
    return _governance_read_text(path).splitlines()


def _governance_row_name(row: Mapping[str, Any]) -> str:
    for key in ("name", "Name", "mobName", "MobName", "characterName", "CharacterName"):
        value = _governance_casefold_lookup(row, key)
        if value not in (None, ""):
            return str(value)
    return "unknown"


def _governance_row_field(row: Mapping[str, Any], *keys: str) -> str:
    for key in keys:
        value = _governance_casefold_lookup(row, key)
        if value not in (None, ""):
            return str(value)
    return "unknown"


def _governance_identity_hex(value: Any) -> str | None:
    if value in (None, ""):
        return None
    text = str(value).strip()
    if re.search(r"\([A-Za-z]+:", text) and "SimpleChar:" not in text:
        return None
    match = re.search(r"SimpleChar:([0-9A-Fa-f]{1,8})", text)
    if match:
        return f"{int(match.group(1), 16):08X}"
    match = re.search(r"\b0x([0-9A-Fa-f]{1,8})\b", text)
    if match:
        return f"{int(match.group(1), 16):08X}"
    match = re.search(r"\b([0-9A-Fa-f]{8})\b", text)
    if match and re.search(r"[A-Fa-f]", match.group(1)):
        return match.group(1).upper()
    if text.isdigit():
        value_int = int(text)
        if 0 < value_int <= 0xFFFFFFFF:
            return f"{value_int:08X}"
    return None


def _governance_typed_identity_hex(value: Any, dynel_type: str) -> str | None:
    if value in (None, ""):
        return None
    match = re.search(
        rf"{re.escape(dynel_type)}:([0-9A-Fa-f]{{8}})",
        str(value).strip(),
    )
    if match:
        return match.group(1).upper()
    return None


def _governance_row_identity(row: Mapping[str, Any], *keys: str) -> str | None:
    for key in keys:
        value = _governance_casefold_lookup(row, key)
        identity = _governance_identity_hex(value)
        if identity is not None:
            return identity
    return None


def _governance_line_identities(line: str) -> list[str]:
    identities = [
        match.group(1).upper()
        for match in re.finditer(r"SimpleChar:([0-9A-Fa-f]{8})", line)
    ]
    identities.extend(
        match.group(1).upper()
        for match in re.finditer(r"\b0x([0-9A-Fa-f]{8})\b", line)
    )
    return sorted(set(identities))


def _governance_line_target_identity(line: str) -> str | None:
    match = re.search(r"Target=\(SimpleChar:([0-9A-Fa-f]{8})\)", line)
    if match:
        return match.group(1).upper()
    return None


def _governance_int(value: Any) -> int | None:
    if value in (None, ""):
        return None
    try:
        return int(str(value).strip(), 0)
    except ValueError:
        return None


def _governance_truthy_count(value: Any) -> int:
    count = _governance_int(value)
    return 0 if count is None else count


def _governance_detail_value(row: Mapping[str, Any], field_name: str) -> str | None:
    detail = _governance_casefold_lookup(row, "Detail")
    if detail in (None, ""):
        return None
    match = re.search(
        rf"\b{re.escape(field_name)}=([^,\s}}]+)",
        str(detail),
        flags=re.IGNORECASE,
    )
    if not match:
        return None
    value = match.group(1).strip()
    return value or None


def _governance_row_or_detail_value(
    row: Mapping[str, Any],
    field_names: Sequence[str],
) -> str | None:
    for field_name in field_names:
        value = _governance_casefold_lookup(row, field_name)
        if value not in (None, ""):
            text = str(value).strip()
            if text:
                return text
    for field_name in field_names:
        value = _governance_detail_value(row, field_name)
        if value not in (None, ""):
            return value
    return None


def _governance_u32_be(payload: bytes, offset: int) -> int:
    return int.from_bytes(payload[offset : offset + 4], "big", signed=False)


def _governance_i32_be(payload: bytes, offset: int) -> int:
    return int.from_bytes(payload[offset : offset + 4], "big", signed=True)


def _governance_decode_raw_attack_info(
    row: Mapping[str, Any],
) -> dict[str, Any] | None:
    type_name = str(
        _governance_casefold_lookup(row, "N3TypeName")
        or _governance_casefold_lookup(row, "MessageType")
        or ""
    ).casefold()
    type_value = _governance_int(_governance_casefold_lookup(row, "N3TypeValue"))
    if (
        type_name != "attackinfo"
        and type_value != GOVERNANCE_DIRECT_ATTACK_INFO_PACKET_TYPE
    ):
        return None
    raw_hex = _governance_casefold_lookup(row, "RawHex")
    if raw_hex in (None, ""):
        return None
    cleaned = re.sub(r"[^0-9A-Fa-f]", "", str(raw_hex))
    if len(cleaned) < 122 or len(cleaned) % 2:
        return None
    try:
        payload = bytes.fromhex(cleaned)
    except ValueError:
        return None
    if len(payload) < 61:
        return None
    message_type = _governance_u32_be(payload, 16)
    if message_type != GOVERNANCE_DIRECT_ATTACK_INFO_PACKET_TYPE:
        return None
    source_type = _governance_u32_be(payload, 20)
    target_type = _governance_u32_be(payload, 41)
    if source_type != GOVERNANCE_SIMPLE_CHAR_TYPE or target_type != GOVERNANCE_SIMPLE_CHAR_TYPE:
        return None
    source_identity = _governance_u32_be(payload, 24)
    target_identity = _governance_u32_be(payload, 45)
    return {
        "sourceIdentity": f"{source_identity:08X}",
        "targetIdentity": f"{target_identity:08X}",
        "n3Unknown": payload[28],
        "amount": _governance_i32_be(payload, 29),
        "ammoCount": _governance_i32_be(payload, 33),
        "weaponSlot": _governance_i32_be(payload, 37),
        "damageTypeWire": _governance_i32_be(payload, 49),
        "hitTypeWire": _governance_i32_be(payload, 53),
        "weaponInstance": _governance_i32_be(payload, 57),
    }


def _governance_sorted_evidence_values(values: Any) -> list[str]:
    return sorted({str(value) for value in values}, key=str.casefold)


def _governance_is_special_or_nano_action(row: Mapping[str, Any]) -> bool:
    text = " ".join(
        str(_governance_casefold_lookup(row, key) or "")
        for key in ("MessageType", "Action", "Detail", "eventType")
    ).casefold()
    return (
        "specialattack" in text
        or "castnanospell" in text
        or "castnano" in text
        or "nano" in text
    )


def _governance_float(value: Any) -> float | None:
    if value is None:
        return None
    text = str(value).strip()
    if not text:
        return None
    try:
        parsed = float(text)
    except ValueError:
        return None
    if parsed != parsed or parsed in {float("inf"), float("-inf")}:
        return None
    return parsed


def _governance_cadence_stream_key(event: Mapping[str, Any]) -> tuple[str, ...]:
    return (
        str(event.get("attackInfoWeaponSlot", "")),
        str(event.get("attackInfoHitTypeWire", "")),
        str(event.get("attackInfoWeaponInstance", "")),
        str(event.get("attackInfoAmmoCount", "")),
        str(event.get("attackInfoN3Unknown", "")),
    )


def _governance_cadence_stream_label(stream_key: Sequence[str]) -> str:
    return (
        "slot="
        + str(stream_key[0])
        + ";hitTypeWire="
        + str(stream_key[1])
        + ";weaponInstance="
        + str(stream_key[2])
        + ";ammo="
        + str(stream_key[3])
        + ";n3="
        + str(stream_key[4])
    )


def _governance_add_raw_ordinary_attack_info_event(
    cohort: dict[str, Any],
    capture_id: str,
    row: Mapping[str, Any],
    attack_info: Mapping[str, Any],
) -> None:
    amount = _governance_int(attack_info.get("amount"))
    hit_type_wire = _governance_int(attack_info.get("hitTypeWire"))
    elapsed_milliseconds = _governance_float(
        _governance_casefold_lookup(row, "ElapsedMilliseconds")
    )
    if (
        amount is None
        or amount <= 0
        or hit_type_wire != GOVERNANCE_NORMAL_HIT_TYPE_WIRE
        or elapsed_milliseconds is None
    ):
        return
    cohort["_rawOrdinaryAttackInfoEvents"].append(
        {
            "captureId": str(capture_id),
            "capturedUtc": str(_governance_casefold_lookup(row, "CapturedUtc") or ""),
            "sourceIdentity": str(attack_info["sourceIdentity"]),
            "targetIdentity": str(attack_info["targetIdentity"]),
            "sequence": _governance_int(_governance_casefold_lookup(row, "Sequence"))
            or 0,
            "elapsedMilliseconds": round(elapsed_milliseconds, 3),
            "amount": amount,
            "damageTypeWire": _governance_int(attack_info.get("damageTypeWire")) or 0,
            "attackInfoAmmoCount": _governance_int(attack_info.get("ammoCount")) or 0,
            "attackInfoWeaponSlot": _governance_int(attack_info.get("weaponSlot"))
            or 0,
            "attackInfoHitTypeWire": hit_type_wire,
            "attackInfoWeaponInstance": _governance_int(
                attack_info.get("weaponInstance")
            )
            or 0,
            "attackInfoN3Unknown": _governance_int(attack_info.get("n3Unknown")) or 0,
        }
    )


def _governance_normalize_observed_cadence_streams(
    streams: Sequence[Mapping[str, Any]],
) -> list[dict[str, Any]]:
    merged: dict[tuple[str, ...], dict[str, Any]] = {}
    for stream in streams:
        stream_key = (
            str(stream.get("attackInfoWeaponSlot", "")),
            str(stream.get("attackInfoHitTypeWire", "")),
            str(stream.get("attackInfoWeaponInstance", "")),
            str(stream.get("attackInfoAmmoCount", "")),
            str(stream.get("attackInfoN3Unknown", "")),
        )
        current = merged.setdefault(
            stream_key,
            {
                "streamKey": _governance_cadence_stream_label(stream_key),
                "attackInfoWeaponSlot": _governance_int(stream_key[0]) or 0,
                "attackInfoHitTypeWire": _governance_int(stream_key[1]) or 0,
                "attackInfoWeaponInstance": _governance_int(stream_key[2]) or 0,
                "attackInfoAmmoCount": _governance_int(stream_key[3]) or 0,
                "attackInfoN3Unknown": _governance_int(stream_key[4]) or 0,
                "sourceCaptureIds": set(),
                "sourceIdentities": set(),
                "targetIdentities": set(),
                "damageTypes": set(),
                "attackInfoObservationCount": 0,
                "landedIntervalsSeconds": [],
                "intervalProvenance": [],
            },
        )
        for field in ("sourceCaptureIds", "sourceIdentities", "targetIdentities"):
            current[field].update(str(value) for value in stream.get(field, ()))
        current["damageTypes"].update(str(value) for value in stream.get("damageTypes", ()))
        current["attackInfoObservationCount"] += _governance_truthy_count(
            stream.get("attackInfoObservationCount")
        )
        for value in stream.get("landedIntervalsSeconds", ()):
            parsed = _governance_float(value)
            if parsed is not None and parsed > 0.0:
                current["landedIntervalsSeconds"].append(round(parsed, 6))
        for value in stream.get("intervalProvenance", ()):
            if isinstance(value, Mapping):
                current["intervalProvenance"].append(dict(value))
    normalized: list[dict[str, Any]] = []
    for stream_key in sorted(merged):
        stream = merged[stream_key]
        provenance = sorted(
            stream["intervalProvenance"],
            key=lambda row: (
                str(row.get("captureId", "")),
                str(row.get("sourceIdentity", "")),
                str(row.get("targetIdentity", "")),
                _governance_truthy_count(row.get("fromSequence")),
                _governance_truthy_count(row.get("toSequence")),
            ),
        )
        intervals = [
            round(_governance_float(row.get("seconds")) or 0.0, 6)
            for row in provenance
        ]
        if not intervals:
            intervals = sorted(stream["landedIntervalsSeconds"])
        normalized.append(
            {
                "streamKey": stream["streamKey"],
                "attackInfoAmmoCount": stream["attackInfoAmmoCount"],
                "attackInfoWeaponSlot": stream["attackInfoWeaponSlot"],
                "attackInfoHitTypeWire": stream["attackInfoHitTypeWire"],
                "attackInfoWeaponInstance": stream["attackInfoWeaponInstance"],
                "attackInfoN3Unknown": stream["attackInfoN3Unknown"],
                "sourceCaptureIds": sorted(stream["sourceCaptureIds"]),
                "sourceIdentities": sorted(stream["sourceIdentities"]),
                "targetIdentities": sorted(stream["targetIdentities"]),
                "damageTypes": sorted(stream["damageTypes"], key=str.casefold),
                "attackInfoObservationCount": stream["attackInfoObservationCount"],
                "landedIntervalCount": len(intervals),
                "landedIntervalsSeconds": intervals,
                "intervalProvenance": provenance,
            }
        )
    return normalized


def _governance_observed_cadence_streams_from_events(
    events: Sequence[Mapping[str, Any]],
) -> list[dict[str, Any]]:
    grouped: dict[tuple[str, ...], list[Mapping[str, Any]]] = {}
    for event in events:
        group_key = (
            str(event.get("captureId", "")),
            str(event.get("sourceIdentity", "")),
            str(event.get("targetIdentity", "")),
            *_governance_cadence_stream_key(event),
        )
        grouped.setdefault(group_key, []).append(event)
    stream_summaries: dict[tuple[str, ...], dict[str, Any]] = {}
    for group_key in sorted(grouped):
        ordered_events = sorted(
            grouped[group_key],
            key=lambda row: (
                _governance_float(row.get("elapsedMilliseconds")) or 0.0,
                _governance_truthy_count(row.get("sequence")),
            ),
        )
        stream_key = group_key[3:]
        summary = stream_summaries.setdefault(
            stream_key,
            {
                "streamKey": _governance_cadence_stream_label(stream_key),
                "attackInfoWeaponSlot": _governance_int(stream_key[0]) or 0,
                "attackInfoHitTypeWire": _governance_int(stream_key[1]) or 0,
                "attackInfoWeaponInstance": _governance_int(stream_key[2]) or 0,
                "attackInfoAmmoCount": _governance_int(stream_key[3]) or 0,
                "attackInfoN3Unknown": _governance_int(stream_key[4]) or 0,
                "sourceCaptureIds": set(),
                "sourceIdentities": set(),
                "targetIdentities": set(),
                "damageTypes": set(),
                "attackInfoObservationCount": 0,
                "landedIntervalsSeconds": [],
                "intervalProvenance": [],
            },
        )
        for event in ordered_events:
            summary["sourceCaptureIds"].add(str(event.get("captureId", "")))
            summary["sourceIdentities"].add(str(event.get("sourceIdentity", "")))
            summary["targetIdentities"].add(str(event.get("targetIdentity", "")))
            summary["damageTypes"].add(str(event.get("damageTypeWire", "")))
            summary["attackInfoObservationCount"] += 1
        for previous, current in zip(ordered_events, ordered_events[1:]):
            previous_elapsed = _governance_float(previous.get("elapsedMilliseconds"))
            current_elapsed = _governance_float(current.get("elapsedMilliseconds"))
            if previous_elapsed is None or current_elapsed is None:
                continue
            interval_seconds = round((current_elapsed - previous_elapsed) / 1000.0, 6)
            if interval_seconds <= 0.0:
                continue
            summary["landedIntervalsSeconds"].append(interval_seconds)
            summary["intervalProvenance"].append(
                {
                    "captureId": str(current.get("captureId", "")),
                    "sourceIdentity": str(current.get("sourceIdentity", "")),
                    "targetIdentity": str(current.get("targetIdentity", "")),
                    "fromSequence": _governance_truthy_count(
                        previous.get("sequence")
                    ),
                    "toSequence": _governance_truthy_count(current.get("sequence")),
                    "fromElapsedMilliseconds": round(previous_elapsed, 3),
                    "toElapsedMilliseconds": round(current_elapsed, 3),
                    "seconds": interval_seconds,
                }
            )
    return _governance_normalize_observed_cadence_streams(
        list(stream_summaries.values())
    )


def _governance_observed_cadence_counts(
    streams: Sequence[Mapping[str, Any]],
) -> dict[str, int]:
    return {
        "attackInfoObservationCount": sum(
            _governance_truthy_count(stream.get("attackInfoObservationCount"))
            for stream in streams
        ),
        "landedStreamCount": sum(
            1
            for stream in streams
            if _governance_truthy_count(stream.get("landedIntervalCount")) > 0
        ),
        "landedIntervalCount": sum(
            _governance_truthy_count(stream.get("landedIntervalCount"))
            for stream in streams
        ),
    }


def _governance_basic_combat_blockers(
    cohort: Mapping[str, Any],
    field_statuses: Mapping[str, str],
    category: str,
    cadence_counts: Mapping[str, int],
) -> list[str]:
    if category != GOVERNANCE_CATEGORY_ORDINARY_HOSTILE:
        return (
            ["ordinaryHostileCategory"]
            if category in GOVERNANCE_COMBAT_CANDIDATE_CATEGORIES
            else []
        )
    blockers = [
        field
        for field in GOVERNANCE_BASIC_COMBAT_PROTOCOL_FIELDS
        if field_statuses.get(field) in {"AMBIGUOUS", "MISSING", "SENTINEL"}
    ]
    if _governance_truthy_count(cohort.get("damageEvents")) <= 0:
        blockers.append("observedDamage")
    if _governance_truthy_count(cadence_counts.get("landedIntervalCount")) <= 0:
        blockers.append("observedOrdinaryCadence")
    return sorted(dict.fromkeys(blockers))


def _governance_field_fix_guidance_categories(
    field_final_statuses: Mapping[str, str],
    basic_blockers: Sequence[str],
    cadence_counts: Mapping[str, int],
) -> dict[str, str]:
    blocker_set = set(basic_blockers)
    guidance: dict[str, str] = {}
    for field in GOVERNANCE_RUNTIME_REQUIRED_FIELDS:
        if field in {"catMesh", "factionAlignment", "nanoOrSpecialBehavior"}:
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_OPTIONAL
        elif field == "attackRange":
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_RUNTIME_POLICY
        elif field == "attackDelay":
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_RESOLVED
        elif field == "naturalOrWeaponMode":
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_RESOLVED
        elif field == "rechargeDelay":
            guidance[field] = (
                GOVERNANCE_FIX_GUIDANCE_RESOLVED
                if _governance_truthy_count(
                    cadence_counts.get("landedIntervalCount")
                )
                > 0
                else GOVERNANCE_FIX_GUIDANCE_CAPTURE_MORE
            )
        elif field in blocker_set:
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_CAPTURE_MORE
        elif (
            field_final_statuses.get(field)
            == GOVERNANCE_FINAL_STATUS_MISSING_PROTOCOL_PROVABLE
        ):
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_CAPTURE_MORE
        elif (
            field_final_statuses.get(field)
            == GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN
        ):
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_ENGINEERING_REQUIRED
        elif (
            field_final_statuses.get(field)
            == GOVERNANCE_FINAL_STATUS_NOT_REQUIRED
        ):
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_OPTIONAL
        else:
            guidance[field] = GOVERNANCE_FIX_GUIDANCE_RESOLVED
    return guidance


def _governance_basic_dry_run_contract(
    cohort: Mapping[str, Any],
    cadence_streams: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    return {
        "model": GOVERNANCE_BASIC_COMBAT_MODEL,
        "productionEligible": False,
        "promotionPolicy": "dry-run evidence only; legacy production gates remain unchanged",
        "damageModel": "replay captured positive ordinary AttackInfo amounts",
        "timingModel": "replay captured landed-interval observations",
        "rangeModel": "generic melee runtime policy",
        "attackPresentation": "explicit raw AttackInfo packet fields",
        "sourceIdentities": sorted(cohort.get("identities", ())),
        "cadenceStreams": cadence_streams,
    }


def _governance_new_cohort(
    name: str,
    level: str,
    monster_data: str,
    *,
    synthetic: bool = False,
) -> dict[str, Any]:
    return {
        "name": name,
        "level": level,
        "monsterData": monster_data,
        "synthetic": synthetic,
        "identities": set(),
        "scfuRows": 0,
        "enemyFullUpdateRows": 0,
        "enemyStateRows": 0,
        "enemyCombatRows": 0,
        "directCombatRows": 0,
        "targetOnlyCombatRows": 0,
        "lifecycleRows": 0,
        "deathCount": 0,
        "damageEvents": 0,
        "attackStarts": 0,
        "attackHits": 0,
        "targetChanges": 0,
        "followChaseRows": 0,
        "vendorEvidence": 0,
        "shopEvidence": 0,
        "dialogueEvidence": 0,
        "interactionEvidence": 0,
        "guardStaticEvidence": 0,
        "maxHealthObserved": 0,
        "damageTypes": set(),
        "hitTypes": set(),
        "attackSlots": set(),
        "specialOrNanoRows": 0,
        "sentinelFields": set(),
        "ambiguousReasons": set(),
        "_rawOrdinaryAttackInfoEvents": [],
        "observedOrdinaryCadenceStreams": [],
    }


def _governance_cohort_key_from_row(row: Mapping[str, Any]) -> tuple[str, str, str]:
    return (
        _governance_row_name(row),
        _governance_row_field(row, "level"),
        _governance_row_field(
            row,
            "monsterData",
            "monsterDataId",
            "monsterDataTemplate",
            "CorpseMonsterData",
        ),
    )


def _governance_is_direct_combat_action(row: Mapping[str, Any]) -> bool:
    text = " ".join(
        str(_governance_casefold_lookup(row, key) or "")
        for key in ("MessageType", "Action", "Detail", "eventType")
    ).casefold()
    if "inforequest" in text or "despawn" in text:
        return False
    combat_terms = (
        "attack",
        "attackinfo",
        "hit",
        "damage",
        "fight",
        "specialattackweapon",
        "missedattack",
    )
    return any(term in text for term in combat_terms)


def _governance_is_attack_start(row: Mapping[str, Any]) -> bool:
    text = " ".join(
        str(_governance_casefold_lookup(row, key) or "")
        for key in ("MessageType", "Action", "Detail")
    ).casefold()
    return "attackinfo" not in text and "missedattackinfo" not in text and "attack" in text


def _governance_is_attack_hit(row: Mapping[str, Any]) -> bool:
    text = " ".join(
        str(_governance_casefold_lookup(row, key) or "")
        for key in ("MessageType", "Action", "Detail")
    ).casefold()
    return "attackinfo" in text or "hit" in text or "missedattackinfo" in text


def _governance_is_target_change(row: Mapping[str, Any]) -> bool:
    text = " ".join(
        str(_governance_casefold_lookup(row, key) or "")
        for key in ("MessageType", "Action", "Detail", "eventType", "Phase")
    ).casefold()
    return "target" in text or "fight" in text


def _governance_index_cohorts(
    cohorts: Mapping[tuple[str, str, str], dict[str, Any]]
) -> tuple[dict[str, dict[str, Any]], set[str]]:
    identity_to_cohort: dict[str, dict[str, Any]] = {}
    ambiguous_identities: set[str] = set()
    for cohort in cohorts.values():
        for identity in cohort["identities"]:
            previous = identity_to_cohort.get(identity)
            if previous is not None and previous is not cohort:
                ambiguous_identities.add(identity)
                previous["ambiguousReasons"].add("identity maps to multiple cohorts")
                cohort["ambiguousReasons"].add("identity maps to multiple cohorts")
            else:
                identity_to_cohort[identity] = cohort
    return identity_to_cohort, ambiguous_identities


def _governance_increment_identity_count(
    identity_to_cohort: Mapping[str, dict[str, Any]],
    identity: str | None,
    field: str,
    amount: int = 1,
) -> bool:
    if identity is None:
        return False
    cohort = identity_to_cohort.get(identity)
    if cohort is None:
        return False
    cohort[field] += amount
    return True


def _governance_contract_field_statuses(cohort: Mapping[str, Any]) -> dict[str, str]:
    statuses = {field: "MISSING" for field in GOVERNANCE_RUNTIME_REQUIRED_FIELDS}
    for field in GOVERNANCE_OPTIONAL_OBSERVATION_FIELDS:
        statuses[field] = "OPTIONAL_NOT_OBSERVED"
    for field in GOVERNANCE_SEPARATE_SPAWN_CONCERN_FIELDS:
        statuses[field] = "SEPARATE_SPAWN_CONCERN"
    if cohort["identities"]:
        statuses["actorIdentity"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    if cohort["monsterData"] != "unknown":
        statuses["monsterData"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    if cohort["level"] != "unknown":
        statuses["level"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    if cohort["maxHealthObserved"]:
        statuses["maxHealth"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    if cohort["damageTypes"]:
        statuses["damageType"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    if cohort["hitTypes"]:
        statuses["hitType"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    if cohort["attackSlots"]:
        statuses["attackSlot"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    hit_type_values = {str(value).casefold() for value in cohort["hitTypes"]}
    if hit_type_values and hit_type_values <= {"normal", "0"}:
        statuses["defaultAttackType"] = "DERIVABLE_BY_EXISTING_GOVERNED_RULE"
    if cohort["followChaseRows"]:
        statuses["followChaseBehavior"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    if cohort["directCombatRows"]:
        statuses["archetypeLinkage"] = "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK"
    if cohort["specialOrNanoRows"]:
        statuses["nanoOrSpecialBehavior"] = "OPTIONAL_OBSERVED"
    for field in cohort["sentinelFields"]:
        normalized = field[:1].lower() + field[1:]
        if normalized in statuses:
            if statuses[normalized].startswith("PROVEN_") or statuses[normalized] == (
                "DERIVABLE_BY_EXISTING_GOVERNED_RULE"
            ):
                continue
            if normalized in GOVERNANCE_SEPARATE_SPAWN_CONCERN_FIELDS:
                statuses[normalized] = "SEPARATE_SPAWN_CONCERN_SENTINEL"
            elif normalized in GOVERNANCE_OPTIONAL_OBSERVATION_FIELDS:
                statuses[normalized] = "OPTIONAL_SENTINEL"
            else:
                statuses[normalized] = "SENTINEL"
    if cohort["ambiguousReasons"]:
        for field in ("actorIdentity", "archetypeLinkage"):
            statuses[field] = "AMBIGUOUS"
    return statuses


def _governance_field_resolution_reason(
    field: str,
    final_status: str,
) -> str:
    if field == "damageType" and final_status == GOVERNANCE_FINAL_STATUS_PROVEN:
        return "decoded raw AttackInfo.damageTypeWire for the attacking NPC"
    if field in {"hitType", "attackSlot"} and final_status == GOVERNANCE_FINAL_STATUS_PROVEN:
        return "decoded or projected raw AttackInfo combat field for the attacking NPC"
    if final_status == GOVERNANCE_FINAL_STATUS_DERIVED_GOVERNED:
        return "governed derivation is allowed only after the required packet field is proven"
    if field in GOVERNANCE_OBSERVED_DAMAGE_BASIC_MODEL_FIELDS:
        return "basic captured combat can replay positive observed damage amounts without treating observed extrema as authoritative min/max stats"
    if field == "attackDelay":
        return "passive ordinary timing does not expose an authoritative NPC attack-delay stat or governed timing model"
    if field == "rechargeDelay":
        return "passive ordinary timing does not expose an authoritative NPC recharge-delay stat or governed timing model"
    if field == "attackRange":
        return "observed hit, chase, and follow distances are lower bounds, not the maximum legal attack envelope"
    if field == "naturalOrWeaponMode":
        return "slot values without governed ownership/equipment context do not prove natural versus weapon mode"
    if final_status == GOVERNANCE_FINAL_STATUS_NOT_REQUIRED:
        return "field is outside the basic runtime combat contract for this audit"
    return "field is protocol-provable but missing from the selected capture projection"


def _governance_final_field_resolutions(
    cohort: Mapping[str, Any],
    field_statuses: Mapping[str, str],
) -> dict[str, dict[str, str]]:
    resolutions: dict[str, dict[str, str]] = {}
    for field in GOVERNANCE_RUNTIME_REQUIRED_FIELDS:
        status = field_statuses.get(field, "MISSING")
        if status.startswith("PROVEN_"):
            final_status = GOVERNANCE_FINAL_STATUS_PROVEN
            resolution_class = GOVERNANCE_FIELD_RESOLUTION_DIRECT_CAPTURE
            authority = GOVERNANCE_FINAL_AUTHORITY_DIRECT_PACKET
        elif status == "DERIVABLE_BY_EXISTING_GOVERNED_RULE":
            final_status = GOVERNANCE_FINAL_STATUS_DERIVED_GOVERNED
            resolution_class = GOVERNANCE_FIELD_RESOLUTION_DERIVED_RULE
            authority = GOVERNANCE_FINAL_AUTHORITY_DERIVED_GOVERNED
        elif status.startswith("OPTIONAL_") or status.startswith("SEPARATE_"):
            final_status = GOVERNANCE_FINAL_STATUS_NOT_REQUIRED
            resolution_class = GOVERNANCE_FIELD_RESOLUTION_NOT_REQUIRED
            authority = GOVERNANCE_FINAL_AUTHORITY_NOT_REQUIRED
        elif (
            field in GOVERNANCE_OBSERVED_DAMAGE_BASIC_MODEL_FIELDS
            and _governance_truthy_count(cohort.get("damageEvents")) > 0
        ):
            final_status = GOVERNANCE_FINAL_STATUS_NOT_REQUIRED
            resolution_class = GOVERNANCE_FIELD_RESOLUTION_NOT_REQUIRED
            authority = GOVERNANCE_FINAL_AUTHORITY_NOT_REQUIRED
        elif field in GOVERNANCE_REQUIRED_NOT_PROTOCOL_PROVEN_FIELDS:
            final_status = GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN
            resolution_class = GOVERNANCE_FIELD_RESOLUTION_REQUIRED_NOT_PROTOCOL_PROVEN
            authority = GOVERNANCE_FINAL_AUTHORITY_REQUIRED_NOT_PROTOCOL_PROVEN
        else:
            final_status = GOVERNANCE_FINAL_STATUS_MISSING_PROTOCOL_PROVABLE
            resolution_class = GOVERNANCE_FIELD_RESOLUTION_DIRECT_CAPTURE
            authority = GOVERNANCE_FINAL_AUTHORITY_MORE_CAPTURE
        resolutions[field] = {
            "status": final_status,
            "resolutionClass": resolution_class,
            "authority": authority,
            "reason": _governance_field_resolution_reason(field, final_status),
        }
    return resolutions


def _governance_classify_cohort(cohort: Mapping[str, Any]) -> str:
    if cohort["ambiguousReasons"]:
        return GOVERNANCE_CATEGORY_AMBIGUOUS
    if cohort["directCombatRows"]:
        if cohort["guardStaticEvidence"]:
            return GOVERNANCE_CATEGORY_GUARD_COMBAT
        if not cohort["followChaseRows"]:
            return GOVERNANCE_CATEGORY_COMBAT_CAPABLE_UNRESOLVED
        if cohort["vendorEvidence"] or cohort["shopEvidence"] or cohort["dialogueEvidence"]:
            return GOVERNANCE_CATEGORY_COMBAT_CAPABLE_UNRESOLVED
        return GOVERNANCE_CATEGORY_ORDINARY_HOSTILE
    if cohort["vendorEvidence"] or cohort["shopEvidence"]:
        return GOVERNANCE_CATEGORY_VENDOR_NONCOMBAT
    if cohort["dialogueEvidence"] or cohort["interactionEvidence"]:
        return GOVERNANCE_CATEGORY_SOCIAL_NONCOMBAT
    return GOVERNANCE_CATEGORY_NO_COMBAT


def _governance_capture_action(
    cohort: Mapping[str, Any],
    unresolved: Sequence[str],
    field_final_statuses: Mapping[str, str] | None = None,
) -> str:
    if not cohort["directCombatRows"]:
        return "No ordinary combat capture needed unless this entity is intentionally tested for aggression."
    if field_final_statuses is not None:
        missing_protocol_fields = [
            field
            for field in unresolved
            if field_final_statuses.get(field)
            == GOVERNANCE_FINAL_STATUS_MISSING_PROTOCOL_PROVABLE
        ]
        not_protocol_proven_fields = [
            field
            for field in unresolved
            if field_final_statuses.get(field)
            == GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN
        ]
        actions: list[str] = []
        if any(field in missing_protocol_fields for field in ("hitType", "attackSlot")):
            actions.append(
                "project named AttackInfo HitType and WeaponSlot from decoded columns or Detail text"
            )
        if "damageType" in missing_protocol_fields:
            actions.append(
                "decode/project existing raw AttackInfo.damageTypeWire; capture more only if raw AttackInfo is absent"
            )
        if "defaultAttackType" in missing_protocol_fields:
            actions.append(
                "require a governed normal-hit AttackInfo contract before deriving the runtime default"
            )
        if "followChaseBehavior" in missing_protocol_fields:
            actions.append("start outside melee range and capture chase/follow into first attack")
        if "maxHealth" in missing_protocol_fields:
            actions.append("capture full health state before first damage and through death")
        remaining_missing = [
            field
            for field in missing_protocol_fields
            if field
            not in {
                "attackSlot",
                "damageType",
                "defaultAttackType",
                "followChaseBehavior",
                "hitType",
                "maxHealth",
            }
        ]
        if remaining_missing:
            actions.append(
                "capture or project protocol-provable fields: "
                + ",".join(sorted(remaining_missing))
            )
        if not_protocol_proven_fields:
            actions.append(
                "no additional ordinary hit-count capture requested; required-but-not-protocol-proven fields need governed runtime/analyzer rules: "
                + ",".join(sorted(not_protocol_proven_fields))
            )
        if not actions:
            actions.append("capture full death to corpse to respawn cycle for lifecycle confirmation")
        return "; ".join(dict.fromkeys(actions))
    actions: list[str] = []
    if any(field in unresolved for field in ("hitType", "attackSlot")):
        actions.append("project named AttackInfo HitType and WeaponSlot from decoded columns or Detail text")
    if "damageType" in unresolved:
        actions.append("require an authoritative decoded AttackInfo damageTypeWire field; do not infer from Unknown or Unk fields")
    if any(field in unresolved for field in ("minDamage", "maxDamage")):
        actions.append("require authoritative stat or formula evidence; ordinary-hit samples are observations, not min/max damage")
    if any(field in unresolved for field in ("attackDelay", "rechargeDelay")):
        actions.append("require authoritative attack/recharge stat evidence or a governed timing model; passive hit intervals alone are insufficient")
    if "defaultAttackType" in unresolved:
        actions.append("require a governed normal-hit AttackInfo contract before deriving the runtime default")
    if "attackRange" in unresolved:
        actions.append("require authoritative range stat or governed reach rule; chase/follow only proves behavior")
    if "followChaseBehavior" in unresolved:
        actions.append("start outside melee range and capture chase/follow into first attack")
    if "naturalOrWeaponMode" in unresolved:
        actions.append("require governed equipment/weapon ownership or visual-mode evidence; unarmed appearance alone is insufficient")
    if "maxHealth" in unresolved:
        actions.append("capture full health state before first damage and through death")
    if not actions:
        actions.append("capture full death to corpse to respawn cycle for lifecycle confirmation")
    return "; ".join(dict.fromkeys(actions))


def _governance_next_capture_priority(cohort: Mapping[str, Any]) -> tuple[Any, ...]:
    category_rank = {
        GOVERNANCE_CATEGORY_ORDINARY_HOSTILE: 0,
        GOVERNANCE_CATEGORY_GUARD_COMBAT: 1,
        GOVERNANCE_CATEGORY_COMBAT_CAPABLE_UNRESOLVED: 2,
        GOVERNANCE_CATEGORY_AMBIGUOUS: 3,
    }.get(str(cohort["category"]), 4)
    singleton_rank = 0 if cohort["identityCount"] > 1 else 1
    return (
        category_rank,
        singleton_rank,
        -_governance_truthy_count(cohort["identityCount"]),
        -_governance_truthy_count(cohort["directCombatRows"]),
        str(cohort["name"]).casefold(),
        str(cohort["level"]),
        str(cohort["monsterData"]),
    )


def _governance_collect_dossier_rows(value: Any) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []

    def visit(node: Any) -> None:
        if isinstance(node, Mapping):
            if _governance_sentinel_fields(node) and (
                _governance_casefold_lookup(node, "name") is not None
                or _governance_casefold_lookup(node, "identity") is not None
                or _governance_casefold_lookup(node, "sourceIdentity") is not None
            ):
                rows.append(dict(node))
                return
            for child in node.values():
                visit(child)
        elif isinstance(node, list):
            for child in node:
                visit(child)

    visit(value)
    return rows


def _governance_load_dossier_rows(capture_root: Path) -> list[dict[str, Any]]:
    path = capture_root / "enemy-dossier.json"
    if not path.is_file():
        return []
    with path.open("r", encoding="utf-8-sig") as handle:
        return _governance_collect_dossier_rows(json.load(handle))


def _governance_load_focused_enemy_identities(capture_root: Path) -> set[str]:
    path = capture_root / "capture_info.json"
    if not path.is_file():
        return set()
    with path.open("r", encoding="utf-8-sig") as handle:
        value = json.load(handle)
    if not isinstance(value, Mapping):
        return set()
    identities = value.get("focusedEnemyIdentities")
    if not isinstance(identities, list):
        return set()
    return {
        identity
        for identity in (
            _governance_identity_hex(raw_identity)
            for raw_identity in identities
        )
        if identity is not None
    }


def _governance_scoped_cohorts(capture_root: Path) -> list[dict[str, Any]]:
    cohorts_by_key: dict[tuple[str, str, str], dict[str, Any]] = {}
    focused_enemy_identities = _governance_load_focused_enemy_identities(capture_root)

    def ensure_cohort(
        row: Mapping[str, Any] | None,
        identity: str | None,
        *,
        synthetic: bool = False,
    ) -> dict[str, Any]:
        if row is None:
            key = (
                f"unknown-0x{identity}" if identity else "unknown",
                "unknown",
                "unknown",
            )
        else:
            key = _governance_cohort_key_from_row(row)
            if key == ("unknown", "unknown", "unknown") and identity:
                key = (f"unknown-0x{identity}", "unknown", "unknown")
        cohort = cohorts_by_key.setdefault(
            key,
            _governance_new_cohort(key[0], key[1], key[2], synthetic=synthetic),
        )
        if identity:
            cohort["identities"].add(identity)
        return cohort

    for row in _governance_load_dossier_rows(capture_root):
        identity = _governance_row_identity(
            row,
            "identity",
            "sourceIdentity",
            "PrimaryIdentity",
            "entityId",
        )
        cohort = ensure_cohort(row, identity)
        cohort["sentinelFields"].update(_governance_sentinel_fields(row))
        max_health = _governance_int(
            _governance_casefold_lookup(row, "maxHealth")
            or _governance_casefold_lookup(row, "maxHp")
            or _governance_casefold_lookup(row, "health")
        )
        if max_health is not None and max_health > 0:
            cohort["maxHealthObserved"] = max(cohort["maxHealthObserved"], max_health)

    identity_to_cohort, ambiguous_identities = _governance_index_cohorts(
        cohorts_by_key
    )

    def ensure_identity(identity: str | None) -> dict[str, Any] | None:
        if identity is None:
            return None
        cohort = identity_to_cohort.get(identity)
        if cohort is not None:
            return cohort
        cohort = ensure_cohort(None, identity, synthetic=True)
        identity_to_cohort[identity] = cohort
        return cohort

    def count_identity_row(
        row: Mapping[str, Any],
        field: str,
        *identity_fields: str,
    ) -> dict[str, Any] | None:
        identity = _governance_row_identity(row, *identity_fields)
        cohort = ensure_identity(identity)
        if cohort is not None:
            cohort[field] += 1
        return cohort

    for row in _governance_csv_rows(capture_root / "scfu-appearance.csv"):
        count_identity_row(row, "scfuRows", "identity", "Identity", "entityId")

    for row in _governance_csv_rows(capture_root / "enemy-full-updates.csv"):
        cohort = count_identity_row(
            row,
            "enemyFullUpdateRows",
            "identity",
            "Identity",
            "entityId",
            "PrimaryIdentity",
        )
        if cohort is not None:
            max_health = _governance_int(
                _governance_casefold_lookup(row, "maxHealth")
                or _governance_casefold_lookup(row, "health")
            )
            if max_health is not None and max_health > 0:
                cohort["maxHealthObserved"] = max(
                    cohort["maxHealthObserved"],
                    max_health,
                )

    for row in _governance_csv_rows(capture_root / "enemy-state.csv"):
        cohort = count_identity_row(row, "enemyStateRows", "entityId", "identity")
        if cohort is not None:
            max_health = _governance_int(_governance_casefold_lookup(row, "maxHealth"))
            if max_health is not None and max_health > 0:
                cohort["maxHealthObserved"] = max(
                    cohort["maxHealthObserved"],
                    max_health,
                )
            if _governance_is_target_change(row):
                cohort["targetChanges"] += 1

    for row in _governance_csv_rows(capture_root / "npc-lifecycle.csv"):
        cohort = count_identity_row(
            row,
            "lifecycleRows",
            "PrimaryIdentity",
            "identity",
            "entityId",
        )
        if cohort is not None:
            text = " ".join(
                str(_governance_casefold_lookup(row, key) or "")
                for key in ("Phase", "MessageType")
            ).casefold()
            if "death" in text or "corpse" in text:
                cohort["deathCount"] += 1
            lifecycle_target_text = " ".join(
                str(_governance_casefold_lookup(row, key) or "")
                for key in ("Phase", "MessageType")
            ).casefold()
            if "target" in lifecycle_target_text or "fight" in lifecycle_target_text:
                cohort["targetChanges"] += 1

    for row in _governance_csv_rows(capture_root / "enemy-movement.csv"):
        cohort = count_identity_row(row, "followChaseRows", "Identity", "identity")
        if cohort is not None:
            text = " ".join(
                str(_governance_casefold_lookup(row, key) or "")
                for key in ("MoveType", "MessageType", "Detail")
            ).casefold()
            if "follow" not in text and "chase" not in text:
                cohort["followChaseRows"] -= 1

    for row in _governance_csv_rows(capture_root / "enemy-stat-updates.csv"):
        cohort = count_identity_row(row, "enemyStateRows", "Identity", "identity")
        if cohort is not None:
            stat_name = str(_governance_casefold_lookup(row, "Stat") or "").casefold()
            value = _governance_int(_governance_casefold_lookup(row, "Value"))
            if value is not None and stat_name == "health" and value > 0:
                cohort["maxHealthObserved"] = max(cohort["maxHealthObserved"], value)

    for row in _governance_csv_rows(capture_root / "enemy-respawns.csv"):
        death_cohort = ensure_identity(_governance_row_identity(row, "DeathIdentity"))
        if death_cohort is not None:
            death_cohort["deathCount"] += 1
        respawn_cohort = ensure_identity(
            _governance_row_identity(row, "RespawnIdentity")
        )
        if respawn_cohort is not None:
            respawn_cohort["lifecycleRows"] += 1

    for row in _governance_csv_rows(capture_root / "corpse-full-updates.csv"):
        count_identity_row(row, "deathCount", "DeadNpcIdentity", "TailDeadNpcIdentity")

    machine_owner_by_identity: dict[str, str] = {}
    for row in _governance_csv_rows(capture_root / "vendor-full-updates.csv"):
        machine_identity = _governance_typed_identity_hex(
            _governance_casefold_lookup(row, "Identity"),
            "VendingMachine",
        )
        owner_identity = _governance_row_identity(row, "OwnerInstance")
        if machine_identity and owner_identity:
            machine_owner_by_identity[machine_identity] = owner_identity
        cohort = ensure_identity(owner_identity)
        if cohort is not None:
            cohort["vendorEvidence"] += 1

    for row in _governance_csv_rows(capture_root / "shop-updates.csv"):
        terminal_identity = (
            _governance_typed_identity_hex(
                _governance_casefold_lookup(row, "TerminalIdentity"),
                "VendingMachine",
            )
            or _governance_typed_identity_hex(
                _governance_casefold_lookup(row, "Identity"),
                "VendingMachine",
            )
        )
        cohort = ensure_identity(machine_owner_by_identity.get(terminal_identity or ""))
        if cohort is not None:
            cohort["shopEvidence"] += 1

    for line in _governance_text_lines(capture_root / "chat-dialogue.log"):
        cohort = ensure_identity(_governance_line_target_identity(line))
        if cohort is not None:
            cohort["dialogueEvidence"] += 1

    for line in _governance_text_lines(capture_root / "npc-interactions.log"):
        cohort = ensure_identity(_governance_line_target_identity(line))
        if cohort is not None:
            cohort["interactionEvidence"] += 1

    local_player_identities: set[str] = set()
    for row in _governance_csv_rows(capture_root / "enemy-combat.csv"):
        source_role = str(_governance_casefold_lookup(row, "SourceRole") or "").casefold()
        target_role = str(_governance_casefold_lookup(row, "TargetRole") or "").casefold()
        source_identity = _governance_row_identity(row, "SourceIdentity", "AttackerIdentity")
        target_identity = _governance_row_identity(row, "TargetIdentity", "DefenderIdentity")
        if source_role == "local-player" and source_identity is not None:
            local_player_identities.add(source_identity)
        if target_role == "local-player" and target_identity is not None:
            local_player_identities.add(target_identity)
        source_cohort = ensure_identity(source_identity) if source_role == "enemy" else None
        target_cohort = ensure_identity(target_identity) if target_role == "enemy" else None
        if source_cohort is not None:
            source_cohort["enemyCombatRows"] += 1
            if _governance_is_direct_combat_action(row):
                source_cohort["directCombatRows"] += 1
                if _governance_is_attack_start(row):
                    source_cohort["attackStarts"] += 1
                if _governance_is_attack_hit(row):
                    source_cohort["attackHits"] += 1
                amount = _governance_int(_governance_row_or_detail_value(row, ("Amount",)))
                if amount is not None and amount > 0:
                    source_cohort["damageEvents"] += 1
                for field_names, target_set in (
                    (("DamageType", "DamageTypeWire"), "damageTypes"),
                    (("HitType", "HitTypeWire"), "hitTypes"),
                    (("WeaponSlot", "AttackInfoWeaponSlot"), "attackSlots"),
                ):
                    value = _governance_row_or_detail_value(row, field_names)
                    if value is not None:
                        source_cohort[target_set].add(value)
                if _governance_is_special_or_nano_action(row):
                    source_cohort["specialOrNanoRows"] += 1
            if _governance_is_target_change(row):
                source_cohort["targetChanges"] += 1
        if target_cohort is not None and target_cohort is not source_cohort:
            target_cohort["enemyCombatRows"] += 1
            target_cohort["targetOnlyCombatRows"] += 1

    for row in _governance_csv_rows(capture_root / "raw-packets.csv"):
        attack_info = _governance_decode_raw_attack_info(row)
        if attack_info is None:
            continue
        if (
            focused_enemy_identities
            and str(attack_info["sourceIdentity"]) not in focused_enemy_identities
        ):
            continue
        if str(attack_info["sourceIdentity"]) in local_player_identities:
            continue
        if (
            local_player_identities
            and str(attack_info["targetIdentity"]) not in local_player_identities
        ):
            continue
        source_cohort = identity_to_cohort.get(str(attack_info["sourceIdentity"]))
        if source_cohort is None:
            continue
        source_cohort["damageTypes"].add(str(attack_info["damageTypeWire"]))
        _governance_add_raw_ordinary_attack_info_event(
            source_cohort,
            capture_id=_governance_capture_id(capture_root) or capture_root.name,
            row=row,
            attack_info=attack_info,
        )

    for identity in ambiguous_identities:
        cohort = identity_to_cohort.get(identity)
        if cohort is not None:
            cohort["ambiguousReasons"].add("identity reuse detected")

    cohorts: list[dict[str, Any]] = []
    for cohort in cohorts_by_key.values():
        cohorts.append(_governance_finalize_cohort(cohort))
    cohorts.sort(
        key=lambda row: (
            not row["combatCandidate"],
            row["category"],
            str(row["name"]).casefold(),
            str(row["level"]),
            str(row["monsterData"]),
        )
    )
    return cohorts


def _governance_unresolved_required_fields(
    field_statuses: Mapping[str, str],
) -> list[str]:
    return sorted(
        field
        for field in GOVERNANCE_BASIC_COMBAT_REQUIRED_FIELDS
        if field_statuses.get(field) in {"AMBIGUOUS", "MISSING", "SENTINEL"}
    )


def _governance_finalize_cohort(
    cohort: Mapping[str, Any],
    *,
    source_capture_ids: Sequence[str] = (),
    source_paths: Sequence[str] = (),
) -> dict[str, Any]:
    category = _governance_classify_cohort(cohort)
    field_statuses = _governance_contract_field_statuses(cohort)
    final_resolutions = _governance_final_field_resolutions(cohort, field_statuses)
    field_final_statuses = {
        field: resolution["status"]
        for field, resolution in final_resolutions.items()
    }
    field_resolution_classes = {
        field: resolution["resolutionClass"]
        for field, resolution in final_resolutions.items()
    }
    field_final_authorities = {
        field: resolution["authority"]
        for field, resolution in final_resolutions.items()
    }
    field_resolution_reasons = {
        field: resolution["reason"]
        for field, resolution in final_resolutions.items()
    }
    observed_cadence_streams = _governance_normalize_observed_cadence_streams(
        [
            *cohort.get("observedOrdinaryCadenceStreams", ()),
            *_governance_observed_cadence_streams_from_events(
                cohort.get("_rawOrdinaryAttackInfoEvents", ())
            ),
        ]
    )
    observed_cadence_counts = _governance_observed_cadence_counts(
        observed_cadence_streams
    )
    unresolved = _governance_unresolved_required_fields(field_statuses)
    combat_candidate = category in GOVERNANCE_COMBAT_CANDIDATE_CATEGORIES
    runtime_ready = combat_candidate and not unresolved
    basic_combat_candidate = category == GOVERNANCE_CATEGORY_ORDINARY_HOSTILE
    basic_combat_blockers = _governance_basic_combat_blockers(
        cohort,
        field_statuses,
        category,
        observed_cadence_counts,
    )
    basic_combat_ready = basic_combat_candidate and not basic_combat_blockers
    field_fix_guidance_categories = _governance_field_fix_guidance_categories(
        field_final_statuses,
        basic_combat_blockers,
        observed_cadence_counts,
    )
    state = (
        GOVERNANCE_STATE_BLOCKED_INSUFFICIENT_EVIDENCE
        if combat_candidate and not runtime_ready
        else GOVERNANCE_STATE_NEW_RAW_VERIFIED
    )
    return {
        "name": cohort["name"],
        "level": cohort["level"],
        "monsterData": cohort["monsterData"],
        "state": state,
        "category": category,
        "combatCandidate": combat_candidate,
        "runtimeReady": runtime_ready,
        "basicCombatCandidate": basic_combat_candidate,
        "basicCombatReady": basic_combat_ready,
        "basicCombatModel": GOVERNANCE_BASIC_COMBAT_MODEL
        if basic_combat_ready
        else "",
        "basicCombatPromotion": "DRY_RUN_ONLY"
        if basic_combat_ready
        else "BLOCKED",
        "basicCombatBlockers": basic_combat_blockers,
        "identityCount": len(cohort["identities"]),
        "identities": sorted(cohort["identities"]),
        "sourceCaptureIds": sorted(source_capture_ids),
        "sourcePaths": sorted(source_paths),
        "scfuRows": cohort["scfuRows"],
        "enemyFullUpdateRows": cohort["enemyFullUpdateRows"],
        "enemyStateRows": cohort["enemyStateRows"],
        "enemyCombatRows": cohort["enemyCombatRows"],
        "directCombatRows": cohort["directCombatRows"],
        "targetOnlyCombatRows": cohort["targetOnlyCombatRows"],
        "lifecycleRows": cohort["lifecycleRows"],
        "deathCount": cohort["deathCount"],
        "damageEvents": cohort["damageEvents"],
        "attackStarts": cohort["attackStarts"],
        "attackHits": cohort["attackHits"],
        "targetChanges": cohort["targetChanges"],
        "followChaseRows": cohort["followChaseRows"],
        "vendorEvidence": cohort["vendorEvidence"],
        "shopEvidence": cohort["shopEvidence"],
        "dialogueEvidence": cohort["dialogueEvidence"],
        "interactionEvidence": cohort["interactionEvidence"],
        "guardStaticEvidence": cohort["guardStaticEvidence"],
        "maxHealthObserved": cohort["maxHealthObserved"],
        "damageTypes": _governance_sorted_evidence_values(cohort["damageTypes"]),
        "hitTypes": _governance_sorted_evidence_values(cohort["hitTypes"]),
        "attackSlots": _governance_sorted_evidence_values(cohort["attackSlots"]),
        "specialOrNanoRows": cohort["specialOrNanoRows"],
        "observedOrdinaryAttackInfoObservationCount": observed_cadence_counts[
            "attackInfoObservationCount"
        ],
        "observedOrdinaryCadenceStreamCount": observed_cadence_counts[
            "landedStreamCount"
        ],
        "observedOrdinaryCadenceIntervalCount": observed_cadence_counts[
            "landedIntervalCount"
        ],
        "observedOrdinaryCadenceStreams": observed_cadence_streams,
        "sentinelFields": sorted(cohort["sentinelFields"]),
        "ambiguousReasons": sorted(cohort["ambiguousReasons"]),
        "fieldStatuses": field_statuses,
        "fieldClassifications": dict(GOVERNANCE_RUNTIME_FIELD_CLASSES),
        "fieldRuntimeExecutionClassifications": dict(
            GOVERNANCE_FIELD_RUNTIME_EXECUTION_CLASSIFICATIONS
        ),
        "fieldFinalStatuses": field_final_statuses,
        "fieldResolutionClasses": field_resolution_classes,
        "fieldFinalAuthorities": field_final_authorities,
        "fieldResolutionReasons": field_resolution_reasons,
        "fieldFixGuidanceCategories": field_fix_guidance_categories,
        "basicCombatDryRunContract": _governance_basic_dry_run_contract(
            cohort,
            observed_cadence_streams,
        )
        if basic_combat_ready
        else None,
        "provenFields": sorted(
            field
            for field, status in field_statuses.items()
            if status.startswith("PROVEN_")
            or status == "DERIVABLE_BY_EXISTING_GOVERNED_RULE"
        ),
        "unresolvedRequiredFields": unresolved,
        "missingProtocolProvableFields": sorted(
            field
            for field in unresolved
            if field_final_statuses.get(field)
            == GOVERNANCE_FINAL_STATUS_MISSING_PROTOCOL_PROVABLE
        ),
        "notProtocolProvenFields": sorted(
            field
            for field in unresolved
            if field_final_statuses.get(field)
            == GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN
        ),
        "notRequiredBasicCombatFields": sorted(
            field
            for field, status in field_final_statuses.items()
            if status == GOVERNANCE_FINAL_STATUS_NOT_REQUIRED
        ),
        "captureAction": _governance_capture_action(
            cohort,
            unresolved,
            field_final_statuses,
        ),
    }


def _governance_aggregate_key(cohort: Mapping[str, Any]) -> tuple[str, str, str] | None:
    key = (str(cohort["name"]), str(cohort["level"]), str(cohort["monsterData"]))
    if any(value == "unknown" or value.startswith("unknown-0x") for value in key):
        return None
    if cohort["ambiguousReasons"]:
        return None
    return key


def _governance_aggregate_scoped_cohorts(
    captures: Sequence[Mapping[str, Any]],
) -> list[dict[str, Any]]:
    counters = (
        "scfuRows",
        "enemyFullUpdateRows",
        "enemyStateRows",
        "enemyCombatRows",
        "directCombatRows",
        "targetOnlyCombatRows",
        "lifecycleRows",
        "deathCount",
        "damageEvents",
        "attackStarts",
        "attackHits",
        "targetChanges",
        "followChaseRows",
        "vendorEvidence",
        "shopEvidence",
        "dialogueEvidence",
        "interactionEvidence",
        "guardStaticEvidence",
        "specialOrNanoRows",
    )
    aggregates: dict[tuple[str, str, str], dict[str, Any]] = {}
    source_capture_ids: dict[tuple[str, str, str], set[str]] = {}
    source_paths: dict[tuple[str, str, str], set[str]] = {}
    for capture in captures:
        for cohort in capture["cohorts"]:
            key = _governance_aggregate_key(cohort)
            if key is None:
                continue
            aggregate = aggregates.setdefault(
                key,
                _governance_new_cohort(key[0], key[1], key[2]),
            )
            for counter in counters:
                aggregate[counter] += _governance_truthy_count(cohort.get(counter))
            aggregate["maxHealthObserved"] = max(
                aggregate["maxHealthObserved"],
                _governance_truthy_count(cohort.get("maxHealthObserved")),
            )
            for field in ("identities", "damageTypes", "hitTypes", "attackSlots"):
                aggregate[field].update(str(value) for value in cohort.get(field, ()))
            aggregate["observedOrdinaryCadenceStreams"].extend(
                cohort.get("observedOrdinaryCadenceStreams", ())
            )
            aggregate["sentinelFields"].update(cohort.get("sentinelFields", ()))
            aggregate["ambiguousReasons"].update(cohort.get("ambiguousReasons", ()))
            source_capture_ids.setdefault(key, set()).add(str(capture["captureId"]))
            source_paths.setdefault(key, set()).add(str(capture["path"]))
    finalized = [
        _governance_finalize_cohort(
            aggregate,
            source_capture_ids=source_capture_ids[key],
            source_paths=source_paths[key],
        )
        for key, aggregate in aggregates.items()
        if len(source_capture_ids[key]) > 1
    ]
    finalized.sort(
        key=lambda row: (
            not row["combatCandidate"],
            row["category"],
            str(row["name"]).casefold(),
            str(row["level"]),
            str(row["monsterData"]),
        )
    )
    return finalized


def _governance_join_mapping(mapping: Mapping[str, Any]) -> str:
    return ",".join(f"{key}:{mapping[key]}" for key in sorted(mapping))


def _governance_print_scoped_cohort(
    prefix: str,
    cohort: Mapping[str, Any],
    *,
    capture_id: str | None = None,
) -> None:
    capture_part = f"captureId={capture_id}|" if capture_id is not None else ""
    source_capture_part = (
        f"sourceCaptureIds={','.join(cohort['sourceCaptureIds'])}|"
        if cohort["sourceCaptureIds"]
        else ""
    )
    print(
        f"{prefix}|"
        f"{capture_part}"
        f"{source_capture_part}"
        f"state={cohort['state']}|"
        f"category={cohort['category']}|"
        f"combatCandidate={str(cohort['combatCandidate']).lower()}|"
        f"runtimeReady={str(cohort['runtimeReady']).lower()}|"
        f"basicCombatCandidate={str(cohort['basicCombatCandidate']).lower()}|"
        f"basicCombatReady={str(cohort['basicCombatReady']).lower()}|"
        f"basicCombatModel={cohort['basicCombatModel']}|"
        f"basicCombatPromotion={cohort['basicCombatPromotion']}|"
        f"basicCombatBlockers={','.join(cohort['basicCombatBlockers'])}|"
        f"name={cohort['name']}|"
        f"level={cohort['level']}|"
        f"monsterData={cohort['monsterData']}|"
        f"identityCount={cohort['identityCount']}|"
        f"scfuRows={cohort['scfuRows']}|"
        f"enemyFullUpdateRows={cohort['enemyFullUpdateRows']}|"
        f"enemyStateRows={cohort['enemyStateRows']}|"
        f"enemyCombatRows={cohort['enemyCombatRows']}|"
        f"directCombatRows={cohort['directCombatRows']}|"
        f"targetOnlyCombatRows={cohort['targetOnlyCombatRows']}|"
        f"lifecycleRows={cohort['lifecycleRows']}|"
        f"deathCount={cohort['deathCount']}|"
        f"damageEvents={cohort['damageEvents']}|"
        f"attackStarts={cohort['attackStarts']}|"
        f"attackHits={cohort['attackHits']}|"
        f"targetChanges={cohort['targetChanges']}|"
        f"followChaseRows={cohort['followChaseRows']}|"
        f"vendorEvidence={cohort['vendorEvidence']}|"
        f"shopEvidence={cohort['shopEvidence']}|"
        f"dialogueEvidence={cohort['dialogueEvidence']}|"
        f"interactionEvidence={cohort['interactionEvidence']}|"
        f"guardStaticEvidence={cohort['guardStaticEvidence']}|"
        f"maxHealthObserved={cohort['maxHealthObserved']}|"
        f"damageTypes={','.join(cohort['damageTypes'])}|"
        f"hitTypes={','.join(cohort['hitTypes'])}|"
        f"attackSlots={','.join(cohort['attackSlots'])}|"
        f"specialOrNanoRows={cohort['specialOrNanoRows']}|"
        f"observedOrdinaryAttackInfoObservationCount={cohort['observedOrdinaryAttackInfoObservationCount']}|"
        f"observedOrdinaryCadenceStreamCount={cohort['observedOrdinaryCadenceStreamCount']}|"
        f"observedOrdinaryCadenceIntervalCount={cohort['observedOrdinaryCadenceIntervalCount']}|"
        f"provenFields={','.join(cohort['provenFields'])}|"
        f"unresolvedRequiredFields={','.join(cohort['unresolvedRequiredFields'])}|"
        f"missingProtocolProvableFields={','.join(cohort['missingProtocolProvableFields'])}|"
        f"notProtocolProvenFields={','.join(cohort['notProtocolProvenFields'])}|"
        f"notRequiredBasicCombatFields={','.join(cohort['notRequiredBasicCombatFields'])}|"
        f"sentinelFields={','.join(cohort['sentinelFields'])}|"
        f"ambiguousReasons={','.join(cohort['ambiguousReasons'])}|"
        f"fieldStatuses={_governance_join_mapping(cohort['fieldStatuses'])}|"
        f"fieldClassifications={_governance_join_mapping(cohort['fieldClassifications'])}|"
        f"fieldRuntimeExecutionClassifications={_governance_join_mapping(cohort['fieldRuntimeExecutionClassifications'])}|"
        f"fieldFinalStatuses={_governance_join_mapping(cohort['fieldFinalStatuses'])}|"
        f"fieldResolutionClasses={_governance_join_mapping(cohort['fieldResolutionClasses'])}|"
        f"fieldFinalAuthorities={_governance_join_mapping(cohort['fieldFinalAuthorities'])}|"
        f"fieldFixGuidanceCategories={_governance_join_mapping(cohort['fieldFixGuidanceCategories'])}|"
        f"captureAction={cohort['captureAction']}"
    )


def validate_legacy_governance_baseline(repo_root: Path) -> int:
    repo_root = repo_root.resolve(strict=True)
    manifest = validate_cohort(repo_root, verify_toolchain=False)
    baseline = load_json_object(
        repo_root / GOVERNANCE_LEGACY_BASELINE,
        "generated-combat legacy governance baseline",
    )
    legacy_generation_identity = baseline["generationIdentity"]
    legacy_artifacts = baseline["artifacts"]
    legacy_expected_counts = baseline["expectedCounts"]
    inventory = _governance_load_inventory(repo_root)
    summary = inventory.get("summary", {})
    artifacts = _governance_manifest_artifacts(manifest)
    mismatches: list[str] = []
    if manifest.get("generationIdentity") != legacy_generation_identity:
        mismatches.append("generationIdentity")
    for role, expected in legacy_artifacts.items():
        actual = artifacts.get(role)
        if not isinstance(actual, Mapping):
            mismatches.append(f"{role}:missing")
            continue
        if actual.get("sha256") != expected["sha256"]:
            mismatches.append(f"{role}:sha256")
        if actual.get("byteLength") != expected["byteLength"]:
            mismatches.append(f"{role}:byteLength")
    for key in (
        "captureSessionsDiscovered",
        "runtimeReadyProfiles",
        "runtimeReadyGeneratedSemanticDefinitions",
    ):
        if summary.get(key) != legacy_expected_counts[key]:
            mismatches.append(key)
    capture_ids = _governance_required_capture_ids(repo_root)
    present_ids = [
        capture_id
        for capture_id in capture_ids
        if not _governance_missing_raw_files(
            repo_root / GOVERNANCE_LEGACY_CAPTURE_ROOT / capture_id
        )
    ]
    missing_ids = sorted(set(capture_ids) - set(present_ids))
    if len(capture_ids) != legacy_expected_counts["requiredHistoricalCaptureRoots"]:
        mismatches.append("requiredHistoricalCaptureRoots")
    if len(present_ids) + len(missing_ids) != len(capture_ids):
        mismatches.append("historicalRawAvailabilityPartition")
    if mismatches:
        raise PipelineError(
            "generated-combat legacy governance baseline drift: "
            + ", ".join(mismatches)
        )
    print(
        "generated-combat legacy baseline PASS "
        f"state={GOVERNANCE_STATE_LEGACY_ACCEPTED_RAW_UNAVAILABLE} "
        f"identity={legacy_generation_identity} "
        f"artifacts={len(legacy_artifacts)} "
        f"runtimeReadyProfiles={summary['runtimeReadyProfiles']} "
        "legacyRuntimeVariantRows="
        f"{legacy_expected_counts['legacyRuntimeVariantRows']} "
        "legacyFullyRawRevalidatableRows="
        f"{legacy_expected_counts['legacyFullyRawRevalidatableRows']} "
        "legacyRawUnavailableRows="
        f"{legacy_expected_counts['legacyRawUnavailableRows']} "
        f"requiredHistoricalRaw={len(capture_ids)} "
        f"presentHistoricalRaw={len(present_ids)} "
        f"missingHistoricalRaw={len(missing_ids)}"
    )
    for role in sorted(legacy_artifacts):
        expected = legacy_artifacts[role]
        print(
            "LEGACY_ARTIFACT|"
            f"role={role}|sha256={expected['sha256']}|byteLength={expected['byteLength']}"
        )
    return 0


def validate_accepted_cohort(repo_root: Path) -> int:
    repo_root = repo_root.resolve(strict=True)
    manifest = validate_cohort(repo_root, verify_toolchain=False)
    print(
        "generated-combat accepted integrity PASS "
        f"identity={manifest['generationIdentity']}"
    )
    return 0


def audit_scoped_raw_captures(
    repo_root: Path,
    capture_roots: Sequence[Path],
    *,
    require_promotable: bool = False,
) -> int:
    repo_root = repo_root.resolve(strict=True)
    if not capture_roots:
        raise PipelineError("--audit-scoped-raw-captures requires --capture-root")
    inventory = _governance_load_inventory(repo_root)
    report: dict[str, Any] = {
        "schemaVersion": 1,
        "pipeline": PIPELINE_NAME,
        "mode": "audit-scoped-raw-captures",
        "historicalRawDependency": "not evaluated",
        "captureRoots": [],
        "aggregate": {
            "state": GOVERNANCE_STATE_NEW_RAW_VERIFIED,
            "sourceCaptures": 0,
            "compatibleAggregatedCohorts": 0,
            "readyCohorts": 0,
            "blockedCohorts": 0,
            "basicReadyCohorts": 0,
            "basicBlockedCohorts": 0,
            "cohorts": [],
            "nextCaptureTargetCohorts": [],
        },
    }
    any_missing = False
    total_ready = 0
    total_blocked = 0
    total_basic_ready = 0
    total_basic_blocked = 0
    for capture_root in capture_roots:
        resolved_root = capture_root
        if not resolved_root.is_absolute():
            resolved_root = repo_root / resolved_root
        resolved_root = resolved_root.resolve(strict=True)
        capture_id = _governance_capture_id(resolved_root) or resolved_root.name
        missing_raw_files = _governance_missing_raw_files(resolved_root)
        if missing_raw_files:
            any_missing = True
        runtime_profiles, runtime_rows = _governance_capture_runtime_rows(
            inventory, capture_id
        )
        cohorts = [] if missing_raw_files else _governance_scoped_cohorts(resolved_root)
        ready_cohorts = [
            cohort
            for cohort in cohorts
            if cohort["combatCandidate"] and cohort["runtimeReady"]
        ]
        blocked_cohorts = [
            cohort
            for cohort in cohorts
            if cohort["combatCandidate"] and not cohort["runtimeReady"]
        ]
        basic_ready_cohorts = [
            cohort
            for cohort in cohorts
            if cohort["basicCombatCandidate"] and cohort["basicCombatReady"]
        ]
        basic_blocked_cohorts = [
            cohort
            for cohort in cohorts
            if cohort["basicCombatCandidate"] and not cohort["basicCombatReady"]
        ]
        missing_protocol_provable_cohorts = [
            cohort
            for cohort in blocked_cohorts
            if cohort["missingProtocolProvableFields"]
        ]
        not_protocol_proven_cohorts = [
            cohort
            for cohort in blocked_cohorts
            if cohort["notProtocolProvenFields"]
        ]
        combat_candidate_cohorts = [
            cohort for cohort in cohorts if cohort["combatCandidate"]
        ]
        ordinary_hostile_cohorts = [
            cohort
            for cohort in cohorts
            if cohort["category"] == GOVERNANCE_CATEGORY_ORDINARY_HOSTILE
        ]
        guard_combat_cohorts = [
            cohort
            for cohort in cohorts
            if cohort["category"] == GOVERNANCE_CATEGORY_GUARD_COMBAT
        ]
        noncombat_observed_cohorts = [
            cohort
            for cohort in cohorts
            if cohort["category"]
            in {
                GOVERNANCE_CATEGORY_SOCIAL_NONCOMBAT,
                GOVERNANCE_CATEGORY_VENDOR_NONCOMBAT,
            }
        ]
        ambiguous_cohorts = [
            cohort
            for cohort in cohorts
            if cohort["category"] == GOVERNANCE_CATEGORY_AMBIGUOUS
        ]
        if missing_raw_files:
            state = GOVERNANCE_STATE_BLOCKED_INSUFFICIENT_EVIDENCE
        elif runtime_rows > 0:
            state = GOVERNANCE_STATE_RAW_REVALIDATABLE
        elif blocked_cohorts:
            state = GOVERNANCE_STATE_BLOCKED_INSUFFICIENT_EVIDENCE
        else:
            state = GOVERNANCE_STATE_NEW_RAW_VERIFIED
        next_capture_targets = sorted(
            blocked_cohorts,
            key=_governance_next_capture_priority,
        )[:12]
        total_ready += len(ready_cohorts)
        total_blocked += len(blocked_cohorts)
        report["captureRoots"].append(
            {
                "captureId": capture_id,
                "path": str(resolved_root),
                "state": state,
                "missingRawFiles": missing_raw_files,
                "requiredRawFiles": list(GOVERNANCE_REQUIRED_RAW_FILES),
                "rawFiles": []
                if missing_raw_files
                else _governance_raw_file_descriptors(resolved_root),
                "runtimeReadyProfiles": runtime_profiles,
                "runtimeReadyRows": runtime_rows,
                "readyCohorts": len(ready_cohorts),
                "blockedCohorts": len(blocked_cohorts),
                "basicReadyCohorts": len(basic_ready_cohorts),
                "basicBlockedCohorts": len(basic_blocked_cohorts),
                "missingProtocolProvableCohorts": len(missing_protocol_provable_cohorts),
                "notProtocolProvenCohorts": len(not_protocol_proven_cohorts),
                "allCohorts": len(cohorts),
                "combatCandidateCohorts": len(combat_candidate_cohorts),
                "ordinaryHostileCohorts": len(ordinary_hostile_cohorts),
                "guardCombatCohorts": len(guard_combat_cohorts),
                "noncombatObservedCohorts": len(noncombat_observed_cohorts),
                "ambiguousCohorts": len(ambiguous_cohorts),
                "nextCaptureTargets": len(next_capture_targets),
                "cohorts": cohorts,
                "nextCaptureTargetCohorts": next_capture_targets,
            }
        )
        total_basic_ready += len(basic_ready_cohorts)
        total_basic_blocked += len(basic_blocked_cohorts)
    aggregate_cohorts = _governance_aggregate_scoped_cohorts(report["captureRoots"])
    aggregate_ready_cohorts = [
        cohort
        for cohort in aggregate_cohorts
        if cohort["combatCandidate"] and cohort["runtimeReady"]
    ]
    aggregate_blocked_cohorts = [
        cohort
        for cohort in aggregate_cohorts
        if cohort["combatCandidate"] and not cohort["runtimeReady"]
    ]
    aggregate_basic_ready_cohorts = [
        cohort
        for cohort in aggregate_cohorts
        if cohort["basicCombatCandidate"] and cohort["basicCombatReady"]
    ]
    aggregate_basic_blocked_cohorts = [
        cohort
        for cohort in aggregate_cohorts
        if cohort["basicCombatCandidate"] and not cohort["basicCombatReady"]
    ]
    aggregate_missing_protocol_provable_cohorts = [
        cohort
        for cohort in aggregate_blocked_cohorts
        if cohort["missingProtocolProvableFields"]
    ]
    aggregate_not_protocol_proven_cohorts = [
        cohort
        for cohort in aggregate_blocked_cohorts
        if cohort["notProtocolProvenFields"]
    ]
    aggregate_next_capture_targets = sorted(
        aggregate_blocked_cohorts,
        key=_governance_next_capture_priority,
    )[:12]
    aggregate_state = (
        GOVERNANCE_STATE_BLOCKED_INSUFFICIENT_EVIDENCE
        if aggregate_blocked_cohorts
        else GOVERNANCE_STATE_NEW_RAW_VERIFIED
    )
    report["aggregate"] = {
        "state": aggregate_state,
        "sourceCaptures": len(report["captureRoots"]),
        "compatibleAggregatedCohorts": len(aggregate_cohorts),
        "readyCohorts": len(aggregate_ready_cohorts),
        "blockedCohorts": len(aggregate_blocked_cohorts),
        "basicReadyCohorts": len(aggregate_basic_ready_cohorts),
        "basicBlockedCohorts": len(aggregate_basic_blocked_cohorts),
        "missingProtocolProvableCohorts": len(
            aggregate_missing_protocol_provable_cohorts
        ),
        "notProtocolProvenCohorts": len(aggregate_not_protocol_proven_cohorts),
        "cohorts": aggregate_cohorts,
        "nextCaptureTargetCohorts": aggregate_next_capture_targets,
    }
    payload = canonical_json_bytes(report)
    if payload != canonical_json_bytes(json.loads(payload.decode("utf-8"))):
        raise PipelineError("scoped raw capture audit is not deterministic")
    audit_sha256 = sha256_bytes(payload)
    for capture in report["captureRoots"]:
        print(
            "SCOPED_CAPTURE|"
            f"captureId={capture['captureId']}|"
            f"state={capture['state']}|"
            f"missingRawFiles={len(capture['missingRawFiles'])}|"
            f"runtimeReadyProfiles={capture['runtimeReadyProfiles']}|"
            f"runtimeReadyRows={capture['runtimeReadyRows']}|"
            f"readyCohorts={capture['readyCohorts']}|"
            f"blockedCohorts={capture['blockedCohorts']}|"
            f"basicReadyCohorts={capture['basicReadyCohorts']}|"
            f"basicBlockedCohorts={capture['basicBlockedCohorts']}|"
            f"missingProtocolProvableCohorts={capture['missingProtocolProvableCohorts']}|"
            f"notProtocolProvenCohorts={capture['notProtocolProvenCohorts']}|"
            f"allCohorts={capture['allCohorts']}|"
            f"combatCandidateCohorts={capture['combatCandidateCohorts']}|"
            f"ordinaryHostileCohorts={capture['ordinaryHostileCohorts']}|"
            f"guardCombatCohorts={capture['guardCombatCohorts']}|"
            f"noncombatObservedCohorts={capture['noncombatObservedCohorts']}|"
            f"ambiguousCohorts={capture['ambiguousCohorts']}|"
            f"nextCaptureTargets={capture['nextCaptureTargets']}"
        )
        for cohort in capture["cohorts"]:
            _governance_print_scoped_cohort(
                "SCOPED_COHORT",
                cohort,
                capture_id=str(capture["captureId"]),
            )
        for index, cohort in enumerate(capture["nextCaptureTargetCohorts"], start=1):
            print(
                "SCOPED_NEXT_CAPTURE|"
                f"captureId={capture['captureId']}|"
                f"priority={index}|"
                f"name={cohort['name']}|"
                f"level={cohort['level']}|"
                f"monsterData={cohort['monsterData']}|"
                f"objective={cohort['captureAction']}"
            )
    aggregate = report["aggregate"]
    print(
        "SCOPED_AGGREGATE|"
        f"state={aggregate['state']}|"
        f"sourceCaptures={aggregate['sourceCaptures']}|"
        f"compatibleAggregatedCohorts={aggregate['compatibleAggregatedCohorts']}|"
        f"readyCohorts={aggregate['readyCohorts']}|"
        f"blockedCohorts={aggregate['blockedCohorts']}|"
        f"basicReadyCohorts={aggregate['basicReadyCohorts']}|"
        f"basicBlockedCohorts={aggregate['basicBlockedCohorts']}|"
        f"missingProtocolProvableCohorts={aggregate['missingProtocolProvableCohorts']}|"
        f"notProtocolProvenCohorts={aggregate['notProtocolProvenCohorts']}|"
        f"nextCaptureTargets={len(aggregate['nextCaptureTargetCohorts'])}"
    )
    for cohort in aggregate["cohorts"]:
        _governance_print_scoped_cohort("SCOPED_AGGREGATE_COHORT", cohort)
    for index, cohort in enumerate(aggregate["nextCaptureTargetCohorts"], start=1):
        print(
            "SCOPED_AGGREGATE_NEXT_CAPTURE|"
            f"priority={index}|"
            f"sourceCaptureIds={','.join(cohort['sourceCaptureIds'])}|"
            f"name={cohort['name']}|"
            f"level={cohort['level']}|"
            f"monsterData={cohort['monsterData']}|"
            f"objective={cohort['captureAction']}"
        )
    if any_missing:
        raise PipelineError("scoped raw capture audit failed: missing validator-grade raw files")
    if require_promotable:
        blocked = [
            str(capture["captureId"])
            for capture in report["captureRoots"]
            if capture["blockedCohorts"] or capture["ambiguousCohorts"]
        ]
        if blocked:
            raise PipelineError(
                "scoped raw capture promotion blocked: " + ", ".join(blocked)
            )
    print(
        "generated-combat scoped raw audit PASS "
        f"captures={len(report['captureRoots'])} "
        f"historicalRawDependency={report['historicalRawDependency']} "
        f"readyCohorts={total_ready} "
        f"blockedCohorts={total_blocked} "
        f"basicReadyCohorts={total_basic_ready} "
        f"basicBlockedCohorts={total_basic_blocked} "
        f"auditSha256={audit_sha256}"
    )
    return 0


def audit_combat_capture_readiness(repo_root: Path) -> int:
    del repo_root
    ready_fields = [
        row
        for row in GOVERNANCE_CAPTURE_READINESS
        if row["captureStatus"] in GOVERNANCE_CAPTURE_READY_STATUSES
        and row["analyzerStatus"] in GOVERNANCE_ANALYZER_READY_STATUSES
        and row["runtimeStatus"] != "NOT_PROTOCOL_PROVEN"
    ]
    not_protocol_proven = [
        row
        for row in GOVERNANCE_CAPTURE_READINESS
        if row["runtimeStatus"] == "NOT_PROTOCOL_PROVEN"
        or row["captureStatus"] == "NOT_PROTOCOL_PROVEN"
        or row["analyzerStatus"] == "NOT_PROTOCOL_PROVEN"
    ]
    pipeline_ready = len(not_protocol_proven) == 0
    analyzer_ready = all(
        row["analyzerStatus"] in GOVERNANCE_ANALYZER_READY_STATUSES
        for row in GOVERNANCE_CAPTURE_READINESS
    )
    print(
        "COMBAT_CAPTURE_READINESS|"
        + f"pipelineReady={'true' if pipeline_ready else 'false'}|"
        + f"analyzerReady={'true' if analyzer_ready else 'false'}|"
        + f"requiredFieldsCaptureProvable={len(ready_fields)}|"
        + f"requiredFieldsNotProtocolProven={len(not_protocol_proven)}"
    )
    for row in GOVERNANCE_CAPTURE_READINESS:
        print(
            "COMBAT_CAPTURE_FIELD|"
            + f"field={row['field']}|"
            + f"runtimeStatus={row['runtimeStatus']}|"
            + f"captureStatus={row['captureStatus']}|"
            + f"analyzerStatus={row['analyzerStatus']}|"
            + f"historicalProvenance={row['historicalProvenance']}|"
            + f"evidencePath={row['evidencePath']}"
        )
    return 0


def self_test_governance() -> int:
    sentinel_row = {
        "minDamage": GOVERNANCE_SENTINEL_TEXT,
        "maxDamage": GOVERNANCE_SENTINEL_TEXT,
        "defaultAttackType": GOVERNANCE_SENTINEL_TEXT,
        "attackDelay": GOVERNANCE_SENTINEL_TEXT,
        "rechargeDelay": GOVERNANCE_SENTINEL_TEXT,
        "catMesh": GOVERNANCE_SENTINEL_TEXT,
    }
    expected_fields = sorted(GOVERNANCE_SENTINEL_FIELDS)
    if _governance_sentinel_fields(sentinel_row) != expected_fields:
        raise PipelineError("governance sentinel rejection self-test failed")
    if _governance_sentinel_fields({"minDamage": 1, "maxDamage": 2}):
        raise PipelineError("governance sentinel false-positive self-test failed")
    vendor = _governance_new_cohort("Vendor", "40", "250380")
    vendor["vendorEvidence"] = 1
    if _governance_classify_cohort(vendor) != GOVERNANCE_CATEGORY_VENDOR_NONCOMBAT:
        raise PipelineError("governance vendor exclusion self-test failed")
    if _governance_identity_hex("(SimpleChar:11CE48)") != "0011CE48":
        raise PipelineError("governance short SimpleChar identity self-test failed")
    if _governance_identity_hex("0xF574E") != "000F574E":
        raise PipelineError("governance short hex identity self-test failed")
    attack_info_detail = {
        "Detail": (
            "AttackInfoMessage { Amount=2 AmmoCount=-1 WeaponSlot=1 "
            "Unk1=4 HitType=Normal WeaponInstance=0 }"
        )
    }
    if _governance_row_or_detail_value(attack_info_detail, ("WeaponSlot",)) != "1":
        raise PipelineError("governance AttackInfo WeaponSlot projection self-test failed")
    if _governance_row_or_detail_value(attack_info_detail, ("HitType",)) != "Normal":
        raise PipelineError("governance AttackInfo HitType projection self-test failed")
    if _governance_row_or_detail_value(attack_info_detail, ("DamageType", "DamageTypeWire")) is not None:
        raise PipelineError("governance AttackInfo Unk damageType rejection self-test failed")
    decoded_attack_info = _governance_decode_raw_attack_info(
        {
            "N3TypeName": "AttackInfo",
            "RawHex": (
                "11AF000A0001003D0011CE480000725446002F160000C350"
                "0011CE480000000004FFFFFFFF000000010000C35000007254"
                "000000000000000300000000"
            ),
        }
    )
    if decoded_attack_info is None:
        raise PipelineError("governance raw AttackInfo decode self-test failed")
    if (
        decoded_attack_info["sourceIdentity"] != "0011CE48"
        or decoded_attack_info["targetIdentity"] != "00007254"
        or decoded_attack_info["amount"] != 4
        or decoded_attack_info["weaponSlot"] != 1
        or decoded_attack_info["damageTypeWire"] != 0
        or decoded_attack_info["hitTypeWire"] != 3
    ):
        raise PipelineError("governance raw AttackInfo field mapping self-test failed")
    combat = _governance_new_cohort("Combat", "4", "17655")
    combat["identities"].add("79F40001")
    combat["directCombatRows"] = 1
    if _governance_classify_cohort(combat) not in GOVERNANCE_COMBAT_CANDIDATE_CATEGORIES:
        raise PipelineError("governance combat inclusion self-test failed")
    combat["followChaseRows"] = 1
    if _governance_classify_cohort(combat) != GOVERNANCE_CATEGORY_ORDINARY_HOSTILE:
        raise PipelineError("governance ordinary combat classification self-test failed")
    ambiguous = _governance_new_cohort("Ambiguous", "4", "17655")
    ambiguous["directCombatRows"] = 1
    ambiguous["ambiguousReasons"].add("identity maps to multiple cohorts")
    if _governance_classify_cohort(ambiguous) != GOVERNANCE_CATEGORY_AMBIGUOUS:
        raise PipelineError("governance ambiguous classification self-test failed")
    combat["sentinelFields"].update(expected_fields)
    statuses = _governance_contract_field_statuses(combat)
    if statuses["minDamage"] != "SENTINEL" or statuses["attackDelay"] != "SENTINEL":
        raise PipelineError("governance sentinel contract-field self-test failed")
    reet_a = _governance_new_cohort("Island Reet", "1", "30365")
    reet_a["identities"].add("0011CE48")
    reet_a["directCombatRows"] = 13
    reet_a["damageEvents"] = 6
    reet_a["attackHits"] = 7
    reet_a["followChaseRows"] = 4577
    reet_a["maxHealthObserved"] = 12
    reet_a["damageTypes"].add("0")
    reet_a["hitTypes"].add("Normal")
    reet_a["attackSlots"].add("1")
    reet_a["sentinelFields"].add("defaultAttackType")
    reet_a["_rawOrdinaryAttackInfoEvents"].extend(
        [
            {
                "captureId": "20260819-014109",
                "capturedUtc": "2026-08-19T06:41:40.5508403Z",
                "sourceIdentity": "0011CE48",
                "targetIdentity": "00007254",
                "sequence": 2022,
                "elapsedMilliseconds": 30581.525,
                "amount": 4,
                "damageTypeWire": 0,
                "attackInfoAmmoCount": -1,
                "attackInfoWeaponSlot": 1,
                "attackInfoHitTypeWire": GOVERNANCE_NORMAL_HIT_TYPE_WIRE,
                "attackInfoWeaponInstance": 0,
                "attackInfoN3Unknown": 0,
            },
            {
                "captureId": "20260819-014109",
                "capturedUtc": "2026-08-19T06:41:51.7488630Z",
                "sourceIdentity": "0011CE48",
                "targetIdentity": "00007254",
                "sequence": 2830,
                "elapsedMilliseconds": 41779.452,
                "amount": 8,
                "damageTypeWire": 0,
                "attackInfoAmmoCount": -1,
                "attackInfoWeaponSlot": 1,
                "attackInfoHitTypeWire": GOVERNANCE_NORMAL_HIT_TYPE_WIRE,
                "attackInfoWeaponInstance": 0,
                "attackInfoN3Unknown": 0,
            },
        ]
    )
    reet_statuses = _governance_contract_field_statuses(reet_a)
    reet_final_resolutions = _governance_final_field_resolutions(
        reet_a,
        reet_statuses,
    )
    reet_unresolved = _governance_unresolved_required_fields(reet_statuses)
    if reet_statuses["hitType"] != "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK":
        raise PipelineError("governance HitType projection contract self-test failed")
    if reet_statuses["attackSlot"] != "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK":
        raise PipelineError("governance attackSlot projection contract self-test failed")
    if reet_statuses["defaultAttackType"] != "DERIVABLE_BY_EXISTING_GOVERNED_RULE":
        raise PipelineError("governance normal-hit default attack self-test failed")
    if reet_statuses["damageType"] != "PROVEN_FROM_DERIVED_CAPTURE_WITH_RAW_LINK":
        raise PipelineError("governance damageType raw projection self-test failed")
    if "damageType" in reet_unresolved:
        raise PipelineError("governance damageType must resolve from raw AttackInfo")
    if (
        reet_final_resolutions["damageType"]["status"]
        != GOVERNANCE_FINAL_STATUS_PROVEN
    ):
        raise PipelineError("governance final damageType status self-test failed")
    if (
        reet_final_resolutions["minDamage"]["status"]
        != GOVERNANCE_FINAL_STATUS_NOT_REQUIRED
        or reet_final_resolutions["maxDamage"]["status"]
        != GOVERNANCE_FINAL_STATUS_NOT_REQUIRED
    ):
        raise PipelineError("governance final observed-damage status self-test failed")
    if (
        reet_final_resolutions["attackDelay"]["status"]
        != GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN
        or reet_final_resolutions["attackRange"]["status"]
        != GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN
        or reet_final_resolutions["naturalOrWeaponMode"]["status"]
        != GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN
        or reet_final_resolutions["rechargeDelay"]["status"]
        != GOVERNANCE_FINAL_STATUS_NOT_PROTOCOL_PROVEN
    ):
        raise PipelineError("governance final non-protocol status self-test failed")
    finalized_reet_a = _governance_finalize_cohort(reet_a)
    if not finalized_reet_a["basicCombatReady"]:
        raise PipelineError("governance basic Reet combat readiness self-test failed")
    if finalized_reet_a["observedOrdinaryCadenceIntervalCount"] != 1:
        raise PipelineError("governance observed cadence self-test failed")
    reet_runtime_classes = finalized_reet_a[
        "fieldRuntimeExecutionClassifications"
    ]
    if (
        reet_runtime_classes["attackDelay"]
        != GOVERNANCE_FIELD_LEGACY_GENERATOR_REQUIRED_ONLY
        or reet_runtime_classes["attackRange"]
        != GOVERNANCE_FIELD_RUNTIME_EXECUTION_REQUIRED
        or reet_runtime_classes["naturalOrWeaponMode"]
        != GOVERNANCE_FIELD_NOT_ACTUALLY_USED
        or reet_runtime_classes["rechargeDelay"]
        != GOVERNANCE_FIELD_DERIVABLE_FROM_OTHER_PROVEN_RUNTIME_STATE
    ):
        raise PipelineError("governance Reet blocker classification self-test failed")
    reet_fix_guidance = finalized_reet_a["fieldFixGuidanceCategories"]
    if (
        reet_fix_guidance["attackDelay"] != GOVERNANCE_FIX_GUIDANCE_RESOLVED
        or reet_fix_guidance["attackRange"]
        != GOVERNANCE_FIX_GUIDANCE_RUNTIME_POLICY
        or reet_fix_guidance["naturalOrWeaponMode"]
        != GOVERNANCE_FIX_GUIDANCE_RESOLVED
        or reet_fix_guidance["rechargeDelay"] != GOVERNANCE_FIX_GUIDANCE_RESOLVED
    ):
        raise PipelineError("governance Reet blocker guidance self-test failed")
    if "catMesh" in reet_unresolved or "nanoOrSpecialBehavior" in reet_unresolved:
        raise PipelineError("governance optional/spawn fields must not block basic combat")
    reet_b = _governance_new_cohort("Island Reet", "1", "30365")
    reet_b["identities"].add("0011CE49")
    reet_b["directCombatRows"] = 15
    reet_b["damageEvents"] = 6
    reet_b["attackHits"] = 8
    reet_b["followChaseRows"] = 3892
    reet_b["maxHealthObserved"] = 12
    reet_b["damageTypes"].add("0")
    reet_b["hitTypes"].add("Normal")
    reet_b["attackSlots"].add("0")
    aggregate = _governance_aggregate_scoped_cohorts(
        (
            {
                "captureId": "20260819-014109",
                "path": "capture-a",
                "cohorts": [_governance_finalize_cohort(reet_a)],
            },
            {
                "captureId": "20260819-015104",
                "path": "capture-b",
                "cohorts": [_governance_finalize_cohort(reet_b)],
            },
        )
    )
    if len(aggregate) != 1 or aggregate[0]["directCombatRows"] != 28:
        raise PipelineError("governance cross-capture aggregation self-test failed")
    if not aggregate[0]["basicCombatReady"]:
        raise PipelineError("governance aggregate basic readiness self-test failed")
    incompatible_aggregate = _governance_aggregate_scoped_cohorts(
        (
            {
                "captureId": "20260819-014109",
                "path": "capture-a",
                "cohorts": [_governance_finalize_cohort(reet_a)],
            },
            {
                "captureId": "incompatible",
                "path": "capture-c",
                "cohorts": [
                    _governance_finalize_cohort(
                        _governance_new_cohort("Island Reet", "2", "30365")
                    )
                ],
            },
        )
    )
    if incompatible_aggregate:
        raise PipelineError("governance strict aggregation compatibility self-test failed")
    missing_dossier = _governance_new_cohort("unknown-0x79F40002", "unknown", "unknown")
    missing_dossier["identities"].add("79F40002")
    missing_dossier["directCombatRows"] = 1
    if (
        _governance_classify_cohort(missing_dossier)
        not in GOVERNANCE_COMBAT_CANDIDATE_CATEGORIES
    ):
        raise PipelineError("governance missing-dossier combat retention self-test failed")
    if set(GOVERNANCE_RUNTIME_FIELD_CLASSES) != set(GOVERNANCE_RUNTIME_REQUIRED_FIELDS):
        raise PipelineError("governance field classification coverage self-test failed")
    if set(GOVERNANCE_FIELD_RUNTIME_EXECUTION_CLASSIFICATIONS) != set(
        GOVERNANCE_RUNTIME_REQUIRED_FIELDS
    ):
        raise PipelineError("governance runtime execution classification coverage self-test failed")
    readiness_fields = {row["field"] for row in GOVERNANCE_CAPTURE_READINESS}
    required_readiness_fields = set(GOVERNANCE_RUNTIME_REQUIRED_FIELDS) - {
        "actorIdentity",
        "monsterData",
        "level",
        "maxHealth",
        "archetypeLinkage",
    }
    if readiness_fields != required_readiness_fields:
        raise PipelineError("governance self-test failed: readiness field coverage drifted")
    if any(
        row["field"] == "factionAlignment"
        and row["runtimeStatus"] != "NOT_PROTOCOL_PROVEN"
        for row in GOVERNANCE_CAPTURE_READINESS
    ):
        raise PipelineError("governance self-test failed: faction readiness must fail closed")
    if any(
        row["field"] == "attackRange"
        and row["historicalProvenance"] == "SPATIAL_DERIVATION"
        for row in GOVERNANCE_CAPTURE_READINESS
    ):
        raise PipelineError("governance self-test failed: attackRange cannot use spatial derivation")
    payload = {
        "states": [
            GOVERNANCE_STATE_LEGACY_ACCEPTED_RAW_UNAVAILABLE,
            GOVERNANCE_STATE_RAW_REVALIDATABLE,
            GOVERNANCE_STATE_NEW_RAW_VERIFIED,
            GOVERNANCE_STATE_BLOCKED_INSUFFICIENT_EVIDENCE,
        ],
        "sentinelFields": expected_fields,
        "requiredRawFiles": list(GOVERNANCE_REQUIRED_RAW_FILES),
    }
    serialized = canonical_json_bytes(payload)
    if serialized != canonical_json_bytes(json.loads(serialized.decode("utf-8"))):
        raise PipelineError("governance deterministic serialization self-test failed")
    print(
        "generated-combat governance self-test PASS "
        "states=4 sentinelRejected=true scopedDeterministic=true "
        "classificationSelfTests=true"
    )
    return 0


def _run_supervised_command(
    command: Sequence[str],
    repo_root: Path,
    lease: Any,
    *,
    timeout_seconds: int = CHILD_PROCESS_TIMEOUT_SECONDS,
) -> int:
    if not command:
        raise PipelineError("--run-read-lease requires a command after --")
    actual = list(command)
    if actual[0] == "--":
        actual = actual[1:]
    if not actual:
        raise PipelineError("--run-read-lease requires a command after --")
    suffix = Path(actual[0]).suffix.casefold()
    if os.name == "nt" and suffix in {".cmd", ".bat"}:
        command_line = subprocess.list2cmdline(actual)
        actual = [os.environ.get("COMSPEC", "cmd.exe"), "/d", "/s", "/c", command_line]
    environment = os.environ.copy()
    environment[LEASE_DELEGATION_ENVIRONMENT] = json.dumps(
        lease.delegation(), sort_keys=True, separators=(",", ":")
    )
    environment[LEASE_REPO_ROOT_ENVIRONMENT] = str(lease.repo_root)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    process = subprocess.Popen(
        actual,
        cwd=repo_root,
        env=environment,
        creationflags=(
            subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
        ),
        start_new_session=os.name != "nt",
    )
    try:
        return process.wait(timeout=timeout_seconds)
    except subprocess.TimeoutExpired as error:
        _terminate_process_tree(process)
        raise PipelineError(
            "generated-combat read-lease command timed out "
            f"after {timeout_seconds}s pid={process.pid}"
        ) from error


def _run_supervised_delegated_cohort_validation(
    repo_root: Path, lease: Any, *, label: str
) -> str:
    completed = run_checked(
        (
            sys.executable,
            "-B",
            "-u",
            "-X",
            "faulthandler",
            str(Path(__file__).resolve()),
            "--_validate-cohort-read-delegation",
            "--repo-root",
            str(repo_root),
        ),
        repo_root=repo_root,
        lease=lease,
        label=label,
        retry_interpreter_failures=True,
    )
    marker = "generated-combat delegated cohort PASS identity="
    for line in completed.stdout.splitlines():
        if line.startswith(marker):
            identity = line[len(marker) :].strip()
            if re.fullmatch(r"[0-9a-f]{64}", identity):
                return identity
    detail = _bounded_process_detail(completed)
    suffix = f": {detail}" if detail else ""
    raise PipelineError(
        f"generated-combat {label} did not report a valid cohort identity{suffix}"
    )


def refresh_accepted_coverage(repo_root: Path) -> int:
    repo_root = repo_root.resolve(strict=True)
    with _shared_lease(repo_root, "write") as lease:
        published = load_json_object(
            repo_root / MANIFEST_RELATIVE_PATH,
            "published generated-combat manifest",
        )
        candidate_root = lease.new_staging_directory("accepted-coverage-refresh")
        artifacts = _candidate_artifact_paths(candidate_root)
        for role, logical_path in ARTIFACT_RELATIVE_PATHS.items():
            source = repo_root / Path(logical_path)
            if role == "activeCoverage":
                continue
            shutil.copyfile(source, artifacts[role])

        inventory_path = repo_root / Path(ARTIFACT_RELATIVE_PATHS["inventory"])
        formula_path = repo_root / Path(ARTIFACT_RELATIVE_PATHS["formulaDataset"])
        run_checked(
            (
                sys.executable,
                "-B",
                "-I",
                "-u",
                "-X",
                "faulthandler",
                str(repo_root / ACTIVE_GENERATOR),
                "--write",
                "--repo-root",
                str(repo_root),
                "--combat-inventory",
                str(inventory_path),
                "--combat-inventory-descriptor",
                str(inventory_path),
                "--combat-inventory-sha256",
                sha256_file(inventory_path),
                "--combat-inventory-byte-length",
                str(inventory_path.stat().st_size),
                "--formula-dataset",
                str(formula_path),
                "--output",
                str(artifacts["activeCoverage"]),
            ),
            repo_root=repo_root,
            lease=lease,
            label="accepted coverage refresh",
        )

        generators = generator_descriptors(repo_root)
        runtime = runtime_descriptor()
        manifest, rendered = build_generation_manifest(
            cohort_root=candidate_root,
            artifacts=artifacts,
            input_snapshot=published["inputSnapshot"],
            auxiliary_input_identity=published["inputSnapshot"][
                "auxiliarySnapshotIdentity"
            ],
            generators=generators,
            runtime=runtime,
            input_snapshot_is_portable=True,
        )
        manifest_path = candidate_root / MANIFEST_RELATIVE_PATH
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_bytes(rendered)
        candidate = CandidateCohort(
            root=candidate_root,
            artifacts=artifacts,
            manifest_path=manifest_path,
            capture_snapshot={},
            generation_identity=manifest["generationIdentity"],
            input_snapshot_identity=manifest["inputSnapshot"]["identity"],
            fixed_point_rounds=0,
        )
        validate_cohort(candidate_root, verify_toolchain=False)

        def revalidate_refresh(_phase: str) -> None:
            for role, logical_path in ARTIFACT_RELATIVE_PATHS.items():
                if role == "activeCoverage":
                    continue
                current = repo_root / Path(logical_path)
                if sha256_file(current) != sha256_file(artifacts[role]):
                    raise PipelineError(
                        "accepted coverage refresh input changed during publication: "
                        f"{logical_path.as_posix()}"
                    )
            if generator_descriptors(repo_root) != generators:
                raise PipelineError(
                    "generator descriptors changed during accepted coverage refresh"
                )
            if runtime_descriptor() != runtime:
                raise PipelineError(
                    "Python runtime changed during accepted coverage refresh"
                )

        _shared_publish(lease, candidate, revalidate_refresh)
        published = validate_cohort(repo_root, verify_toolchain=True)
        print(
            "ACCEPTED_COVERAGE_REGEN=PASS "
            f"identity={published['generationIdentity']} historicalRawDependency=NO"
        )
    return 0


def run_pipeline(
    *,
    repo_root: Path,
    mode: str,
    max_rounds: int,
    command: Sequence[str] = (),
    read_lease_command_timeout_seconds: int = CHILD_PROCESS_TIMEOUT_SECONDS,
    require_promotable_captures: bool = False,
) -> int:
    repo_root = repo_root.resolve(strict=True)
    if mode == "validate-read-delegation":
        _validate_delegated_lease(repo_root, "read")
        return 0
    if mode == "validate-cohort-read-delegation":
        _validate_delegated_lease(repo_root, "read")
        manifest = validate_cohort(repo_root, verify_toolchain=False)
        print(
            "generated-combat delegated cohort PASS "
            f"identity={manifest['generationIdentity']}"
        )
        return 0
    if mode == "validate":
        with _shared_lease(repo_root, "read") as lease:
            manifest = validate_cohort(repo_root, verify_toolchain=True)
            try:
                inputs = capture_auxiliary_inputs(
                    lease, repo_root, require_capture_evidence=True
                )
                revalidate_auxiliary_inputs(
                    inputs, repo_root, require_capture_evidence=True
                )
            except PipelineError as error:
                if "Required capture evidence is unavailable" not in str(error):
                    raise
                print(
                    "EVIDENCE_NOT_LOCALLY_AVAILABLE "
                    f"acceptedState=VALID identity={manifest['generationIdentity']} "
                    f"detail={error}"
                )
                return 0
        print(
            "PROVENANCE_EVIDENCE=AVAILABLE "
            f"acceptedState=VALID identity={manifest['generationIdentity']}"
        )
        return 0
    if mode == "validate-legacy-baseline":
        return validate_legacy_governance_baseline(repo_root)
    if mode == "audit-scoped-raw-captures":
        return audit_scoped_raw_captures(
            repo_root,
            command,
            require_promotable=require_promotable_captures,
        )
    if mode == "audit-combat-capture-readiness":
        return audit_combat_capture_readiness(repo_root)
    if mode == "self-test-governance":
        return self_test_governance()
    if mode == "run-read-lease":
        with _shared_lease(repo_root, "read") as lease:
            identity = _run_supervised_delegated_cohort_validation(
                repo_root, lease, label="pre-command cohort validation"
            )
            exit_code = _run_supervised_command(
                command,
                repo_root,
                lease,
                timeout_seconds=read_lease_command_timeout_seconds,
            )
            after_identity = _run_supervised_delegated_cohort_validation(
                repo_root, lease, label="post-command cohort validation"
            )
            if after_identity != identity:
                raise CohortValidationError(
                    "published cohort changed during read-lease command"
                )
            return exit_code

    if mode == "check":
        return validate_accepted_cohort(repo_root)
    if mode == "refresh-accepted-coverage":
        mode = "write"

    if mode == "write":
        with _shared_lease(repo_root, "write") as lease:
            accepted_manifest = validate_cohort(repo_root, verify_toolchain=False)
            accepted_inventory_path = repo_root / Path(
                ARTIFACT_RELATIVE_PATHS["inventory"]
            )
            accepted_inventory_sha256 = sha256_file(accepted_inventory_path)
            accepted_input_snapshot = dict(accepted_manifest["inputSnapshot"])
            inputs = capture_auxiliary_inputs(
                lease, repo_root, require_capture_evidence=False
            )
            scfu_analyzer_inputs = capture_scfu_analyzer_runtime(lease, repo_root)
            candidate_root = lease.new_staging_directory("accepted-combat-candidate")
            candidate = build_accepted_candidate_cohort(
                repo_root,
                candidate_root,
                accepted_manifest=accepted_manifest,
                auxiliary_snapshot=inputs,
                scfu_analyzer_snapshot=scfu_analyzer_inputs,
                lease=lease,
                max_rounds=max_rounds,
            )

            def revalidate_accepted_inputs(_phase: str) -> None:
                revalidate_auxiliary_inputs(
                    inputs, repo_root, require_capture_evidence=False
                )
                revalidate_scfu_analyzer_runtime(scfu_analyzer_inputs, repo_root)
                if sha256_file(accepted_inventory_path) != accepted_inventory_sha256:
                    raise PipelineError(
                        "accepted inventory changed during canonical regeneration"
                    )
                current_manifest = load_json_object(
                    repo_root / MANIFEST_RELATIVE_PATH,
                    "current generated-combat manifest",
                )
                if current_manifest["inputSnapshot"] != accepted_input_snapshot:
                    raise PipelineError(
                        "accepted provenance changed during canonical regeneration"
                    )

            _shared_publish(lease, candidate, revalidate_accepted_inputs)
            published = validate_cohort(repo_root, verify_toolchain=True)
            if published["generationIdentity"] != candidate.generation_identity:
                raise PipelineError("published generation identity changed during commit")
            print(
                "generated-combat write PASS "
                f"identity={candidate.generation_identity} "
                f"input={candidate.input_snapshot_identity} "
                f"fixedPointRounds={candidate.fixed_point_rounds} "
                "historicalRawDependency=NO"
            )
        return 0

    with _shared_lease(repo_root, "write") as lease:
        inputs = capture_auxiliary_inputs(
            lease, repo_root, require_capture_evidence=True
        )
        scfu_analyzer_inputs = capture_scfu_analyzer_runtime(lease, repo_root)
        candidate_root = lease.new_staging_directory("combat-candidate")
        candidate = build_candidate_cohort(
            repo_root,
            candidate_root,
            auxiliary_snapshot=inputs,
            scfu_analyzer_snapshot=scfu_analyzer_inputs,
            lease=lease,
            max_rounds=max_rounds,
        )
        revalidate_candidate_inputs(
            inputs,
            candidate,
            repo_root,
            lease,
            require_capture_evidence=True,
        )
        revalidate_scfu_analyzer_runtime(scfu_analyzer_inputs, repo_root)
        def revalidate_publication_inputs(phase: str) -> None:
            revalidate_candidate_inputs(
                inputs,
                candidate,
                repo_root,
                lease,
                require_capture_evidence=True,
            )
            revalidate_scfu_analyzer_runtime(scfu_analyzer_inputs, repo_root)

        _shared_publish(
            lease,
            candidate,
            revalidate_publication_inputs,
        )
        published = validate_cohort(repo_root, verify_toolchain=True)
        if published["generationIdentity"] != candidate.generation_identity:
            raise PipelineError("published generation identity changed during commit")
        print(
            f"generated-combat {mode} PASS "
            f"identity={candidate.generation_identity} "
            f"input={candidate.input_snapshot_identity} "
            f"fixedPointRounds={candidate.fixed_point_rounds}"
        )
    return 0


def parse_arguments(argv: Sequence[str] | None = None) -> argparse.Namespace:
    argv_list = list(sys.argv[1:] if argv is None else argv)
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--promote-raw-evidence", action="store_true")
    mode.add_argument("--refresh-accepted-coverage", action="store_true")
    mode.add_argument("--validate-current", action="store_true")
    mode.add_argument("--validate-legacy-baseline", action="store_true")
    mode.add_argument("--audit-scoped-raw-captures", action="store_true")
    mode.add_argument("--audit-combat-capture-readiness", action="store_true")
    mode.add_argument("--self-test-governance", action="store_true")
    mode.add_argument("--run-read-lease", action="store_true")
    mode.add_argument(
        "--_validate-read-delegation", action="store_true", help=argparse.SUPPRESS
    )
    mode.add_argument(
        "--_validate-cohort-read-delegation",
        action="store_true",
        help=argparse.SUPPRESS,
    )
    parser.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    parser.add_argument(
        "--max-fixed-point-rounds",
        type=int,
        default=DEFAULT_MAX_FIXED_POINT_ROUNDS,
    )
    parser.add_argument(
        "--read-lease-command-timeout-seconds",
        type=int,
        default=CHILD_PROCESS_TIMEOUT_SECONDS,
    )
    parser.add_argument(
        "--capture-root",
        type=Path,
        action="append",
        default=[],
        help="Explicit capture root for --audit-scoped-raw-captures.",
    )
    parser.add_argument(
        "--require-promotable-captures",
        action="store_true",
        help=(
            "With --audit-scoped-raw-captures, fail if selected evidence is "
            "blocked by missing raw files or sentinel combat fields."
        ),
    )
    if "--run-read-lease" in argv_list:
        parser.add_argument("command", nargs=argparse.REMAINDER)
    else:
        parser.set_defaults(command=[])
    return parser.parse_args(argv_list)


def main(argv: Sequence[str] | None = None) -> int:
    arguments = parse_arguments(argv)
    if arguments.check:
        mode = "check"
    elif arguments.refresh_accepted_coverage:
        mode = "refresh-accepted-coverage"
    elif arguments.write:
        mode = "write"
    elif arguments.promote_raw_evidence:
        mode = "promote-raw-evidence"
    elif arguments.validate_current:
        mode = "validate"
    elif arguments.validate_legacy_baseline:
        mode = "validate-legacy-baseline"
    elif arguments.audit_scoped_raw_captures:
        mode = "audit-scoped-raw-captures"
    elif arguments.audit_combat_capture_readiness:
        mode = "audit-combat-capture-readiness"
    elif arguments.self_test_governance:
        mode = "self-test-governance"
    elif arguments.run_read_lease:
        mode = "run-read-lease"
    elif arguments._validate_cohort_read_delegation:
        mode = "validate-cohort-read-delegation"
    else:
        mode = "validate-read-delegation"
    if mode != "run-read-lease" and arguments.command:
        raise PipelineError("a trailing command is valid only with --run-read-lease")
    if mode == "audit-scoped-raw-captures":
        command = arguments.capture_root
    else:
        command = arguments.command
    if mode != "audit-scoped-raw-captures" and arguments.capture_root:
        raise PipelineError("--capture-root is valid only with --audit-scoped-raw-captures")
    if mode != "audit-scoped-raw-captures" and arguments.require_promotable_captures:
        raise PipelineError(
            "--require-promotable-captures is valid only with --audit-scoped-raw-captures"
        )
    if not (
        1
        <= arguments.read_lease_command_timeout_seconds
        <= MAX_READ_LEASE_COMMAND_TIMEOUT_SECONDS
    ):
        raise PipelineError(
            "read-lease command timeout must be between 1 and "
            f"{MAX_READ_LEASE_COMMAND_TIMEOUT_SECONDS} seconds"
        )
    if (
        mode != "run-read-lease"
        and arguments.read_lease_command_timeout_seconds
        != CHILD_PROCESS_TIMEOUT_SECONDS
    ):
        raise PipelineError(
            "a custom read-lease command timeout is valid only with --run-read-lease"
        )
    return run_pipeline(
        repo_root=arguments.repo_root,
        mode=mode,
        max_rounds=arguments.max_fixed_point_rounds,
        command=command,
        read_lease_command_timeout_seconds=(
            arguments.read_lease_command_timeout_seconds
        ),
        require_promotable_captures=arguments.require_promotable_captures,
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (CohortValidationError, FixedPointError, PipelineError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
