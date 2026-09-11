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

# Account enforcement is deliberately opt-in. The legacy runtime SQL baseline
# and mission rules above remain independent of this parallel foundation.
ACCOUNT_PERSISTENCE_ROOTS = (
    "AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Accounts",
    "AORebirth/Libraries/Source/AORebirth.Database/Domain/Accounts",
)
ACCOUNT_CONTRACT_TOKENS = CONTRACT_TOKENS + (
    "Connector", "DBLoginData", "LoginDataDao", "MySqlAccountDao",
    "DbProviderFactory", "IDataParameter", "IDbDataParameter", "DbParameter",
    "IDbDataAdapter", "DataAdapter", "DbDataAdapter", "SqlMapper", "IDao",
)
ACCOUNT_DOMAIN_TOKENS = (
    "AORebirth.AccountBroker", "AORebirth.BotService", "IAccountIdentityDao",
    "ICharacterDao", "DBCharacter", "CharacterDao", "LoginDataDao",
)
ACCOUNT_CONCEPT_PATTERN = re.compile(
    r"\b\w*(?:AccountIdentity|AccountBroker|BotService|Token|PasswordReset|"
    r"EmailVerification|Provisioning|ExternalMapping|Logoff|Logout|Offline|Online)\w*\b",
    re.IGNORECASE,
)
ACCOUNT_GM_MUTATION_PATTERN = re.compile(
    r"\b(?:Set|Update|Change|Write|Save|Grant|Revoke|Reset|Assign|Apply|Enable|"
    r"Disable|Promote|Demote)\w*(?:Gm|GameMaster)\w*\s*(?:<[^;{}]*>)?\s*\(",
    re.IGNORECASE,
)
ACCOUNT_GENERIC_PATTERN = re.compile(
    r"\b(?:I?GenericRepository|I?Repository|I?GenericDao|Dao)\s*<|"
    r"\b(?:GetAll|GetWhere|Query|Save|Delete|Add)\s*(?:<|\()|"
    r"\b(?:object|dynamic|tableName|columnName|queryObject|sql)\b", re.IGNORECASE,
)
ACCOUNT_STORAGE_LITERAL_PATTERN = re.compile(
    r"\b(?:login|characters|account_[a-z_]+|bot_[a-z_]+|information_schema)\b|"
    r"^\s*[`\[\]]*(?:Id|CreationDate|Email|FirstName|LastName|Username|Password|"
    r"AllowedCharacters|Flags|AccountFlags|Expansions|GM|Online)[`\[\]]*\s*$",
    re.IGNORECASE,
)

CHARACTER_PERSISTENCE_ROOTS = (
    "AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Characters",
    "AORebirth/Libraries/Source/AORebirth.Database/Domain/Characters",
)
CHARACTER_CONTRACT_TOKENS = ACCOUNT_CONTRACT_TOKENS + (
    "DBCharacter", "CharacterDao", "MySqlCharacterDao", "IAccountDao", "IMissionDao",
)
CHARACTER_RUNTIME_TOKENS = (
    "CharacterOnlineOwnershipGuard", "System.Threading", "System.Diagnostics",
    "File", "Directory", "FileStream", "StreamReader", "StreamWriter", "FileInfo", "DirectoryInfo",
    "Process", "Thread", "ZoneLeaseReference", "HeldZoneLease",
    "AORebirth.AccountBroker", "AORebirth.BotService", "CharacterDao", "LoginDataDao",
)
CHARACTER_EXCLUDED_PATTERN = re.compile(
    r"\b\w*(?:Inventory|Stats|StatValue|StatData|Nano|Perk|Organi[sz]ation|Mission|"
    r"Buddy|RecentMessage|AccountIdentity|Token|Password|Hydrat|Location|Heading|"
    r"Texture|Coordinate|Delete|RemoveBuddy|AddBuddy|SaveProfile|SaveCharacter|"
    r"SaveLocation|CreateCharacter|SetPlayfield)\w*\b", re.IGNORECASE,
)
CHARACTER_STORAGE_LITERAL_PATTERN = re.compile(
    r"\b(?:characters|login|stats|items|instanceditems|organizations|mission[a-z_]+|"
    r"account_[a-z_]+|bot_[a-z_]+|information_schema)\b|"
    r"^\s*[`\[\]]*(?:Id|Username|Name|FirstName|LastName|Playfield|Online|"
    r"BuddyList|X|Y|Z|Heading[XYZW]|Textures[0-4])[`\[\]]*\s*$", re.IGNORECASE,
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


def account_persistence_violations(root: Path) -> list[str]:
    violations = []
    for relative_root in ACCOUNT_PERSISTENCE_ROOTS:
        directory = root / relative_root
        sources = sorted(path for path in directory.rglob("*.cs")
                         if not set(path.relative_to(directory).parts) & EXCLUDED_PARTS)
        if not sources:
            violations.append(relative_root + ":missing-persistence-sources")
        contract = "/AORebirth.Interfaces/" in relative_root
        for path in sources:
            text = path.read_text(encoding="utf-8-sig")
            # C# permits whitespace and comments around a qualified-name dot.
            # Account-only normalization leaves the existing mission scan intact.
            code = re.sub(r"\s*\.\s*", ".", code_only(text))
            relative = normalize(path.relative_to(root))
            tokens = ENGINE_TOKENS + ACCOUNT_DOMAIN_TOKENS
            if contract:
                tokens += ACCOUNT_CONTRACT_TOKENS
            for token in tokens:
                if re.search(r"(?<![\w])" + re.escape(token) + r"(?![\w])", code):
                    violations.append(relative + ":" + token)
            if ACCOUNT_CONCEPT_PATTERN.search(code):
                violations.append(relative + ":cross-domain-account-concept")
            if not contract:
                continue
            if re.search(r"\bDB[A-Z]\w*\b", code):
                violations.append(relative + ":database-row-type")
            if ACCOUNT_GM_MUTATION_PATTERN.search(code):
                violations.append(relative + ":blocked-gm-mutation")
            if ACCOUNT_GENERIC_PATTERN.search(code):
                violations.append(relative + ":generic-or-untyped-persistence-api")
            values = extract_csharp_strings(text)
            literal_candidates = []
            for start in range(len(values)):
                for width in range(1, min(6, len(values) - start) + 1):
                    pieces = values[start:start + width]
                    literal_candidates.extend((" ".join(pieces), "".join(pieces)))
            if any(ACCOUNT_STORAGE_LITERAL_PATTERN.search(value) for value in literal_candidates):
                violations.append(relative + ":storage-name-literal")
            # Inspect both separated and concatenated pieces, including SQL on
            # arbitrary tables. Table-name filters would miss this contract leak.
            if any(pattern.search(value) for value in literal_candidates for pattern in SQL_PATTERNS):
                violations.append(relative + ":embedded-sql")
    return sorted(set(violations))


def character_persistence_violations(root: Path) -> list[str]:
    """Bounded lexical checks, not a semantic C# analyzer or global migration gate."""
    violations = []
    for relative_root in CHARACTER_PERSISTENCE_ROOTS:
        directory = root / relative_root
        sources = sorted(path for path in directory.rglob("*.cs")
                         if not set(path.relative_to(directory).parts) & EXCLUDED_PARTS)
        if not sources:
            violations.append(relative_root + ":missing-persistence-sources")
        contract = "/AORebirth.Interfaces/" in relative_root
        for path in sources:
            text = path.read_text(encoding="utf-8-sig")
            code = re.sub(r"\s*\.\s*", ".", code_only(text))
            relative = normalize(path.relative_to(root))
            # Playfield is an approved scalar projection property, not permission
            # to expose the engine's Playfield class. Qualified names remain
            # covered by the engine namespace rules; bare type uses are below.
            tokens = tuple(token for token in ENGINE_TOKENS if token != "Playfield") + CHARACTER_RUNTIME_TOKENS
            if contract:
                tokens += CHARACTER_CONTRACT_TOKENS
            for token in tokens:
                if re.search(r"(?<![\w])" + re.escape(token) + r"(?![\w])", code):
                    violations.append(relative + ":" + token)
            if re.search(r"\bPlayfield\s*(?:<|>|\[|\?|,|\)|\s+[A-Za-z_]\w*)", code):
                violations.append(relative + ":Playfield-runtime-type")
            if not contract:
                continue
            if re.search(r"\bDB[A-Z]\w*\b", code):
                violations.append(relative + ":database-row-type")
            if CHARACTER_EXCLUDED_PATTERN.search(code):
                violations.append(relative + ":excluded-character-aggregate")
            if re.search(r"\b(?:float|double|decimal)\s+(?:X|Y|Z)\s*\{", code):
                violations.append(relative + ":excluded-character-aggregate")
            if ACCOUNT_GENERIC_PATTERN.search(code) or re.search(
                    r"\b(?:Get|Update|Insert|Remove|Create)\s*(?:<|\()", code):
                violations.append(relative + ":generic-or-untyped-persistence-api")
            values = extract_csharp_strings(text)
            candidates = []
            for start in range(len(values)):
                for width in range(1, min(6, len(values) - start) + 1):
                    pieces = values[start:start + width]
                    candidates.extend((" ".join(pieces), "".join(pieces)))
            if any(CHARACTER_STORAGE_LITERAL_PATTERN.search(value) for value in candidates):
                violations.append(relative + ":storage-name-literal")
            if any(pattern.search(value) for value in candidates for pattern in SQL_PATTERNS):
                violations.append(relative + ":embedded-sql")

    # Do not invent a repository-wide runtime fail mode. The existing governed
    # mission-domain folders are the only runtime roots extended in this scope.
    for path in mission_boundary_files(root):
        relative = normalize(path.relative_to(root))
        code = re.sub(r"\s*\.\s*", ".", code_only(path.read_text(encoding="utf-8-sig")))
        for token in ("MySqlCharacterDao", "MySqlAccountDao", "DatabaseDaoFactory"):
            if re.search(r"(?<![\w])" + re.escape(token) + r"(?![\w])", code):
                violations.append(relative + ":runtime-construction:" + token)
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


def account_self_test() -> int:
    checks = 0
    with tempfile.TemporaryDirectory(prefix="aorebirth-account-dao-guard-") as temporary:
        root = Path(temporary)
        contract = root / ACCOUNT_PERSISTENCE_ROOTS[0] / "IAccountDao.cs"
        implementation = root / ACCOUNT_PERSISTENCE_ROOTS[1] / "MySqlAccountDao.cs"
        good_contract = '''namespace AORebirth.Interfaces.Persistence.Accounts {
            public interface IAccountDao {
                GameAccountData LoadByUsername(string username);
                int ChangePassword(string username, string passwordHash);
            }
            public sealed class GameAccountData {
                public int GmLevel { get; set; }
                public string PasswordHash { get; set; }
            }
            // LogoffChars, DBLoginData, Connector and token ownership are excluded.
        }'''
        good_implementation = '''using System.Data;
            using Dapper; using MySqlConnector;
            class MySqlAccountDao {
                const string Sql = "SELECT GM FROM login WHERE Username=@Username";
                // ZoneEngine_New, CharacterDao and LogoffChars are not dependencies.
            }'''
        write(contract, good_contract)
        write(implementation, good_implementation)
        write(root / ACCOUNT_PERSISTENCE_ROOTS[0] / "bin/Ignored.cs", "class Bad { IDbConnection db; }")
        write(root / "AORebirth/Server/ZoneEngine_New/UnchangedLegacy.cs", "class Bad { IDbConnection db; }")
        if account_persistence_violations(root):
            raise RuntimeError("neutral account contract or internal provider fixture was rejected")
        checks += 1

        contract_cases = (
            ("using Data = System.Data; interface Bad { Data.IDbConnection Get(); }", "System.Data"),
            ("interface Bad { global::System . Data . IDbCommand Get(); }", "System.Data"),
            ("interface Bad { System/*comment*/.Data.IDbTransaction Get(); }", "System.Data"),
            ("using Provider = MySqlConnector; interface Bad {}", "MySqlConnector"),
            ("interface Bad { global::Dapper.SqlMapper.GridReader Get(); }", "Dapper"),
            ("interface Bad { System.Data.Common.DbProviderFactory Get(); }", "DbProviderFactory"),
            ("interface Bad { AORebirth.Database.Dao.DBLoginData Get(); }", "DBLoginData"),
            ("class Bad { Connector connection; }", "Connector"),
            ("interface Bad { ZoneEngine_New.Core.Entities.Player Get(); }", "ZoneEngine_New"),
            ("interface Bad { AORebirth.Stats.CharacterStats Get(); }", "AORebirth.Stats"),
            ("interface Bad { global::AORebirth . Core . Character Get(); }", "AORebirth.Core"),
            ('class Bad { const string QueryText = "SELECT value FROM arbitrary_table"; }', "embedded-sql"),
            ('class Bad { const string QueryText = "SEL" + "ECT value FR" + "OM arbitrary_table"; }', "embedded-sql"),
            ('class Bad { const string QueryText = @"UPDATE arbitrary_table\nSET value=1"; }', "embedded-sql"),
            ('class Bad { const string Table = "login"; }', "storage-name-literal"),
            ('class Bad { const string Table = "lo" + "gin"; }', "storage-name-literal"),
            ('class Bad { const string Column = "Username"; }', "storage-name-literal"),
            ('class Bad { const string Column = "User" + "name"; }', "storage-name-literal"),
            ('class Bad { const string Column = "`CreationDate`"; }', "storage-name-literal"),
            ('class Bad { const string Table = "account_password_reset_tokens"; }', "storage-name-literal"),
            ("interface Bad { IAccountIdentityDao GetIdentity(); }", "cross-domain-account-concept"),
            ("interface Bad { void IssuePasswordResetToken(string username); }", "cross-domain-account-concept"),
            ("interface Bad { void VerifyEmailVerification(string username); }", "cross-domain-account-concept"),
            ("interface Bad { void StartProvisioningJob(string username); }", "cross-domain-account-concept"),
            ("interface Bad { void LogoffChars(string username); }", "cross-domain-account-concept"),
            ("interface Bad { void MarkAllCharactersOffline(string username); }", "cross-domain-account-concept"),
            ("interface Bad { void SetOnline(string username); }", "cross-domain-account-concept"),
            ("interface Bad { void SetGmLevel(string username, int value); }", "blocked-gm-mutation"),
            ("interface Bad { void UpdateAllGM(int value); }", "blocked-gm-mutation"),
            ("interface Bad { void GrantGameMaster(string username); }", "blocked-gm-mutation"),
            ("interface Bad { void AssignGmLevel(string username, int value); }", "blocked-gm-mutation"),
            ("interface Bad { void ApplyGM<T>(T values); }", "blocked-gm-mutation"),
            ("interface Bad { IGenericRepository<int> Get(); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { GenericDao<int> Get(); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { T GetAll<T>(); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { int GetWhere(string username); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { void Save<T>(T entity); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { void Read(string tableName); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { void Read(object parameters); }", "generic-or-untyped-persistence-api"),
        )
        for bad_source, expected in contract_cases:
            write(contract, bad_source)
            violations = account_persistence_violations(root)
            if not any(value.endswith(":" + expected) for value in violations):
                raise RuntimeError("account contract fixture was not rejected as " + expected + ": " + bad_source)
            checks += 1
        write(contract, good_contract)

        for token in (
            "ZoneEngine_New.Core.Entities.Player", "ZoneEngine.Core.Character",
            "global::LoginEngine . Packets . Login", "ChatEngine.Client",
            "WebEngine.Server", "AORebirth.Stats.CharacterStats", "AORebirth.Core.Character",
            "SmokeLounge.AOtomation.Messaging.Packet", "AORebirth.AccountBroker.AccountBrokerService",
            "AORebirth.BotService.BotPersistence", "CharacterDao", "DBCharacter", "LoginDataDao",
        ):
            write(implementation, "class Bad { " + token + " field; }")
            if not account_persistence_violations(root):
                raise RuntimeError("account implementation dependency fixture was not rejected: " + token)
            checks += 1
        write(implementation, good_implementation)
        if account_persistence_violations(root):
            raise RuntimeError("account contract fixture reset did not pass")
        checks += 1

        # Each directory is independently required; a missing surface must not
        # yield a vacuous pass. These are temporary fixtures only.
        contract.unlink()
        if not any(value.endswith(":missing-persistence-sources") for value in account_persistence_violations(root)):
            raise RuntimeError("missing account contracts did not fail closed")
        checks += 1
        write(contract, good_contract)
        implementation.unlink()
        if not any(value.endswith(":missing-persistence-sources") for value in account_persistence_violations(root)):
            raise RuntimeError("missing account implementation did not fail closed")
        checks += 1
    return checks


def character_self_test() -> int:
    checks = 0
    with tempfile.TemporaryDirectory(prefix="aorebirth-character-dao-guard-") as temporary:
        root = Path(temporary)
        contract = root / CHARACTER_PERSISTENCE_ROOTS[0] / "ICharacterDao.cs"
        implementation = root / CHARACTER_PERSISTENCE_ROOTS[1] / "MySqlCharacterDao.cs"
        good_contract = '''namespace AORebirth.Interfaces.Persistence.Characters {
            public interface ICharacterDao {
                CharacterDirectoryData LoadById(int characterId);
                int MarkOnline(int characterId);
                StaleOnlineRecoveryResult RecoverStaleOnline(string expectedDatabase);
            }
            public sealed class CharacterDirectoryData {
                public int CharacterId { get; set; }
                public string AccountUsername { get; set; }
                public string Name { get; set; }
                public string FirstName { get; set; }
                public string LastName { get; set; }
                public int Playfield { get; set; }
                public int? Online { get; set; }
            }
            // DBCharacter, inventory, BuddyList and CharacterOnlineOwnershipGuard remain outside.
        }'''
        good_implementation = '''using System.Data; using Dapper; using MySqlConnector; using System.IO;
            class MySqlCharacterDao {
                const string Sql = "SELECT Id, Online FROM characters WHERE Online<>0 FOR UPDATE";
                private InvalidDataException InvalidResult() { return new InvalidDataException("Invalid result"); }
                // No ZoneEngine, CharacterOnlineOwnershipGuard or file-lock policy here.
            }'''
        write(contract, good_contract)
        write(implementation, good_implementation)
        write(root / CHARACTER_PERSISTENCE_ROOTS[0] / "obj/Ignored.cs", "interface Bad { IDbConnection Get(); }")
        write(root / "AORebirth/Server/ZoneEngine_New/Program.cs", "class Program { MySqlCharacterDao dao; }")
        write(root / "AORebirth/Server/ZoneEngine_New/Core/Data/Legacy.cs", 'class Legacy { string Sql = "SELECT Id FROM characters"; }')
        if character_persistence_violations(root):
            raise RuntimeError("valid character projection, provider implementation or out-of-scope fixture rejected")
        checks += 1
        cases = (
            ("using Ado = System.Data; interface Bad {}", "System.Data"),
            ("interface Bad { global::System . Data . IDbConnection Get(); }", "System.Data"),
            ("interface Bad { System/*comment*/.Data.IDbCommand Get(); }", "System.Data"),
            ("interface Bad { IDataReader Read(); }", "IDataReader"),
            ("interface Bad { IDbTransaction Begin(); }", "IDbTransaction"),
            ("interface Bad { Dapper.SqlMapper.GridReader Read(); }", "Dapper"),
            ("using Provider = MySqlConnector; interface Bad {}", "MySqlConnector"),
            ("interface Bad { NpgsqlConnection Read(); }", "NpgsqlConnection"),
            ("interface Bad { SqlCommand Read(); }", "SqlCommand"),
            ("interface Bad { DbProviderFactory Read(); }", "DbProviderFactory"),
            ("class Bad { Connector connection; }", "Connector"),
            ("interface Bad { AORebirth.Database.Entities.DBCharacter Read(); }", "DBCharacter"),
            ("interface Bad { ZoneEngine_New.Core.Player Read(); }", "ZoneEngine_New"),
            ("interface Bad { Playfield Read(); }", "Playfield-runtime-type"),
            ("interface Bad { IList<Playfield> Read(); }", "Playfield-runtime-type"),
            ("interface Bad { DatabaseDaoFactory Read(); }", "DatabaseDaoFactory"),
            ('class Bad { const string Value = "SELECT x FROM arbitrary_table"; }', "embedded-sql"),
            ('class Bad { const string Value = "SEL" + "ECT x FR" + "OM arbitrary_table"; }', "embedded-sql"),
            ('class Bad { const string Value = @"UPDATE arbitrary_table\nSET x=1"; }', "embedded-sql"),
            ('class Bad { const string Value = "char" + "acters"; }', "storage-name-literal"),
            ('class Bad { const string Value = "On" + "line"; }', "storage-name-literal"),
            ("interface Bad { IGenericRepository<int> Read(); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { T GetAll<T>(); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { T Get<T>(); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { void Update<T>(T row); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { void Save<T>(T row); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { void Add(int id); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { void Read(object query); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { void Read(string tableName); }", "generic-or-untyped-persistence-api"),
            ("interface Bad { IAccountDao Read(); }", "IAccountDao"),
            ("interface Bad { void LoadInventory(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void LoadStats(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void LoadActiveNanos(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void LoadPerks(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void LoadOrganization(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void LoadMission(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void AddBuddy(int id, int buddy); }", "excluded-character-aggregate"),
            ("class Bad { public string BuddyList { get; set; } }", "excluded-character-aggregate"),
            ("interface Bad { void DeleteCharacter(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void DeleteOwnedData(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void SaveProfile(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void CreateCharacter(int id); }", "excluded-character-aggregate"),
            ("interface Bad { void SetPlayfield(int id, int playfield); }", "excluded-character-aggregate"),
            ("class Bad { public float HeadingX { get; set; } }", "excluded-character-aggregate"),
            ("class Bad { public float X { get; set; } }", "excluded-character-aggregate"),
            ("class Bad { public int Textures0 { get; set; } }", "excluded-character-aggregate"),
            ("interface Bad { void ChangePassword(string hash); }", "excluded-character-aggregate"),
        )
        for bad_source, expected in cases:
            write(contract, bad_source)
            if not any(value.endswith(":" + expected) for value in character_persistence_violations(root)):
                raise RuntimeError("character contract fixture was not rejected as " + expected + ": " + bad_source)
            checks += 1
        write(contract, good_contract)
        for token in (
            "ZoneEngine.Core.Character", "global::ZoneEngine_New . Core . Player", "LoginEngine.Client",
            "ChatEngine.Client", "WebEngine.Server", "AORebirth.Core.Character", "AORebirth.Stats.Stats",
            "SmokeLounge.AOtomation.Messaging.Packet", "CharacterOnlineOwnershipGuard",
            "System.IO.FileStream", "System.IO.File", "System.IO.Directory", "System.Threading.Thread", "System.Diagnostics.Process",
            "AORebirth.AccountBroker.AccountBrokerService", "CharacterDao", "LoginDataDao",
        ):
            write(implementation, "class Bad { " + token + " value; }")
            if not character_persistence_violations(root):
                raise RuntimeError("character implementation fixture was not rejected: " + token)
            checks += 1
        write(implementation, good_implementation)
        for engine in ("ZoneEngine", "ZoneEngine_New"):
            runtime = root / ("AORebirth/Server/" + engine + "/Core/Missions/Nested/Adapter.cs")
            for token in ("MySqlCharacterDao", "MySqlAccountDao", "DatabaseDaoFactory"):
                write(runtime, "class Bad { global::AORebirth.Database." + token + " value; }")
                if not any(":runtime-construction:" + token in value for value in character_persistence_violations(root)):
                    raise RuntimeError("scoped runtime construction was not rejected: " + token)
                checks += 1
            write(runtime, '// MySqlCharacterDao DatabaseDaoFactory\nclass Good { string Note="MySqlAccountDao"; }')
        if character_persistence_violations(root):
            raise RuntimeError("character comment/string fixture was rejected")
        checks += 1
        contract.unlink()
        if not any(value.endswith(":missing-persistence-sources") for value in character_persistence_violations(root)):
            raise RuntimeError("missing character contract did not fail closed")
        checks += 1
        write(contract, good_contract)
        implementation.unlink()
        if not any(value.endswith(":missing-persistence-sources") for value in character_persistence_violations(root)):
            raise RuntimeError("missing character implementation did not fail closed")
        checks += 1
    return checks


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--account-self-test", action="store_true")
    parser.add_argument("--character-self-test", action="store_true")
    scope = parser.add_mutually_exclusive_group()
    scope.add_argument("--mission-persistence-only", action="store_true")
    scope.add_argument("--account-persistence-only", action="store_true")
    scope.add_argument("--character-persistence-only", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        print("DAO_ARCHITECTURE_GUARD_SELF_TEST=PASS")
        return 0

    if args.account_self_test:
        count = account_self_test()
        print("ACCOUNT_PERSISTENCE_GUARD_SELF_TEST=PASS")
        print("ACCOUNT_PERSISTENCE_GUARD_SELF_TEST_CHECKS=" + str(count))
        return 0

    if args.character_self_test:
        count = character_self_test()
        print("CHARACTER_PERSISTENCE_GUARD_SELF_TEST=PASS")
        print("CHARACTER_PERSISTENCE_GUARD_SELF_TEST_CHECKS=" + str(count))
        return 0

    if args.root is None:
        parser.error("--root is required unless --self-test is used")
    root = args.root.resolve()
    manifest = (args.manifest or Path(__file__).with_name("known-violations.json")).resolve()
    if args.character_persistence_only:
        try:
            character = character_persistence_violations(root)
            account = account_persistence_violations(root)
            mission = mission_boundary_violations(root) + mission_persistence_violations(root)
        except (OSError, ValueError) as error:
            print("DAO_GUARD=FAIL")
            print("DAO_GUARD_SCOPE=CHARACTER_ACCOUNT_AND_MISSION")
            print("ERROR=" + str(error))
            return 1
        for domain, boundary in (("CHARACTER", character), ("ACCOUNT", account), ("MISSION", mission)):
            for value in boundary:
                print(domain + "_BOUNDARY_VIOLATION=" + value)
            print(domain + "_PERSISTENCE_GUARD=" + ("FAIL" if boundary else "PASS"))
            print(domain + "_BOUNDARY_VIOLATIONS=" + str(len(boundary)))
        passed = not character and not account and not mission
        print("DAO_GUARD=" + ("PASS" if passed else "FAIL"))
        print("DAO_GUARD_SCOPE=CHARACTER_ACCOUNT_AND_MISSION")
        return 0 if passed else 1
    if args.account_persistence_only:
        try:
            boundary = account_persistence_violations(root)
        except (OSError, ValueError) as error:
            print("ACCOUNT_PERSISTENCE_GUARD=FAIL")
            print("ERROR=" + str(error))
            return 1
        for value in boundary:
            print("ACCOUNT_BOUNDARY_VIOLATION=" + value)
        print("ACCOUNT_PERSISTENCE_GUARD=" + ("FAIL" if boundary else "PASS"))
        print("ACCOUNT_BOUNDARY_VIOLATIONS=" + str(len(boundary)))
        return 1 if boundary else 0
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
