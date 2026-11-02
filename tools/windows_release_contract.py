"""Fail-closed Windows release intake, canonical reconciliation and human acceptance.

Private role bindings belong in the ignored local profile, never in this file.
The existing build wrapper owns compilation; this module never builds an engine.
"""
from __future__ import annotations

import argparse
import base64
import ctypes
import hashlib
import json
import ntpath
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import struct
import subprocess
import sys
from datetime import datetime, timezone
import uuid
import xml.etree.ElementTree as ET

PUBLIC_REPOSITORY = "subarumike/AORebirth"
TUPLE = ("PUBLIC_MASTER_SHA", "CONTENT_DUMP_SHA", "WINDOWS_GAMEDATA_MANIFEST")
OUTPUTS = (
    "AORebirth/Built/Debug/ChatEngine.exe", "AORebirth/Built/Debug/ChatEngine.dll",
    "AORebirth/Built/Debug/LoginEngine.exe", "AORebirth/Built/Debug/LoginEngine.dll",
    "AORebirth/Built/Debug/WorldEngine/WorldEngine.exe", "AORebirth/Built/Debug/WorldEngine/WorldEngine.dll",
    "AORebirth/Built/Debug/ZoneEngine_New/ZoneEngine_New.exe", "AORebirth/Built/Debug/ZoneEngine_New/ZoneEngine_New.dll",
)
PRODUCT_PATHS = ("AORebirth/Server", "AORebirth/Libraries", "AORebirth/Config", "WindowsBuildNet10",
                 "NewZoneEngineBuild", "Tools/DatabaseMigrationTool", "Directory.Build.targets", "tools/windows_release_contract.py",
                 "tools/windows_release_contract.cmd", "tools/build_aorebirth_debug.cmd", "start-engines.cmd", "start-engines.ps1")
CONFIG_OVERRIDES = {"AORebirth/Config/Config.xml", "AORebirth/Config/ChatEngine.Config.xml"}
PRIVATE_CHEST_CATALOGS = {"Loot/MissionChestRKAllowlist.json", "Loot/MissionChestSeasonalExclusions.json", "Loot/MissionChestSpecialExclusions.json"}
# Named catalog loaders and GameDataPaths; no extension-only root admission.
RUNTIME_ROOT_CATALOGS = frozenset("""AbilityCosts.json AlienXp.json ComboNames.json
DungeonDoorStyles.json DungeonEntrances.json DungeonGeneration.json DynelCollision.json GoverningForms.json
ItemBehavior.json items.dat ItemTemplates.json MonsterCombatRules.json MonsterData.json
MonsterWeapons.json nanos.dat NewCharacter.json NpcTemplates.json PerkActions.json
Perks.json PetReplies.json PetTypes.json Quests.json QuestRewards.json Research.json Respawn.json
SkillCosts.json SkillTrickle.json SpawnExclusions.json StartPackages.json TeleportRoutes.json Tradeskills.json VendingMachines.json
WaterExits.json Xp.json""".split())
RUNTIME_MISSION_FILES = frozenset("""Locations.json Text.json RollPolicy.json RollMixes.json
RollTemplate.json RollBodies.json RewardObservations.json RareLoot.json RareLootChancePercent.json
Source/MissionLevels.csv Source/MissionLevelRules.json Destinations/MissionDestinations.json
Destinations/MissionEntrancePlacements.json""".split())
RUNTIME_PLAYFIELD_FILES = frozenset("""metadata.json Districts.json Rooms.json Spawns.json
Collision.dat Surfaces.dat Dynels.dat Walls.dat Doors.dat Destinations.dat Water.dat Navmesh.dat
GNDA.png DCGA.png""".split())
NON_RUNTIME_DIRECTORIES = frozenset(""".git .local cache caches tmp temp temporary bin obj build
build-verify logs reports screenshots backups research exports""".split())
MISSING = object()
EXPORTER_PROJECT = "Tools/RDBDataExtractor/RDBDataExtractor.csproj"
EXPORTER_OUTPUT = "Tools/RDBDataExtractor/bin/Release/net10.0"
EXPORTER_BUILD_ARGS = ["build", EXPORTER_PROJECT, "-c", "Release", "--no-incremental", "--nologo", "/m:1", "/nr:false"]
EXPORTER_SKIP_ARGS = ["--overwrite", "--skip-playfields", "--skip-monster-data", "--skip-dynel-collision", "--skip-text"]


class ContentConflict(ValueError):
    def __init__(self, pointer, hashes):
        super().__init__("Unresolved private-content conflict at " + (pointer or "/"))
        self.pointer, self.hashes = pointer, hashes


def now():
    return datetime.now(timezone.utc).isoformat()


def timestamp(value):
    if not isinstance(value, str):
        raise ValueError("Measured UTC timestamp required")
    result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.tzinfo is None:
        raise ValueError("Timestamp must identify its timezone")
    return result.astimezone(timezone.utc)


def encoded(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def blob(data):
    return hashlib.sha1(f"blob {len(data)}\0".encode() + data).hexdigest()


def pairs(items):
    result = {}
    for key, value in items:
        if key in result:
            raise ValueError("Duplicate JSON key")
        result[key] = value
    return result


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"), object_pairs_hook=pairs)


def write_json(path, value, exclusive=False):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb" if exclusive else "wb") as stream:
        stream.write(encoded(value))


def relative(value):
    if not isinstance(value, str) or "\\" in value or ":" in value:
        raise ValueError("Evidence path must be portable and relative")
    path = PurePosixPath(value)
    if not value or path.is_absolute() or any(x in ("", ".", "..") for x in value.split("/")):
        raise ValueError("Evidence path traversal rejected")
    return path


def runtime_content_path(value):
    """Positive current loader paths; an unknown path is denied, not proven unused."""
    parts = relative(value).parts
    if (any(part.casefold() in NON_RUNTIME_DIRECTORIES for part in parts[:-1])
            or parts[-1].casefold().endswith((".tmp", ".bak", ".log", ".md"))):
        return False
    if value in RUNTIME_ROOT_CATALOGS or value in PRIVATE_CHEST_CATALOGS or value == "Teams/LevelEligibility.json":
        return True
    if parts[0] == "Missions":
        return "/".join(parts[1:]) in RUNTIME_MISSION_FILES
    if parts[0] in ("Knubot", "NpcScripts"):
        return len(parts) >= 2 and parts[-1].endswith(".json")
    if len(parts) == 3 and re.fullmatch(r"[1-9][0-9]*", parts[1]):
        if parts[0] == "Playfields":
            return parts[2] in RUNTIME_PLAYFIELD_FILES
        if parts[0] == "PlayfieldContent":
            return parts[2] in {"Npcs.json", "CharacterAppearance.json", "ExitProxyDoors.json"}
    return False


def checked(root, value, exists=True):
    root = Path(root).resolve(strict=True)
    path = root.joinpath(*relative(value).parts)
    current = root
    for part in relative(value).parts:
        current = current / part
        if current.is_symlink() or getattr(current, "is_junction", lambda: False)():
            raise ValueError("Evidence symlink/junction rejected")
    resolved = path.resolve(strict=exists)
    if not resolved.is_relative_to(root):
        raise ValueError("Evidence escaped its bundle")
    return resolved


def descriptor(bundle, path):
    path = Path(path)
    return {"PATH": path.relative_to(bundle).as_posix(), "SHA256": digest(path.read_bytes())}


def load_descriptor(bundle, item):
    if not isinstance(item, dict) or set(item) != {"PATH", "SHA256"}:
        raise ValueError("Hash-bound descriptor required")
    path = checked(bundle, item["PATH"])
    if not path.is_file() or digest(path.read_bytes()) != item["SHA256"]:
        raise ValueError("Evidence digest mismatch")
    return path


def command(args, cwd=None, role="command"):
    result = subprocess.run(args, cwd=cwd, capture_output=True, check=False)
    if result.returncode:
        # Git/GitHub diagnostics can contain private URLs or credentials.
        raise ValueError(role + " failed")
    return result.stdout


def git(root, *args):
    return command(["git", "-C", str(root), *args], role="Git operation")


def gh(profile, endpoint, raw=False):
    headers = ["-H", "Cache-Control: no-cache"]
    if raw:
        headers += ["-H", "Accept: application/vnd.github.raw+json"]
    data = command(["gh", "api", *headers, "repos/" + profile["PRIVATE_CONTENT_REPOSITORY"] + "/" + endpoint],
                   role="Private content authority fetch")
    return data if raw else json.loads(data)


def file_state(path):
    info = path.stat(follow_symlinks=False)
    return [info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns, info.st_ctime_ns]


def content_states(root):
    root = Path(root)
    if root.is_symlink() or getattr(root, "is_junction", lambda: False)() or not root.is_dir():
        raise ValueError("Canonical GameData must be one real directory")
    files, directories = {}, {"": file_state(root)}
    for directory, dirs, names in os.walk(root, followlinks=False):
        for name in dirs:
            child = Path(directory) / name
            if child.is_symlink() or getattr(child, "is_junction", lambda: False)():
                raise ValueError("Canonical GameData directory link rejected")
            directories[child.relative_to(root).as_posix()] = file_state(child)
        for name in names:
            path = Path(directory) / name
            if not stat.S_ISREG(path.lstat().st_mode):
                raise ValueError("Canonical GameData special file rejected")
            files[path.relative_to(root).as_posix()] = file_state(path)
    return files, directories


def inventory(root):
    root = Path(root)
    before_files, before_dirs = content_states(root)
    rows, states = [], {}
    for rel, before in sorted(before_files.items()):
        path = root / rel
        if file_state(path) != before:
            raise ValueError("Canonical GameData changed before measurement")
        sha, git_blob = hashlib.sha256(), hashlib.sha1(f"blob {before[2]}\0".encode())
        with path.open("rb") as stream:
            first = True
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                if first and block.startswith(b"version https://git-lfs.github.com/spec/v1"):
                    raise ValueError("LFS pointer is not canonical content")
                first = False
                sha.update(block)
                git_blob.update(block)
        if file_state(path) != before:
            raise ValueError("Canonical GameData changed during measurement")
        rows.append({"path": rel, "size": before[2], "sha256": sha.hexdigest(), "blob": git_blob.hexdigest()})
        states[rel] = before
    if content_states(root) != (before_files, before_dirs):
        raise ValueError("Canonical path set/directory state changed during measurement")
    return sorted(rows, key=lambda row: row["path"]), states


def validate_manifest(rows):
    if not isinstance(rows, list) or not rows:
        raise ValueError("Complete canonical manifest required")
    names = []
    for row in rows:
        if set(row) != {"path", "size", "sha256", "blob"}:
            raise ValueError("Canonical manifest row schema differs")
        relative(row["path"])
        if (type(row["size"]) is not int or row["size"] < 0
                or not re.fullmatch("[0-9a-f]{64}", row["sha256"])
                or not re.fullmatch("[0-9a-f]{40}", row["blob"])):
            raise ValueError("Invalid canonical manifest identity")
        names.append(row["path"])
    if names != sorted(set(names)):
        raise ValueError("Canonical manifest duplicate or unordered paths")
    return digest(encoded(rows))


def profile_for(root, selected=None):
    root = Path(root).resolve(strict=True)
    selected = selected or os.environ.get("AO_REBIRTH_RELEASE_CONFIG") or root / ".local/release-contract.json"
    selected = Path(selected).resolve(strict=True)
    if not selected.is_relative_to(root) or git(root, "check-ignore", "--", str(selected)).strip() == b"":
        raise ValueError("Private role profile must be Git-ignored inside the product checkout")
    profile = read_json(selected)
    required = ("PRIVATE_CONTENT_REPOSITORY", "PRIVATE_CONTENT_BRANCH", "PRIVATE_CONTENT_PREFIX",
                "CANONICAL_GAMEDATA_ROOT", "INITIAL_PROVENANCE", "INITIAL_PROVENANCE_SHA256", "STATE_ROOT")
    if profile.get("FORMAT_VERSION") != 1 or any(not profile.get(key) for key in required):
        raise ValueError("Private role bindings are incomplete")
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", profile["PRIVATE_CONTENT_REPOSITORY"]):
        raise ValueError("Private content authority binding invalid")
    state = Path(profile["STATE_ROOT"]).resolve()
    if not state.is_relative_to(root) or git(root, "check-ignore", "--", str(state / "probe.json")).strip() == b"":
        raise ValueError("Release state must remain Git-ignored")
    profile["STATE_ROOT"] = str(state)
    if digest(Path(profile["INITIAL_PROVENANCE"]).read_bytes()) != profile["INITIAL_PROVENANCE_SHA256"]:
        raise ValueError("Initial approved canonical provenance was changed")
    return profile


def source_exact(root, public_sha, overrides=()):
    if git(root, "rev-parse", "HEAD").decode().strip() != public_sha:
        raise ValueError("LOCAL_WINDOWS_SOURCE_SHA differs from PUBLIC_MASTER_SHA")
    if set(overrides) - CONFIG_OVERRIDES:
        raise ValueError("Operational configuration allowlist cannot permit product drift")
    changed = git(root, "diff", "--name-only", public_sha, "--", *PRODUCT_PATHS).decode().splitlines()
    untracked = git(root, "ls-files", "--others", "--exclude-standard", "--", *PRODUCT_PATHS).decode().splitlines()
    if any(path not in overrides for path in changed) or untracked:
        raise ValueError("Local Windows runtime/build source differs from public master")


def fresh_sources(root, profile):
    remote = profile.get("PUBLIC_PRODUCT_REMOTE", "origin")
    url = git(root, "remote", "get-url", remote).decode().strip().removesuffix(".git")
    if url not in ("https://github.com/" + PUBLIC_REPOSITORY, "git@github.com:" + PUBLIC_REPOSITORY):
        raise ValueError("Public product remote is not the configured product authority")
    git(root, "fetch", "--no-tags", remote, "+refs/heads/master:refs/remotes/" + remote + "/master")
    public_sha = git(root, "rev-parse", "refs/remotes/" + remote + "/master").decode().strip()
    public_fetch = {"ROLE": "PUBLIC_PRODUCT", "METHOD": "GIT_FETCH", "RESULT": "SUCCESS",
                    "TIP_SHA": public_sha, "FETCHED_AT_UTC": now()}
    source_exact(root, public_sha, profile.get("LOCAL_CONFIG_OVERRIDES", ()))
    commit = gh(profile, "commits/" + profile["PRIVATE_CONTENT_BRANCH"])
    sha = commit.get("sha", "")
    tree_sha = commit.get("commit", {}).get("tree", {}).get("sha", "")
    if not re.fullmatch("[0-9a-f]{40}", sha) or not re.fullmatch("[0-9a-f]{40}", tree_sha):
        raise ValueError("Fresh content-dump SHA/tree is unknown")
    content_fetch = {"ROLE": "PRIVATE_CONTENT_UPSTREAM", "METHOD": "AUTHENTICATED_GITHUB_API",
                     "RESULT": "SUCCESS", "TIP_SHA": sha, "TREE_SHA": tree_sha, "FETCHED_AT_UTC": now()}
    return public_sha, sha, public_fetch, content_fetch


def tree_files(root, profile, authority, sha):
    if authority == "PRIVATE_CONTENT_UPSTREAM":
        value = gh(profile, "git/trees/" + sha + "?recursive=1")
        if value.get("truncated") is not False:
            raise ValueError("Complete upstream tree is required")
        prefix = profile["PRIVATE_CONTENT_PREFIX"].strip("/") + "/"
        result = {}
        for row in value["tree"]:
            if row["path"].startswith(prefix) and row["type"] != "tree":
                if row["type"] != "blob" or row["mode"] not in ("100644", "100755"):
                    raise ValueError("Upstream content special file rejected")
                rel = row["path"][len(prefix):]
                relative(rel)
                result[rel] = {"blob": row["sha"], "size": row["size"]}
        if not result:
            raise ValueError("Published private content projection is empty")
        return result
    result = {}
    for record in git(root, "ls-tree", "-rz", sha, "--", "AORebirth/GameData/").split(b"\0"):
        if not record:
            continue
        meta, path = record.decode().split("\t")
        mode, kind, identity = meta.split()
        if kind != "blob" or mode not in ("100644", "100755"):
            raise ValueError("Public content special file rejected")
        rel = path.removeprefix("AORebirth/GameData/")
        relative(rel)
        result[rel] = {"blob": identity, "size": int(git(root, "cat-file", "-s", identity))}
    return result


def source_blob(root, profile, authority, identity):
    if authority == "PRIVATE_CONTENT_UPSTREAM":
        value = gh(profile, "git/blobs/" + identity)
        if value.get("encoding") != "base64":
            raise ValueError("Private content blob encoding is unknown")
        data = base64.b64decode(value["content"].replace("\n", ""), validate=True)
    else:
        data = git(root, "cat-file", "blob", identity)
    if data.startswith(b"version https://git-lfs.github.com/spec/v1") or blob(data) != identity:
        raise ValueError("Upstream blob is unavailable, changed or an LFS pointer")
    return data


def value_hash(value):
    return "MISSING" if value is MISSING else digest(encoded(value))


def merge_value(base, incoming, private, resolutions, pointer=""):
    """Three-way merge; independently owned keys survive and conflicts fail closed."""
    if value_hash(private) == value_hash(base):
        return incoming
    if value_hash(incoming) == value_hash(base) or value_hash(incoming) == value_hash(private):
        return private
    if isinstance(base, dict) and isinstance(private, dict) and (isinstance(incoming, dict) or incoming is MISSING):
        incoming_missing = incoming is MISSING
        incoming = {} if incoming is MISSING else incoming
        result = {}
        for key in dict.fromkeys([*private, *incoming, *base]):
            child = pointer + "/" + key.replace("~", "~0").replace("/", "~1")
            value = merge_value(base.get(key, MISSING), incoming.get(key, MISSING), private.get(key, MISSING), resolutions, child)
            if value is not MISSING:
                result[key] = value
        return MISSING if incoming_missing and not result else result
    if base is MISSING and isinstance(incoming, dict) and isinstance(private, dict):
        return merge_value({}, incoming, private, resolutions, pointer)
    resolution = resolutions.get(pointer)
    expected = {"BASE": value_hash(base), "UPSTREAM": value_hash(incoming), "PRIVATE": value_hash(private)}
    if (not isinstance(resolution, dict) or resolution.get("INPUT_HASHES") != expected
            or not resolution.get("APPROVAL") or resolution.get("ACTION") not in ("PRESERVE_PRIVATE", "RESOLVED_VALUE")):
        raise ContentConflict(pointer, expected)
    return private if resolution["ACTION"] == "PRESERVE_PRIVATE" else resolution["VALUE"]


def merge_bytes(base, incoming, private, resolutions):
    if private == base:
        return incoming
    if incoming == base or incoming == private:
        return private
    def parse(value):
        return MISSING if value is None else json.loads(value.decode("utf-8-sig"), object_pairs_hook=pairs)
    result = merge_value(parse(base), parse(incoming), parse(private), resolutions)
    if result is MISSING:
        return None
    for raw in (private, incoming):
        if raw is not None and value_hash(parse(raw)) == value_hash(result):
            return raw
    return (json.dumps(result, ensure_ascii=False, indent=2) + "\n").encode()


def changes(before, after):
    return [(path, "A" if path not in before else "D" if path not in after else "M")
            for path in sorted(before.keys() | after.keys()) if before.get(path) != after.get(path)]


def stash(bundle, name, data):
    if data is None:
        return None
    path = bundle / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    return descriptor(bundle, path)


def load_optional(bundle, item):
    return None if item is None else load_descriptor(bundle, item).read_bytes()


def exporter_source_closure(root, public_sha, verify_working_tree=True):
    """The existing exporter project, its static links and tracked MSBuild inputs."""
    project = ET.fromstring(git(root, "show", public_sha + ":" + EXPORTER_PROJECT))
    if project.findall(".//ProjectReference") or project.findall(".//Import"):
        raise ValueError("Exporter source closure needs review after project/import changes")
    paths = set(git(root, "ls-tree", "-r", "--name-only", public_sha, "--", "Tools/RDBDataExtractor").decode().splitlines())
    for node in project.findall(".//Compile"):
        value = node.attrib.get("Include", "").replace("\\", "/")
        if not value or any(char in value for char in "*$?"):
            raise ValueError("Exporter source closure requires static compile links")
        path = (Path(root) / PurePosixPath(EXPORTER_PROJECT).parent / value).resolve(strict=True)
        if not path.is_relative_to(Path(root).resolve()):
            raise ValueError("Exporter source link escaped public product checkout")
        paths.add(path.relative_to(Path(root).resolve()).as_posix())
    for directory in ("", "Tools", "Tools/RDBDataExtractor"):
        for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "NuGet.Config", "nuget.config", "global.json"):
            candidate = (directory + "/" if directory else "") + name
            if git(root, "ls-tree", "--name-only", public_sha, "--", candidate).strip():
                paths.add(candidate)
    rows = []
    for path in sorted(paths):
        data = git(root, "show", public_sha + ":" + path)
        checked(root, path)
        if verify_working_tree and git(root, "hash-object", "--path=" + path, path).decode().strip() != blob(data):
            raise ValueError("Current exporter source differs from exact public master")
        rows.append(content_identity(data, path))
    untracked = git(root, "ls-files", "--others", "--exclude-standard", "--", "Tools/RDBDataExtractor").strip()
    if untracked:
        raise ValueError("Untracked exporter source is not authoritative")
    return rows


def inert_exporter_source_change(path, before, after):
    """Recognize only the reviewed guard opt-in and unused spawn-path declaration."""
    if path == "Directory.Build.targets":
        original, current = ET.fromstring(before), ET.fromstring(after)
        old = original.find("./Target[@Name='RequireGovernedWindowsProductBuild']")
        new = current.find("./Target[@Name='RequireGovernedWindowsProductBuild']")
        normalize = lambda value: " ".join(value.split())
        clause = ("And ('$(AORebirthGovernedRelease)' == 'true' Or '$(AO_REBIRTH_BUILD_CHILD)' != '' "
                  "Or '$(AO_REBIRTH_BUILD_TOKEN)' != '' Or '$(AO_REBIRTH_BUILD_STATE)' != '') ")
        if (old is None or new is None or "'$(AORebirthProductEntry)' == 'true'" not in old.get("Condition", "")
                or normalize(new.get("Condition", "")).count(clause) != 1
                or normalize(new.get("Condition", "")).replace(clause, "", 1) != normalize(old.get("Condition", ""))):
            raise ValueError("Exporter reuse cannot ignore changed MSBuild behavior")
        new.set("Condition", old.get("Condition"))
        for tree in (original, current):
            for node in tree.iter():
                if node.text is not None and not node.text.strip():
                    node.text = None
                if node.tail is not None and not node.tail.strip():
                    node.tail = None
        if ET.tostring(original) != ET.tostring(current):
            raise ValueError("Exporter reuse cannot ignore other MSBuild changes")
        return "PRODUCT_COMPILATION_GUARD_OPT_IN_ONLY"
    if path == "AORebirth/Libraries/Source/AORebirth.Core/GameData/GameDataPaths.cs":
        declaration = ('        /// <summary>Hash spawn points that are not loaded (ZoneEngine_New SpawnExclusionCatalog).</summary>\n'
                       '        public const string SpawnExclusionsFileName = "SpawnExclusions.json";\n\n')
        old, new = before.decode("utf-8"), after.decode("utf-8")
        if new.count(declaration) != 1 or "SpawnExclusionsFileName" in old or new.replace(declaration, "", 1) != old:
            raise ValueError("Exporter reuse cannot ignore changed shared code")
        return "UNUSED_SPAWN_PATH_CONSTANT_ONLY"
    raise ValueError("Exporter functional source or dependency changed: " + path)


def validate_exporter_source_qualification(original, qualification):
    if not isinstance(qualification, dict) or set(qualification) != {"CURRENT_SOURCE_CLOSURE", "INERT_CHANGES"}:
        raise ValueError("Exporter source qualification schema differs")
    current = qualification["CURRENT_SOURCE_CLOSURE"]
    validate_manifest(current)
    old, new = ({row["path"]: row for row in rows} for rows in (original, current))
    if set(old) != set(new):
        raise ValueError("Exporter source path set changed")
    changed = {path for path in old if old[path] != new[path]}
    records = qualification["INERT_CHANGES"]
    if not changed or not isinstance(records, list) or [row.get("PATH") for row in records] != sorted(changed):
        raise ValueError("Exporter source qualification omits or duplicates a change")
    for record in records:
        if set(record) != {"PATH", "BEFORE", "AFTER", "CLASSIFICATION"}:
            raise ValueError("Exporter inert change schema differs")
        path = record["PATH"]
        before, after = (base64.b64decode(record[key], validate=True) for key in ("BEFORE", "AFTER"))
        if content_identity(before, path) != old[path] or content_identity(after, path) != new[path]:
            raise ValueError("Exporter qualification source identity differs")
        if inert_exporter_source_change(path, before, after) != record["CLASSIFICATION"]:
            raise ValueError("Exporter inert change classification differs")


def qualify_exporter_sources(root, original_sha, current_sha, original, current):
    old, new = ({row["path"]: row for row in rows} for rows in (original, current))
    if set(old) != set(new):
        raise ValueError("Exporter source path set changed")
    changed = sorted(path for path in old if old[path] != new[path])
    if not changed:
        return None
    records = []
    for path in changed:
        before, after = (git(root, "show", sha + ":" + path) for sha in (original_sha, current_sha))
        classification = inert_exporter_source_change(path, before, after)
        if classification == "UNUSED_SPAWN_PATH_CONSTANT_ONLY":
            for other in new:
                if other != path:
                    source = git(root, "show", current_sha + ":" + other).decode("utf-8")
                    if (re.search(r"\bSpawnExclusionsFileName\b", source)
                            or re.search(r"\bGameDataPaths\b(?!\s*\.)", source)
                            or re.search(r"\bGet(?:Exported)?Types\s*\(", source)):
                        raise ValueError("New spawn constant is referenced or shared type reflection requires review")
        records.append({"PATH": path, "BEFORE": base64.b64encode(before).decode("ascii"),
                        "AFTER": base64.b64encode(after).decode("ascii"), "CLASSIFICATION": classification})
    value = {"CURRENT_SOURCE_CLOSURE": current, "INERT_CHANGES": records}
    validate_exporter_source_qualification(original, value)
    return value


def validate_generated_items_provenance(proof, public_sha):
    required = {"FORMAT_VERSION", "PUBLIC_MASTER_SHA", "CREATED_UTC", "EXPORTER_SOURCE_CLOSURE", "TOOL_BUILD",
                "RDB_INPUTS", "EXPORT", "OUTPUT", "VALIDATION", "LOCAL_RECEIPT_SHA256"}
    if (not isinstance(proof, dict) or not required <= set(proof) or set(proof) - required - {"QUALIFIED_REUSE"}
            or len(encoded(proof)) > 262144 or proof["FORMAT_VERSION"] != 1
            or not isinstance(proof["PUBLIC_MASTER_SHA"], str) or not re.fullmatch("[0-9a-f]{40}", proof["PUBLIC_MASTER_SHA"])
            or not re.fullmatch("[0-9a-f]{64}", proof["LOCAL_RECEIPT_SHA256"])):
        raise ValueError("Current exact-source generated items provenance required")
    reuse = proof.get("QUALIFIED_REUSE")
    original = {key: value for key, value in proof.items() if key != "QUALIFIED_REUSE"}
    if proof["PUBLIC_MASTER_SHA"] != public_sha:
        fields = {"REQUESTING_PUBLIC_MASTER_SHA", "EXPORTER_SOURCE_CLOSURE_SHA256", "ORIGINAL_PROVENANCE_SHA256"}
        if (not isinstance(reuse, dict) or not fields <= set(reuse) or set(reuse) - fields - {"SOURCE_QUALIFICATION"}
                or reuse["REQUESTING_PUBLIC_MASTER_SHA"] != public_sha
                or reuse["EXPORTER_SOURCE_CLOSURE_SHA256"] != digest(encoded(proof["EXPORTER_SOURCE_CLOSURE"]))
                or reuse["ORIGINAL_PROVENANCE_SHA256"] != digest(encoded(original))):
            raise ValueError("Generated items reuse lacks its requesting source and unchanged original provenance")
        if "SOURCE_QUALIFICATION" in reuse:
            validate_exporter_source_qualification(proof["EXPORTER_SOURCE_CLOSURE"], reuse["SOURCE_QUALIFICATION"])
    elif reuse is not None:
        raise ValueError("Generated items reuse must identify a different requesting source")
    source, build, inputs, export, output, validation = (proof[key] for key in
        ("EXPORTER_SOURCE_CLOSURE", "TOOL_BUILD", "RDB_INPUTS", "EXPORT", "OUTPUT", "VALIDATION"))
    validate_manifest(source)
    if EXPORTER_PROJECT not in {row["path"] for row in source}:
        raise ValueError("Exporter project/source closure is missing")
    if (set(build) != {"COMMAND", "STARTED_UTC", "FINISHED_UTC", "EXIT_CODE", "BINARIES"}
            or build["COMMAND"] != ["dotnet", *EXPORTER_BUILD_ARGS] or type(build["EXIT_CODE"]) is not int or build["EXIT_CODE"] != 0
            or set(export) != {"COMMAND", "STARTED_UTC", "FINISHED_UTC", "EXIT_CODE", "OUTPUT_ABSENT_BEFORE"}
            or export["COMMAND"] != [EXPORTER_OUTPUT + "/RDBDataExtractor.exe", "extract", "--ao-path", "<AO_CLIENT_ROOT>",
                "--gamedata-dir", "<STAGED_GAMEDATA_ROOT>", *EXPORTER_SKIP_ARGS]
            or type(export["EXIT_CODE"]) is not int or export["EXIT_CODE"] != 0 or export["OUTPUT_ABSENT_BEFORE"] is not True
            or not timestamp(build["STARTED_UTC"]) <= timestamp(build["FINISHED_UTC"]) <= timestamp(export["STARTED_UTC"])
                <= timestamp(export["FINISHED_UTC"]) <= timestamp(proof["CREATED_UTC"])):
        raise ValueError("Fresh successful current exporter build/extraction is required")
    validate_manifest(build["BINARIES"])
    binaries = {row["path"]: row for row in build["BINARIES"]}
    required_outputs = {EXPORTER_OUTPUT + "/" + name for name in ("RDBDataExtractor.exe", "RDBDataExtractor.dll", "AODB.dll",
                        "MsgPack.dll", "RDBDataExtractor.deps.json", "RDBDataExtractor.runtimeconfig.json", "FunctionSets.cfg")}
    if not required_outputs <= set(binaries) or any(not path.startswith(EXPORTER_OUTPUT + "/") for path in binaries):
        raise ValueError("Complete measured exporter outputs/dependencies required")
    if set(inputs) != {"INDEX", "SHARDS"} or not isinstance(inputs["SHARDS"], list):
        raise ValueError("Complete measured RDB index/shards required")
    names = []
    for index, row in enumerate([inputs["INDEX"], *inputs["SHARDS"]]):
        fields = {"path", "size", "BEFORE_STATE", "AFTER_STATE"} | ({"sha256"} if index == 0 else set())
        if (set(row) != fields or type(row["size"]) is not int or row["size"] <= 0
                or not isinstance(row["BEFORE_STATE"], list) or len(row["BEFORE_STATE"]) != 5
                or any(type(value) is not int for value in row["BEFORE_STATE"])
                or row["BEFORE_STATE"] != row["AFTER_STATE"] or row["BEFORE_STATE"][2] != row["size"]
                or (index == 0 and (row["path"] != "ResourceDatabase.idx" or not re.fullmatch("[0-9a-f]{64}", row["sha256"])))):
            raise ValueError("RDB input changed or lacks its measured identity")
        names.append(row["path"])
    expected_shards = ["ResourceDatabase.dat", *("ResourceDatabase.dat." + str(number).zfill(3) for number in range(1, len(names) - 1))]
    if len(names) < 2 or names[1:] != expected_shards:
        raise ValueError("Complete contiguous RDB shard set required")
    if set(output) != {"path", "size", "sha256", "blob"} or output["path"] != "items.dat" or output["size"] <= 0:
        raise ValueError("Generated output must be the one protected items.dat")
    validate_manifest([output])
    count, distinct = validation.get("DECODED_POPULATION"), validation.get("DISTINCT_IDS")
    validation_fields = {"CURRENT_ITEMS_DAT_READER", "ITEM_TEMPLATE_CATALOG", "EXPORTER_ASSEMBLY_SHA256", "ITEMS_DAT_SHA256", "ITEMS_DAT_SIZE",
        "HEADER", "SOURCE_POPULATION", "DECODED_POPULATION", "DISTINCT_IDS", "NAMED_TEMPLATES", "UNNAMED_IDS", "BOSS_MODIFIER_COUNT",
        "BOSS_MODIFIERS", "CURRENT_FIELDS", "AURA_OF_REVIVAL_212161"}
    if (set(validation) != validation_fields or validation.get("CURRENT_ITEMS_DAT_READER") != "PASS" or validation.get("ITEM_TEMPLATE_CATALOG") != "DEFERRED_TO_REAL_WINDOWS_STARTUP"
            or validation.get("ITEMS_DAT_SHA256") != output["sha256"] or validation.get("ITEMS_DAT_SIZE") != output["size"]
            or validation.get("EXPORTER_ASSEMBLY_SHA256") != binaries[EXPORTER_OUTPUT + "/RDBDataExtractor.dll"]["sha256"]
            or type(count) is not int or count <= 0 or type(distinct) is not int or not 0 < distinct <= count
            or validation.get("SOURCE_POPULATION", {}).get("expected") != count
            or set(validation["SOURCE_POPULATION"]) != {"itemRecords", "nanoRecords", "expected"}
            or validation["SOURCE_POPULATION"]["itemRecords"] + validation["SOURCE_POPULATION"]["nanoRecords"] != count
            or validation.get("HEADER", {}).get("templateCount") != count
            or validation.get("HEADER", {}).get("version") != "RDBDataExtractor"
            or type(validation.get("NAMED_TEMPLATES")) is not int or validation["NAMED_TEMPLATES"] <= 0
            or not isinstance(validation.get("UNNAMED_IDS"), list) or validation["NAMED_TEMPLATES"] + len(validation["UNNAMED_IDS"]) != count
            or type(validation.get("BOSS_MODIFIER_COUNT")) is not int or validation["BOSS_MODIFIER_COUNT"] <= 0
            or len(validation.get("BOSS_MODIFIERS", [])) != validation["BOSS_MODIFIER_COUNT"]
            or any(set(row) != {"id", "name"} or type(row["id"]) is not int or not row.get("name", "").lower().startswith("boss modifier item")
                   for row in validation["BOSS_MODIFIERS"])):
        raise ValueError("Current reader population/names/boss/generated output proof differs")
    fields = validation.get("CURRENT_FIELDS", {})
    if (set(fields) != {"fullRdb", "stats", "attack", "defend", "eventsOrActions", "shops", "shopLines", "rawFallback", "nanoFields",
            "positiveAmmoType", "positiveMaxEnergy", "positiveMultipleCount", "negativeOneTickFunctions", "periodicFunctions"}
            or fields.get("fullRdb") != count or any(type(value) is not int or value < 0 for value in fields.values())
            or any(set(row) != {"eventType", "functionType", "tickCount", "tickInterval"} for row in validation.get("AURA_OF_REVIVAL_212161", []))
            or not any(row.get("tickCount") == -1 and row.get("tickInterval", 0) > 0 for row in validation.get("AURA_OF_REVIVAL_212161", []))):
        raise ValueError("Current exporter/recent runtime fields were not validated")
    header = validation["HEADER"]
    if (set(header) != {"version", "packCount", "templateCount", "slices", "sliceStates"}
            or type(header["packCount"]) is not int or header["packCount"] <= 0
            or type(header["slices"]) is not int or header["slices"] <= 0 or len(header["sliceStates"]) != header["slices"]
            or any(set(row) != {"index", "compressedBytes", "uncompressedBytes"} or row["index"] != index
                   or row["compressedBytes"] <= 0 or row["uncompressedBytes"] <= 0 for index, row in enumerate(header["sliceStates"]))):
        raise ValueError("Current reader/header slice metadata differs")
    return output


def generated_items_receipt(root, profile, receipt_path, public_sha):
    root = Path(root).resolve(strict=True)
    path = Path(receipt_path)
    path = checked(root, path.relative_to(root).as_posix() if path.is_absolute() else path.as_posix())
    if git(root, "check-ignore", "--", str(path)).strip() == b"":
        raise ValueError("Generated items receipt must remain ignored/local")
    raw = path.read_bytes()
    receipt = read_json(path)
    fields = {"FORMAT_VERSION", "PUBLIC_MASTER_SHA", "CREATED_UTC", "EXPORTER_SOURCE_CLOSURE", "TOOL_BUILD", "RDB_INPUTS", "EXPORT", "OUTPUT", "VALIDATION"}
    if set(receipt) != fields:
        raise ValueError("Generated items receipt schema differs")
    staged_value = receipt["OUTPUT"]["STAGED_PATH"]
    staged_path = Path(staged_value)
    staged = checked(root, staged_path.relative_to(root).as_posix() if staged_path.is_absolute() else staged_path.as_posix())
    if (staged.name != "items.dat" or staged.is_relative_to(Path(profile["CANONICAL_GAMEDATA_ROOT"]).resolve())
            or git(root, "check-ignore", "--", str(staged)).strip() == b""):
        raise ValueError("Generated output must be a new ignored stage outside canonical GameData")
    before = file_state(staged)
    data = staged.read_bytes()
    if before != file_state(staged) or receipt["OUTPUT"] != {"STAGED_PATH": staged_value, **content_identity(data)}:
        raise ValueError("Staged generated items changed or has a different identity")
    proof = json.loads(encoded(receipt))
    proof["LOCAL_RECEIPT_SHA256"] = digest(raw)
    proof["OUTPUT"] = content_identity(data, "items.dat")
    inputs = proof["RDB_INPUTS"]
    database_root = Path(inputs.pop("ROOT")).resolve(strict=True)
    if [part.lower() for part in database_root.parts[-3:]] != ["cd_image", "data", "db"]:
        raise ValueError("Current exporter RDB root must be the installed client's cd_image/data/db")
    client_root = database_root.parents[2]
    input_names = {path.name for path in database_root.iterdir() if path.name == "ResourceDatabase.dat" or re.fullmatch(r"ResourceDatabase\.dat\.[0-9]{3}", path.name)}
    if input_names != {row["path"] for row in inputs["SHARDS"]}:
        raise ValueError("Current RDB shard path set differs from generation")
    for index, row in enumerate([inputs["INDEX"], *inputs["SHARDS"]]):
        source = checked(database_root, row["path"])
        if file_state(source) != row["AFTER_STATE"] or (index == 0 and digest(source.read_bytes()) != row["sha256"]):
            raise ValueError("Current RDB input changed since extraction")
    build_command = proof["TOOL_BUILD"]["COMMAND"]
    if not build_command or ntpath.basename(build_command[0]).lower() not in ("dotnet", "dotnet.exe"):
        raise ValueError("Current exporter build command differs")
    proof["TOOL_BUILD"]["COMMAND"] = ["dotnet", *build_command[1:]]
    export_command = proof["EXPORT"]["COMMAND"]
    actual_command = [str(root / EXPORTER_OUTPUT / "RDBDataExtractor.exe"), "extract", "--ao-path", str(client_root),
                      "--gamedata-dir", str(staged.parent), *EXPORTER_SKIP_ARGS]
    if len(export_command) != len(actual_command) or any(ntpath.normcase(ntpath.normpath(actual)) != ntpath.normcase(ntpath.normpath(expected))
            for actual, expected in zip(export_command, actual_command)):
        raise ValueError("Generated items extraction must use the existing items-only exporter command")
    proof["EXPORT"]["COMMAND"] = [EXPORTER_OUTPUT + "/RDBDataExtractor.exe", "extract", "--ao-path", "<AO_CLIENT_ROOT>",
                                 "--gamedata-dir", "<STAGED_GAMEDATA_ROOT>", *EXPORTER_SKIP_ARGS]
    validate_generated_items_provenance(proof, proof["PUBLIC_MASTER_SHA"])
    original = exporter_source_closure(root, proof["PUBLIC_MASTER_SHA"], verify_working_tree=False)
    current = exporter_source_closure(root, public_sha)
    if proof["EXPORTER_SOURCE_CLOSURE"] != original:
        raise ValueError("Exporter source closure differs from original generation")
    qualification = qualify_exporter_sources(root, proof["PUBLIC_MASTER_SHA"], public_sha, original, current)
    output_rows, _ = inventory(checked(root, EXPORTER_OUTPUT))
    output_rows = [{**row, "path": EXPORTER_OUTPUT + "/" + row["path"]} for row in output_rows]
    if output_rows != proof["TOOL_BUILD"]["BINARIES"]:
        raise ValueError("Current exporter binaries/dependencies differ from measured generation")
    if proof["PUBLIC_MASTER_SHA"] != public_sha:
        proof["QUALIFIED_REUSE"] = {"REQUESTING_PUBLIC_MASTER_SHA": public_sha,
            "EXPORTER_SOURCE_CLOSURE_SHA256": digest(encoded(proof["EXPORTER_SOURCE_CLOSURE"])),
            "ORIGINAL_PROVENANCE_SHA256": digest(encoded(proof))}
        if qualification is not None:
            proof["QUALIFIED_REUSE"]["SOURCE_QUALIFICATION"] = qualification
    validate_generated_items_provenance(proof, public_sha)
    return proof, data, raw


def surface_cells(data):
    """Read the existing signed little-endian layout without tolerating trailing bytes."""
    if not isinstance(data, bytes) or len(data) < 4:
        raise ValueError("Surfaces.dat header is missing")
    count, = struct.unpack_from("<i", data)
    if count < 0 or count > (len(data) - 4) // 8:
        raise ValueError("Surfaces.dat entry count exceeds its byte bounds")
    offset, cells = 4, {}
    for _ in range(count):
        if len(data) - offset < 8:
            raise ValueError("Surfaces.dat entry header is truncated")
        cell, size = struct.unpack_from("<ii", data, offset)
        offset += 8
        if cell < 0 or cell in cells or size < 0 or size > len(data) - offset:
            raise ValueError("Surfaces.dat cell identity/length is invalid or duplicate")
        cells[cell] = data[offset:offset + size]
        offset += size
    if offset != len(data):
        raise ValueError("Surfaces.dat has trailing bytes")
    return cells


def surface_cell_index(cells):
    return [{"CELL_ID": cell, "size": len(payload), "sha256": digest(payload)}
            for cell, payload in sorted(cells.items())]


def validate_surface_cell_index(rows):
    if not isinstance(rows, list):
        raise ValueError("Measured Surfaces.dat cell index required")
    for row in rows:
        if (not isinstance(row, dict) or set(row) != {"CELL_ID", "size", "sha256"}
                or type(row["CELL_ID"]) is not int or not 0 <= row["CELL_ID"] <= 2147483647
                or type(row["size"]) is not int or not 0 <= row["size"] <= 2147483647
                or not re.fullmatch("[0-9a-f]{64}", row["sha256"])):
            raise ValueError("Measured Surfaces.dat cell identity differs")
    if [row["CELL_ID"] for row in rows] != sorted({row["CELL_ID"] for row in rows}):
        raise ValueError("Surfaces.dat cell index is duplicate or unordered")
    return {row["CELL_ID"]: row for row in rows}


def validate_generated_surfaces_provenance(proof, items, public_sha):
    fields = {"FORMAT_VERSION", "PUBLIC_MASTER_SHA", "CREATED_UTC", "GENERATED_ITEMS_PROVENANCE_SHA256",
              "LOCAL_RECEIPT_SHA256", "EXPORTS"}
    if (not isinstance(proof, dict) or set(proof) != fields or proof["FORMAT_VERSION"] != 1
            or not items or proof["PUBLIC_MASTER_SHA"] != items["PUBLIC_MASTER_SHA"]
            or proof["GENERATED_ITEMS_PROVENANCE_SHA256"] != digest(encoded(items))
            or not re.fullmatch("[0-9a-f]{64}", proof["LOCAL_RECEIPT_SHA256"])
            or not isinstance(proof["EXPORTS"], list) or not proof["EXPORTS"] or len(proof["EXPORTS"]) > 65535):
        raise ValueError("Current measured surfaces must bind the same fresh items exporter/input provenance")
    validate_generated_items_provenance(items, public_sha)
    if timestamp(items["CREATED_UTC"]) > timestamp(proof["CREATED_UTC"]):
        raise ValueError("Surfaces receipt predates its measured exporter/input proof")
    assembly = next(row["sha256"] for row in items["TOOL_BUILD"]["BINARIES"]
                    if row["path"] == EXPORTER_OUTPUT + "/RDBDataExtractor.dll")
    seen = set()
    for row in proof["EXPORTS"]:
        if set(row) != {"PATH", "SOURCE_RECORD", "EXPORT", "OUTPUT", "VALIDATION", "CELLS"}:
            raise ValueError("Bounded generated surfaces export schema differs")
        match = re.fullmatch(r"Playfields/([1-9][0-9]*)/Surfaces\.dat", row["PATH"])
        if not match or int(match[1]) > 65535 or row["PATH"] in seen:
            raise ValueError("Generated surfaces must identify unique decimal playfield paths")
        seen.add(row["PATH"])
        source, output, export, validation = (row[key] for key in ("SOURCE_RECORD", "OUTPUT", "EXPORT", "VALIDATION"))
        if (set(source) != {"TYPE", "ID", "size", "sha256"} or source["TYPE"] != 1000013
                or type(source["ID"]) is not int or source["ID"] != int(match[1]) << 16
                or type(source["size"]) is not int or source["size"] <= 0
                or not re.fullmatch("[0-9a-f]{64}", source["sha256"])):
            raise ValueError("SurfaceResource body identity differs from its exact playfield record")
        validate_manifest([output])
        cells = validate_surface_cell_index(row["CELLS"])
        if (output["path"] != row["PATH"] or 0 not in cells or cells[0]["size"] != source["size"]
                or cells[0]["sha256"] != source["sha256"]
                or output["size"] != 4 + sum(8 + cell["size"] for cell in cells.values())):
            raise ValueError("Generated surface cell zero differs from the measured header-stripped RDB body")
        expected_command = [EXPORTER_OUTPUT + "/RDBDataExtractor.exe", "extract", "--ao-path", "<AO_CLIENT_ROOT>",
            "--gamedata-dir", "<STAGED_GAMEDATA_ROOT>", "--output-dir", "<STAGED_GAMEDATA_ROOT>/Playfields",
            "--tilemap-id", match[1], "--overwrite", "--skip-items-dat", "--skip-monster-data", "--skip-dynel-collision", "--skip-text"]
        if (set(export) != {"COMMAND", "EXIT_CODE", "STARTED_UTC", "FINISHED_UTC"}
                or export["COMMAND"] != expected_command or type(export["EXIT_CODE"]) is not int or export["EXIT_CODE"] != 0
                or not timestamp(items["TOOL_BUILD"]["FINISHED_UTC"]) <= timestamp(export["STARTED_UTC"])
                    <= timestamp(export["FINISHED_UTC"]) <= timestamp(proof["CREATED_UTC"])):
            raise ValueError("Successful measured current playfield-only exporter invocation required")
        expected_validation = dict(CURRENT_PLAYFIELD_SURFACES_READER="PASS", READER_ASSEMBLY_SHA256=assembly,
            ENTRY_COUNT=len(cells), CELL0_COUNT=1, CELL0_SIZE=source["size"], CELL0_SHA256=source["sha256"],
            NONZERO_CELL_COUNT=len(cells) - 1, STRICT_LAYOUT_EOF=True, CANONICAL_NONZERO_CELLS_PRESERVED=True)
        if (validation != expected_validation
                or any(type(validation.get(key)) is not int for key in ("ENTRY_COUNT", "CELL0_COUNT", "CELL0_SIZE", "NONZERO_CELL_COUNT"))
                or validation.get("STRICT_LAYOUT_EOF") is not True or validation.get("CANONICAL_NONZERO_CELLS_PRESERVED") is not True):
            raise ValueError("Actual compiled surfaces reader/source-body/preservation measurements differ")
    return {row["PATH"]: row for row in proof["EXPORTS"]}


def generated_surfaces_receipt(root, profile, receipt_path, public_sha, items):
    root = Path(root).resolve(strict=True)
    path = Path(receipt_path)
    path = checked(root, path.relative_to(root).as_posix() if path.is_absolute() else path.as_posix())
    if git(root, "check-ignore", "--", str(path)).strip() == b"":
        raise ValueError("Generated surfaces receipt must remain ignored/local")
    raw, receipt = path.read_bytes(), read_json(path)
    if set(receipt) != {"FORMAT_VERSION", "PUBLIC_MASTER_SHA", "CREATED_UTC", "GENERATED_ITEMS_RECEIPT", "EXPORTS"}:
        raise ValueError("Generated surfaces local receipt schema differs")
    measured_items, _, _ = generated_items_receipt(root, profile, receipt["GENERATED_ITEMS_RECEIPT"], public_sha)
    if measured_items != items:
        raise ValueError("Surface export differs from the intake's actual exporter/source/RDB-input proof")
    item_path = Path(receipt["GENERATED_ITEMS_RECEIPT"])
    item_path = checked(root, item_path.relative_to(root).as_posix() if item_path.is_absolute() else item_path.as_posix())
    client_root = Path(read_json(item_path)["RDB_INPUTS"]["ROOT"]).resolve(strict=True).parents[2]
    proof = {key: receipt[key] for key in ("FORMAT_VERSION", "PUBLIC_MASTER_SHA", "CREATED_UTC")}
    proof.update(GENERATED_ITEMS_PROVENANCE_SHA256=digest(encoded(items)), LOCAL_RECEIPT_SHA256=digest(raw), EXPORTS=[])
    outputs = {}
    canonical = Path(profile["CANONICAL_GAMEDATA_ROOT"]).resolve(strict=True)
    for row in receipt["EXPORTS"]:
        if set(row) != {"PATH", "STAGED_PATH", "SOURCE_RECORD", "EXPORT", "OUTPUT", "VALIDATION"}:
            raise ValueError("Generated surfaces staged export schema differs")
        match = re.fullmatch(r"Playfields/([1-9][0-9]*)/Surfaces\.dat", row["PATH"])
        if not match or row["PATH"] in outputs:
            raise ValueError("Only unique playfield Surfaces.dat outputs may be adopted")
        staged = Path(row["STAGED_PATH"])
        staged = checked(root, staged.relative_to(root).as_posix() if staged.is_absolute() else staged.as_posix())
        if (staged.is_relative_to(canonical) or git(root, "check-ignore", "--", str(staged)).strip() == b""
                or staged.parts[-3:] != ("Playfields", match[1], "Surfaces.dat")):
            raise ValueError("Surface output must be a rooted ignored stage outside canonical data")
        before = file_state(staged)
        data = staged.read_bytes()
        if file_state(staged) != before or row["OUTPUT"] != content_identity(data):
            raise ValueError("Staged surfaces changed or differ from the measured export")
        stage_root = staged.parents[2]
        expected_command = [str(root / EXPORTER_OUTPUT / "RDBDataExtractor.exe"), "extract", "--ao-path", str(client_root),
            "--gamedata-dir", str(stage_root), "--output-dir", str(stage_root / "Playfields"), "--tilemap-id", match[1],
            "--overwrite", "--skip-items-dat", "--skip-monster-data", "--skip-dynel-collision", "--skip-text"]
        actual = row["EXPORT"]["COMMAND"]
        if len(actual) != len(expected_command) or any(ntpath.normcase(ntpath.normpath(a)) != ntpath.normcase(ntpath.normpath(b))
                                                     for a, b in zip(actual, expected_command)):
            raise ValueError("Surface command differs from the existing per-playfield exporter")
        normalized = [EXPORTER_OUTPUT + "/RDBDataExtractor.exe", "extract", "--ao-path", "<AO_CLIENT_ROOT>",
            "--gamedata-dir", "<STAGED_GAMEDATA_ROOT>", "--output-dir", "<STAGED_GAMEDATA_ROOT>/Playfields", *actual[8:]]
        proof["EXPORTS"].append({key: row[key] for key in ("PATH", "SOURCE_RECORD", "VALIDATION")} | {
            "OUTPUT": content_identity(data, row["PATH"]), "EXPORT": {**row["EXPORT"], "COMMAND": normalized},
            "CELLS": surface_cell_index(surface_cells(data))})
        outputs[row["PATH"]] = data
    proof["EXPORTS"].sort(key=lambda row: row["PATH"])
    validate_generated_surfaces_provenance(proof, items, public_sha)
    return proof, outputs, raw


def validate_generated_surfaces_adoptions(bundle, value, actual, portable=False):
    proof, adoptions = value.get("GENERATED_SURFACES_PROVENANCE"), value.get("GENERATED_SURFACES_ADOPTIONS", [])
    if not proof:
        if adoptions or value.get("GENERATED_SURFACES_LOCAL_RECEIPT"):
            raise ValueError("Surface adoption lacks measured generation provenance")
        return
    outputs = validate_generated_surfaces_provenance(proof, value.get("GENERATED_ITEMS_PROVENANCE"), value["PUBLIC_MASTER_SHA"])
    if not portable:
        receipt = value.get("GENERATED_SURFACES_LOCAL_RECEIPT")
        if not receipt or digest(load_descriptor(bundle, receipt).read_bytes()) != proof["LOCAL_RECEIPT_SHA256"]:
            raise ValueError("Local surface replay lost its original measured receipt")
    seen = set()
    for row in adoptions:
        if set(row) != {"PATH", "CANONICAL_BEFORE", "CANONICAL_AFTER", "BEFORE_CELLS"}:
            raise ValueError("Surface before/after adoption schema differs")
        path = row["PATH"]
        if path not in outputs or path in seen:
            raise ValueError("Unexpected or duplicate surface adoption")
        seen.add(path)
        before = row["CANONICAL_BEFORE"] if portable else content_identity(load_optional(bundle, row["CANONICAL_BEFORE"]), path)
        after = row["CANONICAL_AFTER"] if portable else content_identity(load_optional(bundle, row["CANONICAL_AFTER"]), path)
        before_cells = validate_surface_cell_index(row["BEFORE_CELLS"])
        after_cells = validate_surface_cell_index(outputs[path]["CELLS"])
        if (before != actual.get(path) or after != outputs[path]["OUTPUT"] or before == after
                or 0 in before_cells or {cell: item for cell, item in after_cells.items() if cell != 0} != before_cells
                or before is None and bool(before_cells)
                or before is not None and before["size"] != 4 + sum(8 + cell["size"] for cell in before_cells.values())):
            raise ValueError("Surface adoption must add only missing cell zero while preserving every private cell")
        if not portable:
            old_bytes, new_bytes = load_optional(bundle, row["CANONICAL_BEFORE"]), load_optional(bundle, row["CANONICAL_AFTER"])
            old_cells = surface_cells(old_bytes) if old_bytes is not None else {}
            new_cells = surface_cells(new_bytes)
            if (surface_cell_index(old_cells) != row["BEFORE_CELLS"] or surface_cell_index(new_cells) != outputs[path]["CELLS"]
                    or any(new_cells.get(cell) != payload for cell, payload in old_cells.items())):
                raise ValueError("Surface adoption changed a private cell's actual payload")
        actual[path] = after
    if any(actual.get(path) != row["OUTPUT"] for path, row in outputs.items()):
        raise ValueError("Canonical surfaces differ from their measured generated outputs")


def required_catalogs(value):
    if not isinstance(value, list):
        raise ValueError("Explicit required-current catalog mapping required")
    seen = set()
    for row in value:
        fields = {"AUTHORITY", "PATH", "APPROVAL"}
        if (not isinstance(row, dict) or not fields <= set(row) or set(row) - fields - {"ENTRIES"}
                or row["AUTHORITY"] not in ("PUBLIC_PRODUCT_DATA", "PRIVATE_CONTENT_UPSTREAM")
                or not isinstance(row["PATH"], str) or not isinstance(row["APPROVAL"], str)
                or not row["APPROVAL"].strip() or row["PATH"] in seen):
            raise ValueError("Required-current catalog mapping is invalid/duplicate")
        relative(row["PATH"])
        if "ENTRIES" in row:
            entries = row["ENTRIES"]
            if (row["AUTHORITY"] != "PUBLIC_PRODUCT_DATA" or row["PATH"] != "ItemTemplates.json"
                    or not isinstance(entries, list) or not entries
                    or any(not isinstance(key, str) or not re.fullmatch("[A-Za-z0-9]+", key) for key in entries)
                    or len(set(entries)) != len(entries)):
                raise ValueError("Selective restoration requires explicit unique public ItemTemplates keys")
        seen.add(row["PATH"])
    return value


def restore_catalog_entries(incoming, private, entries):
    source = json.loads(incoming.decode("utf-8-sig"), object_pairs_hook=pairs)
    before = {} if private is None else json.loads(private.decode("utf-8-sig"), object_pairs_hook=pairs)
    if not isinstance(source, dict) or not isinstance(before, dict) or any(key not in source for key in entries):
        raise ValueError("Required ItemTemplates entries lack a current source or object catalog")
    restored = [key for key in entries if key not in before]
    after = {**before, **{key: source[key] for key in restored}}
    proof = {"VALUES": {key: {"SOURCE_SHA256": value_hash(source[key]),
                             "BEFORE_SHA256": value_hash(before.get(key, MISSING)),
                             "AFTER_SHA256": value_hash(after[key])} for key in entries},
             "BEFORE_ENTRY_COUNT": len(before), "AFTER_ENTRY_COUNT": len(after),
             "PRESERVED_BEFORE_SHA256": value_hash(before),
             "PRESERVED_AFTER_SHA256": value_hash({key: item for key, item in after.items() if key not in restored})}
    result = private if not restored else (json.dumps(after, ensure_ascii=False, indent=2) + "\n").encode()
    return result, proof


def validate_catalog_entry_proof(proof, entries, before, after):
    fields = {"VALUES", "BEFORE_ENTRY_COUNT", "AFTER_ENTRY_COUNT", "PRESERVED_BEFORE_SHA256", "PRESERVED_AFTER_SHA256"}
    if (not isinstance(proof, dict) or set(proof) != fields or not isinstance(proof["VALUES"], dict)
            or set(proof["VALUES"]) != set(entries)
            or any(type(proof[key]) is not int or proof[key] < 0 for key in ("BEFORE_ENTRY_COUNT", "AFTER_ENTRY_COUNT"))
            or any(not isinstance(proof[key], str) or not re.fullmatch("[0-9a-f]{64}", proof[key])
                   for key in ("PRESERVED_BEFORE_SHA256", "PRESERVED_AFTER_SHA256"))
            or proof["PRESERVED_BEFORE_SHA256"] != proof["PRESERVED_AFTER_SHA256"]):
        raise ValueError("Selective catalog preservation measurement differs")
    restored = 0
    for item in proof["VALUES"].values():
        if (not isinstance(item, dict) or set(item) != {"SOURCE_SHA256", "BEFORE_SHA256", "AFTER_SHA256"}
                or any(not isinstance(item[key], str) or not re.fullmatch("[0-9a-f]{64}", item[key])
                       for key in ("SOURCE_SHA256", "AFTER_SHA256"))
                or (item["BEFORE_SHA256"] != "MISSING" and (not isinstance(item["BEFORE_SHA256"], str)
                    or not re.fullmatch("[0-9a-f]{64}", item["BEFORE_SHA256"])))):
            raise ValueError("Selective catalog entry identities differ")
        missing = item["BEFORE_SHA256"] == "MISSING"
        restored += missing
        if item["AFTER_SHA256"] != item["SOURCE_SHA256" if missing else "BEFORE_SHA256"]:
            raise ValueError("Selective catalog restoration replaces a present private entry")
    if (proof["BEFORE_ENTRY_COUNT"] < len(entries) - restored
            or proof["AFTER_ENTRY_COUNT"] != proof["BEFORE_ENTRY_COUNT"] + restored
            or (before is None and (proof["BEFORE_ENTRY_COUNT"] != 0 or restored != len(entries)
                or proof["PRESERVED_BEFORE_SHA256"] != value_hash({})))
            or (after == before) != (restored == 0)):
        raise ValueError("Selective catalog entry counts or unchanged identities differ")


def validate_private_catalog_adoptions(bundle, value, actual, portable=False):
    rows = value.get("PRIVATE_CATALOG_ADOPTIONS", [])
    if not isinstance(rows, list) or any(not isinstance(row, dict) for row in rows) or (rows and {row.get("PATH") for row in rows} not in (PRIVATE_CHEST_CATALOGS, {"QuestRewards.json"}, PRIVATE_CHEST_CATALOGS | {"QuestRewards.json"})):
        raise ValueError("Private catalog adoption must bind chest policies together; QuestRewards.json may be adopted independently")
    seen = set()
    for row in rows:
        if set(row) != {"PATH", "SHA256", "APPROVAL", "APPROVAL_INSTRUCTION", "CANONICAL_BEFORE", "CANONICAL_AFTER"}:
            raise ValueError("Private catalog adoption schema differs")
        path, approval = row["PATH"], row["APPROVAL"]
        if (path in seen or path in value["PROTECTED_GENERATED_FILES"] or not re.fullmatch("[0-9a-f]{64}", row["SHA256"])
                or not isinstance(approval, dict) or set(approval) != {"BY", "INSTRUCTION_SHA256", "TEXT"}
                or approval["BY"] != "Mike" or not re.fullmatch("[0-9a-f]{64}", approval["INSTRUCTION_SHA256"])
                or not isinstance(approval["TEXT"], str) or not 0 < len(approval["TEXT"]) <= 4096
                or PurePosixPath(path).name not in approval["TEXT"]):
            raise ValueError("Private catalog adoption lacks exact hash-bound user approval")
        seen.add(path)
        instruction = row["APPROVAL_INSTRUCTION"] if portable else content_identity(load_optional(bundle, row["APPROVAL_INSTRUCTION"]))
        if instruction is None or set(instruction) != {"size", "sha256", "blob"}:
            raise ValueError("Private catalog approval instruction identity is missing")
        validate_manifest([{"path": "approval.txt", **instruction}])
        if instruction["sha256"] != approval["INSTRUCTION_SHA256"] or instruction["size"] <= 0:
            raise ValueError("Private catalog approval instruction hash differs")
        if not portable and approval["TEXT"].replace("\r\n", "\n") not in load_optional(bundle, row["APPROVAL_INSTRUCTION"]).decode("utf-8-sig").replace("\r\n", "\n"):
            raise ValueError("Private catalog approval is not the actual user instruction")
        before = row["CANONICAL_BEFORE"] if portable else content_identity(load_optional(bundle, row["CANONICAL_BEFORE"]), path)
        after = row["CANONICAL_AFTER"] if portable else content_identity(load_optional(bundle, row["CANONICAL_AFTER"]), path)
        if before != actual.get(path) or after is None:
            raise ValueError("Private catalog adoption does not bind its complete before/after state")
        validate_manifest([after])
        if after["path"] != path or after["sha256"] != row["SHA256"] or after["size"] <= 0:
            raise ValueError("Private catalog result differs from approved staged bytes")
        if before is not None and before != after:
            source = value["SOURCE_SNAPSHOTS"]["PUBLIC_PRODUCT_DATA"]["AFTER"].get(path)
            if source != {key: before[key] for key in ("size", "blob")}:
                raise ValueError("Private catalog adoption would overwrite unexplained private content")
            if not portable:
                placeholder = read_json(load_descriptor(bundle, row["CANONICAL_BEFORE"]))
                if path != "QuestRewards.json" and (not isinstance(placeholder, dict) or placeholder.get("Entries") != []):
                    raise ValueError("Only an empty public placeholder may be replaced by private adoption")
        if not portable:
            content = read_json(load_descriptor(bundle, row["CANONICAL_AFTER"]))
            if path == "QuestRewards.json":
                if (not isinstance(content, dict) or not content
                        or any(not isinstance(table, dict) or not isinstance(table.get("Entries"), list)
                               or not table["Entries"] for table in content.values())):
                    raise ValueError("Private quest rewards must contain nonempty reward tables")
                if before is not None:
                    existing = read_json(load_descriptor(bundle, row["CANONICAL_BEFORE"]))
                    if (not isinstance(existing, dict)
                            or any(value_hash(content.get(key, MISSING)) != value_hash(table)
                                   for key, table in existing.items())):
                        raise ValueError("Private quest reward adoption must preserve every existing reward table")
            elif not isinstance(content, dict) or not isinstance(content.get("Entries"), list) or not content["Entries"]:
                raise ValueError("Private policy adoption cannot install an empty public placeholder")
        actual[path] = after


def validate_runtime_exclusions(bundle, value, actual, portable=False):
    # Absence preserves validation of historical receipts; every new plan supplies the list.
    if "RUNTIME_EXCLUSIONS" not in value:
        return
    rows = value["RUNTIME_EXCLUSIONS"]
    if not isinstance(rows, list):
        raise ValueError("Runtime exclusions must be a complete ordered list")
    before = {row["path"]: row for row in read_json(load_descriptor(bundle, value["BEFORE_MANIFEST"]))}
    seen = []
    for row in rows:
        if not isinstance(row, dict) or set(row) != {"PATH", "CANONICAL_BEFORE", "EXCLUDED_CONTENT", "REASON"}:
            raise ValueError("Runtime exclusion schema differs")
        path = row["PATH"]
        relative(path)
        if runtime_content_path(path) or path in seen or row["REASON"] != "DENIED_ADMISSION":
            raise ValueError("Runtime exclusion does not identify a denied path")
        seen.append(path)
        identities = {}
        for field in ("CANONICAL_BEFORE", "EXCLUDED_CONTENT"):
            identity = row[field] if portable else content_identity(load_optional(bundle, row[field]), path)
            if identity is not None:
                validate_manifest([identity])
                if identity["path"] != path:
                    raise ValueError("Runtime exclusion identity names another path")
            identities[field] = identity
        if (identities["CANONICAL_BEFORE"] != before.get(path)
                or identities["EXCLUDED_CONTENT"] is None
                or identities["EXCLUDED_CONTENT"] != actual.get(path)):
            raise ValueError("Runtime exclusion does not preserve complete before/projected identities")
        actual.pop(path)
    if seen != sorted(seen) or any(not runtime_content_path(path) for path in actual):
        raise ValueError("Canonical runtime admission is incomplete")


def validate_supplemental_content(bundle, value, actual, portable=False):
    """Replay restorations, private policies, generated items and runtime exclusions."""
    catalogs = required_catalogs(value.get("REQUIRED_CURRENT_CATALOGS", []))
    expected, seen = {}, set()
    for mapping in catalogs:
        path = mapping["PATH"]
        source = value["SOURCE_SNAPSHOTS"][mapping["AUTHORITY"]]["AFTER"].get(path)
        if source is None or path in value["PROTECTED_GENERATED_FILES"]:
            raise ValueError("Required catalog has no current source identity or is protected")
        if "ENTRIES" in mapping or path not in actual:
            expected[path] = mapping
    for restoration in value.get("CATALOG_RESTORATIONS", []):
        if not isinstance(restoration, dict) or not isinstance(restoration.get("PATH"), str):
            raise ValueError("Required-current catalog restoration schema differs")
        path = restoration["PATH"]
        mapping = expected.get(path)
        if mapping is None or path in seen:
            raise ValueError("Catalog restoration is unapproved or duplicated")
        fields = set(mapping) | {"CANONICAL_BEFORE", "CANONICAL_AFTER"}
        if "ENTRIES" in mapping:
            fields |= {"UPSTREAM_CURRENT", "ENTRY_PROOF"}
        if set(restoration) != fields or {key: restoration[key] for key in mapping} != mapping:
            raise ValueError("Catalog restoration is unapproved or would overwrite existing content")
        seen.add(path)
        before_data = None if portable else load_optional(bundle, restoration["CANONICAL_BEFORE"])
        after_data = None if portable else load_optional(bundle, restoration["CANONICAL_AFTER"])
        before = restoration["CANONICAL_BEFORE"] if portable else content_identity(before_data, path)
        result = restoration["CANONICAL_AFTER"] if portable else content_identity(after_data, path)
        if result is None:
            raise ValueError("Required-current catalog restoration is empty")
        validate_manifest([result])
        source = value["SOURCE_SNAPSHOTS"][restoration["AUTHORITY"]]["AFTER"][path]
        if result["path"] != path or before != actual.get(path):
            raise ValueError("Catalog restoration does not bind the current canonical identity")
        if "ENTRIES" in mapping:
            source_data = None if portable else load_optional(bundle, restoration["UPSTREAM_CURRENT"])
            identity = restoration["UPSTREAM_CURRENT"] if portable else content_identity(source_data)
            if (not isinstance(identity, dict) or set(identity) != {"size", "sha256", "blob"}
                    or {key: identity[key] for key in ("size", "blob")} != source):
                raise ValueError("Selective catalog entries differ from the current source snapshot")
            validate_manifest([{"path": path, **identity}])
            if not portable:
                replayed, proof = restore_catalog_entries(source_data, before_data, mapping["ENTRIES"])
                if replayed != after_data or proof != restoration["ENTRY_PROOF"]:
                    raise ValueError("Selective catalog result is not the measured add-only replay")
            validate_catalog_entry_proof(restoration["ENTRY_PROOF"], mapping["ENTRIES"], before, result)
        elif before is not None or {key: result[key] for key in ("size", "blob")} != source:
            raise ValueError("Catalog restoration differs from exact current source bytes")
        actual[path] = result
    if seen != set(expected):
        raise ValueError("Required-current catalog restorations are incomplete")
    validate_private_catalog_adoptions(bundle, value, actual, portable)
    validate_generated_surfaces_adoptions(bundle, value, actual, portable)
    proof, adoption = value.get("GENERATED_ITEMS_PROVENANCE"), value.get("GENERATED_ITEMS_ADOPTION")
    if not proof:
        if adoption or value.get("GENERATED_ITEMS_LOCAL_RECEIPT"):
            raise ValueError("Generated adoption lacks measured provenance")
        validate_runtime_exclusions(bundle, value, actual, portable)
        return
    output = validate_generated_items_provenance(proof, value["PUBLIC_MASTER_SHA"])
    if "items.dat" not in value["PROTECTED_GENERATED_FILES"]:
        raise ValueError("Generated adoption must retain protected items.dat ownership")
    if not portable:
        local_receipt = value.get("GENERATED_ITEMS_LOCAL_RECEIPT")
        if local_receipt:
            path = load_descriptor(bundle, local_receipt)
            raw = read_json(path)
            if (digest(path.read_bytes()) != proof["LOCAL_RECEIPT_SHA256"]
                    or any(raw.get(key) != proof[key] for key in ("FORMAT_VERSION", "PUBLIC_MASTER_SHA", "CREATED_UTC", "EXPORTER_SOURCE_CLOSURE", "VALIDATION"))
                    or {key: raw["OUTPUT"][key] for key in ("size", "sha256", "blob")} != {key: output[key] for key in ("size", "sha256", "blob")}):
                raise ValueError("Local generated receipt/projection identity differs")
        else:
            prior = value.get("PRIOR_LOCAL_RECONCILIATION")
            if not prior or read_json(load_descriptor(Path(prior["STATE_ROOT"]), prior["RECONCILIATION"])).get("GENERATED_ITEMS_PROVENANCE") != proof:
                raise ValueError("Generated provenance lacks the validated applied local receipt")
    if adoption:
        if set(adoption) != {"CANONICAL_BEFORE", "CANONICAL_AFTER"}:
            raise ValueError("Generated items adoption schema differs")
        before = adoption["CANONICAL_BEFORE"] if portable else content_identity(load_optional(bundle, adoption["CANONICAL_BEFORE"]), "items.dat")
        after = adoption["CANONICAL_AFTER"] if portable else content_identity(load_optional(bundle, adoption["CANONICAL_AFTER"]), "items.dat")
        if before != actual.get("items.dat") or after != output:
            raise ValueError("Generated items adoption does not bind complete before/after identities")
        actual["items.dat"] = after
    if actual.get("items.dat") != output:
        raise ValueError("Canonical items.dat differs from current generated provenance")
    validate_runtime_exclusions(bundle, value, actual, portable)


def reconcile_plan(root, profile, bundle, public_sha, dump_sha, baseline, before_rows, resolutions=None, generated_receipt=None, surfaces_receipt=None):
    resolutions = resolutions or {}
    canonical = Path(profile["CANONICAL_GAMEDATA_ROOT"]).resolve(strict=True)
    snapshots, records, projected = {}, [], {}
    for authority, base_sha, target_sha in (
            ("PRIVATE_CONTENT_UPSTREAM", baseline["CONTENT_DUMP_SHA"], dump_sha),
            ("PUBLIC_PRODUCT_DATA", baseline["PUBLIC_MASTER_SHA"], public_sha)):
        old = tree_files(root, profile, authority, base_sha)
        new = tree_files(root, profile, authority, target_sha)
        snapshots[authority] = {"BASE_SHA": base_sha, "CURRENT_SHA": target_sha, "BEFORE": old, "AFTER": new}
        for path, action in changes(old, new):
            target = checked(canonical, path, exists=False)
            private = projected[path] if path in projected else target.read_bytes() if target.is_file() else None
            base = source_blob(root, profile, authority, old[path]["blob"]) if path in old else None
            incoming = source_blob(root, profile, authority, new[path]["blob"]) if path in new else None
            resolved = resolutions.get(authority + ":" + path, {})
            try:
                result = merge_bytes(base, incoming, private, resolved)
            except ContentConflict as conflict:
                write_json(bundle / "conflict.json", {"AUTHORITY": authority, "PATH": path,
                           "POINTER": conflict.pointer, "INPUT_HASHES": conflict.hashes,
                           "UPSTREAM_BEFORE": stash(bundle, "conflict/upstream-before", base),
                           "UPSTREAM_AFTER": stash(bundle, "conflict/upstream-after", incoming),
                           "CANONICAL_BEFORE": stash(bundle, "conflict/canonical-before", private)})
                raise
            if path in profile.get("PROTECTED_GENERATED_FILES", ()) and result != private:
                raise ValueError("Required independently generated content would be overwritten")
            prefix = "reconciliation-payload/" + str(len(records)) + "/"
            records.append({"AUTHORITY": authority, "PATH": path, "STATUS": action,
                            "UPSTREAM_BEFORE": stash(bundle, prefix + "upstream-before", base),
                            "UPSTREAM_AFTER": stash(bundle, prefix + "upstream-after", incoming),
                            "CANONICAL_BEFORE": stash(bundle, prefix + "canonical-before", private),
                            "CANONICAL_AFTER": stash(bundle, prefix + "canonical-after", result),
                            "RESOLUTIONS": resolved})
            projected[path] = result
    catalogs, restorations = required_catalogs(profile.get("REQUIRED_CURRENT_CATALOGS", [])), []
    for mapping in catalogs:
        authority, path = mapping["AUTHORITY"], mapping["PATH"]
        current_source = snapshots[authority]["AFTER"].get(path)
        if current_source is None or path in profile.get("PROTECTED_GENERATED_FILES", ()):
            raise ValueError("Required catalog is not present in its current authority or is generated")
        target = checked(canonical, path, exists=False)
        private = projected[path] if path in projected else target.read_bytes() if target.is_file() else None
        if "ENTRIES" in mapping:
            incoming = source_blob(root, profile, authority, current_source["blob"])
            result, proof = restore_catalog_entries(incoming, private, mapping["ENTRIES"])
            prefix = "catalog-restorations/" + str(len(restorations)) + "/"
            before = stash(bundle, prefix + "canonical-before", private)
            restorations.append({**mapping, "CANONICAL_BEFORE": before,
                "CANONICAL_AFTER": before if result == private else stash(bundle, prefix + "canonical-after", result),
                "UPSTREAM_CURRENT": stash(bundle, prefix + "upstream-current", incoming), "ENTRY_PROOF": proof})
            if result != private:
                projected[path] = result
            continue
        if private is not None:
            continue
        incoming = source_blob(root, profile, authority, current_source["blob"])
        restorations.append({**mapping, "CANONICAL_BEFORE": None,
            "CANONICAL_AFTER": stash(bundle, "catalog-restorations/" + str(len(restorations)), incoming)})
        projected[path] = incoming
    private_adoptions = []
    mappings = profile.get("PRIVATE_CATALOG_ADOPTIONS", [])
    if not isinstance(mappings, list) or any(not isinstance(row, dict) for row in mappings) or (mappings and {row.get("PATH") for row in mappings} not in (PRIVATE_CHEST_CATALOGS, {"QuestRewards.json"}, PRIVATE_CHEST_CATALOGS | {"QuestRewards.json"})):
        raise ValueError("Private catalog adoption requires grouped chest policy mappings or independent QuestRewards.json")
    for mapping in sorted(mappings, key=lambda row: row["PATH"]):
        if set(mapping) != {"PATH", "STAGED_PATH", "SHA256", "APPROVAL"}:
            raise ValueError("Private policy profile mapping schema differs")
        path, approval = mapping["PATH"], mapping["APPROVAL"]
        if not isinstance(approval, dict) or set(approval) != {"BY", "INSTRUCTION_PATH", "INSTRUCTION_SHA256", "TEXT"}:
            raise ValueError("Private policy mapping requires the actual hash-bound user instruction")
        inputs = []
        for selected in (mapping["STAGED_PATH"], approval["INSTRUCTION_PATH"]):
            selected = Path(selected)
            source = checked(root, selected.relative_to(root).as_posix() if selected.is_absolute() else selected.as_posix())
            if git(root, "check-ignore", "--", str(source)).strip() == b"":
                raise ValueError("Private policy inputs and approval must stay ignored in the owning checkout")
            inputs.append(source.read_bytes())
        data, instruction = inputs
        if digest(data) != mapping["SHA256"] or digest(instruction) != approval["INSTRUCTION_SHA256"]:
            raise ValueError("Staged private policy or actual instruction hash changed")
        target = checked(canonical, path, exists=False)
        private = projected[path] if path in projected else target.read_bytes() if target.is_file() else None
        prefix = "private-catalog-adoptions/" + str(len(private_adoptions)) + "/"
        private_adoptions.append({"PATH": path, "SHA256": mapping["SHA256"],
            "APPROVAL": {key: approval[key] for key in ("BY", "INSTRUCTION_SHA256", "TEXT")},
            "APPROVAL_INSTRUCTION": stash(bundle, prefix + "instruction", instruction),
            "CANONICAL_BEFORE": stash(bundle, prefix + "before", private),
            "CANONICAL_AFTER": stash(bundle, prefix + "after", data)})
        if data != private:
            projected[path] = data
    generated, adoption, local_receipt = None, None, None
    if surfaces_receipt and not generated_receipt:
        surface_path = Path(surfaces_receipt)
        surface_path = checked(root, surface_path.relative_to(root).as_posix() if surface_path.is_absolute() else surface_path.as_posix())
        if git(root, "check-ignore", "--", str(surface_path)).strip() == b"":
            raise ValueError("Surface intake requires an ignored measured local receipt")
        generated_receipt = read_json(surface_path)["GENERATED_ITEMS_RECEIPT"]
    if generated_receipt:
        if "items.dat" not in profile.get("PROTECTED_GENERATED_FILES", ()):
            raise ValueError("Generated items adoption requires the existing protected items.dat binding")
        generated, data, raw = generated_items_receipt(root, profile, generated_receipt, public_sha)
        local_receipt = stash(bundle, "generated-items-local-receipt.json", raw)
        target = checked(canonical, "items.dat", exists=False)
        private = projected["items.dat"] if "items.dat" in projected else target.read_bytes() if target.is_file() else None
        if data != private:
            adoption = {"CANONICAL_BEFORE": stash(bundle, "generated-items-adoption/before", private),
                        "CANONICAL_AFTER": stash(bundle, "generated-items-adoption/after", data)}
            projected["items.dat"] = data
    elif baseline.get("APPLIED_RECONCILIATION_BUNDLE"):
        previous = Path(baseline["APPLIED_RECONCILIATION_BUNDLE"])
        prior_file = previous / "reconciliation.json"
        if (not previous.resolve().is_relative_to(Path(profile["STATE_ROOT"]).resolve())
                or digest(prior_file.read_bytes()) != baseline["APPLIED_RECONCILIATION_SHA256"]):
            raise ValueError("Prior generated items provenance was changed")
        prior = read_json(prior_file)
        validate_reconciliation(previous, prior)
        candidate = prior.get("GENERATED_ITEMS_PROVENANCE")
        if candidate:
            receipt_bundle, receipt_value = previous, prior
            while not receipt_value.get("GENERATED_ITEMS_LOCAL_RECEIPT"):
                checkpoint = receipt_value.get("PRIOR_LOCAL_RECONCILIATION")
                if not checkpoint:
                    raise ValueError("Generated items reuse lacks its original measured local receipt")
                checkpoint_path = load_descriptor(Path(checkpoint["STATE_ROOT"]), checkpoint["RECONCILIATION"])
                receipt_bundle, receipt_value = checkpoint_path.parent, read_json(checkpoint_path)
            path = load_descriptor(receipt_bundle, receipt_value["GENERATED_ITEMS_LOCAL_RECEIPT"])
            generated, _, raw = generated_items_receipt(root, profile, path, public_sha)
            if ({key: item for key, item in generated.items() if key != "QUALIFIED_REUSE"}
                    != {key: item for key, item in candidate.items() if key != "QUALIFIED_REUSE"}):
                raise ValueError("Generated items reuse differs from the validated original provenance")
            local_receipt = stash(bundle, "generated-items-local-receipt.json", raw)
    surface_proof, surface_adoptions, surface_local_receipt = None, [], None
    if surfaces_receipt:
        surface_proof, outputs, raw = generated_surfaces_receipt(root, profile, surfaces_receipt, public_sha, generated)
        surface_local_receipt = stash(bundle, "generated-surfaces-local-receipt.json", raw)
        for path, data in sorted(outputs.items()):
            target = checked(canonical, path, exists=False)
            private = projected[path] if path in projected else target.read_bytes() if target.is_file() else None
            if private == data:
                continue
            before_cells = surface_cells(private) if private is not None else {}
            after_cells = surface_cells(data)
            if 0 in before_cells or {cell: payload for cell, payload in after_cells.items() if cell != 0} != before_cells:
                raise ValueError("Generated surfaces may only add missing cell zero; every private cell must remain verbatim")
            prefix = "generated-surfaces-adoptions/" + str(len(surface_adoptions)) + "/"
            surface_adoptions.append({"PATH": path, "CANONICAL_BEFORE": stash(bundle, prefix + "before", private),
                "CANONICAL_AFTER": stash(bundle, prefix + "after", data), "BEFORE_CELLS": surface_cell_index(before_cells)})
            projected[path] = data
    if profile.get("REQUIRE_GENERATED_ITEMS") is True and not generated:
        raise ValueError("This release requires current generated items.dat or verified unchanged exporter/input/output reuse")
    expected = {row["path"]: row for row in before_rows}
    for path, data in projected.items():
        if data is None:
            expected.pop(path, None)
        else:
            expected[path] = {"path": path, "size": len(data), "sha256": digest(data), "blob": blob(data)}
    exclusions = []
    for path in sorted(expected):
        if runtime_content_path(path):
            continue
        target = checked(canonical, path, exists=False)
        private = target.read_bytes() if target.is_file() else None
        excluded = projected[path] if path in projected else private
        if excluded is None:
            raise ValueError("Denied runtime path lacks its projected preserved content")
        prefix = "runtime-exclusions/" + str(len(exclusions)) + "/"
        original = stash(bundle, prefix + "canonical-before", private)
        archive = original if excluded == private else stash(bundle, prefix + "excluded-content", excluded)
        exclusions.append({"PATH": path, "CANONICAL_BEFORE": original,
                           "EXCLUDED_CONTENT": archive, "REASON": "DENIED_ADMISSION"})
        projected[path] = None
    for exclusion in exclusions:
        expected.pop(exclusion["PATH"])
    if any(not runtime_content_path(path) for path in expected):
        raise ValueError("Target manifest contains a path without positive runtime admission")
    after_rows = sorted(expected.values(), key=lambda row: row["path"])
    plan = {"FORMAT_VERSION": 1, "PUBLIC_MASTER_SHA": public_sha, "CONTENT_DUMP_SHA": dump_sha,
            "CANONICAL_BEFORE_MANIFEST": digest(encoded(before_rows)),
            "WINDOWS_GAMEDATA_MANIFEST": digest(encoded(after_rows)), "SOURCE_SNAPSHOTS": snapshots,
            "CHANGES": records, "PROTECTED_GENERATED_FILES": profile.get("PROTECTED_GENERATED_FILES", []),
            "RUNTIME_EXCLUSIONS": exclusions,
            "BEFORE_MANIFEST": descriptor(bundle, bundle / "before-manifest.json")}
    if catalogs:
        plan.update(REQUIRED_CURRENT_CATALOGS=catalogs, CATALOG_RESTORATIONS=restorations)
    if private_adoptions:
        plan["PRIVATE_CATALOG_ADOPTIONS"] = private_adoptions
    if generated:
        plan["GENERATED_ITEMS_PROVENANCE"] = generated
        if local_receipt:
            plan["GENERATED_ITEMS_LOCAL_RECEIPT"] = local_receipt
        if adoption:
            plan["GENERATED_ITEMS_ADOPTION"] = adoption
    if surface_proof:
        plan.update(GENERATED_SURFACES_PROVENANCE=surface_proof, GENERATED_SURFACES_ADOPTIONS=surface_adoptions,
                    GENERATED_SURFACES_LOCAL_RECEIPT=surface_local_receipt)
    if baseline.get("APPLIED_RECONCILIATION_BUNDLE"):
        previous = Path(baseline["APPLIED_RECONCILIATION_BUNDLE"])
        if not previous.resolve().is_relative_to(Path(profile["STATE_ROOT"]).resolve()):
            raise ValueError("Prior reconciliation escaped local release state")
        prior_file = previous / "reconciliation.json"
        if digest(prior_file.read_bytes()) != baseline["APPLIED_RECONCILIATION_SHA256"]:
            raise ValueError("Prior applied reconciliation was changed")
        prior = read_json(prior_file)
        validate_reconciliation(previous, prior)
        if (prior["WINDOWS_GAMEDATA_MANIFEST"] != baseline["WINDOWS_GAMEDATA_MANIFEST"]
                or any(prior[key] != baseline[key] for key in ("PUBLIC_MASTER_SHA", "CONTENT_DUMP_SHA"))):
            raise ValueError("Canonical baseline differs from its applied reconciliation")
        state_root = Path(profile["STATE_ROOT"]).resolve()
        plan["PRIOR_LOCAL_RECONCILIATION"] = {"STATE_ROOT": str(state_root),
                    "RECONCILIATION": descriptor(state_root, prior_file)}
    write_json(bundle / "manifest.json", after_rows)
    plan["CANONICAL_MANIFEST"] = descriptor(bundle, bundle / "manifest.json")
    write_json(bundle / "reconciliation.json", plan)
    return plan, projected


def validate_reconciliation(bundle, reconciliation):
    before = read_json(load_descriptor(bundle, reconciliation["BEFORE_MANIFEST"]))
    after = read_json(load_descriptor(bundle, reconciliation["CANONICAL_MANIFEST"]))
    if validate_manifest(before) != reconciliation["CANONICAL_BEFORE_MANIFEST"]:
        raise ValueError("Reconciliation base manifest differs")
    if reconciliation.get("PRIOR_LOCAL_RECONCILIATION"):
        prior_local = reconciliation["PRIOR_LOCAL_RECONCILIATION"]
        state_root = Path(prior_local["STATE_ROOT"]).resolve(strict=True)
        if not Path(bundle).resolve().is_relative_to(state_root):
            raise ValueError("Local replay history escaped ignored release state")
        prior_path = load_descriptor(state_root, prior_local["RECONCILIATION"])
        prior = read_json(prior_path)
        validate_reconciliation(prior_path.parent, prior)
        if prior["WINDOWS_GAMEDATA_MANIFEST"] != reconciliation["CANONICAL_BEFORE_MANIFEST"]:
            raise ValueError("Approved reconciliation history does not bind the canonical baseline")
        snapshots = reconciliation["SOURCE_SNAPSHOTS"]
        if (snapshots["PRIVATE_CONTENT_UPSTREAM"]["BASE_SHA"] != prior["CONTENT_DUMP_SHA"]
                or snapshots["PUBLIC_PRODUCT_DATA"]["BASE_SHA"] != prior["PUBLIC_MASTER_SHA"]):
            raise ValueError("Reconciliation history skips an upstream transition")
    expected_changes = set()
    for authority, snapshot in reconciliation["SOURCE_SNAPSHOTS"].items():
        if authority not in ("PRIVATE_CONTENT_UPSTREAM", "PUBLIC_PRODUCT_DATA"):
            raise ValueError("Unknown content authority")
        expected_changes |= {(authority, path, action) for path, action in changes(snapshot["BEFORE"], snapshot["AFTER"])}
    if set(reconciliation["SOURCE_SNAPSHOTS"]) != {"PRIVATE_CONTENT_UPSTREAM", "PUBLIC_PRODUCT_DATA"}:
        raise ValueError("Both private upstream and public resource transitions must be accounted for")
    if (reconciliation["SOURCE_SNAPSHOTS"]["PRIVATE_CONTENT_UPSTREAM"]["CURRENT_SHA"] != reconciliation["CONTENT_DUMP_SHA"]
            or reconciliation["SOURCE_SNAPSHOTS"]["PUBLIC_PRODUCT_DATA"]["CURRENT_SHA"] != reconciliation["PUBLIC_MASTER_SHA"]):
        raise ValueError("Reconciliation snapshots do not bind the frozen source/dump tuple")
    actual_changes, actual = set(), {row["path"]: row for row in before}
    previous_results = {}
    for change in reconciliation["CHANGES"]:
        authority, path, action = change["AUTHORITY"], change["PATH"], change["STATUS"]
        key = authority, path, action
        if key in actual_changes:
            raise ValueError("Duplicate content transition")
        actual_changes.add(key)
        snapshot = reconciliation["SOURCE_SNAPSHOTS"][authority]
        base = load_optional(bundle, change["UPSTREAM_BEFORE"])
        incoming = load_optional(bundle, change["UPSTREAM_AFTER"])
        private = load_optional(bundle, change["CANONICAL_BEFORE"])
        result = load_optional(bundle, change["CANONICAL_AFTER"])
        for rows, data in ((snapshot["BEFORE"], base), (snapshot["AFTER"], incoming)):
            if (data is None) != (path not in rows) or (data is not None and
                    (blob(data) != rows[path]["blob"] or len(data) != rows[path]["size"])):
                raise ValueError("Upstream payload does not match freshly fetched blob states")
        old = actual.get(path)
        if ((private is None) != (old is None) or (private is not None and
                (digest(private) != old["sha256"] or blob(private) != old["blob"] or len(private) != old["size"]))):
            raise ValueError("Canonical before-payload differs from the complete manifest")
        if merge_bytes(base, incoming, private, change["RESOLUTIONS"]) != result:
            raise ValueError("Content result is not the guarded three-way reconciliation")
        if path in reconciliation["PROTECTED_GENERATED_FILES"] and private != result:
            raise ValueError("Required generated/private content was overwritten")
        if result is None:
            actual.pop(path, None)
        else:
            actual[path] = {"path": path, "size": len(result), "sha256": digest(result), "blob": blob(result)}
        previous_results[path] = result
    if actual_changes != expected_changes:
        raise ValueError("Approved upstream additions/modifications/removals are incomplete")
    validate_supplemental_content(bundle, reconciliation, actual)
    if sorted(actual.values(), key=lambda row: row["path"]) != after:
        raise ValueError("Canonical manifest loses or changes unrelated private content")
    if validate_manifest(after) != reconciliation["WINDOWS_GAMEDATA_MANIFEST"]:
        raise ValueError("Canonical result digest differs")
    return after


def intake(root, profile, resolutions=None, generated_receipt=None, surfaces_receipt=None):
    attempt_id, started = uuid.uuid4().hex, now()
    public_sha, dump_sha, public_fetch, dump_fetch = fresh_sources(root, profile)
    public_fetch["ATTEMPT_ID"] = dump_fetch["ATTEMPT_ID"] = attempt_id
    state_root = Path(profile["STATE_ROOT"])
    state_root.mkdir(parents=True, exist_ok=True)
    baseline_path = state_root / "canonical-state.json"
    baseline = read_json(baseline_path if baseline_path.is_file() else profile["INITIAL_PROVENANCE"])
    if baseline_path.is_file() and not baseline.get("APPLIED_RECONCILIATION_BUNDLE"):
        raise ValueError("Canonical state lacks an actually applied reconciliation")
    for key in ("PUBLIC_MASTER_SHA", "CONTENT_DUMP_SHA", "WINDOWS_GAMEDATA_MANIFEST"):
        if not baseline.get(key):
            raise ValueError("Canonical content baseline is unknown")
    rows, states = inventory(profile["CANONICAL_GAMEDATA_ROOT"])
    if validate_manifest(rows) != baseline["WINDOWS_GAMEDATA_MANIFEST"]:
        raise ValueError("Canonical GameData changed without approved reconciliation")
    bundle = state_root / ("attempt-" + uuid.uuid4().hex)
    bundle.mkdir()
    write_json(bundle / "before-manifest.json", rows)
    plan, projected = reconcile_plan(root, profile, bundle, public_sha, dump_sha, baseline, rows, resolutions, generated_receipt, surfaces_receipt)
    validate_reconciliation(bundle, plan)
    attempt = {"FORMAT_VERSION": 1, "PUBLIC_MASTER_SHA": public_sha, "LOCAL_WINDOWS_SOURCE_SHA": public_sha,
               "ATTEMPT_ID": attempt_id, "ATTEMPT_STARTED_UTC": started,
               "CONTENT_DUMP_SHA": dump_sha, "WINDOWS_GAMEDATA_MANIFEST": plan["WINDOWS_GAMEDATA_MANIFEST"],
               "PUBLIC_FETCH": public_fetch, "CONTENT_DUMP_FETCH": dump_fetch,
               "CREATED_UTC": now(), "WINDOWS_BUILD": "NOT_RUN", "WINDOWS_USER_ACCEPTED": "NO",
               "RECONCILIATION": descriptor(bundle, bundle / "reconciliation.json"),
               "CANONICAL_MANIFEST": descriptor(bundle, bundle / "manifest.json"),
               "LAUNCHER_SHA256": digest((Path(root) / "start-engines.ps1").read_bytes())}
    write_json(bundle / "prepared.json", attempt)
    write_json(state_root / "latest-plan.json", {"BUNDLE": str(bundle), "PLAN_SHA256": digest(encoded(plan))})
    return bundle, attempt, plan, projected, states


def apply_content(root, profile, approved_plan_sha256, resolutions=None, generated_receipt=None, surfaces_receipt=None):
    bundle, attempt, plan, projected, states = intake(root, profile, resolutions, generated_receipt, surfaces_receipt)
    if digest(encoded(plan)) != approved_plan_sha256:
        raise ValueError("Fresh content plan differs from the approved plan")
    canonical = Path(profile["CANONICAL_GAMEDATA_ROOT"])
    excluded = {row["PATH"]: row["CANONICAL_BEFORE"] for row in plan.get("RUNTIME_EXCLUSIONS", [])}
    old = {}
    try:
        if {row["path"]: file_state(checked(canonical, row["path"])) for row in read_json(bundle / "before-manifest.json")} != states:
            raise ValueError("Canonical data changed before reconciliation")
        for path, data in projected.items():
            target = checked(canonical, path, exists=False)
            old[path] = ("RUNTIME_EXCLUSION", excluded[path]) if path in excluded else target.read_bytes() if target.is_file() else None
            if data is None:
                target.unlink(missing_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                temporary = target.with_name(target.name + ".release-contract-tmp")
                if temporary.exists():
                    raise ValueError("Canonical temporary path already exists")
                temporary.write_bytes(data)
                os.replace(temporary, target)
        actual, _ = inventory(canonical)
        if actual != read_json(bundle / "manifest.json"):
            raise ValueError("Reconciled physical content differs from approved complete result")
    except Exception:
        for path, data in old.items():
            target = checked(canonical, path, exists=False)
            if isinstance(data, tuple):
                archive = data[1]
                if archive is None:
                    target.unlink(missing_ok=True)
                else:
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(load_descriptor(bundle, archive), target)
            elif data is None:
                target.unlink(missing_ok=True)
            else:
                target.write_bytes(data)
        raise
    write_json(Path(profile["STATE_ROOT"]) / "canonical-state.json",
               {**{key: attempt[key] for key in TUPLE}, "APPLIED_RECONCILIATION_BUNDLE": str(bundle),
                "APPLIED_RECONCILIATION_SHA256": digest((bundle / "reconciliation.json").read_bytes())})
    return bundle


def process_parent(pid):
    if os.name != "nt":
        try:
            return int(Path(f"/proc/{pid}/stat").read_text().rsplit(")", 1)[1].split()[1])
        except (OSError, ValueError, IndexError):
            return 0
    from ctypes import wintypes
    class Entry(ctypes.Structure):
        _fields_ = [("dwSize", wintypes.DWORD), ("cntUsage", wintypes.DWORD), ("th32ProcessID", wintypes.DWORD),
                    ("th32DefaultHeapID", ctypes.c_size_t), ("th32ModuleID", wintypes.DWORD),
                    ("cntThreads", wintypes.DWORD), ("th32ParentProcessID", wintypes.DWORD),
                    ("pcPriClassBase", wintypes.LONG), ("dwFlags", wintypes.DWORD), ("szExeFile", wintypes.WCHAR * 260)]
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    kernel.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel.Process32FirstW.argtypes = [wintypes.HANDLE, ctypes.POINTER(Entry)]
    kernel.Process32FirstW.restype = wintypes.BOOL
    kernel.Process32NextW.argtypes = [wintypes.HANDLE, ctypes.POINTER(Entry)]
    kernel.Process32NextW.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.CloseHandle.restype = wintypes.BOOL
    handle = kernel.CreateToolhelp32Snapshot(2, 0)
    if handle == ctypes.c_void_p(-1).value:
        return 0
    entry = Entry()
    entry.dwSize = ctypes.sizeof(entry)
    try:
        more = kernel.Process32FirstW(handle, ctypes.byref(entry))
        while more:
            if entry.th32ProcessID == pid:
                return int(entry.th32ParentProcessID)
            more = kernel.Process32NextW(handle, ctypes.byref(entry))
    finally:
        kernel.CloseHandle(handle)
    return 0


def feature_source(root):
    """Identify a development checkout without promoting it to public master."""
    head = git(root, "rev-parse", "HEAD").decode().strip()
    branch = git(root, "symbolic-ref", "--short", "HEAD").decode().strip()
    if not branch.startswith("codex/") or not re.fullmatch("[0-9a-f]{40}", head):
        raise ValueError("Feature validation requires an isolated codex/ branch")
    paths = (*PRODUCT_PATHS, "Tools", "SharedBuild", "Directory.Build.props", "global.json", "NuGet.Config")
    patch = git(root, "diff", "--binary", "--no-ext-diff", "--no-renames", "HEAD", "--", *paths)
    untracked = []
    for name in sorted(set(git(root, "ls-files", "--others", "--exclude-standard", "-z", "--", *paths).split(b"\0")) - {b""}):
        path = checked(root, name.decode())
        before = file_state(path)
        hashed = digest(path.read_bytes())
        if file_state(path) != before:
            raise ValueError("Feature source changed during measurement")
        untracked.append({"path": path.relative_to(root).as_posix(), "size": before[2], "sha256": hashed})
    return {"HEAD": head, "BRANCH": branch, "TRACKED_DIFF_SHA256": digest(patch), "UNTRACKED_SOURCE": untracked}


def feature_command(root, child):
    """Use the existing build wrapper or a rooted, explicit development project."""
    approved = [os.environ.get("ComSpec", r"C:\Windows\System32\cmd.exe"), "/d", "/c",
                str(Path(root) / "NewZoneEngineBuild/build.cmd")]
    if len(child) == len(approved) and all(os.path.normcase(os.path.normpath(a)) == os.path.normcase(os.path.normpath(b))
                                          for a, b in zip(child, approved)):
        return None
    if len(child) >= 4 and all(os.path.normcase(os.path.normpath(a)) == os.path.normcase(os.path.normpath(b))
                              for a, b in zip(child[:3], approved[:3])):
        wrapper = os.path.normcase(os.path.normpath(child[3]))
        dao_wrapper = checked(root, "Tools/run_mission_dao_validation.cmd")
        connected_wrapper = checked(root, "Tools/run_newengine_connected_acceptance.cmd")
        if wrapper == os.path.normcase(str(dao_wrapper)) and child[4:] == ["--chests-only"]:
            return checked(root, "Tools/MissionDaoValidation/MissionDaoValidation.csproj")
        if wrapper == os.path.normcase(str(connected_wrapper)) and len(child) == 11 and (
                child[4], child[6], child[8], child[9]) == ("--engine", "--login-engine", "--chests-only", "--world-engine"):
            expected = ((child[5], ("AORebirth/Built/Debug/ZoneEngine_New/ZoneEngine_New.dll",)),
                        (child[7], ("AORebirth/Built/Debug/LoginEngine.exe", "AORebirth/Built/Debug/LoginEngine.dll")),
                        (child[10], ("AORebirth/Built/Debug/WorldEngine/WorldEngine.dll",)))
            for actual, candidates in expected:
                if not Path(actual).is_absolute() or Path(actual).resolve(strict=True) not in {checked(root, name) for name in candidates if (Path(root) / name).is_file()}:
                    raise ValueError("Connected feature validation requires this checkout's measured engine outputs")
            return checked(root, "Tools/ZoneEngineSchemaValidation/ZoneEngineSchemaValidation.csproj")
        raise ValueError("Unknown feature validation wrapper or arguments")
    if len(child) < 3 or Path(child[0]).name.lower() not in ("dotnet", "dotnet.exe") or child[1] not in ("build", "test", "run"):
        raise ValueError("Feature validation accepts only the approved build wrapper or dotnet build/test/run")
    if any(value.lower() == "--no-build" for value in child):
        raise ValueError("Feature validation must compile the measured source")
    if child[1] == "run":
        if len(child) < 4 or child[2] != "--project":
            raise ValueError("Feature tool execution requires an explicit --project")
        name = child[3]
    else:
        name = child[2]
    project = checked(root, name.replace("\\", "/"))
    relative_project = project.relative_to(root)
    if project.suffix.lower() != ".csproj" or relative_project.parts[0] not in ("AORebirth", "Tools", "WindowsBuildNet10"):
        raise ValueError("Feature project must belong to this product checkout")
    if child[1] == "run" and not (relative_project.parts[0] == "Tools" and
            (project.parent.name.endswith("Validation") or project.parent.name == "MissionChestRKCatalog")):
        raise ValueError("Feature execution is limited to existing validation tools")
    return project


def run_feature_validation(root, profile, child):
    project = feature_command(root, child)
    source_only_dao = (project == Path(root) / "Tools/MissionDaoValidation/MissionDaoValidation.csproj"
                       and len(child) == 5 and Path(child[3]).name.lower() == "run_mission_dao_validation.cmd")
    source = feature_source(root)
    rows, states = inventory(profile["CANONICAL_GAMEDATA_ROOT"])
    current, directories = content_states(profile["CANONICAL_GAMEDATA_ROOT"])
    if current != states:
        raise ValueError("Selected content changed during feature intake")
    bundle = Path(profile["STATE_ROOT"]) / ("feature-validation-" + uuid.uuid4().hex)
    bundle.mkdir(parents=True)
    write_json(bundle / "manifest.json", rows, exclusive=True)
    attempt = {"FORMAT_VERSION": 1, "BUILD_PURPOSE": "FEATURE_VALIDATION", "DEPLOYABLE": False,
               "WINDOWS_USER_ACCEPTED": "NO", "FEATURE_SOURCE": source,
               "SELECTED_GAMEDATA_MANIFEST": validate_manifest(rows),
               "COMMAND": child, "STARTED_UTC": now(), "VALIDATION_RESULT": "NOT_RUN"}
    token = uuid.uuid4().hex + uuid.uuid4().hex
    active = bundle / "active.json"
    write_json(active, {"BUILD_PURPOSE": "FEATURE_VALIDATION", "REPOSITORY_ROOT": str(Path(root).resolve()), "TOKEN_SHA256": digest(token.encode()),
                       "OWNER_PID": os.getpid(), "FEATURE_SOURCE": source, "FILE_STATES": states,
                       "DIRECTORY_STATES": directories, "CREATED_UTC": now()}, exclusive=True)
    env = dict(os.environ, AO_REBIRTH_BUILD_STATE=str(active), AO_REBIRTH_BUILD_TOKEN=token, AO_REBIRTH_BUILD_CHILD="1",
               MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0")
    try:
        result = subprocess.run(child, cwd=root, env=env, check=False)
        attempt.update(EXIT_CODE=result.returncode, FINISHED_UTC=now(), VALIDATION_RESULT="FAIL")
        if result.returncode == 0:
            if feature_source(root) != source or content_states(profile["CANONICAL_GAMEDATA_ROOT"]) != (states, directories):
                raise ValueError("Feature source or selected content changed during validation")
            if source_only_dao and not (active.parent / "build-observations").exists():
                observations = {"WRAPPER_ENTERED": False, "PRODUCT_HOOKS": []}
            else:
                observations = collect_build_observations(active)
            if not observations["PRODUCT_HOOKS"] and not source_only_dao:
                raise ValueError("Feature validation did not observe the product compiler guard")
            if project is None and (not observations["WRAPPER_ENTERED"] or not
                    {"ChatEngine.WinNet10", "LoginEngine.WinNet10", "WorldEngine", "ZoneEngine_New"} <= set(observations["PRODUCT_HOOKS"])):
                raise ValueError("Approved feature build wrapper did not compile all four engines")
            outputs = {checked(root, name) for name in OUTPUTS if (Path(root) / name).is_file()}
            if project is not None and (project.parent / "bin").is_dir():
                outputs.update(path for path in (project.parent / "bin").rglob(project.stem + ".dll") if path.is_file())
            if source_only_dao:
                tool = checked(root, "Tools/MissionDaoValidation/bin/Release/net10.0/MissionDaoValidation.dll")
                state = file_state(tool)
                tool_hash = digest(tool.read_bytes())
                if not state[2] or file_state(tool) != state:
                    raise ValueError("Source-only validation assembly changed during measurement")
                attempt["SOURCE_ONLY_VALIDATION_TOOL"] = {"path": tool.relative_to(root).as_posix(),
                    "bytes": state[2], "sha256": tool_hash,
                    "METHOD": "EXISTING_CHESTS_ONLY_WRAPPER_COMPILES_LINKED_PERSISTENCE_SOURCES"}
            attempt["AVAILABLE_OUTPUTS_POST_COMMAND"] = [{"path": path.relative_to(root).as_posix(),
                    "bytes": path.stat().st_size, "sha256": digest(path.read_bytes())} for path in sorted(outputs)]
            attempt.update(VALIDATION_RESULT="PASS", PRODUCT_COMPILER_HOOKS=observations["PRODUCT_HOOKS"])
        return result.returncode
    except Exception as error:
        attempt.update(VALIDATION_RESULT="FAIL", VALIDATION_FAILURE_TYPE=type(error).__name__)
        raise
    finally:
        attempt.setdefault("FINISHED_UTC", now())
        write_json(bundle / "feature-validation.json", attempt, exclusive=True)
        active.unlink(missing_ok=True)
        print("FEATURE_VALIDATION_RECEIPT=" + (bundle / "feature-validation.json").relative_to(root).as_posix())
        print("DEPLOYABLE=NO; WINDOWS_USER_ACCEPTED=NO")


def verify_build_token(root, profile, entrypoint=None, product=None, publishing=False):
    state_path = Path(os.environ.get("AO_REBIRTH_BUILD_STATE", ""))
    state_root = Path(profile["STATE_ROOT"]).resolve()
    if not state_path.is_file() or not state_path.resolve().is_relative_to(state_root):
        raise ValueError("Direct product MSBuild lacks a live governed build attempt")
    state = read_json(state_path)
    token = os.environ.get("AO_REBIRTH_BUILD_TOKEN", "")
    if not token or digest(token.encode()) != state["TOKEN_SHA256"]:
        raise ValueError("Build capability is missing or forged")
    pid, visited = os.getpid(), set()
    while pid and pid not in visited and pid != state["OWNER_PID"]:
        visited.add(pid)
        pid = process_parent(pid)
    if not pid or pid != state["OWNER_PID"]:
        raise ValueError("Build attempt is stale or not an ancestor of this compiler")
    purpose = state.get("BUILD_PURPOSE", "PRODUCTION_RELEASE")
    if purpose == "FEATURE_VALIDATION":
        if publishing:
            raise ValueError("Feature validation cannot publish production output")
        if state.get("REPOSITORY_ROOT") != str(Path(root).resolve()):
            raise ValueError("Feature capability belongs to another checkout")
        if feature_source(root) != state["FEATURE_SOURCE"]:
            raise ValueError("Feature source changed during validation")
    elif purpose == "PRODUCTION_RELEASE":
        source_exact(root, state["PUBLIC_MASTER_SHA"], profile.get("LOCAL_CONFIG_OVERRIDES", ()))
    else:
        raise ValueError("Unknown build capability purpose")
    canonical = Path(profile["CANONICAL_GAMEDATA_ROOT"])
    actual, directories = content_states(canonical)
    if actual != state["FILE_STATES"] or directories != state["DIRECTORY_STATES"]:
        raise ValueError("Canonical file set/state changed during build")
    observations = []
    if entrypoint:
        if entrypoint != "NewZoneEngineBuild/build.cmd":
            raise ValueError("Unknown governed build entrypoint")
        observations.append(("WRAPPER", entrypoint))
    if product:
        if not re.fullmatch(r"[A-Za-z0-9_.-]+", product):
            raise ValueError("Unknown product compiler hook")
        observations.append(("PRODUCT", product))
    event_root = state_path.parent / "build-observations"
    if event_root.is_symlink() or getattr(event_root, "is_junction", lambda: False)():
        raise ValueError("Build observation directory link rejected")
    event_root.mkdir(exist_ok=True)
    state_hash = digest(state_path.read_bytes())
    for kind, name in observations:
        write_json(event_root / (uuid.uuid4().hex + ".json"),
                   {"STATE_SHA256": state_hash, "KIND": kind, "NAME": name,
                    "OBSERVED_PID": os.getpid(), "OBSERVED_AT_UTC": now()}, exclusive=True)
    return state


def collect_build_observations(state_path):
    """Aggregate immutable per-hook events after the approved build child has exited."""
    state_path = Path(state_path)
    state, state_hash = read_json(state_path), digest(state_path.read_bytes())
    event_root = checked(state_path.parent, "build-observations")
    wrapper, products = False, set()
    for path in sorted(event_root.iterdir()):
        event = read_json(checked(event_root, path.name))
        if (set(event) != {"STATE_SHA256", "KIND", "NAME", "OBSERVED_PID", "OBSERVED_AT_UTC"}
                or event["STATE_SHA256"] != state_hash
                or type(event["OBSERVED_PID"]) is not int or event["OBSERVED_PID"] <= 0
                or timestamp(event["OBSERVED_AT_UTC"]) < timestamp(state["CREATED_UTC"])):
            raise ValueError("Build observation does not bind immutable live capability")
        if event["KIND"] == "WRAPPER" and event["NAME"] == "NewZoneEngineBuild/build.cmd":
            wrapper = True
        elif event["KIND"] == "PRODUCT" and re.fullmatch(r"[A-Za-z0-9_.-]+", event["NAME"]):
            products.add(event["NAME"])
        else:
            raise ValueError("Unknown governed build observation")
    return {"WRAPPER_ENTERED": wrapper, "PRODUCT_HOOKS": sorted(products)}


def run_build(root, profile, child):
    approved = [os.environ.get("ComSpec", r"C:\Windows\System32\cmd.exe"), "/d", "/c",
                str(Path(root) / "NewZoneEngineBuild/build.cmd")]
    if len(child) != len(approved) or any(os.path.normcase(os.path.normpath(actual)) != os.path.normcase(os.path.normpath(expected))
                                        for actual, expected in zip(child, approved)):
        raise ValueError("run-build may invoke only the tracked approved build entrypoint without overrides")
    bundle, attempt, plan, projected, states = intake(root, profile)
    if plan["CANONICAL_BEFORE_MANIFEST"] != plan["WINDOWS_GAMEDATA_MANIFEST"]:
        raise ValueError("Approved content A/M/D must be reconciled before Windows build; inspect latest-plan.json")
    current_states, directories = content_states(profile["CANONICAL_GAMEDATA_ROOT"])
    if current_states != states:
        raise ValueError("Canonical file set/state changed before compilation")
    token = uuid.uuid4().hex + uuid.uuid4().hex
    active = bundle / "active.json"
    write_json(active, {"TOKEN_SHA256": digest(token.encode()), "OWNER_PID": os.getpid(),
                        "PUBLIC_MASTER_SHA": attempt["PUBLIC_MASTER_SHA"], "FILE_STATES": states,
                        "DIRECTORY_STATES": directories, "CREATED_UTC": now()}, exclusive=True)
    env = dict(os.environ, AO_REBIRTH_BUILD_STATE=str(active), AO_REBIRTH_BUILD_TOKEN=token,
               AO_REBIRTH_BUILD_CHILD="1")
    attempt["BUILD_STARTED_UTC"] = now()
    try:
        result = subprocess.run(child, cwd=root, env=env, check=False)
        attempt["WINDOWS_BUILD_EXIT_CODE"] = result.returncode
        attempt["BUILD_FINISHED_UTC"] = now()
        if result.returncode:
            attempt["WINDOWS_BUILD"] = "FAIL"
        else:
            observed = collect_build_observations(active)
            required = {"ChatEngine.WinNet10", "LoginEngine.WinNet10", "WorldEngine", "ZoneEngine_New"}
            if observed.get("WRAPPER_ENTERED") is not True or not required <= set(observed.get("PRODUCT_HOOKS", [])):
                raise ValueError("Approved wrapper and all four product compiler hooks were not observed")
            attempt["APPROVED_BUILD_ENTRYPOINT"] = "NewZoneEngineBuild/build.cmd"
            attempt["PRODUCT_COMPILER_HOOKS"] = observed["PRODUCT_HOOKS"]
            source_exact(root, attempt["PUBLIC_MASTER_SHA"], profile.get("LOCAL_CONFIG_OVERRIDES", ()))
            after, _ = inventory(profile["CANONICAL_GAMEDATA_ROOT"])
            if validate_manifest(after) != attempt["WINDOWS_GAMEDATA_MANIFEST"]:
                raise ValueError("Canonical content mutated during compilation")
            binaries = []
            for name in OUTPUTS:
                output = checked(root, name)
                before = file_state(output)
                output_hash = digest(output.read_bytes())
                if file_state(output) != before:
                    raise ValueError("Windows output changed during final measurement")
                binaries.append({"path": name, "bytes": before[2], "sha256": output_hash})
            attempt["BINARIES"] = binaries
            summary = summarize_reconciliation(bundle, plan)
            write_json(bundle / "reconciliation-summary.json", summary, exclusive=True)
            attempt["RECONCILIATION"] = descriptor(bundle, bundle / "reconciliation-summary.json")
            attempt["WINDOWS_CONTENT_VALIDATION"] = {"METHOD": "LOCAL_GUARDED_REPLAY_AND_WHOLE_TREE_MEASUREMENT",
                        "SUMMARY_SHA256": attempt["RECONCILIATION"]["SHA256"],
                        "LOCAL_REPLAY_SHA256": summary["LOCAL_REPLAY_SHA256"],
                        "CANONICAL_MANIFEST": attempt["WINDOWS_GAMEDATA_MANIFEST"], "VALIDATED_AT_UTC": now()}
            attempt["WINDOWS_BUILD"] = "PASS"
        write_json(bundle / "build-attempt.json", attempt, exclusive=True)
        print("BUILD_ATTEMPT_RECEIPT=" + (bundle / "build-attempt.json").relative_to(root).as_posix())
        print("WINDOWS_USER_ACCEPTED=NO")
        return result.returncode
    except Exception as error:
        if not (bundle / "build-attempt.json").exists():
            attempt.update(WINDOWS_BUILD="FAIL", BUILD_FINISHED_UTC=now(), VALIDATION_FAILURE_TYPE=type(error).__name__)
            write_json(bundle / "build-attempt.json", attempt, exclusive=True)
        raise
    finally:
        active.unlink(missing_ok=True)


def content_identity(data, path=None):
    if data is None:
        return None
    result = {"size": len(data), "sha256": digest(data), "blob": blob(data)}
    return {"path": path, **result} if path is not None else result


def summarize_reconciliation(bundle, reconciliation):
    """Export identities only after the ignored local three-way payload replay succeeds."""
    validate_reconciliation(bundle, reconciliation)
    records = []
    for change in reconciliation["CHANGES"]:
        approvals = {}
        for pointer, resolution in change["RESOLUTIONS"].items():
            approvals[pointer] = {key: resolution[key] for key in ("ACTION", "INPUT_HASHES", "APPROVAL")}
            if resolution["ACTION"] == "RESOLVED_VALUE":
                approvals[pointer]["RESOLVED_VALUE_SHA256"] = value_hash(resolution["VALUE"])
        records.append({**{key: change[key] for key in ("AUTHORITY", "PATH", "STATUS")},
                        **{key: content_identity(load_optional(bundle, change[key]),
                                                change["PATH"] if key.startswith("CANONICAL_") else None)
                           for key in ("UPSTREAM_BEFORE", "UPSTREAM_AFTER", "CANONICAL_BEFORE", "CANONICAL_AFTER")},
                        "RESOLUTION_APPROVALS": approvals})
    summary = {key: reconciliation[key] for key in ("FORMAT_VERSION", *TUPLE, "CANONICAL_BEFORE_MANIFEST",
                "SOURCE_SNAPSHOTS", "PROTECTED_GENERATED_FILES", "BEFORE_MANIFEST", "CANONICAL_MANIFEST")}
    summary.update(PROOF_KIND="PORTABLE_MEASURED_RECONCILIATION_V1", CHANGES=records,
                   LOCAL_REPLAY_SHA256=digest(encoded(reconciliation)))
    for key in ("REQUIRED_CURRENT_CATALOGS", "GENERATED_ITEMS_PROVENANCE", "GENERATED_SURFACES_PROVENANCE"):
        if key in reconciliation:
            summary[key] = reconciliation[key]
    if "CATALOG_RESTORATIONS" in reconciliation:
        summary["CATALOG_RESTORATIONS"] = []
        for row in reconciliation["CATALOG_RESTORATIONS"]:
            item = {**{key: row[key] for key in ("AUTHORITY", "PATH", "APPROVAL")},
                **{key: content_identity(load_optional(bundle, row[key]), row["PATH"])
                   for key in ("CANONICAL_BEFORE", "CANONICAL_AFTER")}}
            if "ENTRIES" in row:
                item.update(ENTRIES=row["ENTRIES"], ENTRY_PROOF=row["ENTRY_PROOF"],
                    UPSTREAM_CURRENT=content_identity(load_optional(bundle, row["UPSTREAM_CURRENT"])))
            summary["CATALOG_RESTORATIONS"].append(item)
    if "GENERATED_ITEMS_ADOPTION" in reconciliation:
        summary["GENERATED_ITEMS_ADOPTION"] = {key: content_identity(load_optional(bundle, item), "items.dat")
            for key, item in reconciliation["GENERATED_ITEMS_ADOPTION"].items()}
    if "GENERATED_SURFACES_ADOPTIONS" in reconciliation:
        summary["GENERATED_SURFACES_ADOPTIONS"] = [{"PATH": row["PATH"], "BEFORE_CELLS": row["BEFORE_CELLS"],
            **{key: content_identity(load_optional(bundle, row[key]), row["PATH"])
               for key in ("CANONICAL_BEFORE", "CANONICAL_AFTER")}}
            for row in reconciliation["GENERATED_SURFACES_ADOPTIONS"]]
    if "PRIVATE_CATALOG_ADOPTIONS" in reconciliation:
        summary["PRIVATE_CATALOG_ADOPTIONS"] = [{**{key: row[key] for key in ("PATH", "SHA256", "APPROVAL")},
            "APPROVAL_INSTRUCTION": content_identity(load_optional(bundle, row["APPROVAL_INSTRUCTION"])),
            **{key: content_identity(load_optional(bundle, row[key]), row["PATH"])
               for key in ("CANONICAL_BEFORE", "CANONICAL_AFTER")}}
            for row in reconciliation["PRIVATE_CATALOG_ADOPTIONS"]]
    if "RUNTIME_EXCLUSIONS" in reconciliation:
        summary["RUNTIME_EXCLUSIONS"] = [{"PATH": row["PATH"], "REASON": row["REASON"],
            **{key: content_identity(load_optional(bundle, row[key]), row["PATH"])
               for key in ("CANONICAL_BEFORE", "EXCLUDED_CONTENT")}}
            for row in reconciliation["RUNTIME_EXCLUSIONS"]]
    prior = reconciliation.get("PRIOR_LOCAL_RECONCILIATION")
    if prior:
        prior_path = load_descriptor(Path(prior["STATE_ROOT"]), prior["RECONCILIATION"])
        prior_value = read_json(prior_path)
        summary["PRIOR_CHECKPOINT"] = {**{key: prior_value[key] for key in TUPLE},
                                       "LOCAL_REPLAY_SHA256": digest(encoded(prior_value))}
    validate_reconciliation_summary(bundle, summary)
    return summary


def validate_reconciliation_summary(bundle, summary):
    required = {"FORMAT_VERSION", *TUPLE, "CANONICAL_BEFORE_MANIFEST", "SOURCE_SNAPSHOTS",
                "PROTECTED_GENERATED_FILES", "BEFORE_MANIFEST", "CANONICAL_MANIFEST", "PROOF_KIND",
                "CHANGES", "LOCAL_REPLAY_SHA256"}
    optional = {"PRIOR_CHECKPOINT", "REQUIRED_CURRENT_CATALOGS", "CATALOG_RESTORATIONS", "GENERATED_ITEMS_PROVENANCE", "GENERATED_ITEMS_ADOPTION", "GENERATED_SURFACES_PROVENANCE", "GENERATED_SURFACES_ADOPTIONS", "PRIVATE_CATALOG_ADOPTIONS", "RUNTIME_EXCLUSIONS"}
    if (not required <= set(summary) or set(summary) - required - optional or summary["FORMAT_VERSION"] != 1
            or summary["PROOF_KIND"] != "PORTABLE_MEASURED_RECONCILIATION_V1"
            or not re.fullmatch("[0-9a-f]{64}", summary["LOCAL_REPLAY_SHA256"])):
        raise ValueError("One bounded measured reconciliation summary is required")
    before = read_json(load_descriptor(bundle, summary["BEFORE_MANIFEST"]))
    after = read_json(load_descriptor(bundle, summary["CANONICAL_MANIFEST"]))
    if (validate_manifest(before) != summary["CANONICAL_BEFORE_MANIFEST"]
            or validate_manifest(after) != summary["WINDOWS_GAMEDATA_MANIFEST"]):
        raise ValueError("Complete reconciliation summary manifests differ")
    snapshots, expected = summary["SOURCE_SNAPSHOTS"], set()
    if set(snapshots) != {"PRIVATE_CONTENT_UPSTREAM", "PUBLIC_PRODUCT_DATA"}:
        raise ValueError("Both complete authority snapshots are required")
    for authority, snapshot in snapshots.items():
        if (set(snapshot) != {"BASE_SHA", "CURRENT_SHA", "BEFORE", "AFTER"}
                or any(not re.fullmatch("[0-9a-f]{40}", snapshot[key]) for key in ("BASE_SHA", "CURRENT_SHA"))):
            raise ValueError("Authority snapshot identity differs")
        target = summary["CONTENT_DUMP_SHA" if authority == "PRIVATE_CONTENT_UPSTREAM" else "PUBLIC_MASTER_SHA"]
        if snapshot["CURRENT_SHA"] != target:
            raise ValueError("Authority snapshot does not bind frozen source")
        for state in (snapshot["BEFORE"], snapshot["AFTER"]):
            if not isinstance(state, dict):
                raise ValueError("Complete authority path state required")
            for path, row in state.items():
                relative(path)
                if (set(row) != {"blob", "size"} or type(row["size"]) is not int or row["size"] < 0
                        or not re.fullmatch("[0-9a-f]{40}", row["blob"])):
                    raise ValueError("Authority path identity differs")
        expected |= {(authority, path, action) for path, action in changes(snapshot["BEFORE"], snapshot["AFTER"])}
    if "PRIOR_CHECKPOINT" in summary:
        prior = summary["PRIOR_CHECKPOINT"]
        if (set(prior) != {*TUPLE, "LOCAL_REPLAY_SHA256"}
                or not re.fullmatch("[0-9a-f]{64}", prior["LOCAL_REPLAY_SHA256"])
                or prior["WINDOWS_GAMEDATA_MANIFEST"] != summary["CANONICAL_BEFORE_MANIFEST"]
                or snapshots["PRIVATE_CONTENT_UPSTREAM"]["BASE_SHA"] != prior["CONTENT_DUMP_SHA"]
                or snapshots["PUBLIC_PRODUCT_DATA"]["BASE_SHA"] != prior["PUBLIC_MASTER_SHA"]):
            raise ValueError("Measured canonical checkpoint/source identity differs")
    actual, seen = {row["path"]: row for row in before}, set()
    for change in summary["CHANGES"]:
        if set(change) != {"AUTHORITY", "PATH", "STATUS", "UPSTREAM_BEFORE", "UPSTREAM_AFTER", "CANONICAL_BEFORE", "CANONICAL_AFTER", "RESOLUTION_APPROVALS"}:
            raise ValueError("Bounded content transition schema differs")
        authority, path, action = change["AUTHORITY"], change["PATH"], change["STATUS"]
        relative(path)
        key = authority, path, action
        if key not in expected or key in seen:
            raise ValueError("Unexpected or duplicate authority transition")
        seen.add(key)
        for field, state in (("UPSTREAM_BEFORE", "BEFORE"), ("UPSTREAM_AFTER", "AFTER")):
            identity, source = change[field], snapshots[authority][state].get(path)
            if (identity is None) != (source is None):
                raise ValueError("Upstream addition/removal summary differs")
            if identity is not None and (set(identity) != {"size", "sha256", "blob"}
                    or type(identity["size"]) is not int or identity["size"] < 0
                    or not re.fullmatch("[0-9a-f]{64}", identity["sha256"])
                    or {key: identity[key] for key in ("size", "blob")} != source):
                raise ValueError("Upstream summary blob/size/hash differs")
        for field in ("CANONICAL_BEFORE", "CANONICAL_AFTER"):
            if change[field] is not None:
                validate_manifest([change[field]])
                if change[field]["path"] != path:
                    raise ValueError("Canonical transition path differs")
        if change["CANONICAL_BEFORE"] != actual.get(path):
            raise ValueError("Canonical transition does not bind full before manifest")
        approvals = change["RESOLUTION_APPROVALS"]
        if not isinstance(approvals, dict):
            raise ValueError("Explicit resolution bindings required")
        for pointer, approval in approvals.items():
            fields = {"ACTION", "INPUT_HASHES", "APPROVAL"}
            if approval.get("ACTION") == "RESOLVED_VALUE":
                fields.add("RESOLVED_VALUE_SHA256")
            if (not isinstance(pointer, str) or set(approval) != fields or not approval["APPROVAL"]
                    or approval["ACTION"] not in ("PRESERVE_PRIVATE", "RESOLVED_VALUE")
                    or set(approval["INPUT_HASHES"]) != {"BASE", "UPSTREAM", "PRIVATE"}
                    or any(value != "MISSING" and not re.fullmatch("[0-9a-f]{64}", value)
                           for value in approval["INPUT_HASHES"].values())
                    or ("RESOLVED_VALUE_SHA256" in approval and not re.fullmatch("[0-9a-f]{64}", approval["RESOLVED_VALUE_SHA256"]))):
                raise ValueError("Explicit resolution approval/hash binding differs")
        if path in summary["PROTECTED_GENERATED_FILES"] and change["CANONICAL_BEFORE"] != change["CANONICAL_AFTER"]:
            raise ValueError("Required generated/private content changed")
        if change["CANONICAL_AFTER"] is None:
            actual.pop(path, None)
        else:
            actual[path] = change["CANONICAL_AFTER"]
    validate_supplemental_content(bundle, summary, actual, portable=True)
    if seen != expected or sorted(actual.values(), key=lambda row: row["path"]) != after:
        raise ValueError("Complete A/M/D or unrelated canonical path preservation differs")
    return after


def export_accepted_bundle(repository_root, bundle, acceptance_path):
    """Copy only the fixed portable metadata/evidence graph; raw replay scratch stays local."""
    context = load_accepted_release(repository_root, acceptance_path)
    acceptance, windows, attempt = context["acceptance"], context["windows"], context["build_attempt"]
    summary = context["reconciliation"]
    files = {Path(acceptance_path).relative_to(bundle).as_posix()}
    objects = (acceptance, windows, attempt, summary)
    def collect(value):
        if isinstance(value, dict):
            if set(value) == {"PATH", "SHA256"}:
                files.add(load_descriptor(bundle, value).relative_to(bundle).as_posix())
            else:
                for nested in value.values():
                    collect(nested)
        elif isinstance(value, list):
            for nested in value:
                collect(nested)
    for value in objects:
        collect(value)
    files.update(windows["EVIDENCE"])
    target = bundle.parent / ("portable-" + attempt["ATTEMPT_ID"])
    target.mkdir()
    for name in sorted(files):
        source, destination = checked(bundle, name), target.joinpath(*relative(name).parts)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
    result = target / Path(acceptance_path).relative_to(bundle)
    load_accepted_release(repository_root, result)
    return result


def require_successful_attempt(bundle, attempt):
    if attempt.get("BUILD_PURPOSE", "PRODUCTION_RELEASE") != "PRODUCTION_RELEASE":
        raise ValueError("Feature validation is not a production build or Windows acceptance")
    if (attempt.get("FORMAT_VERSION") != 1 or attempt.get("WINDOWS_BUILD") != "PASS"
            or attempt.get("WINDOWS_BUILD_EXIT_CODE") != 0 or not attempt.get("BUILD_FINISHED_UTC")
            or not attempt.get("BUILD_STARTED_UTC") or timestamp(attempt["BUILD_STARTED_UTC"]) > timestamp(attempt["BUILD_FINISHED_UTC"])
            or attempt.get("LOCAL_WINDOWS_SOURCE_SHA") != attempt.get("PUBLIC_MASTER_SHA")):
        raise ValueError("Prepared, failed or unfinished Windows build cannot be accepted")
    if (not re.fullmatch("[0-9a-f]{32}", attempt.get("ATTEMPT_ID", ""))
            or attempt.get("APPROVED_BUILD_ENTRYPOINT") != "NewZoneEngineBuild/build.cmd"
            or not {"ChatEngine.WinNet10", "LoginEngine.WinNet10", "WorldEngine", "ZoneEngine_New"}
                <= set(attempt.get("PRODUCT_COMPILER_HOOKS", []))):
        raise ValueError("Successful approved wrapper and product build observations are required")
    if any(not re.fullmatch("[0-9a-f]{40}", attempt.get(key, "")) for key in ("PUBLIC_MASTER_SHA", "CONTENT_DUMP_SHA")):
        raise ValueError("Exact public and dump source SHAs are required")
    for key, field in (("PUBLIC_FETCH", "PUBLIC_MASTER_SHA"), ("CONTENT_DUMP_FETCH", "CONTENT_DUMP_SHA")):
        fetch = attempt.get(key, {})
        expected_role, expected_method = (("PUBLIC_PRODUCT", "GIT_FETCH") if key == "PUBLIC_FETCH"
                                         else ("PRIVATE_CONTENT_UPSTREAM", "AUTHENTICATED_GITHUB_API"))
        if (fetch.get("RESULT") != "SUCCESS" or fetch.get("TIP_SHA") != attempt[field]
                or fetch.get("ATTEMPT_ID") != attempt["ATTEMPT_ID"] or fetch.get("ROLE") != expected_role
                or fetch.get("METHOD") != expected_method or not fetch.get("FETCHED_AT_UTC")
                or not timestamp(attempt["ATTEMPT_STARTED_UTC"]) <= timestamp(fetch["FETCHED_AT_UTC"]) <= timestamp(attempt["BUILD_STARTED_UTC"])):
            raise ValueError("Fresh successful pre-build source/content fetch is required")
    binaries = attempt.get("BINARIES", [])
    if {row["path"] for row in binaries} != set(OUTPUTS) or len(binaries) != len(OUTPUTS):
        raise ValueError("All eight actual current build outputs must be bound")
    for row in binaries:
        if type(row.get("bytes")) is not int or row["bytes"] <= 0 or not re.fullmatch("[0-9a-f]{64}", row.get("sha256", "")):
            raise ValueError("Measured Windows output identity is invalid")
    reconciliation = read_json(load_descriptor(bundle, attempt["RECONCILIATION"]))
    rows = validate_reconciliation_summary(bundle, reconciliation)
    validation = attempt.get("WINDOWS_CONTENT_VALIDATION", {})
    if (set(validation) != {"METHOD", "SUMMARY_SHA256", "LOCAL_REPLAY_SHA256", "CANONICAL_MANIFEST", "VALIDATED_AT_UTC"}
            or validation["METHOD"] != "LOCAL_GUARDED_REPLAY_AND_WHOLE_TREE_MEASUREMENT"
            or validation["SUMMARY_SHA256"] != attempt["RECONCILIATION"]["SHA256"]
            or validation["LOCAL_REPLAY_SHA256"] != reconciliation["LOCAL_REPLAY_SHA256"]
            or validation["CANONICAL_MANIFEST"] != attempt["WINDOWS_GAMEDATA_MANIFEST"]
            or timestamp(validation["VALIDATED_AT_UTC"]) < timestamp(attempt["BUILD_FINISHED_UTC"])):
        raise ValueError("Successful Windows local replay and whole-tree measurement attestation required")
    if (any(attempt[key] != reconciliation[key] for key in TUPLE)
            or read_json(load_descriptor(bundle, attempt["CANONICAL_MANIFEST"])) != rows):
        raise ValueError("Build tuple/content reconciliation mismatch")
    generated = reconciliation.get("GENERATED_ITEMS_PROVENANCE")
    if generated and timestamp(generated["CREATED_UTC"]) > timestamp(attempt["BUILD_STARTED_UTC"]):
        raise ValueError("Generated items must be adopted before the actual Windows build")
    surfaces = reconciliation.get("GENERATED_SURFACES_PROVENANCE")
    if surfaces and timestamp(surfaces["CREATED_UTC"]) > timestamp(attempt["BUILD_STARTED_UTC"]):
        raise ValueError("Generated surfaces must be adopted before the actual Windows build")
    return rows, reconciliation


def current_matches(root, profile, bundle, attempt):
    require_successful_attempt(bundle, attempt)
    source_exact(root, attempt["PUBLIC_MASTER_SHA"], profile.get("LOCAL_CONFIG_OVERRIDES", ()))
    rows, _ = inventory(profile["CANONICAL_GAMEDATA_ROOT"])
    if validate_manifest(rows) != attempt["WINDOWS_GAMEDATA_MANIFEST"]:
        raise ValueError("Current Windows GameData differs from the build handed to Mike")
    for row in attempt["BINARIES"]:
        path = checked(root, row["path"])
        if path.stat().st_size != row["bytes"] or digest(path.read_bytes()) != row["sha256"]:
            raise ValueError("Current Windows binary differs from the completed build")


def validate_startup(bundle, windows, attempt):
    if (windows.get("WINDOWS_BUILD") != "PASS" or windows.get("WINDOWS_STARTUP") != "PASS"
            or windows.get("WINDOWS_PRODUCT_SOURCE_SHA") != attempt["PUBLIC_MASTER_SHA"]
            or windows.get("WINDOWS_RUNTIME_DIFFS") != 0
            or any(windows.get(key) != attempt[key] for key in TUPLE)
            or windows.get("WORLD_RECOVERY_COMPLETE") is not True):
        raise ValueError("Actual successful current-tuple Windows startup required")
    if {row["path"]: (row["sha256"], row["bytes"]) for row in windows.get("BINARIES", [])} != {
            row["path"]: (row["sha256"], row["bytes"]) for row in attempt["BINARIES"]}:
        raise ValueError("Startup binary identity differs from actual build")
    evidence = windows.get("EVIDENCE", {})
    for name, expected in evidence.items():
        if digest(checked(bundle, name).read_bytes()) != expected:
            raise ValueError("Raw Windows evidence changed")
    raw = read_json(load_descriptor(bundle, windows["RAW_WINDOWS_RECEIPT"]))
    if (any(raw.get(key) != windows.get(key) for key in (*TUPLE, "WINDOWS_PRODUCT_SOURCE_SHA", "WINDOWS_RUNTIME_DIFFS", "WINDOWS_BUILD", "WINDOWS_STARTUP", "WORLD_EPOCH", "BINARIES"))
            or sorted(raw.get("EVIDENCE", {}).values()) != sorted(evidence.values())):
        raise ValueError("Derived readiness receipt differs from its immutable actual raw receipt")
    ready_paths = [path for path in evidence if PurePosixPath(path).name == "startup-ready.json"]
    world_paths = [path for path in evidence if PurePosixPath(path).name == "world-status.json"]
    observation_paths = [path for path in evidence if PurePosixPath(path).name == "world-status-observation.json"]
    status_paths = [path for path in evidence if PurePosixPath(path).name == "status.log"]
    if any(len(paths) != 1 for paths in (ready_paths, world_paths, observation_paths, status_paths)):
        raise ValueError("Actual startup process/status and raw World evidence required")
    ready, world = read_json(checked(bundle, ready_paths[0])), read_json(checked(bundle, world_paths[0]))
    startup = ready.get("COMMANDS", {}).get("startup", {})
    status = ready.get("COMMANDS", {}).get("status", {})
    if (ready.get("PUBLIC_MASTER_SHA") != attempt["PUBLIC_MASTER_SHA"] or ready.get("FAILURE") is not None
            or startup.get("exit_code") != 0 or status.get("exit_code") != 0
            or not startup.get("started_utc") or timestamp(startup["started_utc"]) < timestamp(attempt["BUILD_FINISHED_UTC"])
            or set(row["engine"] for row in ready.get("HELD_PROCESSES", [])) != {"ChatEngine", "LoginEngine", "WorldEngine", "ZoneEngine_New"}
            or any(row.get("handle_opened") is not True for row in ready["HELD_PROCESSES"])):
        raise ValueError("Startup does not prove a fresh successful measured generation")
    held = ready["HELD_PROCESSES"]
    if len(held) != 4 or len({row.get("pid") for row in held}) != 4:
        raise ValueError("Actual retained startup process identities are incomplete")
    status_text = checked(bundle, status_paths[0]).read_text(encoding="utf-8-sig")
    for row in held:
        engine = row["engine"]
        suffix = next(path for path in OUTPUTS if path.endswith("/" + engine + ".exe")).lower()
        executable = row.get("executable", "").replace("\\", "/").lower()
        if (type(row.get("pid")) is not int or row["pid"] <= 0 or not executable.endswith("/" + suffix)
                or executable != row.get("expected_executable", "").replace("\\", "/").lower()
                or timestamp(row["start_time_utc"]) < timestamp(attempt["BUILD_FINISHED_UTC"])
                or not re.search(r"engine=" + re.escape(engine) + r"\b[^\r\n]*processPid=" + str(row["pid"]) + r"\b[^\r\n]*ownership=PASS", status_text)):
            raise ValueError("Actual current executable/listener ownership is unproven")
    selections = [read_json(load_descriptor(bundle, item)) for item in windows.get("GAMEDATA_SELECTION_EVIDENCE", [])]
    if len(selections) != 4 or {row.get("Engine") for row in selections} != {row["engine"] for row in held}:
        raise ValueError("Measured launcher GameData selection is missing")
    selected_root = windows.get("CANONICAL_GAMEDATA_ROOT_AT_STARTUP", "")
    if not selected_root or read_json(load_descriptor(bundle, windows["STARTUP_GAMEDATA_MANIFEST"])) != read_json(load_descriptor(bundle, attempt["CANONICAL_MANIFEST"])):
        raise ValueError("Started engines are not bound to the complete canonical manifest")
    for selection in selections:
        process = next(row for row in held if row["engine"] == selection["Engine"])
        if (selection.get("Pid") != process["pid"]
                or timestamp(selection["ProcessStartUtc"]) != timestamp(process["start_time_utc"])
                or ntpath.normcase(ntpath.normpath(selection.get("Path", ""))) != ntpath.normcase(ntpath.normpath(process["executable"]))
                or selection.get("BuildAttemptSha256") != windows["BUILD_ATTEMPT_RECEIPT"]["SHA256"]
                or selection.get("PublicMasterSha") != attempt["PUBLIC_MASTER_SHA"]
                or selection.get("GameDataManifest") != attempt["WINDOWS_GAMEDATA_MANIFEST"]
                or selection.get("LauncherSha256") != attempt["LAUNCHER_SHA256"]
                or (selection.get("InheritedOverride") or "").strip()
                or ntpath.normcase(ntpath.normpath(selection.get("ResolvedGameDataRoot", ""))) != ntpath.normcase(ntpath.normpath(selected_root))):
            raise ValueError("Started process used a different data root/overlay/generation/build")
    for name, marker in (("LoginEngine.out.full.log", "LoginEngine GameData root: "),
                         ("ZoneEngine_New.out.delta.log", "GameData ready root=")):
        paths = [path for path in evidence if PurePosixPath(path).name == name]
        if len(paths) != 1:
            raise ValueError("Actual Login/Zone loader-root logs are required")
        matches = [line.split(marker, 1)[1].strip() for line in checked(bundle, paths[0]).read_text(encoding="utf-8-sig").splitlines() if marker in line]
        if name.startswith("Zone"):
            matches = [value.rsplit(" mobs=", 1)[0] for value in matches]
        if not matches or any(ntpath.normcase(ntpath.normpath(value)) != ntpath.normcase(ntpath.normpath(selected_root)) for value in matches):
            raise ValueError("Loader selected GameData differs from the measured canonical root")
    generated = read_json(load_descriptor(bundle, attempt["RECONCILIATION"])).get("GENERATED_ITEMS_PROVENANCE")
    if generated:
        zone_path = next(path for path in evidence if PurePosixPath(path).name == "ZoneEngine_New.out.delta.log")
        zone_text = checked(bundle, zone_path).read_text(encoding="utf-8-sig")
        loaded = re.findall(r"Loaded ([0-9]+) item templates from ([^\r\n]+)", zone_text)
        ready_counts = re.findall(r"ItemTemplateCatalog ready with ([0-9]+) templates", zone_text)
        validation = generated["VALIDATION"]
        expected_path = ntpath.normcase(ntpath.join(selected_root, "items.dat"))
        if (not loaded or not ready_counts or "Failed to load GameData items.dat" in zone_text or "GameData items.dat not found" in zone_text
                or any(int(count) != validation["DECODED_POPULATION"] or ntpath.normcase(ntpath.normpath(path.strip())) != expected_path for count, path in loaded)
                or any(int(count) != validation["DISTINCT_IDS"] for count in ready_counts)):
            raise ValueError("Current ItemTemplateCatalog did not load the regenerated canonical items.dat population")
    current = world.get("world", {})
    roles = {row.get("role") for row in current.get("instances", []) if row.get("connected") is True
             and 0 <= row.get("heartbeatAgeMs", -1) <= windows.get("WORLD_LEASE_TTL_MS", 15000)}
    if current.get("recovering") is not False or current.get("epoch") != windows.get("WORLD_EPOCH") or not {"RoleChat", "RoleZone"} <= roles:
        raise ValueError("Fresh World recovery/current registrations are unproven")
    observation = read_json(checked(bundle, observation_paths[0]))
    if (observation.get("sha256") != digest(checked(bundle, world_paths[0]).read_bytes())
            or observation.get("source") != attempt["PUBLIC_MASTER_SHA"]
            or timestamp(observation["requested_utc"]) < timestamp(startup["started_utc"])
            or timestamp(observation["captured_utc"]) < timestamp(observation["requested_utc"])):
        raise ValueError("Raw World observation is not bound to this fresh startup")


def explicit_mike_acceptance(evidence, expected):
    if (evidence.get("AUTHOR") != "Mike" or evidence.get("ROLE") != "user" or evidence.get("EXPLICIT_ACCEPTANCE") is not True
            or not evidence.get("MESSAGE_ID") or not evidence.get("RECORDED_UTC")
            or any(evidence.get(key) != value for key, value in expected.items())):
        raise ValueError("Explicit Mike acceptance of this exact frozen tuple/receipt is missing")
    timestamp(evidence["RECORDED_UTC"])
    text = evidence.get("TEXT", "").strip().lower().replace("\u2019", "'")
    negative = r"\b(?:haven't|have not|hasn't|has not|didn't|did not|doesn't|does not|don't|do not|not|never|won't|will not|isn't|is not)\s+(?:\w+\s+){0,3}(?:accept(?:ed|ing)?|approv(?:e|ed|ing)|work(?:s|ing)?|pass(?:ed)?|good|ready)\b"
    if re.search(negative, text) or re.search(r"\breject(?:ed|ing)?\b", text):
        raise ValueError("Human evidence does not explicitly accept this build")
    # Questions and isolated approval vocabulary cannot manufacture a human acceptance.
    affirmative = r"(?:^|[.!;\n]\s*)(?:i (?:accept|approve)\b|looks good\b|works on windows\b|working on windows\b|windows works\b|(?:this|it|the build|windows build) (?:works|is good|is accepted|is approved)\b)"
    direct = any("?" not in sentence and re.search(affirmative, sentence)
                 for sentence in re.split(r"(?<=[.!;\n])", text))
    prompted = text in ("yes", "yes.", "accept", "approve") and all(
        value in evidence.get("ACCEPTANCE_PROMPT", "") for value in expected.values())
    if not direct and not prompted:
        raise ValueError("A YES boolean or unrelated human text is not Windows acceptance")


def record_startup(root, profile, attempt_path, raw_receipt_path):
    attempt_path = checked(root, Path(attempt_path).as_posix())
    bundle, attempt = attempt_path.parent, read_json(attempt_path)
    current_matches(root, profile, bundle, attempt)
    raw = checked(root, Path(raw_receipt_path).as_posix())
    windows = read_json(raw)
    # Copy only the explicitly named raw evidence into the portable job bundle.
    copied = {}
    for index, (path, expected) in enumerate(windows.get("EVIDENCE", {}).items()):
        source = checked(root, path)
        if digest(source.read_bytes()) != expected:
            raise ValueError("Windows raw evidence digest mismatch")
        target = bundle / "startup-evidence" / str(index) / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        copied[target.relative_to(bundle).as_posix()] = expected
    windows["EVIDENCE"] = copied
    windows["BUILD_ATTEMPT_RECEIPT"] = descriptor(bundle, attempt_path)
    windows["RAW_WINDOWS_RECEIPT"] = stash(bundle, "raw-windows-receipt.json", raw.read_bytes())
    windows["WINDOWS_USER_ACCEPTED"] = "NO"
    selections = bundle / "startup-selection"
    if not selections.is_dir():
        raise ValueError("Release-mode launcher selection evidence is absent")
    ready_path = next((path for path in copied if PurePosixPath(path).name == "startup-ready.json"), None)
    if ready_path is None:
        raise ValueError("Retained startup generation evidence is absent")
    held = read_json(checked(bundle, ready_path))["HELD_PROCESSES"]
    selected = []
    for path in selections.glob("*.json"):
        value = read_json(path)
        if any(value.get("Engine") == row["engine"] and value.get("Pid") == row["pid"]
               and timestamp(value["ProcessStartUtc"]) == timestamp(row["start_time_utc"]) for row in held):
            selected.append(descriptor(bundle, path))
    windows["GAMEDATA_SELECTION_EVIDENCE"] = selected
    windows["CANONICAL_GAMEDATA_ROOT_AT_STARTUP"] = str(Path(profile["CANONICAL_GAMEDATA_ROOT"]).resolve())
    windows["STARTUP_GAMEDATA_MANIFEST"] = attempt["CANONICAL_MANIFEST"]
    validate_startup(bundle, windows, attempt)
    target = bundle / "windows-receipt.json"
    write_json(target, windows, exclusive=True)
    print("WINDOWS_USER_ACCEPTED=NO")
    print("STOP_FOR_MIKE_TESTING=YES")
    return target


def accept_windows(root, profile, windows_path, evidence_path):
    windows_path = checked(root, Path(windows_path).as_posix())
    bundle, windows = windows_path.parent, read_json(windows_path)
    attempt_path = load_descriptor(bundle, windows["BUILD_ATTEMPT_RECEIPT"])
    attempt = read_json(attempt_path)
    current_matches(root, profile, bundle, attempt)
    validate_startup(bundle, windows, attempt)
    evidence_path = checked(root, Path(evidence_path).as_posix())
    evidence = read_json(evidence_path)
    expected = {**{key: attempt[key] for key in TUPLE}, "WINDOWS_RECEIPT_SHA256": digest(windows_path.read_bytes())}
    explicit_mike_acceptance(evidence, expected)
    if timestamp(evidence["RECORDED_UTC"]) < timestamp(windows["CREATED_UTC"]):
        raise ValueError("Human acceptance predates the current Windows readiness receipt")
    receipt = {"FORMAT_VERSION": 1, **expected, "WINDOWS_USER_ACCEPTED": "YES", "ACCEPTED_BY": "Mike",
               "ACCEPTED_UTC": now(), "EXPLICIT_ACCEPTANCE_EVIDENCE": stash(bundle, "explicit-mike-acceptance.json", evidence_path.read_bytes()),
               "BUILD_ATTEMPT_RECEIPT": descriptor(bundle, attempt_path), "WINDOWS_RECEIPT": descriptor(bundle, windows_path)}
    target = bundle / "acceptance.json"
    write_json(target, receipt, exclusive=True)
    load_accepted_release(root, target)
    return export_accepted_bundle(root, bundle, target)


def load_accepted_release(repository_root, acceptance_path):
    """Validate one portable immutable accepted bundle; no fetches or source writes."""
    repository_root = Path(repository_root).resolve(strict=True)
    acceptance_path = Path(acceptance_path)
    if acceptance_path.is_absolute():
        acceptance_path = checked(repository_root, acceptance_path.relative_to(repository_root).as_posix())
    else:
        acceptance_path = checked(repository_root, acceptance_path.as_posix())
    bundle, acceptance = acceptance_path.parent, read_json(acceptance_path)
    if (acceptance.get("FORMAT_VERSION") != 1 or acceptance.get("WINDOWS_USER_ACCEPTED") != "YES"
            or acceptance.get("ACCEPTED_BY") != "Mike" or not acceptance.get("ACCEPTED_UTC")):
        raise ValueError("Explicit accepted Windows release is required")
    attempt_path = load_descriptor(bundle, acceptance["BUILD_ATTEMPT_RECEIPT"])
    windows_path = load_descriptor(bundle, acceptance["WINDOWS_RECEIPT"])
    attempt, windows = read_json(attempt_path), read_json(windows_path)
    rows, reconciliation = require_successful_attempt(bundle, attempt)
    validate_startup(bundle, windows, attempt)
    if windows["BUILD_ATTEMPT_RECEIPT"] != acceptance["BUILD_ATTEMPT_RECEIPT"]:
        raise ValueError("Windows receipt is bound to a different build attempt")
    expected = {**{key: attempt[key] for key in TUPLE}, "WINDOWS_RECEIPT_SHA256": digest(windows_path.read_bytes())}
    evidence = read_json(load_descriptor(bundle, acceptance["EXPLICIT_ACCEPTANCE_EVIDENCE"]))
    if any(acceptance.get(key) != value for key, value in expected.items()):
        raise ValueError("Acceptance evidence/receipt/tuple differs")
    explicit_mike_acceptance(evidence, expected)
    if not timestamp(windows["CREATED_UTC"]) <= timestamp(evidence["RECORDED_UTC"]) <= timestamp(acceptance["ACCEPTED_UTC"]):
        raise ValueError("Human acceptance chronology differs from the measured Windows receipt")
    return {"acceptance": acceptance, "windows": windows, "build_attempt": attempt, "manifest": rows,
            "reconciliation": reconciliation, "tuple": expected,
            "descriptors": {"bundle": bundle, "windows_path": windows_path, "attempt_path": attempt_path}}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("prepare-build", "reconcile-content", "run-build", "run-feature-validation", "verify-build-token", "verify-startup-attempt", "record-startup", "accept-windows"))
    parser.add_argument("--repository-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--profile", type=Path)
    parser.add_argument("--approved-plan-sha256")
    parser.add_argument("--resolutions", type=Path)
    parser.add_argument("--generated-items-receipt", type=Path)
    parser.add_argument("--generated-surfaces-receipt", type=Path)
    parser.add_argument("--build-attempt")
    parser.add_argument("--windows-receipt")
    parser.add_argument("--acceptance-evidence")
    parser.add_argument("--entrypoint")
    parser.add_argument("--product")
    parser.add_argument("--publishing", action="store_true")
    parser.add_argument("--developer-build", action="store_true",
                        help="check ordinary compilation scope only; cannot validate or authorize release output")
    args, child = parser.parse_known_args(argv)
    try:
        root = args.repository_root.resolve(strict=True)
        if args.developer_build:
            release_arguments = (args.profile, args.approved_plan_sha256, args.resolutions,
                                 args.generated_items_receipt, args.generated_surfaces_receipt, args.build_attempt, args.windows_receipt,
                                 args.acceptance_evidence, args.publishing, child)
            if args.mode != "verify-build-token" or any(release_arguments):
                raise ValueError("Developer compilation cannot replace a governed build, publishing or release validation")
            print("WINDOWS_BUILD_SCOPE=DEVELOPER; PRIVATE_RELEASE_PROFILE=NOT_REQUIRED")
            print("DEPLOYABLE=NO; WINDOWS_USER_ACCEPTED=NO")
            return 0
        profile = profile_for(root, args.profile)
        if args.generated_items_receipt and args.mode not in ("prepare-build", "reconcile-content"):
            raise ValueError("Generated items may be adopted only through existing content preparation/reconciliation")
        if args.generated_surfaces_receipt and args.mode not in ("prepare-build", "reconcile-content"):
            raise ValueError("Generated surfaces may be adopted only through existing content preparation/reconciliation")
        if args.mode == "verify-build-token":
            verify_build_token(root, profile, args.entrypoint, args.product, args.publishing)
        elif args.mode == "verify-startup-attempt":
            if not args.build_attempt:
                raise ValueError("A successful current build attempt is required")
            attempt_path = checked(root, args.build_attempt)
            current_matches(root, profile, attempt_path.parent, read_json(attempt_path))
        elif args.mode in ("run-build", "run-feature-validation"):
            child = child[1:] if child[:1] == ["--"] else child
            if not child:
                raise ValueError("Existing build child command is required")
            return (run_feature_validation if args.mode == "run-feature-validation" else run_build)(root, profile, child)
        elif child:
            raise ValueError("Unknown contract command arguments")
        elif args.mode == "reconcile-content":
            if not args.approved_plan_sha256:
                raise ValueError("Explicit hash-bound safe content-plan approval required")
            apply_content(root, profile, args.approved_plan_sha256, read_json(args.resolutions) if args.resolutions else None, args.generated_items_receipt, args.generated_surfaces_receipt)
        elif args.mode == "prepare-build":
            bundle, attempt, plan, _, _ = intake(root, profile, read_json(args.resolutions) if args.resolutions else None, args.generated_items_receipt, args.generated_surfaces_receipt)
            print("CONTENT_PLAN_SHA256=" + digest(encoded(plan)))
            print("BUILD_ATTEMPT_RECEIPT=" + (bundle / "prepared.json").relative_to(root).as_posix())
            if plan["CANONICAL_BEFORE_MANIFEST"] != plan["WINDOWS_GAMEDATA_MANIFEST"]:
                raise ValueError("Canonical content must be reconciled before build")
        elif args.mode == "record-startup":
            if not args.build_attempt or not args.windows_receipt:
                raise ValueError("Successful actual build attempt and raw startup receipt required")
            record_startup(root, profile, args.build_attempt, args.windows_receipt)
        elif args.mode == "accept-windows":
            if not args.windows_receipt or not args.acceptance_evidence:
                raise ValueError("Frozen Windows receipt and explicit Mike acceptance evidence required")
            print("WINDOWS_ACCEPTANCE=" + accept_windows(root, profile, args.windows_receipt, args.acceptance_evidence).relative_to(root).as_posix())
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        print("WINDOWS_RELEASE_CONTRACT=FAIL " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
