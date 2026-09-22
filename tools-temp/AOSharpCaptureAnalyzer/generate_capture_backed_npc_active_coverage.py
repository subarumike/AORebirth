#!/usr/bin/env python3
"""Preserve retired population evidence and audit current editable NPC consumers.

The retired Legacy roster is immutable offline capture evidence. It remains an
input to historical combat formula analysis, but never establishes NewEngine
population, runtime permissions, or current combat readiness. Current consumers
and editable content are inventoried independently from the current worktree.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from typing import Any, Mapping, Optional, Sequence

HISTORICAL_COVERAGE_PATH = "Tests/Fixtures/Combat/RetiredPopulationCoverage.json"
HISTORICAL_COVERAGE_SHA256 = "cf20612078047b8dc90146c9b741eecf42f92181b0c59b7f9e54fd6a6f48504e"
HISTORICAL_SOURCE_COMMIT = "c5af4ac18a1378dc41c37d31b9ac62ac46c5f8a0"
RUNTIME_SOURCE_ROOT = "AORebirth/Server/ZoneEngine_New"
CAPTURED_COMBAT_SOURCE_ROOT = "Tests/Fixtures/Gameplay/Combat"
CAPTURED_COMBAT_SHARED_SOURCE_INPUTS = tuple(
    CAPTURED_COMBAT_SOURCE_ROOT + "/" + name
    for name in (
        "CapturedEnemyCombatData.cs", "CapturedEnemyCombatSequenceData.cs",
        "CapturedEnemyCombatContract.Data.cs", "CapturedEnemyCombatProfileData.cs",
        "CapturedEnemyCombatProfileMatching.cs", "CapturedEnemyCombatPacketFactory.Data.cs",
        "OrdinaryEnemyCombatSetupGenerator.Data.cs",
    )
)
RUNTIME_CONSUMERS = {
    "Core/Playfield/HashSpawnSystem.cs": ("LoadSpawns(", "SpawnContentValidation.IsValid(", "HashSpawnSystem"),
    "Core/Playfield/SpawnService.cs": ("SpawnService", "NpcContentActivationService"),
    "Core/Mobs/WorldNpcFactory.cs": ("WorldNpcFactory", "WorldNpcDefinition"),
    "Core/GameData/PlayfieldNpcContentCatalog.cs": ("PlayfieldNpcContentCatalog", "Parse(", "StandaloneShops"),
    "Core/GameData/NpcTemplateCatalog.cs": ("CanResolve(", "TryResolve(", "FallbackHash"),
    "Core/Playfield/SpawnContentValidation.cs": ("IsValid(", "ValidSite(", "float.IsFinite"),
}
EDITABLE_PLAYFIELD_CONTENT_ROOT = "AORebirth/GameData/PlayfieldContent"
EDITABLE_SUPPORTING_CONTENT = (
    "docs/accepted/npc/delmus/NpcTemplate.json",
    "docs/accepted/npc/delmus/NpcFamilyStatTemplates.json",
    "docs/accepted/npc/delmus/NpcStatTemplateOverlays.json",
)

class CoverageError(RuntimeError):
    pass

def repo_path(repo_root: Path, relative: str) -> Path:
    candidate = (repo_root / relative).resolve()
    if not candidate.is_relative_to(repo_root.resolve()) or not candidate.is_file():
        raise CoverageError(f"required repository input is missing: {relative}")
    return candidate

def read_source(repo_root: Path, relative: str) -> str:
    return repo_path(repo_root, relative).read_text(encoding="utf-8-sig")

def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def sha256_utf8_text_lf(path: Path) -> str:
    return hashlib.sha256(path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").encode("utf-8")).hexdigest()

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

def load_json(path: Path, *, expected_sha256=None, expected_byte_length=None):
    if (expected_sha256 is None) != (expected_byte_length is None):
        raise CoverageError("JSON input integrity descriptor is incomplete")
    payload = path.read_bytes()
    if expected_byte_length is not None and len(payload) != expected_byte_length:
        raise CoverageError("canonical combat inventory length changed during generation")
    if expected_sha256 is not None and hashlib.sha256(payload).hexdigest() != expected_sha256:
        raise CoverageError("canonical combat inventory hash changed during generation")
    return decode_json_text(payload.decode("utf-8-sig"))

def historical_coverage(repo_root: Path) -> dict[str, Any]:
    source = repo_path(repo_root, HISTORICAL_COVERAGE_PATH)
    if sha256_utf8_text_lf(source) != HISTORICAL_COVERAGE_SHA256:
        raise CoverageError("retired population evidence changed; explicit evidence review is required")
    document = load_json(source)
    counts = document["totals"]
    if counts["certified"] + counts["unresolved"] != counts["initialActorCount"]:
        raise CoverageError("historical population classifications do not reconcile")
    if sum(row["actorCount"] for row in document["profiles"]) != counts["initialActorCount"]:
        raise CoverageError("historical population rows do not reconcile")
    return document

def discover_current_consumers(repo_root: Path) -> list[dict[str, Any]]:
    result = []
    for suffix, markers in sorted(RUNTIME_CONSUMERS.items()):
        relative = RUNTIME_SOURCE_ROOT + "/" + suffix
        source = read_source(repo_root, relative)
        missing = [marker for marker in markers if marker not in source]
        if missing:
            raise CoverageError(f"editable content consumer contract changed: {relative}: {missing}")
        result.append({"path": relative, "sha256": sha256_utf8_text_lf(repo_path(repo_root, relative)), "hashNormalization": "utf8-sig-text-lf"})
    for relative in CAPTURED_COMBAT_SHARED_SOURCE_INPUTS:
        result.append({"path": relative, "sha256": sha256_utf8_text_lf(repo_path(repo_root, relative)), "hashNormalization": "utf8-sig-text-lf"})
    return result

def editable_playfield_content_paths(repo_root: Path) -> list[str]:
    root = (repo_root / EDITABLE_PLAYFIELD_CONTENT_ROOT).resolve()
    if not root.is_relative_to(repo_root.resolve()) or not root.is_dir():
        raise CoverageError(f"editable playfield content root is missing: {EDITABLE_PLAYFIELD_CONTENT_ROOT}")
    paths = sorted(root.glob("*/Npcs.json"))
    if not paths:
        raise CoverageError(f"editable playfield NPC content is missing: {EDITABLE_PLAYFIELD_CONTENT_ROOT}")
    return [str(path.relative_to(repo_root)).replace("\\", "/") for path in paths]

def editable_content_inventory(repo_root: Path) -> dict[str, Any]:
    inputs = []
    playfield_content = editable_playfield_content_paths(repo_root)
    for relative in [*playfield_content, *EDITABLE_SUPPORTING_CONTENT]:
        source = repo_path(repo_root, relative)
        document = load_json(source)
        if not isinstance(document, (dict, list)):
            raise CoverageError(f"editable content root must be an object or array: {relative}")
        inputs.append({"path": relative, "sha256": sha256_utf8_text_lf(source), "hashNormalization": "utf8-sig-text-lf"})
    actors = []
    playfields = []
    standalone_shop_count = 0
    for relative in playfield_content:
        document = load_json(repo_path(repo_root, relative))
        if not isinstance(document, dict):
            raise CoverageError(f"playfield NPC content must be an object: {relative}")
        npcs = document.get("Npcs")
        shops = document.get("StandaloneShops", [])
        playfield_id = document.get("PlayfieldId")
        if not isinstance(npcs, list):
            raise CoverageError(f"PlayfieldContent.Npcs must be an array: {relative}")
        if not isinstance(shops, list):
            raise CoverageError(f"PlayfieldContent.StandaloneShops must be an array: {relative}")
        if not isinstance(playfield_id, int):
            raise CoverageError(f"PlayfieldContent.PlayfieldId must be an integer: {relative}")
        actors.extend(npcs)
        playfields.append(playfield_id)
        standalone_shop_count += len(shops)
    keys = [row.get("Key") for row in actors]
    if any(not isinstance(key, str) or not key.strip() for key in keys) or len(set(keys)) != len(keys):
        raise CoverageError("PlayfieldContent.Npcs keys must be present and unique")
    return {
        "scope": "repository editable authored playfield content; private deployment hash catalogs and extracted playfield population are not inferred",
        "inputs": inputs,
        "authoredNpcDefinitionCount": len(actors),
        "authoredStandaloneShopDefinitionCount": standalone_shop_count,
        "authoredPlayfields": sorted(set(playfields)),
        "runtimeActivationPermissionFromEvidence": False,
        "historicalRosterIsCurrentPopulation": False,
        "privateHashCatalogPopulationEvaluated": False,
        "runtimeReadinessClaim": "none; parser/registration behavior is covered by NewEngine runtime tests",
    }

def build_inventory(repo_root: Path, combat_inventory_path: Path,
                    formula_dataset_path: Optional[Path] = None,
                    combat_inventory_descriptor_path: Optional[Path] = None,
                    combat_inventory_sha256: Optional[str] = None,
                    combat_inventory_byte_length: Optional[int] = None) -> dict[str, Any]:
    del formula_dataset_path
    historical = historical_coverage(repo_root)
    canonical = load_json(combat_inventory_path, expected_sha256=combat_inventory_sha256,
                          expected_byte_length=combat_inventory_byte_length)
    descriptor = combat_inventory_descriptor_path or combat_inventory_path
    consumers = discover_current_consumers(repo_root)
    content = editable_content_inventory(repo_root)
    # Historical profiles remain available to formula analysis without promotion
    # into runtime activation. Their old source references retain their meaning
    # only at the recorded source commit.
    document = copy.deepcopy(historical)
    document["schemaVersion"] = 2
    document["generator"] = "tools-temp/AOSharpCaptureAnalyzer/generate_capture_backed_npc_active_coverage.py"
    document["scope"] = "historical capture classification plus independent current NewEngine content-consumer audit"
    document["historicalEvidence"] = {
        "path": HISTORICAL_COVERAGE_PATH, "sha256": HISTORICAL_COVERAGE_SHA256,
        "sourceCommit": HISTORICAL_SOURCE_COMMIT, "immutable": True,
        "currentPopulation": False, "runtimeActivationPermission": False,
        "classificationMeaning": "accepted historical capture evidence; no current spawn or gameplay readiness assertion",
    }
    document["combatInventory"] = {"path": str(descriptor.relative_to(repo_root)).replace("\\", "/"),
        "sha256": sha256_file(descriptor), "schemaVersion": canonical.get("schemaVersion")}
    document["contentInputs"] = sorted(consumers + content["inputs"] + [{"path": HISTORICAL_COVERAGE_PATH,
        "sha256": HISTORICAL_COVERAGE_SHA256, "hashNormalization": "utf8-sig-text-lf"}], key=lambda row: row["path"])
    document["populationContract"]["scope"] = "retired Legacy historical roster only"
    document["populationContract"]["currentRuntimePopulationClaim"] = False
    document["currentNewEngineContent"] = content
    document["runtimePrepareAudit"] = {
        "productionRoot": RUNTIME_SOURCE_ROOT, "legacyRuntimeRetired": True,
        "entryPointFileCount": len(consumers), "entryPointCount": len(RUNTIME_CONSUMERS),
        "entries": consumers, "evidenceBasedActivation": False,
        "auditStrength": "source-contract presence and content hashes; not runtime reachability or gameplay acceptance",
    }
    document["historicalResolverAudits"] = {name: document.pop(name) for name in
        ("pf127OrdinaryProfileResolverAudit", "pf1931CaptureContractResolverAudit")}
    document["historicalIccShuttleportEntryGovernance"] = document["iccShuttleportEntryGovernance"]
    document["iccShuttleportEntryGovernance"] = {"playfield":4582, "acceptedEntries":0,
        "activeEvidenceEntries":0,"blockedUnauditedEntries":0,"entries":[],
        "scope":"retired evidence allowlist; current spawn activation uses editable content"}
    return document

def canonical_json(document: Mapping[str, Any]) -> str:
    return json.dumps(document, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def find_repo_root(start: Path) -> Path:
    current = start.resolve()
    for candidate in (current, *current.parents):
        if (candidate / "AI_START_HERE.md").is_file() and (candidate / ".git").exists():
            return candidate
    raise CoverageError("could not locate AORebirth repository root")


def same_file_or_path(left: Path, right: Path) -> bool:
    if left.resolve() == right.resolve():
        return True
    if os.path.lexists(left) and os.path.lexists(right):
        try:
            return os.path.samefile(left, right)
        except OSError:
            return False
    return False


def enter_governed_read_lease(
    checkout_root: Path, original_arguments: Sequence[str]
) -> int | None:
    delegation_name = "AO_REBIRTH_GENERATED_COMBAT_LEASE_DELEGATION"
    root_name = "AO_REBIRTH_GENERATED_COMBAT_LEASE_REPO_ROOT"
    raw_delegation = os.environ.get(delegation_name)
    raw_root = os.environ.get(root_name)
    if raw_delegation is None and raw_root is None:
        command = [
            sys.executable,
            str(checkout_root / "Tools" / "generated_combat_pipeline.py"),
            "--run-read-lease",
            "--",
            sys.executable,
            str(Path(__file__).resolve()),
            *original_arguments,
        ]
        return subprocess.run(command, cwd=checkout_root, check=False).returncode
    if raw_delegation is None or raw_root is None:
        raise CoverageError("generated-combat lease delegation is incomplete")
    lease_root = Path(raw_root).resolve(strict=True)
    if lease_root != checkout_root.resolve(strict=True):
        raise CoverageError(
            "generated-combat lease delegation belongs to a different checkout"
        )
    sys.path.insert(0, str(lease_root / "Tools"))
    try:
        import generated_artifact_transaction as transaction

        delegation = json.loads(raw_delegation)
        record = transaction.GeneratedArtifactLease.validate_delegation(
            lease_root, delegation
        )
    except Exception as error:
        raise CoverageError("generated-combat lease delegation is invalid") from error
    if record.get("domain") != "capture-backed-npc-combat":
        raise CoverageError("generated-combat lease delegation domain is invalid")
    return None


def main(argv: Optional[Sequence[str]] = None) -> int:
    original_arguments = list(sys.argv[1:] if argv is None else argv)
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write", action="store_true", help="write the generated inventory")
    mode.add_argument("--check", action="store_true", help="verify the checked-in inventory")
    parser.add_argument("--repo-root", type=Path)
    parser.add_argument(
        "--combat-inventory",
        default="docs/generated/capture_backed_npc_combat_inventory.json",
    )
    parser.add_argument("--combat-inventory-descriptor")
    parser.add_argument("--combat-inventory-sha256")
    parser.add_argument("--combat-inventory-byte-length", type=int)
    parser.add_argument(
        "--output",
        default="docs/generated/capture_backed_npc_combat_active_coverage.json",
    )
    parser.add_argument(
        "--formula-dataset",
        default="docs/generated/enemy_combat_setup_formula_dataset.json",
    )
    args = parser.parse_args(original_arguments)

    script_repo_root = find_repo_root(Path(__file__).resolve().parent)
    repo_root = (
        args.repo_root.resolve()
        if args.repo_root is not None
        else script_repo_root
    )
    output_path = (repo_root / args.output).resolve()
    governed_output = (
        script_repo_root
        / "docs"
        / "generated"
        / "capture_backed_npc_combat_active_coverage.json"
    ).resolve()
    if same_file_or_path(output_path, governed_output):
        parser.error(
            "the governed active-coverage artifact must be checked or written "
            "through Tools/generated_combat_pipeline.py"
        )
    combat_inventory_path = repo_path(repo_root, args.combat_inventory)
    combat_inventory_descriptor_path = (
        repo_path(repo_root, args.combat_inventory_descriptor)
        if args.combat_inventory_descriptor
        else None
    )
    formula_dataset_path = repo_path(repo_root, args.formula_dataset)
    governed_inputs = (
        script_repo_root
        / "docs"
        / "generated"
        / "capture_backed_npc_combat_inventory.json",
        script_repo_root
        / "docs"
        / "generated"
        / "enemy_combat_setup_formula_dataset.json",
    )
    if any(
        same_file_or_path(candidate, governed)
        for candidate in (
            combat_inventory_path,
            formula_dataset_path,
            combat_inventory_descriptor_path,
        )
        if candidate is not None
        for governed in governed_inputs
    ):
        delegated_result = enter_governed_read_lease(
            script_repo_root, original_arguments
        )
        if delegated_result is not None:
            return delegated_result
    document = build_inventory(
        repo_root,
        combat_inventory_path,
        formula_dataset_path,
        combat_inventory_descriptor_path,
        args.combat_inventory_sha256,
        args.combat_inventory_byte_length,
    )
    rendered = canonical_json(document)

    if args.write:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(rendered, encoding="utf-8", newline="\n")
        print(
            "WROTE "
            f"{format_generated_output_path(output_path, repo_root)} actors={document['totals']['initialActorCount']} "
            f"certified={document['totals']['certified']} unresolved={document['totals']['unresolved']} "
            f"ICC_ACCEPTED_ENTRIES={document['iccShuttleportEntryGovernance']['acceptedEntries']} "
            f"ICC_ACTIVE_EVIDENCE_ENTRIES={document['iccShuttleportEntryGovernance']['activeEvidenceEntries']} "
            f"ICC_BLOCKED_UNAUDITED_ENTRIES={document['iccShuttleportEntryGovernance']['blockedUnauditedEntries']}"
        )
        return 0

    if not output_path.is_file():
        print(f"ERROR: generated inventory is missing: {output_path}", file=sys.stderr)
        return 1
    existing = output_path.read_text(encoding="utf-8")
    if existing != rendered:
        print(
            "ERROR: active coverage inventory is stale; run this generator with --write",
            file=sys.stderr,
        )
        return 1
    print(
        "PASS "
        f"actors={document['totals']['initialActorCount']} "
        f"certified={document['totals']['certified']} unresolved={document['totals']['unresolved']} "
        f"ICC_ACCEPTED_ENTRIES={document['iccShuttleportEntryGovernance']['acceptedEntries']} "
        f"ICC_ACTIVE_EVIDENCE_ENTRIES={document['iccShuttleportEntryGovernance']['activeEvidenceEntries']} "
        f"ICC_BLOCKED_UNAUDITED_ENTRIES={document['iccShuttleportEntryGovernance']['blockedUnauditedEntries']}"
    )
    return 0


def format_generated_output_path(output_path: Path, repo_root: Path) -> str:
    """Render diagnostics without assuming staging is inside the worktree."""
    try:
        return output_path.relative_to(repo_root).as_posix()
    except ValueError:
        return "<external-staging>/" + output_path.name


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except CoverageError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
