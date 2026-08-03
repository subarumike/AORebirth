#!/usr/bin/env python3
"""Coordinate the capture-backed NPC combat generated-artifact cohort.

The coordinator never asks the existing generators to write a production path.
It builds a complete candidate cohort under one isolated root, proves the
active-coverage/formula cycle has reached a fixed point, and delegates the
lease and publication transaction to ``generated_artifact_transaction``.
"""

from __future__ import annotations

import argparse
import contextlib
import dataclasses
import hashlib
import importlib
import json
import os
import platform
import re
import signal
import subprocess
import sys
import tempfile
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Iterator, Mapping, Sequence


PIPELINE_NAME = "capture-backed-npc-combat"
MANIFEST_SCHEMA_VERSION = 1
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
ITEM_DATABASE = Path("AORebirth/Datafiles/items.dat")
SCFU_ANALYZER = Path(
    "tools-temp/AOSharpCaptureAnalyzer/bin/Debug/AOSharpCaptureAnalyzer.exe"
)
FORMULA_STATIC_INPUTS = (
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
}
MANIFEST_RELATIVE_PATH = PurePosixPath(
    "docs/generated/capture_backed_npc_combat_generation_manifest.json"
)
JSON_ARTIFACT_ROLES = frozenset(
    ("inventory", "activeCoverage", "formulaDataset")
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
    "scfuAnalyzer": PurePosixPath(SCFU_ANALYZER.as_posix()),
}

LEASE_DELEGATION_ENVIRONMENT = "AO_REBIRTH_GENERATED_COMBAT_LEASE_DELEGATION"
LEASE_REPO_ROOT_ENVIRONMENT = "AO_REBIRTH_GENERATED_COMBAT_LEASE_REPO_ROOT"
PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT = (
    "AO_REBIRTH_GENERATED_COMBAT_PRIMARY_CAPTURE_REPO_ROOT"
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
    decoder = object.__new__(json.JSONDecoder)
    decoder.object_hook = None
    decoder.parse_float = float
    decoder.parse_int = int
    decoder.parse_constant = json.decoder._CONSTANTS.__getitem__
    decoder.strict = True
    decoder.object_pairs_hook = None
    decoder.parse_object = json.decoder.JSONObject
    decoder.parse_array = json.decoder.JSONArray
    decoder.parse_string = json.decoder.py_scanstring
    decoder.memo = {}
    decoder.scan_once = json.scanner.py_make_scanner(decoder)
    start = 0
    while start < len(raw) and raw[start] in " \t\r\n":
        start += 1
    value, end = decoder.raw_decode(raw, start)
    while end < len(raw) and raw[end] in " \t\r\n":
        end += 1
    if end != len(raw):
        raise json.JSONDecodeError("Extra data", raw, end)
    return value


def _is_transient_json_decoder_failure(error: BaseException) -> bool:
    if isinstance(error, json.JSONDecodeError):
        return True
    if not isinstance(error, TypeError) or str(error) not in {
        "unsupported operand type(s) for -: 'str' and 'int'",
        "'str_ascii_iterator' object is not callable",
    }:
        return False
    traceback = error.__traceback__
    while traceback is not None:
        code = traceback.tb_frame.f_code
        filename = code.co_filename.replace("\\", "/").casefold()
        if code.co_name in {"JSONArray", "_scan_once", "scan_once"} and (
            filename.endswith("/json/decoder.py")
            or filename.endswith("/json/scanner.py")
        ):
            return True
        traceback = traceback.tb_next
    return False


def build_generator_inventory_projection(inventory: Mapping[str, Any]) -> dict[str, Any]:
    missing = [key for key in GENERATOR_INVENTORY_PROJECTION_KEYS if key not in inventory]
    if missing:
        raise CohortValidationError(
            "primary inventory is missing generator projection keys: "
            + ", ".join(missing)
        )
    return {key: inventory[key] for key in GENERATOR_INVENTORY_PROJECTION_KEYS}


def primary_output_signature(
    artifacts: Mapping[str, Path], snapshot_descriptor: Mapping[str, Any]
) -> str:
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
    return sha256_bytes(canonical_json_bytes(descriptors))


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
        except (json.JSONDecodeError, TypeError) as error:
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
    executable = (executable or Path(sys.executable)).resolve(strict=True)
    return {
        "implementation": platform.python_implementation(),
        "version": platform.python_version(),
        "executableSha256": sha256_file(executable),
        "executableByteLength": executable.stat().st_size,
    }


def generator_descriptors(repo_root: Path) -> dict[str, dict[str, Any]]:
    descriptors: dict[str, dict[str, Any]] = {}
    for name, logical_path in sorted(GENERATOR_PATHS.items()):
        descriptors[name] = artifact_descriptor(
            repo_root / Path(logical_path), logical_path
        )
    return descriptors


def auxiliary_input_paths(repo_root: Path) -> tuple[str, ...]:
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
    for capture_id in sorted(capture_ids):
        capture_root = (
            repo_root
            / "tools-temp"
            / "AOSharpLiveCapture"
            / "bin"
            / "Debug"
            / "captures"
            / capture_id
        )
        for source_name in FORMULA_CAPTURE_SOURCE_NAMES:
            source = capture_root / source_name
            if source.is_file():
                values.add(source.relative_to(repo_root).as_posix())
    analyzer_directory = repo_root / SCFU_ANALYZER.parent
    if not analyzer_directory.is_dir():
        raise PipelineError("SCFU analyzer dependency directory is missing")
    for source in analyzer_directory.iterdir():
        if source.is_file() and not source.is_symlink():
            values.add(source.relative_to(repo_root).as_posix())
    runtime_root = repo_root / "AORebirth" / "Server" / "ZoneEngine" / "Core"
    if not runtime_root.is_dir():
        raise PipelineError("active-coverage runtime source root is missing")
    for path in runtime_root.rglob("*.cs"):
        relative = path.relative_to(repo_root).as_posix()
        if relative.casefold() not in excluded:
            values.add(relative)
    return tuple(sorted(values))


def capture_auxiliary_inputs(lease: Any, repo_root: Path) -> Any:
    transaction = _load_transaction_module()
    return transaction.InputSnapshot.capture(
        lease,
        auxiliary_input_paths(repo_root),
    )


def revalidate_auxiliary_inputs(snapshot: Any, repo_root: Path) -> None:
    snapshot.revalidate(auxiliary_input_paths(repo_root))


_ABSOLUTE_WINDOWS_PATH = re.compile(r"^[A-Za-z]:[\\/]")
_ABSOLUTE_WINDOWS_PATH_IN_TEXT = re.compile(
    r"(?<![A-Za-z0-9_+.-])[A-Za-z]:[\\/]|\\\\[^\\/\s]+[\\/]"
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
    if isinstance(value, dict):
        for key, child in value.items():
            assert_generated_value_is_path_independent(child, f"{location}.{key}")
        return
    if isinstance(value, list):
        for index, child in enumerate(value):
            assert_generated_value_is_path_independent(child, f"{location}[{index}]")
        return
    if isinstance(value, str) and _ABSOLUTE_WINDOWS_PATH_IN_TEXT.search(value):
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
) -> tuple[dict[str, Any], bytes]:
    if set(artifacts) != set(ARTIFACT_RELATIVE_PATHS):
        raise PipelineError("candidate cohort does not contain the exact artifact roles")

    inventory = load_json_object(artifacts["inventory"], "primary inventory")
    active = load_json_object(artifacts["activeCoverage"], "active coverage")
    formula = load_json_object(artifacts["formulaDataset"], "formula dataset")
    assert_generated_value_is_path_independent(inventory, "primary inventory")
    assert_generated_value_is_path_independent(active, "active coverage")
    assert_generated_value_is_path_independent(formula, "formula dataset")
    counts = extract_acceptance_counts(inventory, active, formula)

    artifact_rows = []
    for role, logical_path in ARTIFACT_RELATIVE_PATHS.items():
        row = artifact_descriptor(artifacts[role], logical_path)
        row["role"] = role
        artifact_rows.append(row)

    manifest: dict[str, Any] = {
        "schemaVersion": MANIFEST_SCHEMA_VERSION,
        "pipeline": PIPELINE_NAME,
        "inputSnapshot": _portable_snapshot_descriptor(
            input_snapshot, auxiliary_input_identity
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
            "TypeError: 'str_ascii_iterator' object is not callable",
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
    verified_item_database_failure = (
        "verified item-template decode failure:" in detail
    )
    return (
        json_decoder_failure
        or governed_json_parse_failure
        or verified_item_database_failure
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


def _run_active_formula_fixed_point(
    *,
    repo_root: Path,
    frozen_repo_root: Path,
    inventory_payload: bytes,
    authoritative_inventory_path: Path,
    item_database_payload: bytes,
    lease: Any,
    max_rounds: int,
) -> FixedPointResult:
    rounds_root = frozen_repo_root / "_active-formula-rounds"
    rounds_root.mkdir()
    initial_payload = canonical_json_bytes({})
    initial = PairState(initial_payload, initial_payload)
    inventory_sha256 = sha256_bytes(inventory_payload)
    inventory_byte_length = len(inventory_payload)
    item_database_sha256 = sha256_bytes(item_database_payload)
    item_database_byte_length = len(item_database_payload)

    def transition(previous: PairState, round_number: int) -> PairState:
        round_root = rounds_root / f"round-{round_number:02d}"
        active_inventory_path = (
            round_root / "active-input" / "combat-inventory.json"
        )
        formula_inventory_path = (
            round_root / "formula-input" / "combat-inventory.json"
        )
        formula_item_database_path = round_root / "formula-items" / "items.dat"
        formula_seed = round_root / "formula-seed" / "formula-dataset.json"
        _write_round_seed(active_inventory_path, inventory_payload)
        _write_round_seed(formula_inventory_path, inventory_payload)
        _write_round_seed(formula_seed, previous.formula_dataset)
        active_output = round_root / "output" / "active-coverage.json"
        active_output.parent.mkdir(parents=True)
        active_completed = run_checked(
            (
                sys.executable,
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
                inventory_sha256,
                "--combat-inventory-byte-length",
                str(inventory_byte_length),
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
        _write_round_seed(formula_item_database_path, item_database_payload)
        formula_output = round_root / "output" / "formula-dataset.json"
        formula_completed = run_checked(
            (
                sys.executable,
                "-I",
                "-u",
                "-X",
                "faulthandler",
                str(frozen_repo_root / FORMULA_GENERATOR),
                "--write",
                "--inventory",
                str(formula_inventory_path),
                "--inventory-sha256",
                inventory_sha256,
                "--inventory-byte-length",
                str(inventory_byte_length),
                "--items",
                str(formula_item_database_path),
                "--items-sha256",
                item_database_sha256,
                "--items-byte-length",
                str(item_database_byte_length),
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
        return PairState(active_output.read_bytes(), formula_output.read_bytes())

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
    with tempfile.TemporaryDirectory(
        prefix="aorebirth-generated-combat-input-snapshot-"
    ) as snapshot_root_name:
        snapshot_path = Path(snapshot_root_name) / "capture-input-snapshot.json"
        accepted_primary_signatures: set[str] = set()
        observed_primary_signatures: list[str] = []
        for primary_attempt in range(1, PRIMARY_AGGREGATION_MAX_ATTEMPTS + 1):
            snapshot_path.unlink(missing_ok=True)
            run_checked(
                (
                    sys.executable,
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
                    PRIMARY_SNAPSHOT_ARGUMENT,
                    str(snapshot_path),
                ),
                repo_root=repo_root,
                lease=lease,
                label="primary aggregation",
                environment_overrides={
                    PRIMARY_CAPTURE_REPO_ROOT_ENVIRONMENT: str(repo_root)
                },
                retry_interpreter_failures=True,
            )
            try:
                if not snapshot_path.is_file():
                    raise CohortValidationError(
                        "capture input snapshot is missing or is not a regular file"
                    )
                inventory = load_json_object(artifacts["inventory"], "primary inventory")
                inventory_projection = build_generator_inventory_projection(inventory)
                snapshot = load_json_object(snapshot_path, "capture input snapshot")
                snapshot_descriptor = _portable_snapshot_descriptor(
                    snapshot, auxiliary_snapshot.identity
                )
                signature = primary_output_signature(
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
            if signature in accepted_primary_signatures:
                break
            accepted_primary_signatures.add(signature)
        else:
            raise PipelineError(
                "primary aggregation did not produce two matching validated outputs "
                f"in {PRIMARY_AGGREGATION_MAX_ATTEMPTS} attempts: "
                + ", ".join(observed_primary_signatures)
            )

    frozen_repo_root = auxiliary_snapshot.snapshot_root
    for role in ("inventory", "catalog", "fixtures"):
        frozen_path = frozen_repo_root / Path(ARTIFACT_RELATIVE_PATHS[role])
        frozen_path.parent.mkdir(parents=True, exist_ok=True)
        frozen_path.write_bytes(artifacts[role].read_bytes())

    authoritative_inventory_path = frozen_repo_root / Path(
        ARTIFACT_RELATIVE_PATHS["inventory"]
    )
    inventory_projection_bytes = canonical_json_bytes(inventory_projection)
    if decode_json_text(inventory_projection_bytes.decode("utf-8")) != inventory_projection:
        raise PipelineError("private generator inventory projection is not canonical")

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
    item_database_payload = _read_verified_private_input(
        auxiliary_snapshot.path_for(ITEM_DATABASE.as_posix()),
        expected_sha256=item_database_record.sha256,
        expected_byte_length=item_database_record.size,
        label="frozen item database",
    )

    fixed_point = _run_active_formula_fixed_point(
        repo_root=repo_root,
        frozen_repo_root=frozen_repo_root,
        inventory_payload=inventory_projection_bytes,
        authoritative_inventory_path=authoritative_inventory_path,
        item_database_payload=item_database_payload,
        lease=lease,
        max_rounds=max_rounds,
    )
    artifacts["activeCoverage"].write_bytes(fixed_point.state.active_coverage)
    artifacts["formulaDataset"].write_bytes(fixed_point.state.formula_dataset)

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
    if manifest["schemaVersion"] != MANIFEST_SCHEMA_VERSION:
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
    if not isinstance(runtime, dict) or set(runtime) != {
        "implementation",
        "version",
        "executableSha256",
        "executableByteLength",
    }:
        raise CohortValidationError("generation manifest runtime descriptor is invalid")
    if not isinstance(runtime["implementation"], str) or not runtime["implementation"]:
        raise CohortValidationError("runtime implementation is invalid")
    if not isinstance(runtime["version"], str) or not runtime["version"]:
        raise CohortValidationError("runtime version is invalid")
    if not isinstance(runtime["executableSha256"], str) or not re.fullmatch(
        r"[0-9a-f]{64}", runtime["executableSha256"]
    ):
        raise CohortValidationError("runtime executable SHA-256 is invalid")
    _require_nonnegative_int(
        runtime["executableByteLength"], "runtime executable byte length"
    )
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
            if _ABSOLUTE_WINDOWS_PATH_IN_TEXT.search(path.read_text(encoding="utf-8")):
                raise CohortValidationError(
                    "artifact contains an absolute repository-location-dependent "
                    f"path: {logical_path}"
                )

    inventory = parsed_json_artifacts["inventory"]
    active = parsed_json_artifacts["activeCoverage"]
    formula = parsed_json_artifacts["formulaDataset"]
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
        if manifest["runtime"] != runtime_descriptor():
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
    value = json.loads(payload.decode("utf-8"))
    if not isinstance(value, dict):
        raise ValueError("generated JSON root must be an object")
    assert_generated_value_is_path_independent(value, "generated JSON")


def _validate_utf8_bytes(payload: bytes) -> None:
    text = payload.decode("utf-8")
    if _ABSOLUTE_WINDOWS_PATH_IN_TEXT.search(text):
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
) -> None:
    revalidate_auxiliary_inputs(auxiliary_snapshot, repo_root)
    with tempfile.TemporaryDirectory(
        prefix="aorebirth-generated-combat-publication-validation-"
    ) as validation_root_name:
        snapshot_path = Path(validation_root_name) / "capture-input-snapshot.json"
        snapshot_path.write_bytes(canonical_json_bytes(candidate.capture_snapshot))
        run_checked(
            (
                sys.executable,
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


def run_pipeline(
    *,
    repo_root: Path,
    mode: str,
    max_rounds: int,
    command: Sequence[str] = (),
    read_lease_command_timeout_seconds: int = CHILD_PROCESS_TIMEOUT_SECONDS,
) -> int:
    repo_root = repo_root.resolve(strict=True)
    if mode == "validate-read-delegation":
        _validate_delegated_lease(repo_root, "read")
        return 0
    if mode == "validate":
        with _shared_lease(repo_root, "read") as lease:
            inputs = capture_auxiliary_inputs(lease, repo_root)
            manifest = validate_cohort(repo_root, verify_toolchain=True)
            if (
                manifest["inputSnapshot"]["auxiliarySnapshotIdentity"]
                != inputs.identity
            ):
                raise CohortValidationError(
                    "published cohort auxiliary input snapshot is stale"
                )
            revalidate_auxiliary_inputs(inputs, repo_root)
        print(f"generated-combat cohort PASS identity={manifest['generationIdentity']}")
        return 0
    if mode == "run-read-lease":
        with _shared_lease(repo_root, "read") as lease:
            manifest = validate_cohort(repo_root, verify_toolchain=False)
            exit_code = _run_supervised_command(
                command,
                repo_root,
                lease,
                timeout_seconds=read_lease_command_timeout_seconds,
            )
            after = validate_cohort(repo_root, verify_toolchain=False)
            if after["generationIdentity"] != manifest["generationIdentity"]:
                raise CohortValidationError(
                    "published cohort changed during read-lease command"
                )
            return exit_code

    lease_mode = "read" if mode == "check" else "write"
    with _shared_lease(repo_root, lease_mode) as lease:
        inputs = capture_auxiliary_inputs(lease, repo_root)
        if mode == "check":
            published_manifest = validate_cohort(repo_root, verify_toolchain=True)
            if (
                published_manifest["inputSnapshot"]["auxiliarySnapshotIdentity"]
                != inputs.identity
            ):
                raise CohortValidationError(
                    "published cohort auxiliary input snapshot is stale"
                )
        candidate_root = lease.new_staging_directory("combat-candidate")
        candidate = build_candidate_cohort(
            repo_root,
            candidate_root,
            auxiliary_snapshot=inputs,
            lease=lease,
            max_rounds=max_rounds,
        )
        revalidate_candidate_inputs(inputs, candidate, repo_root, lease)
        if mode == "check":
            differences = cohort_differences(candidate.root, repo_root)
            if differences:
                joined = ", ".join(differences)
                raise PipelineError(f"generated-combat cohort is dirty: {joined}")
            revalidate_candidate_inputs(inputs, candidate, repo_root, lease)
        else:
            _shared_publish(
                lease,
                candidate,
                lambda phase: revalidate_candidate_inputs(
                    inputs, candidate, repo_root, lease
                ),
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
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--validate-current", action="store_true")
    mode.add_argument("--run-read-lease", action="store_true")
    mode.add_argument(
        "--_validate-read-delegation", action="store_true", help=argparse.SUPPRESS
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
    parser.add_argument("command", nargs=argparse.REMAINDER)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    arguments = parse_arguments(argv)
    if arguments.check:
        mode = "check"
    elif arguments.write:
        mode = "write"
    elif arguments.validate_current:
        mode = "validate"
    elif arguments.run_read_lease:
        mode = "run-read-lease"
    else:
        mode = "validate-read-delegation"
    if mode != "run-read-lease" and arguments.command:
        raise PipelineError("a trailing command is valid only with --run-read-lease")
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
        command=arguments.command,
        read_lease_command_timeout_seconds=(
            arguments.read_lease_command_timeout_seconds
        ),
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (CohortValidationError, FixedPointError, PipelineError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
