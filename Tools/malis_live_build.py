#!/usr/bin/env python3
"""Reproducibly build, package, validate, and install audited live Malis tooling."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import struct
import sys
import xml.etree.ElementTree as ET
import zipfile
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ZIP = ROOT / "docs/reference/missions/malis/raw/malis-source-3ac9943a4943b8cb80eda9e40359729e656686b0.zip"
SOURCE_SHA256 = "c1dc1bf4c919193c0ea9b5ba3cc5419075becd5b94e1041391f0d9ebbae0074d"
SOURCE_COMMIT = "3ac9943a4943b8cb80eda9e40359729e656686b0"
BASELINE_COMMIT = "aea19aba8d0069f4b6c34578247ec2ab53a6e584"
SOURCE_ROOT_NAME = "malis-3ac9943a"
RUNTIME_FILES = (
    "AOSharp.Bootstrap.dll",
    "AOSharp.Common.dll",
    "AOSharp.Core.dll",
    "AOSharp.exe",
    "Newtonsoft.Json.dll",
)
BANNED_PRIVATE_DEPENDENCIES = {
    "AOSharp.Bootstrap.dll",
    "AOSharp.Common.dll",
    "AOSharp.Core.dll",
    "Newtonsoft.Json.dll",
    "Serilog.dll",
    "HtmlAgilityPack.dll",
}
MALIS_DIRECTORY = "Malis Mission Roller 2"
HARVESTER_DIRECTORY = "MissionOfferHarvester"
MANIFEST_RELATIVE = Path("docs/generated/missions/malis-live/deployment-manifest.json")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical_json(value: object) -> str:
    return json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(message)


def require_within(path: Path, parent: Path) -> Path:
    resolved = path.resolve()
    root = parent.resolve()
    require(resolved == root or resolved.is_relative_to(root), f"Path escapes governed root: {resolved}")
    return resolved


def recreate_owned_directory(path: Path, parent: Path) -> None:
    target = require_within(path, parent)
    require(target != parent.resolve(), f"Refusing to recreate broad root: {target}")
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True)


def safe_extract(archive_path: Path, destination: Path) -> None:
    with zipfile.ZipFile(archive_path) as archive:
        for member in archive.infolist():
            target = (destination / member.filename).resolve()
            require(target.is_relative_to(destination.resolve()), f"Unsafe ZIP member: {member.filename}")
        archive.extractall(destination)


def parse_pe(path: Path) -> dict[str, object]:
    data = path.read_bytes()
    require(data[:2] == b"MZ", f"Not a PE file: {path}")
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    require(data[pe_offset : pe_offset + 4] == b"PE\0\0", f"Invalid PE signature: {path}")
    machine, section_count, _, _, _, optional_size, _ = struct.unpack_from("<HHIIIHH", data, pe_offset + 4)
    optional = pe_offset + 24
    magic = struct.unpack_from("<H", data, optional)[0]
    data_directory = optional + (96 if magic == 0x10B else 112)
    cli_rva, _ = struct.unpack_from("<II", data, data_directory + 14 * 8)
    section_table = optional + optional_size
    cli_offset = None
    for index in range(section_count):
        section = section_table + index * 40
        virtual_size, virtual_address, raw_size, raw_pointer = struct.unpack_from("<IIII", data, section + 8)
        if virtual_address <= cli_rva < virtual_address + max(virtual_size, raw_size):
            cli_offset = raw_pointer + (cli_rva - virtual_address)
            break
    require(cli_offset is not None, f"CLI header not found: {path}")
    flags = struct.unpack_from("<I", data, cli_offset + 16)[0]
    return {
        "Machine": f"0x{machine:04X}",
        "CorFlags": f"0x{flags:08X}",
        "ILOnly": bool(flags & 0x1),
        "Requires32Bit": bool(flags & 0x2),
        "Prefers32Bit": bool(flags & 0x20000),
    }


def prepare(runtime: Path, work_root: Path) -> None:
    runtime = runtime.resolve()
    require(runtime.is_dir(), f"Installed AOSharp runtime missing: {runtime}")
    for name in RUNTIME_FILES:
        require((runtime / name).is_file(), f"Installed runtime file missing: {name}")
    require(sha256(SOURCE_ZIP) == SOURCE_SHA256, "Audited Malis source ZIP SHA-256 mismatch")
    tools_temp = ROOT / "tools-temp"
    recreate_owned_directory(work_root, tools_temp)
    source_parent = work_root / "source"
    source_parent.mkdir()
    safe_extract(SOURCE_ZIP, source_parent)
    source_root = source_parent / SOURCE_ROOT_NAME
    require((source_root / "Malis Mission Roller 2.csproj").is_file(), "Audited Malis project missing after extraction")
    print("MALIS_LIVE_PREPARE=PASS")


def build_safe_settings(default_path: Path) -> dict[str, object]:
    settings = json.loads(default_path.read_text(encoding="utf-8-sig"))
    for key in ("ReturnItem", "KillTarget", "FindTarget", "FindItem", "UseItem"):
        settings["Types"][key] = False
    settings["Extras"]["AutoAccept"] = False
    settings["Extras"]["AutoAdjustQl"] = False
    settings["Extras"]["RemoveRoll"] = False
    return settings


def copy_tree_files(source: Path, destination: Path) -> None:
    for path in sorted(source.rglob("*")):
        if path.is_file():
            relative = path.relative_to(source)
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)


def provenance_for(relative: Path) -> str:
    text = relative.as_posix()
    if text == f"{MALIS_DIRECTORY}/Malis Mission Roller 2.dll":
        return "COMPILED_FROM_AUDITED_MALIS_SOURCE_COMMIT"
    if text == f"{HARVESTER_DIRECTORY}/AOSharpMissionOfferHarvester.dll":
        return "COMPILED_FROM_TRACKED_AOREBIRTH_HARVESTER_SOURCE"
    if text == f"{MALIS_DIRECTORY}/JSON/Settings.json":
        return "DERIVED_SAFE_EVIDENCE_CONFIGURATION_FROM_AUDITED_DEFAULT"
    if text.startswith(f"{MALIS_DIRECTORY}/"):
        return "BYTE_IDENTICAL_AUDITED_MALIS_SOURCE_RESOURCE_OR_BUILD_CONFIG"
    return "TRACKED_AOREBIRTH_PACKAGE_DOCUMENTATION"


def package(runtime: Path, work_root: Path, package_root: Path, metadata_path: Path) -> None:
    runtime = runtime.resolve()
    source_root = work_root / "source" / SOURCE_ROOT_NAME
    malis_output = source_root / "bin/Release"
    harvester_output = ROOT / "Tools/AOSharpMissionOfferHarvester/bin/Release/AOSharpMissionOfferHarvester.dll"
    require((malis_output / "Malis Mission Roller 2.dll").is_file(), "Malis Release DLL missing; build first")
    require(harvester_output.is_file(), "Harvester Release DLL missing; build first")
    require(metadata_path.is_file(), "Compatibility metadata missing; run checker first")
    recreate_owned_directory(package_root, ROOT / "build-verify")

    malis_target = package_root / MALIS_DIRECTORY
    harvester_target = package_root / HARVESTER_DIRECTORY
    malis_target.mkdir()
    harvester_target.mkdir()
    shutil.copy2(malis_output / "Malis Mission Roller 2.dll", malis_target / "Malis Mission Roller 2.dll")
    shutil.copy2(malis_output / "Malis Mission Roller 2.dll.config", malis_target / "Malis Mission Roller 2.dll.config")
    for directory in ("JSON", "Sound", "UI"):
        copy_tree_files(malis_output / directory, malis_target / directory)
    safe_settings = build_safe_settings(source_root / "JSON/Default_Settings.json")
    (malis_target / "JSON/Settings.json").write_text(canonical_json(safe_settings), encoding="utf-8", newline="\n")
    shutil.copy2(harvester_output, harvester_target / "AOSharpMissionOfferHarvester.dll")
    shutil.copy2(ROOT / "Tools/AOSharpMissionOfferHarvester/README.md", harvester_target / "README.txt")
    shutil.copy2(ROOT / "Tools/MalisLivePackage/README-FIRST.txt", package_root / "README-FIRST.txt")

    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    for row in metadata["RuntimeAssemblies"]:
        runtime_file = Path(row["Path"])
        row["Sha256"] = sha256(runtime_file)
        row["ByteLength"] = runtime_file.stat().st_size
        row["Pe"] = parse_pe(runtime_file)
    deploy_files = []
    for path in sorted(package_root.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(package_root)
        if relative.name in {"deployment-manifest.json", "SHA256SUMS.txt", "install-receipt.json"}:
            continue
        destination = None
        if relative.parts[0] in {MALIS_DIRECTORY, HARVESTER_DIRECTORY}:
            destination = (Path("Plugins") / relative).as_posix()
        deploy_files.append(
            {
                "ByteLength": path.stat().st_size,
                "DestinationRelativePath": destination,
                "PackageRelativePath": relative.as_posix(),
                "Provenance": provenance_for(relative),
                "Sha256": sha256(path),
            }
        )

    manifest = {
        "SchemaVersion": 1,
        "BuildDefinitionBaseline": BASELINE_COMMIT,
        "BuildTimestampUtc": None,
        "TimestampPolicy": "OMITTED_FOR_DETERMINISTIC_REGENERATION",
        "MalisSource": {
            "Commit": SOURCE_COMMIT,
            "Archive": SOURCE_ZIP.relative_to(ROOT).as_posix(),
            "ArchiveSha256": SOURCE_SHA256,
            "SourceChanges": [],
        },
        "Build": {
            "Framework": ".NET Framework 4.8",
            "PlatformTarget": "x86",
            "MSBuildVersion": "18.8.2.30814",
            "MalisWarnings": [
                "CS0672 Main.Run(string) overrides the retained obsolete AOPluginEntry.Run(string) compatibility seam",
                "CS0649 MainWindow._missionLevel is an audited pre-existing unassigned field; not an installed-runtime API mismatch",
            ],
        },
        "InstalledAOSharpRuntime": metadata,
        "MalisCompatibilityChanges": [],
        "HarvesterCompatibilityChanges": [
            "Added passive Network.N3MessageSent observation mode for Malis-generated requests",
            "Preserved the existing active request mode independently",
        ],
        "SafeEvidenceConfiguration": {
            "AutoAccept": False,
            "AutoAdjustQl": False,
            "RemoveRoll": False,
            "EnabledMissionTypes": [],
            "DefaultSettingsRetainedUnchanged": True,
            "MissionSelectionAlgorithmChanged": False,
        },
        "RuntimeResources": {
            "JsonFiles": 8,
            "SoundFiles": 2,
            "UiTextureFiles": 40,
            "UiViewFiles": 11,
            "UiWindowFiles": 3,
        },
        "ExcludedPrivateDependencies": sorted(BANNED_PRIVATE_DEPENDENCIES),
        "Files": deploy_files,
        "MissionOfferHarvesterCoexistence": {
            "ChatCommands": {"Malis": "/mmr (developer-only)", "Harvester": "/missionharvest"},
            "AssemblyIdentity": "PASS",
            "DependencyClosure": "PASS",
            "PrivateAOSharpAssembliesBundled": False,
            "EvidenceAuthority": "MissionOfferHarvester raw JSONL",
            "LiveValidation": "NOT_PERFORMED",
        },
        "MissionBehaviorIntentionallyChanged": False,
        "RuntimeMissionLogicChanged": False,
    }
    manifest_text = canonical_json(manifest)
    (package_root / "deployment-manifest.json").write_text(manifest_text, encoding="utf-8", newline="\n")
    tracked_manifest = ROOT / MANIFEST_RELATIVE
    tracked_manifest.parent.mkdir(parents=True, exist_ok=True)
    tracked_manifest.write_text(manifest_text, encoding="utf-8", newline="\n")
    sums = []
    for path in sorted(package_root.rglob("*")):
        if path.is_file() and path.name != "SHA256SUMS.txt":
            sums.append(f"{sha256(path).upper()}  {path.relative_to(package_root).as_posix()}")
    (package_root / "SHA256SUMS.txt").write_text("\n".join(sums) + "\n", encoding="utf-8", newline="\n")
    print("MALIS_LIVE_PACKAGE_WRITE=PASS")


def validate_package(package_root: Path) -> dict[str, object]:
    manifest_path = package_root / "deployment-manifest.json"
    require(manifest_path.is_file(), "Deployment manifest missing")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    require((ROOT / MANIFEST_RELATIVE).read_text(encoding="utf-8") == manifest_path.read_text(encoding="utf-8"), "Tracked deployment manifest is stale")
    expected = {row["PackageRelativePath"] for row in manifest["Files"]} | {"deployment-manifest.json", "SHA256SUMS.txt"}
    actual = {path.relative_to(package_root).as_posix() for path in package_root.rglob("*") if path.is_file() and path.name != "install-receipt.json"}
    require(actual == expected, f"Package file set mismatch: missing={sorted(expected-actual)} extra={sorted(actual-expected)}")
    for row in manifest["Files"]:
        path = package_root / row["PackageRelativePath"]
        require(path.stat().st_size == row["ByteLength"], f"Byte length mismatch: {path}")
        require(sha256(path) == row["Sha256"], f"SHA-256 mismatch: {path}")
    for path in package_root.rglob("*.dll"):
        require(path.name not in BANNED_PRIVATE_DEPENDENCIES, f"Private host dependency must not be packaged: {path.name}")
    for path in (package_root / MALIS_DIRECTORY / "JSON").glob("*.json"):
        json.loads(path.read_text(encoding="utf-8-sig"))
    for path in (package_root / MALIS_DIRECTORY / "UI").rglob("*.xml"):
        ET.parse(path)
    ET.parse(package_root / MALIS_DIRECTORY / "Malis Mission Roller 2.dll.config")
    settings = json.loads((package_root / MALIS_DIRECTORY / "JSON/Settings.json").read_text(encoding="utf-8"))
    require(settings["Extras"]["AutoAccept"] is False, "Safe Settings.json must disable AutoAccept")
    require(settings["Extras"]["AutoAdjustQl"] is False, "Safe Settings.json must disable AutoAdjustQl")
    require(not any(settings["Types"].values()), "Safe Settings.json must disable match types")
    for path in (package_root / MALIS_DIRECTORY / "UI/Textures").glob("*.png"):
        require(path.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n", f"Invalid PNG: {path}")
    for path in (package_root / MALIS_DIRECTORY / "Sound").glob("*.wav"):
        data = path.read_bytes()[:12]
        require(data[:4] == b"RIFF" and data[8:12] == b"WAVE", f"Invalid WAV: {path}")
    sums_expected = []
    for path in sorted(package_root.rglob("*")):
        if path.is_file() and path.name not in {"SHA256SUMS.txt", "install-receipt.json"}:
            sums_expected.append(f"{sha256(path).upper()}  {path.relative_to(package_root).as_posix()}")
    sums_actual = (package_root / "SHA256SUMS.txt").read_text(encoding="utf-8").splitlines()
    require(sums_actual == sums_expected, "SHA256SUMS.txt is stale")
    require(manifest["MissionBehaviorIntentionallyChanged"] is False, "Manifest claims a Malis behavior change")
    require(manifest["RuntimeMissionLogicChanged"] is False, "Manifest claims an AORebirth runtime change")
    return manifest


def check(package_root: Path) -> None:
    validate_package(package_root)
    print("MALIS_LIVE_PACKAGE_CHECK=PASS")


def md5_key(path: Path) -> str:
    return hashlib.md5(path.read_bytes()).hexdigest().upper()


def install(runtime: Path, package_root: Path) -> None:
    validate_package(package_root)
    runtime = runtime.resolve()
    plugin_root = require_within(runtime / "Plugins", runtime)
    require(plugin_root.is_dir(), "Installed AOSharp Plugins directory missing")
    config_path = runtime / "config.json"
    require(config_path.is_file(), "Installed AOSharp config.json missing")
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup_root = runtime / "Backups" / f"MalisLiveInstall-{timestamp}"
    backup_root.mkdir(parents=True)
    shutil.copy2(config_path, backup_root / "config.json.before")
    previous_files = [{
        "Path": str(config_path),
        "BackupPath": str(backup_root / "config.json.before"),
        "Sha256": sha256(config_path),
    }]

    targets = {
        "Malis Mission Roller 2": (package_root / MALIS_DIRECTORY, plugin_root / MALIS_DIRECTORY, "Malis Mission Roller 2.dll", "1.0.0.0"),
        "AOSharp Mission Offer Harvester": (package_root / HARVESTER_DIRECTORY, plugin_root / HARVESTER_DIRECTORY, "AOSharpMissionOfferHarvester.dll", "1.1.0.0"),
    }
    config = json.loads(config_path.read_text(encoding="utf-8"))
    stale_keys = [key for key, value in config.get("Plugins", {}).items() if value.get("Name") in targets]
    for key in stale_keys:
        old_path = Path(config["Plugins"][key]["Path"])
        if old_path.exists():
            backup_target = backup_root / "previous-configured-plugins" / key
            backup_target.parent.mkdir(parents=True, exist_ok=True)
            if old_path.is_dir():
                shutil.copytree(old_path, backup_target)
                for old_file in sorted(path for path in old_path.rglob("*") if path.is_file()):
                    previous_files.append({
                        "Path": str(old_file),
                        "BackupPath": str(backup_target / old_file.relative_to(old_path)),
                        "Sha256": sha256(old_file),
                    })
            else:
                backup_file = backup_target.with_suffix(old_path.suffix)
                shutil.copy2(old_path, backup_file)
                previous_files.append({"Path": str(old_path), "BackupPath": str(backup_file), "Sha256": sha256(old_path)})
        del config["Plugins"][key]
        for profile in config.get("Profiles", []):
            profile["EnabledPlugins"] = [value for value in profile.get("EnabledPlugins", []) if value != key]

    installed = []
    for name, (source, target, dll_name, version) in targets.items():
        target = require_within(target, plugin_root)
        if target.exists():
            shutil.copytree(target, backup_root / target.name)
            for old_file in sorted(path for path in target.rglob("*") if path.is_file()):
                previous_files.append({
                    "Path": str(old_file),
                    "BackupPath": str(backup_root / target.name / old_file.relative_to(target)),
                    "Sha256": sha256(old_file),
                })
            shutil.rmtree(target)
        shutil.copytree(source, target)
        dll_path = target / dll_name
        key = md5_key(dll_path)
        config["Plugins"][key] = {"Name": name, "Version": version, "Path": str(dll_path)}
        for profile in config.get("Profiles", []):
            enabled_plugins = profile.setdefault("EnabledPlugins", [])
            if key not in enabled_plugins:
                enabled_plugins.append(key)
        installed.append({"Name": name, "Key": key, "Path": str(dll_path), "Sha256": sha256(dll_path)})

    temporary = runtime / "config.json.malis-live-install.tmp"
    temporary.write_text(json.dumps(config, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    with temporary.open("r+b") as stream:
        os.fsync(stream.fileno())
    os.replace(temporary, config_path)
    receipt = {
        "InstalledAtUtc": datetime.now(timezone.utc).isoformat(),
        "Runtime": str(runtime),
        "ConfigBackup": str(backup_root / "config.json.before"),
        "BackupRoot": str(backup_root),
        "InstalledPlugins": installed,
        "PreviousFiles": previous_files,
        "ProfilesEnabled": [profile["Name"] for profile in config.get("Profiles", [])],
        "LiveAoTested": False,
    }
    (package_root / "install-receipt.json").write_text(canonical_json(receipt), encoding="utf-8", newline="\n")
    for item in installed:
        require(sha256(Path(item["Path"])) == item["Sha256"], f"Installed hash verification failed: {item['Path']}")
    print("MALIS_LIVE_INSTALL=PASS")
    print("MALIS_LIVE_BACKUP=" + str(backup_root))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument("--prepare", action="store_true")
    modes.add_argument("--package", action="store_true")
    modes.add_argument("--check", action="store_true")
    modes.add_argument("--install", action="store_true")
    parser.add_argument("--runtime", type=Path, required=True)
    parser.add_argument("--work-root", type=Path, default=ROOT / "tools-temp/MalisLiveBuild")
    parser.add_argument("--package-root", type=Path, default=ROOT / "build-verify/MalisMissionLive")
    parser.add_argument("--metadata", type=Path, default=ROOT / "tools-temp/MalisLiveBuild/compatibility-metadata.json")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.prepare:
        prepare(args.runtime, args.work_root)
    elif args.package:
        package(args.runtime, args.work_root, args.package_root, args.metadata)
    elif args.check:
        check(args.package_root)
    else:
        install(args.runtime, args.package_root)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
