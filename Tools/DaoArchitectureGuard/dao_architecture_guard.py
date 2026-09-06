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
    "MySqlMissionDao",
    "DatabaseDaoFactory",
    "MySqlConnector",
    "Npgsql",
    "SqlClient",
    "IDbTransaction",
    "DbTransaction",
    "IDataReader",
)

EXCLUDED_PARTS = {"bin", "obj", "packages", ".git"}

PERSISTENCE_ROOTS = (
    "AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Missions",
    "AORebirth/Libraries/Source/AORebirth.Database/Domain/Missions",
)
ENGINE_TOKENS = (
    "ZoneEngine", "ZoneEngine_New", "LoginEngine", "ChatEngine", "WebEngine",
    "AORebirth.Core", "AORebirth.Stats", "SmokeLounge.AOtomation", "Cell.Core",
    "Player", "Character", "ZoneClient", "IZoneSession", "IPlayfield", "Playfield",
)
CONTRACT_TOKENS = MISSION_FORBIDDEN_TOKENS + (
    "AORebirth.Database", "AORebirth.Enums", "Utility", "MySqlConnector", "Npgsql",
    "SqlClient", "IDbTransaction", "DbTransaction", "IDataReader", "DataTable",
    "DataSet", "IQueryable", "DbDataReader",
)


def code_only(text: str) -> str:
    # Contracts use C# 7.3. Strip comments and string/character literals before
    # checking references, so documentation and SQL values do not impersonate code.
    return re.sub(
        r'//[^\n]*|/\*.*?\*/|(?:\$@|@\$|@)"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',
        " ", text, flags=re.DOTALL,
    )


def mission_persistence_violations(root: Path) -> list[str]:
    violations = []
    for relative_root in PERSISTENCE_ROOTS:
        directory = root / relative_root
        sources = sorted(path for path in directory.rglob("*.cs")
                         if not set(path.relative_to(directory).parts) & EXCLUDED_PARTS)
        if not sources:
            violations.append(relative_root + ":missing-persistence-sources")
        contract = "/AORebirth.Interfaces/" in relative_root
        for path in sources:
            text = path.read_text(encoding="utf-8-sig")
            code = code_only(text)
            relative = normalize(path.relative_to(root))
            tokens = ENGINE_TOKENS + (CONTRACT_TOKENS if contract else ())
            for token in tokens:
                if re.search(r"(?<![\w])" + re.escape(token) + r"(?![\w])", code):
                    violations.append(relative + ":" + token)
            if contract and re.search(r"\bDB[A-Z]\w*\b", code):
                violations.append(relative + ":database-row-type")
            if contract and any(pattern.search(value) for value in extract_csharp_strings(text)
                                for pattern in SQL_PATTERNS):
                violations.append(relative + ":embedded-sql")
    return sorted(set(violations))


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
    result = []
    for engine in ("ZoneEngine", "ZoneEngine_New"):
        mission_root = root / "AORebirth" / "Server" / engine / "Core" / "Missions"
        result.extend(sorted(path for path in mission_root.rglob("*.cs")
                             if not set(path.relative_to(mission_root).parts) & EXCLUDED_PARTS))
    result.extend(
        path
        for path in (
            root / "AORebirth" / "Server" / "ZoneEngine" / "Core" / "NewCharacterStartAreaSelectionRuntime.cs",
            root / "AORebirth" / "Server" / "LoginEngine" / "Packets" / "CharacterName.cs",
            root / "AORebirth" / "Server" / "ZoneEngine_New" / "Core" / "NewCharacterStartAreaSelectionRuntime.cs",
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
        code = code_only(text)
        for token in MISSION_FORBIDDEN_TOKENS:
            if re.search(r"(?<![\w])" + re.escape(token) + r"(?![\w])", code):
                violations.append(relative + ":" + token)
    # A retained public compatibility shim may forward calls, never own mission SQL.
    shim = root / "AORebirth/Libraries/Source/AORebirth.Database/Dao/NewCharacterStartAreaSelectionDao.cs"
    if shim.exists():
        text = shim.read_text(encoding="utf-8-sig")
        relative = normalize(shim.relative_to(root))
        if contains_sql(text):
            violations.append(relative + ":duplicate-mission-sql")
        for token in ("Dapper", "Connector", "System.Data", "MySqlConnector"):
            if re.search(r"(?<![\w])" + re.escape(token) + r"(?![\w])", code_only(text)):
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
    boundary = mission_boundary_violations(root) + mission_persistence_violations(root)
    return new_sites, stale, boundary


def write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def self_test() -> None:
    with tempfile.TemporaryDirectory(prefix="aorebirth-dao-guard-") as temporary:
        root = Path(temporary)
        contract = root / PERSISTENCE_ROOTS[0] / "IMissionDao.cs"
        implementation = root / PERSISTENCE_ROOTS[1] / "MySqlMissionDao.cs"
        write(contract, 'namespace AORebirth.Interfaces.Persistence.Missions { public interface IMissionDao {} }')
        write(implementation, 'using Dapper; class Dao { const string Sql = "SELECT State FROM missionstates"; }')
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

        for bad_source in (
            "using Data = System.Data; interface Bad { Data.IDbConnection Get(); }",
            "interface Bad { AORebirth.Database.Entities.DBCharacter Get(); }",
            "interface Bad { ZoneEngine_New.Core.Entities.Player Get(); }",
            "using System.Linq; interface Bad { IQueryable<int> Get(); }",
            'class Bad { const string Sql = "DELETE FROM arbitrary_table"; }',
        ):
            write(contract, bad_source)
            if not mission_persistence_violations(root):
                raise RuntimeError("persistence contract fixture was not rejected: " + bad_source)
        write(contract, '// IDbConnection ZoneEngine_New\ninterface IMissionDao {}')
        for token in ("ZoneEngine_New.Core.Entities.Player", "ZoneEngine.Core.Missions.MissionRuntime", "AORebirth.Core.Character"):
            write(implementation, "class Bad { " + token + " field; }")
            if not mission_persistence_violations(root):
                raise RuntimeError("persistence engine dependency fixture was not rejected")
        write(implementation, 'using Dapper; class Dao { const string Note = "ZoneEngine_New"; }')
        if mission_persistence_violations(root):
            raise RuntimeError("persistence comment/string fixture was rejected")

        write(
            root / "AORebirth/Server/ZoneEngine/Core/Missions/BadMission.cs",
            "using System.Data; class BadMission { IDbConnection value; }",
        )
        if not mission_boundary_violations(root):
            raise RuntimeError("mission provider fixture was not rejected")

        (root / "AORebirth/Server/ZoneEngine/Core/Missions/BadMission.cs").unlink()
        for engine in ("ZoneEngine", "ZoneEngine_New"):
            nested = root / ("AORebirth/Server/" + engine + "/Core/Missions/Nested/Adapter.cs")
            for bad_source in (
                "using Provider = MySqlConnector; class Bad {}",
                "class Bad { IDbTransaction transaction; }",
                "class Bad { object Create() => DatabaseDaoFactory.CreateMissionDao(); }",
                'class Bad { string Sql = "SELECT State FROM missionstates"; }',
            ):
                write(nested, bad_source)
                if not mission_boundary_violations(root):
                    raise RuntimeError("nested mission boundary fixture was not rejected")
            write(nested, '// IDbConnection MySqlMissionDao\nclass Good { string Note = "Dapper"; }')
            if mission_boundary_violations(root):
                raise RuntimeError("mission comment/string fixture was rejected")
        shim = root / "AORebirth/Libraries/Source/AORebirth.Database/Dao/NewCharacterStartAreaSelectionDao.cs"
        write(shim, 'class Shim { string Sql = "SELECT Value FROM missionflags"; }')
        if not mission_boundary_violations(root):
            raise RuntimeError("duplicate mission SQL fixture was not rejected")
        write(shim, "class Shim { object Create() => DatabaseDaoFactory.CreateMissionDao(); }")
        if mission_boundary_violations(root):
            raise RuntimeError("forwarding compatibility shim fixture was rejected")

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
    parser.add_argument("--mission-persistence-only", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        print("DAO_ARCHITECTURE_GUARD_SELF_TEST=PASS")
        return 0

    if args.root is None:
        parser.error("--root is required unless --self-test is used")
    root = args.root.resolve()
    manifest = (args.manifest or Path(__file__).with_name("known-violations.json")).resolve()
    if args.mission_persistence_only:
        try:
            boundary = mission_boundary_violations(root) + mission_persistence_violations(root)
        except (OSError, ValueError) as error:
            print("MISSION_PERSISTENCE_GUARD=FAIL")
            print("ERROR=" + str(error))
            return 1
        for value in boundary:
            print("MISSION_BOUNDARY_VIOLATION=" + value)
        print("MISSION_PERSISTENCE_GUARD=" + ("FAIL" if boundary else "PASS"))
        print("MISSION_BOUNDARY_VIOLATIONS=" + str(len(boundary)))
        return 1 if boundary else 0
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
