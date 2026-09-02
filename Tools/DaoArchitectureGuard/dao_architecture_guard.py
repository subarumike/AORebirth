#!/usr/bin/env python3
"""Deterministic audit-mode guard for AORebirth DAO migration boundaries."""

from __future__ import annotations

import argparse
import json
import re
import tempfile
from pathlib import Path


SQL_PATTERNS = (
    re.compile(r"\bSELECT\b.+\bFROM\b", re.IGNORECASE | re.DOTALL),
    re.compile(r"\bINSERT\s+(?:IGNORE\s+)?INTO\b", re.IGNORECASE),
    re.compile(r"\bUPDATE\b.+\bSET\b", re.IGNORECASE | re.DOTALL),
    re.compile(r"\bDELETE\s+FROM\b", re.IGNORECASE),
    re.compile(r"\bREPLACE\s+INTO\b", re.IGNORECASE),
    re.compile(r"\bCALL\s+[A-Za-z_]", re.IGNORECASE),
    re.compile(r"\b(?:CREATE|ALTER)\s+TABLE\b", re.IGNORECASE),
)

TABLE_PATTERN = re.compile(
    r"\b(?:characters|stats|login|missionstates|missionobjectiveprogress|"
    r"missionobjectiveobservations|missionflags|missionaccountflags|missionrewardledger|"
    r"bot_[a-z_]+|account_[a-z_]+|information_schema|receivedmessages|items|instanceditems)\b",
    re.IGNORECASE,
)

MISSION_FORBIDDEN_TOKENS = (
    "System.Data",
    "Dapper",
    "Connector.GetConnection",
    "IDbConnection",
    "IDbCommand",
    "DbConnection",
    "DbCommand",
    "MySqlConnection",
    "MySqlCommand",
    "NpgsqlConnection",
    "NpgsqlCommand",
    "SqlConnection",
    "SqlCommand",
    "MySqlMissionRepository",
    "MissionRollFeeClaimRepository",
    "NewCharacterStartAreaSelectionDao",
)

EXCLUDED_PARTS = {"bin", "obj", "packages", ".git"}


def normalize(path: Path) -> str:
    return path.as_posix()


def extract_csharp_strings(text: str) -> list[str]:
    values: list[str] = []
    index = 0
    length = len(text)
    while index < length:
        if text.startswith("//", index):
            end = text.find("\n", index + 2)
            index = length if end < 0 else end + 1
            continue
        if text.startswith("/*", index):
            end = text.find("*/", index + 2)
            index = length if end < 0 else end + 2
            continue

        prefix_length = 0
        verbatim = False
        if text.startswith('$@"', index) or text.startswith('@$"', index):
            prefix_length = 3
            verbatim = True
        elif text.startswith('@"', index):
            prefix_length = 2
            verbatim = True
        elif text.startswith('$"', index):
            prefix_length = 2
        elif text[index] == '"':
            prefix_length = 1

        if prefix_length:
            cursor = index + prefix_length
            value: list[str] = []
            while cursor < length:
                if verbatim and text.startswith('""', cursor):
                    value.append('"')
                    cursor += 2
                    continue
                if text[cursor] == '"':
                    cursor += 1
                    break
                if not verbatim and text[cursor] == "\\" and cursor + 1 < length:
                    value.append(text[cursor + 1])
                    cursor += 2
                    continue
                value.append(text[cursor])
                cursor += 1
            values.append("".join(value))
            index = cursor
            continue

        if text[index] == "'":
            index += 1
            while index < length:
                if text[index] == "\\" and index + 1 < length:
                    index += 2
                    continue
                if text[index] == "'":
                    index += 1
                    break
                index += 1
            continue

        index += 1
    return values


def contains_sql(text: str) -> bool:
    values = extract_csharp_strings(text)
    for start in range(len(values)):
        for width in range(1, min(6, len(values) - start) + 1):
            candidate = " ".join(values[start : start + width])
            if TABLE_PATTERN.search(candidate) and any(pattern.search(candidate) for pattern in SQL_PATTERNS):
                return True
    return False


def is_excluded(relative: Path) -> bool:
    parts = set(relative.parts)
    if parts & EXCLUDED_PARTS:
        return True
    normalized = normalize(relative)
    return (
        normalized.startswith("AORebirth/Libraries/Source/AORebirth.Database/")
        or normalized.startswith("Tools/")
        or normalized.startswith("LinuxBuild/")
        or "/Tests/" in "/" + normalized
        or normalized.endswith("Tests.cs")
        or "/Migrations/" in "/" + normalized
    )


def production_sources(root: Path) -> list[Path]:
    roots = (
        root / "AORebirth" / "Server",
        root / "AORebirth" / "Libraries" / "Source" / "AORebirth.AccountBroker",
        root / "AORebirth" / "Libraries" / "Source" / "AORebirth.BotService",
    )
    result: list[Path] = []
    for source_root in roots:
        if not source_root.exists():
            continue
        for path in source_root.rglob("*.cs"):
            relative = path.relative_to(root)
            if not is_excluded(relative):
                result.append(path)
    return sorted(result)


def direct_sql_sites(root: Path) -> list[str]:
    sites = []
    for path in production_sources(root):
        if contains_sql(path.read_text(encoding="utf-8-sig")):
            sites.append(normalize(path.relative_to(root)))
    return sorted(sites)


def mission_boundary_files(root: Path) -> list[Path]:
    mission_root = root / "AORebirth" / "Server" / "ZoneEngine" / "Core" / "Missions"
    result = sorted(mission_root.glob("*.cs")) if mission_root.exists() else []
    result.extend(
        path
        for path in (
            root / "AORebirth" / "Server" / "ZoneEngine" / "Core" / "NewCharacterStartAreaSelectionRuntime.cs",
            root / "AORebirth" / "Server" / "LoginEngine" / "Packets" / "CharacterName.cs",
        )
        if path.exists()
    )
    return result


def mission_boundary_violations(root: Path) -> list[str]:
    violations: list[str] = []
    for path in mission_boundary_files(root):
        text = path.read_text(encoding="utf-8-sig")
        relative = normalize(path.relative_to(root))
        if contains_sql(text):
            violations.append(relative + ":embedded-sql")
        for token in MISSION_FORBIDDEN_TOKENS:
            if token in text:
                violations.append(relative + ":" + token)
    return sorted(set(violations))


def load_manifest(path: Path) -> list[str]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    entries = payload.get("directRuntimeSqlSites")
    if not isinstance(entries, list):
        raise ValueError("directRuntimeSqlSites must be a list")
    paths = []
    for entry in entries:
        if not isinstance(entry, dict):
            raise ValueError("every baseline entry must be an object")
        for required in ("path", "category", "owner", "targetPhase"):
            if required not in entry:
                raise ValueError("baseline entry is missing " + required)
        paths.append(str(entry["path"]).replace("\\", "/"))
    if len(paths) != len(set(paths)):
        raise ValueError("baseline paths must be unique")
    return sorted(paths)


def validate(root: Path, manifest_path: Path) -> tuple[list[str], list[str], list[str]]:
    actual = direct_sql_sites(root)
    baseline = load_manifest(manifest_path)
    new_sites = sorted(set(actual) - set(baseline))
    stale = sorted(set(baseline) - set(actual))
    boundary = mission_boundary_violations(root)
    return new_sites, stale, boundary


def write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def self_test() -> None:
    with tempfile.TemporaryDirectory(prefix="aorebirth-dao-guard-") as temporary:
        root = Path(temporary)
        runtime = root / "AORebirth/Server/ZoneEngine/RuntimeSql.cs"
        write(runtime, 'class RuntimeSql { const string Sql = "SELECT Id FROM characters"; }')
        write(
            root / "AORebirth/Libraries/Source/AORebirth.Database/Domain/Ignored.cs",
            'class Ignored { const string Sql = "DELETE FROM characters"; }',
        )
        write(root / "Tools/Ignored.cs", 'class Ignored { const string Sql = "UPDATE stats SET StatValue=1"; }')
        write(root / "AORebirth/Server/Tests/Ignored.cs", 'class Ignored { const string Sql = "SELECT Id FROM login"; }')
        write(
            root / "AORebirth/Libraries/Source/AORebirth.Database/Migrations/Ignored.cs",
            'class Ignored { const string Sql = "ALTER TABLE stats ADD X INT"; }',
        )
        manifest = root / "manifest.json"
        write(
            manifest,
            json.dumps(
                {
                    "directRuntimeSqlSites": [
                        {
                            "path": normalize(runtime.relative_to(root)),
                            "category": "fixture",
                            "owner": "fixture",
                            "targetPhase": 1,
                        }
                    ]
                }
            ),
        )
        new_sites, stale, boundary = validate(root, manifest)
        if new_sites or stale or boundary:
            raise RuntimeError("positive fixture failed")

        write(
            root / "AORebirth/Server/ZoneEngine/Core/Missions/BadMission.cs",
            "using System.Data; class BadMission { IDbConnection value; }",
        )
        if not mission_boundary_violations(root):
            raise RuntimeError("mission provider fixture was not rejected")

        write(
            root / "AORebirth/Server/ZoneEngine/Unexpected.cs",
            'class Unexpected { const string Sql = "INSERT INTO stats (StatId) VALUES (1)"; }',
        )
        new_sites, _, _ = validate(root, manifest)
        if "AORebirth/Server/ZoneEngine/Unexpected.cs" not in new_sites:
            raise RuntimeError("new runtime SQL fixture was not rejected")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        print("DAO_ARCHITECTURE_GUARD_SELF_TEST=PASS")
        return 0

    if args.root is None:
        parser.error("--root is required unless --self-test is used")
    root = args.root.resolve()
    manifest = (args.manifest or Path(__file__).with_name("known-violations.json")).resolve()
    try:
        new_sites, stale, boundary = validate(root, manifest)
        actual = direct_sql_sites(root)
        baseline = load_manifest(manifest)
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print("DAO_ARCHITECTURE_GUARD=FAIL")
        print("ERROR=" + str(error))
        return 1

    for value in new_sites:
        print("NEW_VIOLATION=" + value)
    for value in stale:
        print("STALE_BASELINE_EXCEPTION=" + value)
    for value in boundary:
        print("MISSION_BOUNDARY_VIOLATION=" + value)

    passed = not new_sites and not stale and not boundary and actual == baseline
    print("DAO_ARCHITECTURE_GUARD=" + ("PASS" if passed else "FAIL"))
    print("PRODUCTION_SQL_SITES=" + str(len(actual)))
    print("LEGACY_BASELINE_EXCEPTIONS=" + str(len(baseline)))
    print("NEW_VIOLATIONS=" + str(len(new_sites) + len(boundary)))
    print("MISSION_RUNTIME_DIRECT_SQL=" + str(sum(1 for item in boundary if item.endswith(":embedded-sql"))))
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
