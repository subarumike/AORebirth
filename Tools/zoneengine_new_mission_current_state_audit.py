"""Pinned-source comparisons and deterministic publication of a reviewed static audit.

Semantic findings are human-reviewed JSON records, not inferred by this script.
No engine, test suite, database, capture, or network service is executed.
"""
import argparse
import hashlib
import json
import pathlib
import re
import struct
import subprocess
from functools import lru_cache

ROOT = pathlib.Path(__file__).resolve().parents[1]
ENGINE_SHA = "bddea56ca2d47c5eb64ed53f6e9efd279ff4f694"
CATALOG_SHA = "f07bb3c1a99218433e7d459c95ba29ccb22c36b2"
OUTPUT = ROOT / "docs/generated/missions/zoneengine-new-current-state-audit"
ENTRANCES = "AORebirth/GameData/MissionEntrances.json"
POOL = "AORebirth/Server/ZoneEngine_New/Core/Missions/Content/MissionLocationPool.cs"
PLACEMENTS = "AORebirth/GameData/Missions/Destinations/MissionEntrancePlacements.json"
OBSERVATIONS = "AORebirth/GameData/Missions/Destinations/ObservedMissionDestinations.json"
NAMES = (
    "component-inventory", "mission-pipeline-map", "current-entrance-catalog-comparison",
    "current-destination-selection", "mission-roll-policy-audit", "ql-system-audit",
    "mission-type-capability-matrix", "entrance-instance-flow", "generated-dungeon-flow",
    "persistence-table-map", "csharp-content-findings", "mission-test-inventory",
    "component-disposition", "integration-roadmap",
)
TERMS = (
    "Mission", "GeneratedMission", "MissionTerminal", "MissionRoll", "MissionOffer",
    "MissionDestination", "MissionEntrance", "QuestDungeon", "MissionPlayfield", "Dungeon",
    "WorldPos", "ACG", "CreateQuest", "DeleteQuest", "MissionKey", "MissionObjective",
    "MissionReward", "MissionExpiry", "MissionCorpse", "MissionDoor", "MissionChest", "Quest",
)
SCOPES = (
    "AORebirth/Server/ZoneEngine_New/",
    "AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Missions/",
    "AORebirth/Libraries/Source/AORebirth.Database/Domain/Missions/",
)
REPORTS = (
    "ACGENTRANCE_REGISTRY_AND_MISSION_DESTINATION_RECONSTRUCTION",
    "MISSION_DESTINATION_ELIGIBILITY_FROM_RESOLVED_CAPTURE_CORPUS",
    "MISSION_DESTINATION_DUPLICATE_AUDIT", "MISSION_DESTINATION_CATALOG_FOUNDATION",
    "ZONEENGINE_NEW_MISSIONS_RECONCILIATION", "ZONEENGINE_NEW_MISSION_GAP_CLOSURE",
    "ZONEENGINE_NEW_GAMEPLAY_CHECKPOINT_20260908",
)
DISPOSITIONS = frozenset((
    "KEEP_AS_IS", "KEEP_AND_CONNECT_NEW_DESTINATION_CATALOG",
    "KEEP_WITH_SMALL_EVIDENCE_CORRECTION", "KEEP_BUT_REPLACE_DATA_SOURCE",
    "REFACTOR_LATER", "DEFER_PENDING_EVIDENCE", "REMOVE_OBSOLETE_DUPLICATE",
    "TEST_ONLY", "UNKNOWN_NEEDS_FOLLOWUP",
))
CAPABILITIES = frozenset((
    "ROLL_GENERATION", "WIRE_OFFER", "ACCEPTANCE", "PERSISTENCE", "DUNGEON_CONTENT",
    "OBJECTIVE", "REWARD", "RECONNECT", "RESTART", "EXPIRY", "CLEANUP",
))
CAPABILITY_STATUSES = frozenset((
    "COMPLETE_ACTIVE", "PARTIAL_ACTIVE", "TEST_ONLY", "PLACEHOLDER", "UNREACHABLE",
    "MISSING", "UNKNOWN",
))
MISSION_TYPES = frozenset(("Find Item", "Find Person", "Kill Person", "Repair", "Return Item"))
PIPELINE_FIELDS = frozenset((
    "file", "class", "method", "input", "output", "data_source", "persistence_owner",
    "tests", "runtime_reachability",
))


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, check=True, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE).stdout


@lru_cache(maxsize=None)
def source(path, sha=ENGINE_SHA):
    validate_source_reference(path, sha)
    return git("show", sha + ":" + path)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_source_reference(path, sha):
    require(isinstance(path, str) and bool(path) and "\\" not in path
            and ":" not in path and not pathlib.PurePosixPath(path).is_absolute()
            and ".." not in pathlib.PurePosixPath(path).parts,
            "Source path must be repository-relative: " + str(path))
    require(sha in (ENGINE_SHA, CATALOG_SHA), "Unpinned source SHA: " + str(sha))


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def encode(value):
    return (json.dumps(value, sort_keys=True, ensure_ascii=True, indent=2, allow_nan=False) + "\n").encode()


def bits(value):
    return struct.unpack("<I", struct.pack("<f", value))[0]


def identity(row):
    return row["IdentityType"], row["IdentityInstance"]


def placement_key(row):
    return row["PlayfieldId"], *(row["Local" + axis + "Bits"] for axis in "XYZ")


def current_key(row):
    return row["Playfield"], *(bits(row[axis]) for axis in "XYZ")


def current_identity(row):
    # The engine fixes type to MissionEntrance (0xDAC6); signed Instance carries the same 32 bits.
    return 0xDAC6, row["Instance"] & 0xFFFFFFFF


def compare(configured):
    public_bytes = source(ENTRANCES)
    public = json.loads(public_bytes)
    configured_bytes = configured.read_bytes()
    configured_document = json.loads(configured_bytes)
    if configured_document != public:
        raise ValueError("Configured entrance file differs from this audit's public sample; review before publication")
    full = json.loads(source(PLACEMENTS, CATALOG_SHA))["Placements"]
    observed_rows = json.loads(source(OBSERVATIONS, CATALOG_SHA))["Destinations"]
    full_ids = [identity(r) for r in full]
    full_coordinates = [placement_key(r) for r in full]
    observed_ids = [identity(r) for r in observed_rows]
    require(len(full) == 2242 and len(set(full_ids)) == 2242,
            "Catalog must contain2242 unique complete placement identities")
    require(len(set(full_coordinates)) == 2242,
            "Catalog contains an exact playfield/XYZ-bit collision")
    require(len(observed_rows) == 812 and len(set(observed_ids)) == 812,
            "Observed catalog must contain812 unique complete identities")
    require(set(observed_ids).issubset(full_ids), "Observed identity is absent from full catalog")
    for row in full:
        require(all(type(value) is int and 0 <= value <= 0xFFFFFFFF for value in identity(row)),
                "Placement identity is not a complete uint32 pair")
        require(all(bits(row["Local" + axis]) == row["Local" + axis + "Bits"] for axis in "XYZ"),
                "Placement coordinate value/bit disagreement")
    observed = set(observed_ids)
    by_id = {identity(r): r for r in full}
    by_coordinate = {placement_key(r): r for r in full}
    rows = public["Entrances"]
    accepted, skipped = {}, []
    for index, row in enumerate(rows):
        key = current_identity(row)
        if row["Playfield"] <= 0 or not row["Name"].strip() or key in accepted:
            skipped.append(index)
        else:
            accepted[key] = row
    overlap = set(accepted) & set(by_id)
    comparisons = []
    for key in sorted(overlap):
        old, new = accepted[key], by_id[key]
        same_coordinate = current_key(old) == placement_key(new)
        current_rotation = [bits(old["Heading" + axis]) for axis in "XYZW"]
        proven_rotation = [bits(new["RotationComponent" + str(i)]) for i in range(4)]
        comparisons.append({
            "identity_type": key[0], "identity_instance": key[1],
            "current_instance_signed": old["Instance"], "current": old,
            "proven": {k: new[k] for k in ("PlayfieldId", "LocalX", "LocalY", "LocalZ", "LocalXBits", "LocalYBits", "LocalZBits", "DisplayName")},
            "current_coordinate_bits": list(current_key(old)[1:]),
            "current_rotation_bits_xyzw": current_rotation,
            "proven_raw_rotation_component_bits_0_to_3": proven_rotation,
            "same_playfield": old["Playfield"] == new["PlayfieldId"],
            "exact_coordinate_match": same_coordinate,
            "raw_rotation_component_match": current_rotation == proven_rotation,
            "exact_name_match": old["Name"] == new["DisplayName"],
        })
    pool_text = source(POOL).decode("utf-8-sig")
    pool_rows = []
    for match in re.finditer(r"new Spot\s*\{([^{}]+)\}", pool_text):
        fields = {}
        for name, literal in re.findall(r"(\w+)\s*=\s*([-+\d.eE]+[Ff]?)", match.group(1)):
            fields[name] = float(literal.rstrip("fF")) if name in "XYZ" else int(literal)
        if set(fields) != {"Playfield", "X", "Y", "Z", "EntranceLow", "EntranceHigh"}:
            raise ValueError("Unparsed destination initializer")
        exact = by_coordinate.get(current_key(fields))
        pool_rows.append({
            "row_index": len(pool_rows), "source_line": pool_text.count("\n", 0, match.start()) + 1,
            "source_values": fields, "coordinate_bits": list(current_key(fields)[1:]),
            "exact_catalog_identity": list(identity(exact)) if exact else None,
            "in_observed_812_by_exact_key": identity(exact) in observed if exact else False,
        })
    if not pool_rows:
        raise ValueError("No current source destination initializers parsed")
    require(len(pool_rows) == len(re.findall(r"\bnew\s+Spot\s*\{", pool_text)),
            "Destination initializer coverage is incomplete")
    full_matches = sum(r["exact_catalog_identity"] is not None for r in pool_rows)
    observed_matches = sum(r["in_observed_812_by_exact_key"] for r in pool_rows)
    return {
        "schema_version": 1, "engine_source_sha": ENGINE_SHA, "catalog_source_sha": CATALOG_SHA,
        "evidence_status": "PROVEN_STATIC_DATA_COMPARISON",
        "current_entrance_source": ENTRANCES,
        "configured_external_source": {"logical_name": "SELECTED_GAMEDATA/MissionEntrances.json",
            "sha256": sha256(configured_bytes), "json_equal_to_public_source": True,
            "byte_equal_to_public_git_blob": configured_bytes == public_bytes,
            "private_payload_published": False},
        "public_source_git_blob_sha256": sha256(public_bytes),
        "identity_comparison_rule": "Implicit fixed MissionEntrance type 56006 + exact 32-bit Instance; signed source representation is retained",
        "rotation_comparison_rule": "Raw slot order HeadingX/Y/Z/W vs RotationComponent0/1/2/3; no normalization or inferred convention conversion",
        "rotation_semantic_limit": "ROTATION_MISMATCHES counts raw slot-value bit disagreements only. The complete client quaternion convention is unproven; this does not prove a different physical orientation or authorize a rotation correction.",
        "counts": {
            "CURRENT_ENGINE_ENTRANCES": len(accepted), "CURRENT_SOURCE_ROWS": len(rows),
            "CURRENT_SKIPPED_ROWS": len(skipped), "PROVEN_CATALOG_ENTRANCES": len(full),
            "EXACT_IDENTITY_MATCHES": len(overlap), "CURRENT_ONLY": len(set(accepted) - set(by_id)),
            "PROVEN_ONLY": len(set(by_id) - set(accepted)),
            "EXACT_COORDINATE_MATCHES": sum(r["exact_coordinate_match"] for r in comparisons),
            "COORDINATE_MISMATCHES": sum(not r["exact_coordinate_match"] for r in comparisons),
            "ROTATION_MISMATCHES": sum(not r["raw_rotation_component_match"] for r in comparisons),
            "NAME_ONLY_DIFFERENCES": sum(not r["exact_name_match"] and r["exact_coordinate_match"] and r["raw_rotation_component_match"] for r in comparisons),
            "IDENTITY_CONFLICTS": len(skipped),
            "PLAYFIELD_MISMATCHES": sum(not r["same_playfield"] for r in comparisons),
            "NAME_DIFFERENCES": sum(not r["exact_name_match"] for r in comparisons),
        },
        "identity_conflict_definition": "Duplicate current complete keys, invalid rows or duplicate proven keys; transform disagreements are separately counted, not normalized away",
        "identity_matches": comparisons,
        "current_only_identities": [list(k) for k in sorted(set(accepted) - set(by_id))],
        "proven_only_identities": [list(k) for k in sorted(set(by_id) - set(accepted))],
        "selection_pool": {
            "source": POOL, "contains_complete_placement_identities": False,
            "membership_rule": "PF and all three binary32 coordinate bits only; missing match means unresolvable under proven exact rule, not invalid destination",
            "CURRENT_DESTINATION_POOL_SIZE": len(pool_rows),
            "CURRENT_POOL_IN_OBSERVED_812": observed_matches,
            "CURRENT_POOL_OUTSIDE_OBSERVED_812": len(pool_rows) - observed_matches,
            "CURRENT_POOL_IN_FULL_2242": full_matches,
            "CURRENT_POOL_NOT_IN_FULL_2242": len(pool_rows) - full_matches,
            "pool_playfields": sorted({r["source_values"]["Playfield"] for r in pool_rows}),
            "current_catalog_playfields": sorted({r["Playfield"] for r in accepted.values()}),
            "rows": pool_rows,
        },
        "limitations": [
            "Counts describe audited source and supplied data, not a live process observation.",
            "Current JSON decimal values are compared as their actual engine binary32 values, with no tolerance.",
            "No approximate match or name match promotes placement membership.",
            "812 is observed coverage, not a complete eligible pool; unmatched rounded points are not declared invalid.",
            "No operational stat BD or entrance registry key is resolved by this comparison.",
            "Raw rotation mismatch is not a proven semantic orientation mismatch; the complete client quaternion convention remains unresolved.",
        ],
    }


def source_inventory():
    """Discover by pinned file CONTENT, not filenames; never scans retired engine source."""
    command = ["git", "grep", "-z", "-l", "-I", "-i", "-F"]
    for term in TERMS:
        command.extend(("-e", term))
    command.extend((ENGINE_SHA, "--", *SCOPES))
    found = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    require(found.returncode in (0, 1), "Pinned content discovery failed: " + found.stderr.decode("utf-8", "replace"))
    prefix = ENGINE_SHA + ":"
    paths = []
    for item in found.stdout.split(b"\0"):
        if not item:
            continue
        qualified = item.decode("utf-8")
        require(qualified.startswith(prefix), "Unexpected pinned content-search path")
        path = qualified[len(prefix):]
        require(any(path.startswith(scope) for scope in SCOPES), "Content search escaped authorized scopes")
        paths.append(path)
    matches = []
    for path in sorted(set(paths)):
        data = source(path)
        # ASCII protocol/source terms have unambiguous matching even when surrounding
        # operator text uses a non-UTF8 encoding. No source text is copied into reports.
        text = data.decode("utf-8", "replace")
        lines = []
        terms = set()
        for number, line in enumerate(text.splitlines(), 1):
            folded = line.casefold()
            hit_terms = [term for term in TERMS if term.casefold() in folded]
            if hit_terms:
                lines.append({"line": number, "matched_terms": hit_terms})
                terms.update(hit_terms)
        require(bool(lines), "Discovered content hit has no matching source line: " + path)
        matches.append({"path": path, "source_sha": ENGINE_SHA, "sha256": sha256(data),
                        "matched_terms": sorted(terms), "lines": lines,
                        "semantic_status": "CONTENT_HIT_REQUIRES_REVIEW_NOT_RUNTIME_REACHABILITY"})
    return {"source_sha": ENGINE_SHA, "terms": list(TERMS), "scopes": list(SCOPES),
            "matching": "Case-insensitive literal substring; tracked pinned text content; binary files excluded",
            "file_count": len(matches), "matched_line_count": sum(len(row["lines"]) for row in matches),
            "matches": matches,
            "limitation": "Complete requested term-search inventory within the three scopes; a hit is not proof of mission ownership, runtime reachability or correctness."}


def component_rows(document):
    return document.get("components", document.get("dispositions", []))


def validate_reviewed(reviewed):
    """Validate report structure/boundaries, never turn source review into runtime PASS."""
    components = reviewed["component-inventory"].get("components", [])
    require(bool(components), "Component inventory is empty")
    ids = [row.get("id") for row in components]
    require(all(isinstance(value, str) and value for value in ids) and len(set(ids)) == len(ids),
            "Component IDs must be nonempty and unique")
    for row in components:
        require(row.get("disposition") in DISPOSITIONS,
                "Component requires exactly one allowed disposition: " + str(row.get("id")))
    dispositions = component_rows(reviewed["component-disposition"])
    require(bool(dispositions), "Disposition inventory is empty")
    disposition_ids = [row.get("id") for row in dispositions]
    require(len(set(disposition_ids)) == len(disposition_ids) and set(disposition_ids) == set(ids),
            "Disposition inventory does not cover every component exactly once")
    expected = {row["id"]: row["disposition"] for row in components}
    for row in dispositions:
        require(row.get("disposition") in DISPOSITIONS and row["disposition"] == expected[row["id"]],
                "Conflicting component disposition: " + str(row.get("id")))
    stages = reviewed["mission-pipeline-map"].get("stages", [])
    require(bool(stages), "Mission pipeline stages are empty")
    for row in stages:
        require(PIPELINE_FIELDS.issubset(row), "Missing required pipeline fields: " + str(row.get("id")))
        require(all(row[name] is not None for name in PIPELINE_FIELDS),
                "Null pipeline field; use explicit NONE/UNKNOWN where justified: " + str(row.get("id")))
    matrix = reviewed["mission-type-capability-matrix"].get("types", [])
    require(len(matrix) == 5 and {row.get("name") for row in matrix} == MISSION_TYPES,
            "Capability matrix must contain exactly the five requested mission types")
    for row in matrix:
        capabilities = row.get("capabilities", {})
        require(set(capabilities) == CAPABILITIES,
                "Capability matrix must contain11 requested cells per type: " + row["name"])
        for stage, cell in capabilities.items():
            status = cell.get("status") if isinstance(cell, dict) else cell
            require(status in CAPABILITY_STATUSES, "Invalid capability status: " + row["name"] + "/" + stage)
    test_inventory = reviewed["mission-test-inventory"]
    tests = test_inventory.get("tests", [])
    require(bool(tests), "Mission test inventory is empty")
    if "test_count" in test_inventory:
        require(test_inventory["test_count"] == len(tests), "Test declaration count mismatch")
    for row in tests:
        require(str(row.get("current_status", "")).startswith("NOT_RUN"),
                "Audit test status must remain NOT_RUN: " + str(row.get("test")))
    selection = reviewed["current-destination-selection"]
    comparison = reviewed["current-entrance-catalog-comparison"]["selection_pool"]
    for key in ("CURRENT_DESTINATION_POOL_SIZE", "CURRENT_POOL_IN_OBSERVED_812",
                "CURRENT_POOL_OUTSIDE_OBSERVED_812", "CURRENT_POOL_IN_FULL_2242", "CURRENT_POOL_NOT_IN_FULL_2242"):
        require(selection.get(key.lower()) == comparison[key], "Reviewed selection/comparison count mismatch: " + key)
    for name, document in reviewed.items():
        for field in ("runtime_executed", "tests_executed"):
            if field in document:
                require(document[field] is False, "Audit cannot claim runtime/test execution: " + name)


def inspected_sources(reviewed):
    """Honor each pinned authority; the catalog/evidence tests do not live at ENGINE_SHA."""
    references = set()

    def add(item, default_sha=ENGINE_SHA):
        if isinstance(item, str):
            path, sha = item, default_sha
        else:
            path = item.get("path", item.get("file"))
            sha = item.get("source_sha", item.get("source_commit", default_sha))
        require(path is not None, "Inspected source record has no path")
        validate_source_reference(path, sha)
        references.add((path, sha))

    inventory = reviewed["component-inventory"]
    for field in ("inspected_files", "source_files"):
        for item in inventory.get(field, []):
            add(item)
    for row in inventory.get("content_search", {}).get("matches", []):
        add(row)
    for row in inventory["components"]:
        if row.get("file"):
            add(row)
    test_inventory = reviewed["mission-test-inventory"]
    test_paths = set()
    for row in test_inventory.get("tests", []):
        reference = row.get("reference", {})
        path = reference.get("file", row.get("file"))
        if path:
            test_paths.add(path)
            add({"path": path, "source_sha": reference.get("source_sha", reference.get("source_commit",
                row.get("source_sha", row.get("source_commit", ENGINE_SHA))))})
    # Older inventories use a bare list here. The per-test pin wins for any path
    # that also appears in that list; otherwise its explicit pin/default applies.
    for item in test_inventory.get("test_files_inspected", []):
        if isinstance(item, str) and item in test_paths:
            continue
        add(item)
    for path in (ENTRANCES, POOL):
        add(path)
    for index, report in enumerate(REPORTS):
        add({"path": "docs/evidence/" + report + ".md", "source_sha": CATALOG_SHA if index < 4 else ENGINE_SHA})
    for path in (PLACEMENTS, OBSERVATIONS, "AORebirth/GameData/Missions/Destinations/MissionDestinationCatalogManifest.json"):
        add({"path": path, "source_sha": CATALOG_SHA})
    return [{"path": path, "source_sha": sha, "sha256": sha256(source(path, sha))}
            for path, sha in sorted(references)]


def write(path, value, check):
    content = encode(value)
    if check:
        if not path.exists() or path.read_bytes() != content:
            raise ValueError("STALE_ARTIFACT: " + path.relative_to(ROOT).as_posix())
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("compare", "generate"))
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--configured-entrances", type=pathlib.Path, required=True)
    args = parser.parse_args()
    result = compare(args.configured_entrances)
    if args.command == "compare":
        write(OUTPUT / "current-entrance-catalog-comparison.json", result, args.check)
        print(json.dumps({"status": "PASS", **result["counts"],
                          "CURRENT_DESTINATION_POOL_SIZE": result["selection_pool"]["CURRENT_DESTINATION_POOL_SIZE"],
                          "EXACT_POOL_MATCHES": result["selection_pool"]["CURRENT_POOL_IN_FULL_2242"]}, sort_keys=True))
        return
    reviewed = {}
    for name in NAMES:
        path = OUTPUT / (name + ".json")
        reviewed[name] = result if name == "current-entrance-catalog-comparison" else json.loads(path.read_bytes())
    reviewed["component-inventory"]["content_search"] = source_inventory()
    validate_reviewed(reviewed)
    report_path = ROOT / "docs/evidence/ZONEENGINE_NEW_MISSION_CURRENT_STATE_AUDIT.md"
    require(report_path.is_file(), "Reviewed audit report must exist before publication")
    manifest = {
        "schema_version": 1, "start_sha": CATALOG_SHA, "engine_source_sha": ENGINE_SHA,
        "branch": "codex/zoneengine-new-mission-current-state-audit",
        "semantic_findings": "Reviewed source audit records; canonicalization does not automatically prove their interpretation",
        "comparison_algorithm": "Exact identity bits and PF/XYZ binary32; no tolerance or name fallback",
        "runtime_execution": "NOT_RUN", "test_suites": "NOT_RUN", "database_access": "NONE",
        "outputs": [{"path": (OUTPUT / (name + ".json")).relative_to(ROOT).as_posix(),
                     "sha256": sha256(encode(reviewed[name]))} for name in sorted(NAMES)],
        "inputs": inspected_sources(reviewed),
        "configured_entrance_observation": result["configured_external_source"],
        "tool": {"path": pathlib.Path(__file__).relative_to(ROOT).as_posix(),
                 "sha256": sha256(pathlib.Path(__file__).read_bytes())},
    }
    manifest["report"] = {"path": report_path.relative_to(ROOT).as_posix(), "sha256": sha256(report_path.read_bytes())}
    for name in NAMES:
        write(OUTPUT / (name + ".json"), reviewed[name], args.check)
    write(OUTPUT / "audit-manifest.json", manifest, args.check)
    print("PASS: deterministic audit artifacts, exact comparisons and pinned source hashes")


if __name__ == "__main__":
    main()
