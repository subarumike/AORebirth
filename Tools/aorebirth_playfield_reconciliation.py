"""Build the deterministic AORebirth/official-placement reconciliation inventory.

This tool is intentionally offline.  It inventories only sources whose scope is
declared by the governed representation manifest.  It never queries a database,
scans source files for coordinates, or infers a placement link from names or
proximity.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OFFICIAL_INDEX = (
    REPOSITORY_ROOT / "docs/generated/playfields/official-placement-index.json"
)
DEFAULT_REPRESENTATION_MANIFEST = (
    REPOSITORY_ROOT
    / "docs/reference/playfields/aorebirth-playfield-representation-manifest.json"
)
DEFAULT_OUTPUT = (
    REPOSITORY_ROOT
    / "docs/generated/playfields/official-playfield-reconciliation.json"
)

OFFICIAL_INDEX_FIELDS = {
    "SchemaVersion",
    "SourceClientVariant",
    "SourceClientBuild",
    "ResourceType",
    "SourceManifestSha256",
    "Playfields",
}
OFFICIAL_INDEX_PLAYFIELD_FIELDS = {
    "PlayfieldId",
    "ResourceInstance",
    "FormatVersion",
    "ParseStatus",
    "DistrictCount",
    "OfficialSpawnCount",
    "Path",
    "Sha256",
}
OFFICIAL_PARSE_STATUSES = {"PARSED", "MALFORMED_FOR_CURRENT_EXTRACTOR"}
MANIFEST_FIELDS = {
    "SchemaVersion",
    "Description",
    "OfficialIndexExpectations",
    "PlayfieldsXml",
    "CompileEvidence",
    "FixedPlayfields",
    "DynamicScopes",
    "GovernedPlacementReconciliations",
    "Safety",
}
OFFICIAL_EXPECTATION_FIELDS = {
    "SchemaVersion",
    "SourceClientVariant",
    "SourceClientBuild",
    "ResourceType",
    "ResourceCount",
    "ParsedResourceCount",
    "MalformedResourceCount",
    "DistrictCount",
    "OfficialSpawnCount",
    "MalformedPlayfieldIds",
}
PLAYFIELDS_XML_FIELDS = {"Path", "ExpectedPlayfieldCount", "Authority"}
COMPILE_EVIDENCE_FIELDS = {
    "WindowsProjectPath",
    "LinuxCompileInventoryPath",
    "RuntimeRegistrationPath",
}
FIXED_PLAYFIELD_FIELDS = {
    "PlayfieldId",
    "Modules",
    "ExistingSpawnCountStatus",
}
MODULE_FIELDS = {"Name", "SourcePath"}
DYNAMIC_SCOPE_FIELDS = {
    "ScopeId",
    "Module",
    "ModuleSourcePath",
    "PredicateSourcePath",
    "Description",
    "RangeMinimum",
    "RangeMaximum",
    "ExplicitPlayfieldIds",
    "ExpandRangeIntoInventory",
    "IncludeExplicitPlayfieldIdsInInventory",
    "EnumerationStatus",
}
GOVERNED_RECONCILIATION_FIELDS = {
    "PlayfieldId",
    "ExistingAoRebirthSpawnCount",
    "ExistingSpawnsReconciled",
    "ExistingSpawnsUnmatched",
    "CurrentActiveOfficialSpawnCount",
    "OfficialSpawnsWithoutAoRebirthRuntimeEntry",
    "OfficialSpawnsWithoutAoRebirthPlacement",
    "Evidence",
}
SAFETY_FIELDS = {
    "LiveDatabaseQueriesAllowed",
    "RuntimeConsumptionAllowed",
    "ProximityReconciliationAllowed",
    "FilenameInferredImplementationAllowed",
}


class ReconciliationError(ValueError):
    """Raised when a governed input or deterministic output is invalid."""


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ReconciliationError(message)


def _require_exact_fields(value: Any, fields: set[str], owner: str) -> None:
    _require(isinstance(value, dict), f"{owner} must be an object")
    actual = set(value)
    _require(
        actual == fields,
        f"{owner} fields differ from the governed schema: "
        f"missing={sorted(fields - actual)} unexpected={sorted(actual - fields)}",
    )


def _is_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    try:
        return _sha256_bytes(path.read_bytes())
    except OSError as exc:
        raise ReconciliationError(f"cannot read required source {path}: {exc}") from exc


def _load_json(path: Path, owner: str) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ReconciliationError(f"cannot read {owner} {path}: {exc}") from exc


def _repo_path(repository_root: Path, value: str, owner: str) -> Path:
    _require(isinstance(value, str) and value, f"{owner} must be a non-empty path")
    normalized = value.replace("\\", "/")
    _require(not Path(normalized).is_absolute(), f"{owner} must be repository-relative")
    _require(".." not in Path(normalized).parts, f"{owner} cannot escape the repository")
    return repository_root / Path(normalized)


def _portable_identity(path: Path, repository_root: Path) -> str:
    try:
        return path.resolve().relative_to(repository_root.resolve()).as_posix()
    except ValueError:
        return path.name


def validate_manifest(manifest: Any) -> dict[str, Any]:
    _require_exact_fields(manifest, MANIFEST_FIELDS, "representation manifest")
    _require(manifest["SchemaVersion"] == 1, "unsupported representation-manifest schema")
    _require(
        isinstance(manifest["Description"], str) and manifest["Description"],
        "representation manifest Description must be non-empty",
    )

    expectations = manifest["OfficialIndexExpectations"]
    _require_exact_fields(expectations, OFFICIAL_EXPECTATION_FIELDS, "OfficialIndexExpectations")
    for name in (
        "SchemaVersion",
        "ResourceType",
        "ResourceCount",
        "ParsedResourceCount",
        "MalformedResourceCount",
        "DistrictCount",
        "OfficialSpawnCount",
    ):
        _require(_is_int(expectations[name]) and expectations[name] >= 0, f"{name} is invalid")
    for name in ("SourceClientVariant", "SourceClientBuild"):
        _require(isinstance(expectations[name], str) and expectations[name], f"{name} is invalid")
    malformed_ids = expectations["MalformedPlayfieldIds"]
    _require(
        isinstance(malformed_ids, list)
        and all(_is_int(value) and value > 0 for value in malformed_ids)
        and malformed_ids == sorted(set(malformed_ids)),
        "MalformedPlayfieldIds must be sorted unique positive integers",
    )
    _require(
        expectations["ParsedResourceCount"] + expectations["MalformedResourceCount"]
        == expectations["ResourceCount"],
        "official parsed/malformed resource counts do not add to ResourceCount",
    )
    _require(
        len(malformed_ids) == expectations["MalformedResourceCount"],
        "MalformedPlayfieldIds count differs from MalformedResourceCount",
    )

    playfields_xml = manifest["PlayfieldsXml"]
    _require_exact_fields(playfields_xml, PLAYFIELDS_XML_FIELDS, "PlayfieldsXml")
    _require(
        _is_int(playfields_xml["ExpectedPlayfieldCount"])
        and playfields_xml["ExpectedPlayfieldCount"] > 0,
        "PlayfieldsXml.ExpectedPlayfieldCount must be positive",
    )
    _require(
        isinstance(playfields_xml["Authority"], str) and playfields_xml["Authority"],
        "PlayfieldsXml.Authority must be non-empty",
    )

    compile_evidence = manifest["CompileEvidence"]
    _require_exact_fields(compile_evidence, COMPILE_EVIDENCE_FIELDS, "CompileEvidence")

    fixed_ids: set[int] = set()
    module_names: set[str] = set()
    fixed = manifest["FixedPlayfields"]
    _require(isinstance(fixed, list), "FixedPlayfields must be an array")
    for index, row in enumerate(fixed):
        owner = f"FixedPlayfields[{index}]"
        _require_exact_fields(row, FIXED_PLAYFIELD_FIELDS, owner)
        playfield_id = row["PlayfieldId"]
        _require(_is_int(playfield_id) and playfield_id > 0, f"{owner}.PlayfieldId is invalid")
        _require(playfield_id not in fixed_ids, f"duplicate fixed PlayfieldId {playfield_id}")
        fixed_ids.add(playfield_id)
        _require(
            row["ExistingSpawnCountStatus"]
            in {"NOT_ENUMERATED_OFFLINE", "GOVERNED_PLACEMENT_CATALOG_ENUMERATED"},
            f"{owner}.ExistingSpawnCountStatus is invalid",
        )
        modules = row["Modules"]
        _require(isinstance(modules, list) and modules, f"{owner}.Modules must be non-empty")
        row_modules: set[str] = set()
        for module_index, module in enumerate(modules):
            module_owner = f"{owner}.Modules[{module_index}]"
            _require_exact_fields(module, MODULE_FIELDS, module_owner)
            name = module["Name"]
            _require(isinstance(name, str) and name, f"{module_owner}.Name is invalid")
            _require(name not in row_modules, f"duplicate module {name} for PF {playfield_id}")
            row_modules.add(name)
            module_names.add(name)

    scopes = manifest["DynamicScopes"]
    _require(isinstance(scopes, list), "DynamicScopes must be an array")
    scope_ids: set[str] = set()
    for index, scope in enumerate(scopes):
        owner = f"DynamicScopes[{index}]"
        _require_exact_fields(scope, DYNAMIC_SCOPE_FIELDS, owner)
        scope_id = scope["ScopeId"]
        _require(isinstance(scope_id, str) and scope_id, f"{owner}.ScopeId is invalid")
        _require(scope_id not in scope_ids, f"duplicate dynamic ScopeId {scope_id}")
        scope_ids.add(scope_id)
        _require(
            isinstance(scope["Module"], str) and scope["Module"],
            f"{owner}.Module is invalid",
        )
        module_names.add(scope["Module"])
        minimum = scope["RangeMinimum"]
        maximum = scope["RangeMaximum"]
        _require(
            (minimum is None and maximum is None)
            or (
                _is_int(minimum)
                and _is_int(maximum)
                and minimum > 0
                and maximum >= minimum
            ),
            f"{owner} has an invalid dynamic range",
        )
        explicit_ids = scope["ExplicitPlayfieldIds"]
        _require(
            isinstance(explicit_ids, list)
            and all(_is_int(value) and value > 0 for value in explicit_ids)
            and explicit_ids == sorted(set(explicit_ids)),
            f"{owner}.ExplicitPlayfieldIds must be sorted unique positive integers",
        )
        _require(
            scope["ExpandRangeIntoInventory"] is False,
            f"{owner} must not expand a dynamic range into per-playfield rows",
        )
        _require(
            isinstance(scope["IncludeExplicitPlayfieldIdsInInventory"], bool),
            f"{owner}.IncludeExplicitPlayfieldIdsInInventory must be boolean",
        )
        _require(
            isinstance(scope["EnumerationStatus"], str) and scope["EnumerationStatus"],
            f"{owner}.EnumerationStatus must be non-empty",
        )

    governed = manifest["GovernedPlacementReconciliations"]
    _require(isinstance(governed, list), "GovernedPlacementReconciliations must be an array")
    governed_ids: set[int] = set()
    for index, row in enumerate(governed):
        owner = f"GovernedPlacementReconciliations[{index}]"
        _require_exact_fields(row, GOVERNED_RECONCILIATION_FIELDS, owner)
        playfield_id = row["PlayfieldId"]
        _require(playfield_id in fixed_ids, f"{owner} does not target a fixed playfield")
        _require(playfield_id not in governed_ids, f"duplicate governed PlayfieldId {playfield_id}")
        governed_ids.add(playfield_id)
        for name in GOVERNED_RECONCILIATION_FIELDS - {"Evidence"}:
            _require(_is_int(row[name]) and row[name] >= 0, f"{owner}.{name} is invalid")
        _require(
            row["ExistingAoRebirthSpawnCount"]
            == row["ExistingSpawnsReconciled"] + row["ExistingSpawnsUnmatched"],
            f"{owner} existing count does not reconcile",
        )
        _require(
            isinstance(row["Evidence"], list)
            and row["Evidence"]
            and all(isinstance(value, str) and value for value in row["Evidence"]),
            f"{owner}.Evidence must be a non-empty string array",
        )

    safety = manifest["Safety"]
    _require_exact_fields(safety, SAFETY_FIELDS, "Safety")
    _require(all(safety[name] is False for name in SAFETY_FIELDS), "all Safety switches must be false")

    # PF4582 is the only currently governed exact placement reconciliation.
    _require(governed_ids == {4582}, "the exact governed reconciliation set must be PF4582 only")
    pf4582 = governed[0]
    exact_pf4582 = {
        "ExistingAoRebirthSpawnCount": 206,
        "ExistingSpawnsReconciled": 206,
        "ExistingSpawnsUnmatched": 0,
        "CurrentActiveOfficialSpawnCount": 25,
        "OfficialSpawnsWithoutAoRebirthRuntimeEntry": 182,
        "OfficialSpawnsWithoutAoRebirthPlacement": 1,
    }
    _require(
        all(pf4582[name] == value for name, value in exact_pf4582.items()),
        "PF4582 governed reconciliation values differ from the accepted baseline",
    )
    return manifest


def validate_compile_and_registration_evidence(
    manifest: dict[str, Any], repository_root: Path
) -> dict[str, str]:
    compile_evidence = manifest["CompileEvidence"]
    windows_path = _repo_path(
        repository_root, compile_evidence["WindowsProjectPath"], "WindowsProjectPath"
    )
    linux_path = _repo_path(
        repository_root,
        compile_evidence["LinuxCompileInventoryPath"],
        "LinuxCompileInventoryPath",
    )
    registration_path = _repo_path(
        repository_root,
        compile_evidence["RuntimeRegistrationPath"],
        "RuntimeRegistrationPath",
    )
    try:
        windows_text = windows_path.read_text(encoding="utf-8").replace("\\", "/")
        linux_text = linux_path.read_text(encoding="utf-8").replace("\\", "/")
        registration_text = registration_path.read_text(encoding="utf-8")
    except (OSError, UnicodeError) as exc:
        raise ReconciliationError(f"cannot read compile/registration evidence: {exc}") from exc

    evidence_hashes = {
        compile_evidence["WindowsProjectPath"]: _sha256_file(windows_path),
        compile_evidence["LinuxCompileInventoryPath"]: _sha256_file(linux_path),
        compile_evidence["RuntimeRegistrationPath"]: _sha256_file(registration_path),
    }

    modules: list[dict[str, Any]] = []
    for fixed in manifest["FixedPlayfields"]:
        modules.extend(fixed["Modules"])
    modules.extend(
        {
            "Name": scope["Module"],
            "SourcePath": scope["ModuleSourcePath"],
        }
        for scope in manifest["DynamicScopes"]
    )

    seen: set[tuple[str, str]] = set()
    for module in modules:
        key = (module["Name"], module["SourcePath"])
        if key in seen:
            continue
        seen.add(key)
        source_path = _repo_path(repository_root, module["SourcePath"], "module SourcePath")
        _require(source_path.is_file(), f"module source is missing: {module['SourcePath']}")
        evidence_hashes[module["SourcePath"]] = _sha256_file(source_path)
        normalized = module["SourcePath"].replace("\\", "/")
        windows_suffix = normalized.split("AORebirth/Server/ZoneEngine/", 1)[-1]
        _require(
            windows_suffix in windows_text,
            f"Windows ZoneEngine project does not compile {module['SourcePath']}",
        )
        _require(
            normalized in linux_text,
            f"Linux ZoneEngine inventory does not compile {module['SourcePath']}",
        )
        _require(
            f"new {module['Name']}()" in registration_text,
            f"runtime content coordinator does not register {module['Name']}",
        )

    for scope in manifest["DynamicScopes"]:
        predicate_path = _repo_path(
            repository_root, scope["PredicateSourcePath"], "PredicateSourcePath"
        )
        _require(predicate_path.is_file(), f"dynamic predicate source is missing: {predicate_path}")
        evidence_hashes[scope["PredicateSourcePath"]] = _sha256_file(predicate_path)

    for governed in manifest["GovernedPlacementReconciliations"]:
        for evidence in governed["Evidence"]:
            evidence_path = _repo_path(repository_root, evidence, "governed Evidence")
            _require(evidence_path.is_file(), f"governed evidence is missing: {evidence}")
            evidence_hashes[evidence] = _sha256_file(evidence_path)

    return evidence_hashes


def load_playfields_xml(
    manifest: dict[str, Any], repository_root: Path
) -> tuple[set[int], str]:
    specification = manifest["PlayfieldsXml"]
    path = _repo_path(repository_root, specification["Path"], "PlayfieldsXml.Path")
    try:
        root = ET.parse(path).getroot()
    except (OSError, ET.ParseError) as exc:
        raise ReconciliationError(f"cannot parse Playfields.xml {path}: {exc}") from exc
    _require(root.tag == "Playfields", "Playfields.xml root must be Playfields")
    ids: list[int] = []
    for index, element in enumerate(root.findall("Playfield")):
        raw_id = element.get("id")
        try:
            playfield_id = int(raw_id or "")
        except ValueError as exc:
            raise ReconciliationError(f"Playfields.xml row {index} has invalid id") from exc
        _require(playfield_id > 0, f"Playfields.xml row {index} has non-positive id")
        ids.append(playfield_id)
    _require(len(ids) == len(set(ids)), "Playfields.xml contains duplicate ids")
    _require(
        len(ids) == specification["ExpectedPlayfieldCount"],
        "Playfields.xml count differs from the governed expectation: "
        f"expected={specification['ExpectedPlayfieldCount']} actual={len(ids)}",
    )
    return set(ids), _sha256_file(path)


def validate_official_index(
    index: Any,
    expectations: dict[str, Any],
    repository_root: Path | None = None,
) -> dict[int, dict[str, Any]]:
    _require_exact_fields(index, OFFICIAL_INDEX_FIELDS, "official placement index")
    for name in ("SchemaVersion", "SourceClientVariant", "SourceClientBuild", "ResourceType"):
        _require(index[name] == expectations[name], f"official placement index {name} drifted")
    _require(
        isinstance(index["SourceManifestSha256"], str)
        and len(index["SourceManifestSha256"]) == 64
        and all(
            character in "0123456789abcdef"
            for character in index["SourceManifestSha256"]
        ),
        "official placement index SourceManifestSha256 is invalid",
    )
    rows = index["Playfields"]
    _require(isinstance(rows, list), "official placement index Playfields must be an array")
    _require(
        len(rows) == expectations["ResourceCount"],
        "official placement resource count drifted",
    )

    by_id: dict[int, dict[str, Any]] = {}
    parsed_count = 0
    malformed_ids: list[int] = []
    district_total = 0
    spawn_total = 0
    for row_index, row in enumerate(rows):
        owner = f"official placement index Playfields[{row_index}]"
        _require_exact_fields(row, OFFICIAL_INDEX_PLAYFIELD_FIELDS, owner)
        playfield_id = row["PlayfieldId"]
        _require(_is_int(playfield_id) and playfield_id > 0, f"{owner}.PlayfieldId is invalid")
        _require(playfield_id not in by_id, f"duplicate official PlayfieldId {playfield_id}")
        _require(row["ResourceInstance"] == playfield_id, f"{owner} instance/playfield mismatch")
        _require(
            row["FormatVersion"] is None
            or (_is_int(row["FormatVersion"]) and row["FormatVersion"] >= 0),
            f"{owner}.FormatVersion is invalid",
        )
        _require(
            row["ParseStatus"] in OFFICIAL_PARSE_STATUSES,
            f"{owner}.ParseStatus is invalid",
        )
        _require(
            isinstance(row["Path"], str) and row["Path"] and not Path(row["Path"]).is_absolute(),
            f"{owner}.Path must be repository-relative",
        )
        _require(
            isinstance(row["Sha256"], str)
            and len(row["Sha256"]) == 64
            and all(character in "0123456789abcdef" for character in row["Sha256"]),
            f"{owner}.Sha256 is invalid",
        )
        if repository_root is not None:
            shard_path = _repo_path(repository_root, row["Path"], f"{owner}.Path")
            _require(shard_path.is_file(), f"{owner} shard is missing: {row['Path']}")
            _require(
                _sha256_file(shard_path) == row["Sha256"],
                f"{owner} shard SHA-256 drifted: {row['Path']}",
            )
        if row["ParseStatus"] == "PARSED":
            _require(
                _is_int(row["DistrictCount"]) and row["DistrictCount"] >= 0,
                f"{owner}.DistrictCount is unavailable for a parsed resource",
            )
            _require(
                _is_int(row["OfficialSpawnCount"]) and row["OfficialSpawnCount"] >= 0,
                f"{owner}.OfficialSpawnCount is unavailable for a parsed resource",
            )
            parsed_count += 1
            district_total += row["DistrictCount"]
            spawn_total += row["OfficialSpawnCount"]
        else:
            _require(
                row["DistrictCount"] is None and row["OfficialSpawnCount"] is None,
                f"{owner} must retain unavailable malformed counts as null",
            )
            malformed_ids.append(playfield_id)
        by_id[playfield_id] = row

    _require(parsed_count == expectations["ParsedResourceCount"], "parsed resource count drifted")
    _require(
        len(malformed_ids) == expectations["MalformedResourceCount"],
        "malformed resource count drifted",
    )
    _require(
        sorted(malformed_ids) == expectations["MalformedPlayfieldIds"],
        "malformed resource identity set drifted",
    )
    _require(district_total == expectations["DistrictCount"], "official district count drifted")
    _require(spawn_total == expectations["OfficialSpawnCount"], "official spawn count drifted")
    return by_id


def _source(source_type: str, path: str, detail: str) -> dict[str, str]:
    return {"Type": source_type, "Path": path, "Detail": detail}


def _fixed_by_id(manifest: dict[str, Any]) -> dict[int, dict[str, Any]]:
    return {row["PlayfieldId"]: row for row in manifest["FixedPlayfields"]}


def _dynamic_by_explicit_id(manifest: dict[str, Any]) -> dict[int, list[dict[str, Any]]]:
    result: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for scope in manifest["DynamicScopes"]:
        if not scope["IncludeExplicitPlayfieldIdsInInventory"]:
            continue
        for playfield_id in scope["ExplicitPlayfieldIds"]:
            result[playfield_id].append(scope)
    return result


def build_model(
    official_index_path: Path = DEFAULT_OFFICIAL_INDEX,
    representation_manifest_path: Path = DEFAULT_REPRESENTATION_MANIFEST,
    repository_root: Path = REPOSITORY_ROOT,
) -> dict[str, Any]:
    manifest = validate_manifest(
        _load_json(representation_manifest_path, "AORebirth representation manifest")
    )
    official_index = _load_json(official_index_path, "official placement index")
    official_by_id = validate_official_index(
        official_index,
        manifest["OfficialIndexExpectations"],
        repository_root,
    )
    xml_ids, xml_sha256 = load_playfields_xml(manifest, repository_root)
    compile_hashes = validate_compile_and_registration_evidence(manifest, repository_root)

    fixed_by_id = _fixed_by_id(manifest)
    dynamic_by_id = _dynamic_by_explicit_id(manifest)
    governed_by_id = {
        row["PlayfieldId"]: row for row in manifest["GovernedPlacementReconciliations"]
    }
    playfield_ids = sorted(
        set(official_by_id) | xml_ids | set(fixed_by_id) | set(dynamic_by_id)
    )

    rows: list[dict[str, Any]] = []
    for playfield_id in playfield_ids:
        official = official_by_id.get(playfield_id)
        fixed = fixed_by_id.get(playfield_id)
        dynamic = sorted(dynamic_by_id.get(playfield_id, []), key=lambda value: value["ScopeId"])
        governed = governed_by_id.get(playfield_id)
        implementation_present = fixed is not None or bool(dynamic)

        representation_sources: list[dict[str, str]] = []
        if official is not None:
            representation_sources.append(
                _source(
                    "OFFICIAL_PLACEMENT_RESOURCE",
                    official["Path"],
                    official["ParseStatus"],
                )
            )
        if playfield_id in xml_ids:
            representation_sources.append(
                _source(
                    "AO_REBIRTH_PLAYFIELDS_XML_METADATA",
                    manifest["PlayfieldsXml"]["Path"],
                    manifest["PlayfieldsXml"]["Authority"],
                )
            )
        if fixed is not None:
            for module in sorted(fixed["Modules"], key=lambda value: value["Name"]):
                representation_sources.append(
                    _source(
                        "AO_REBIRTH_COMPILED_REGISTERED_CONTENT_MODULE",
                        module["SourcePath"],
                        module["Name"],
                    )
                )
        for scope in dynamic:
            representation_sources.append(
                _source(
                    "AO_REBIRTH_DYNAMIC_SCOPE_EXPLICIT_ID",
                    scope["PredicateSourcePath"],
                    scope["ScopeId"],
                )
            )
        if governed is not None:
            for evidence in governed["Evidence"]:
                representation_sources.append(
                    _source(
                        "AO_REBIRTH_GOVERNED_PLACEMENT_RECONCILIATION",
                        evidence,
                        "EXACT_IDENTITY_BRIDGE",
                    )
                )

        if fixed is not None:
            implementation_status = "COMPILED_REGISTERED_FIXED_PLAYFIELD"
            existing_count_status = fixed["ExistingSpawnCountStatus"]
        elif dynamic:
            implementation_status = "COMPILED_REGISTERED_DYNAMIC_SCOPE_EXPLICIT_ID"
            existing_count_status = "DYNAMIC_SCOPE_NOT_ENUMERATED_OFFLINE"
        else:
            implementation_status = "NOT_PROVEN_BY_OFFLINE_REPRESENTATION_MANIFEST"
            existing_count_status = "NOT_ENUMERATED_OFFLINE"

        if governed is None:
            existing_count = None
            existing_reconciled = None
            existing_unmatched = None
            current_active_official = None
            official_without_runtime = None
            official_without_placement = None
            reconciliation_status = "NOT_ENUMERATED_OR_IDENTITY_BRIDGED_OFFLINE"
            official_without_runtime_status = "UNAVAILABLE_WITHOUT_EXACT_RUNTIME_INVENTORY"
            official_without_placement_status = "UNAVAILABLE_WITHOUT_EXACT_PLACEMENT_INVENTORY"
        else:
            _require(official is not None, f"governed PF {playfield_id} lacks official resource")
            _require(official["ParseStatus"] == "PARSED", f"governed PF {playfield_id} is not parsed")
            _require(
                official["OfficialSpawnCount"]
                == governed["ExistingSpawnsReconciled"]
                + governed["OfficialSpawnsWithoutAoRebirthPlacement"],
                f"governed PF {playfield_id} official/placement counts do not reconcile",
            )
            _require(
                official["OfficialSpawnCount"]
                == governed["CurrentActiveOfficialSpawnCount"]
                + governed["OfficialSpawnsWithoutAoRebirthRuntimeEntry"],
                f"governed PF {playfield_id} official/runtime counts do not reconcile",
            )
            existing_count = governed["ExistingAoRebirthSpawnCount"]
            existing_reconciled = governed["ExistingSpawnsReconciled"]
            existing_unmatched = governed["ExistingSpawnsUnmatched"]
            current_active_official = governed["CurrentActiveOfficialSpawnCount"]
            official_without_runtime = governed[
                "OfficialSpawnsWithoutAoRebirthRuntimeEntry"
            ]
            official_without_placement = governed[
                "OfficialSpawnsWithoutAoRebirthPlacement"
            ]
            reconciliation_status = "EXACT_GOVERNED_RECONCILIATION"
            official_without_runtime_status = "EXACT_GOVERNED_COUNT"
            official_without_placement_status = "EXACT_GOVERNED_COUNT"

        rows.append(
            {
                "PlayfieldId": playfield_id,
                "AoRebirthImplementationPresent": implementation_present,
                "OfficialPlacementResourcePresent": official is not None,
                "OfficialPlacementParseStatus": (
                    official["ParseStatus"] if official is not None else "NOT_PRESENT"
                ),
                "OfficialDistrictCount": (
                    official["DistrictCount"] if official is not None else None
                ),
                "OfficialSpawnCount": (
                    official["OfficialSpawnCount"] if official is not None else None
                ),
                "ExistingAoRebirthSpawnCount": existing_count,
                "ExistingSpawnsReconciled": existing_reconciled,
                "ExistingSpawnsUnmatched": existing_unmatched,
                "OfficialSpawnsWithoutAoRebirthRuntimeEntry": official_without_runtime,
                "OfficialSpawnsWithoutAoRebirthPlacement": official_without_placement,
                "CurrentActiveOfficialSpawnCount": current_active_official,
                "AoRebirthMetadataPresent": playfield_id in xml_ids,
                "AoRebirthImplementationStatus": implementation_status,
                "ExistingAoRebirthSpawnCountStatus": existing_count_status,
                "ReconciliationStatus": reconciliation_status,
                "OfficialSpawnsWithoutAoRebirthRuntimeEntryStatus": official_without_runtime_status,
                "OfficialSpawnsWithoutAoRebirthPlacementStatus": official_without_placement_status,
                "RepresentationSources": representation_sources,
            }
        )

    parsed = [row for row in official_by_id.values() if row["ParseStatus"] == "PARSED"]
    output = {
        "SchemaVersion": 1,
        "Authority": "OFFLINE_EVIDENCE_AND_REPRESENTATION_INVENTORY_ONLY",
        "SourceProvenance": {
            "OfficialPlacementIndex": {
                "Path": _portable_identity(official_index_path, repository_root),
                "Sha256": _sha256_file(official_index_path),
            },
            "AoRebirthRepresentationManifest": {
                "Path": _portable_identity(representation_manifest_path, repository_root),
                "Sha256": _sha256_file(representation_manifest_path),
            },
            "PlayfieldsXml": {
                "Path": manifest["PlayfieldsXml"]["Path"],
                "Sha256": xml_sha256,
            },
            "AoRebirthImplementationEvidence": [
                {"Path": path, "Sha256": digest}
                for path, digest in sorted(compile_hashes.items())
            ],
        },
        "Summary": {
            "InventoryPlayfieldCount": len(rows),
            "OfficialResourceCount": len(official_by_id),
            "OfficialParsedResourceCount": len(parsed),
            "OfficialMalformedResourceCount": len(official_by_id) - len(parsed),
            "OfficialDistrictCount": sum(row["DistrictCount"] for row in parsed),
            "OfficialSpawnCount": sum(row["OfficialSpawnCount"] for row in parsed),
            "PlayfieldsXmlMetadataCount": len(xml_ids),
            "CompiledRegisteredFixedPlayfieldCount": len(fixed_by_id),
            "DynamicExplicitPlayfieldCount": len(dynamic_by_id),
            "ExistingSpawnCountsFullyEnumeratedPlayfieldCount": len(governed_by_id),
            "NewRuntimeSpawnsActivated": 0,
            "ExistingRuntimeBehaviorChanged": False,
            "LiveDatabaseQueried": False,
        },
        "DynamicScopes": manifest["DynamicScopes"],
        "Playfields": rows,
    }
    return output


def render_json(model: dict[str, Any]) -> str:
    return json.dumps(model, indent=2, ensure_ascii=False, sort_keys=False) + "\n"


def _write_or_check(path: Path, content: str, check: bool) -> None:
    if check:
        try:
            current = path.read_text(encoding="utf-8")
        except OSError as exc:
            raise ReconciliationError(f"generated reconciliation is missing: {path}") from exc
        _require(current == content, f"generated reconciliation is stale: {path}")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Build the offline AORebirth playfield reconciliation inventory"
    )
    parser.add_argument("--official-index", type=Path, default=DEFAULT_OFFICIAL_INDEX)
    parser.add_argument(
        "--representation-manifest",
        type=Path,
        default=DEFAULT_REPRESENTATION_MANIFEST,
    )
    parser.add_argument("--repository-root", type=Path, default=REPOSITORY_ROOT)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    args = parser.parse_args(list(argv) if argv is not None else None)

    try:
        model = build_model(
            official_index_path=args.official_index,
            representation_manifest_path=args.representation_manifest,
            repository_root=args.repository_root,
        )
        _write_or_check(args.output, render_json(model), args.check)
    except ReconciliationError as exc:
        print(f"AORebirth playfield reconciliation failed: {exc}", file=sys.stderr)
        return 1

    print(
        "AORebirth playfield reconciliation "
        + ("validated" if args.check else "generated")
        + f": playfields={model['Summary']['InventoryPlayfieldCount']} "
        + f"officialSpawns={model['Summary']['OfficialSpawnCount']} "
        + "runtimeChanges=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
