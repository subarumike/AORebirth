#!/usr/bin/env python3
"""Generate deterministic offline evidence from the retained Malis corpus."""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import tempfile
import zipfile


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
REFERENCE_ROOT = REPOSITORY_ROOT / "docs" / "reference" / "missions" / "malis"
GENERATED_ROOT = REPOSITORY_ROOT / "docs" / "generated" / "missions" / "malis"
SOURCE_MANIFEST = REFERENCE_ROOT / "source-manifest.json"
MALIS_COMMIT = "3ac9943a4943b8cb80eda9e40359729e656686b0"
LEVEL_80_COMMIT = "e19bb1ddc25e2647688c7996c8b09d50198fc486"
QL200_COMMIT = "7e5b921cebabee99051252a4883f324b38a519fc"
SLIDER_CENTER_COMMIT = "fb7ea4b7933f1b804eb924c5ba3a83996afe1f1a"
AOSHARP_COMMIT = "b45b7a05f9ffd9676d37e620f2f7d481b82ed212"
GENERATOR_VERSION = 1
ITEM_FILES = (
    ("implants", "JSON/ItemDB_Implants.json", "ItemDB_Implants.json"),
    ("refined", "JSON/ItemDb_Refined.json", "ItemDb_Refined.json"),
    ("clusters", "JSON/ItemDb_Clusters.json", "ItemDb_Clusters.json"),
    ("nanos", "JSON/ItemDb_Nanos.json", "ItemDb_Nanos.json"),
    ("rest", "JSON/ItemDb_Rest.json", "ItemDb_Rest.json"),
)
PSEUDO_REWARD_ID = 297315


class EvidenceError(ValueError):
    pass


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")


def load_json_bytes(data: bytes, label: str) -> object:
    try:
        return json.loads(data.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise EvidenceError(f"Malformed JSON in {label}: {error}") from error


class ZipTree:
    def __init__(
        self,
        path: Path,
        expected_prefix: str | None = None,
        allow_root_files: bool = False,
    ):
        self.path = path
        self._members: dict[str, zipfile.ZipInfo] = {}
        with zipfile.ZipFile(path) as archive:
            files = [member for member in archive.infolist() if not member.is_dir()]
            if not files:
                raise EvidenceError(f"Archive has no files: {path}")
            first_parts = {PurePosixPath(member.filename).parts[0] for member in files}
            if expected_prefix is None:
                if len(first_parts) != 1:
                    raise EvidenceError(f"Archive lacks one canonical root: {path}")
                expected_prefix = next(iter(first_parts))
            for member in files:
                parts = PurePosixPath(member.filename).parts
                if ".." in parts:
                    raise EvidenceError(f"Unsafe archive member: {member.filename}")
                if parts[0] == expected_prefix and len(parts) >= 2:
                    relative = PurePosixPath(*parts[1:]).as_posix()
                elif allow_root_files and len(parts) == 1:
                    relative = parts[0]
                else:
                    raise EvidenceError(f"Unexpected archive member root: {member.filename}")
                if relative in self._members:
                    raise EvidenceError(f"Duplicate archive member: {relative}")
                self._members[relative] = member

    def names(self) -> list[str]:
        return sorted(self._members)

    def read(self, relative: str) -> bytes:
        if relative not in self._members:
            raise EvidenceError(f"Required archive member is missing: {relative}")
        with zipfile.ZipFile(self.path) as archive:
            return archive.read(self._members[relative])

    def size(self, relative: str) -> int:
        if relative not in self._members:
            raise EvidenceError(f"Required archive member is missing: {relative}")
        return self._members[relative].file_size


def verify_source_manifest() -> dict[str, object]:
    manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    acquisition = manifest.get("Acquisition", {})
    if acquisition.get("MalisCommit") != MALIS_COMMIT:
        raise EvidenceError("Malis source manifest commit is not governed.")
    if acquisition.get("ReleaseBinariesExecuted") is not False:
        raise EvidenceError("Release execution safety boundary is missing.")
    for artifact in manifest.get("Artifacts", []):
        path = REFERENCE_ROOT / str(artifact["RelativePath"])
        if not path.is_file():
            raise EvidenceError(f"Retained artifact is missing: {path}")
        if path.stat().st_size != int(artifact["ByteLength"]):
            raise EvidenceError(f"Retained artifact length mismatch: {path}")
        if sha256_file(path) != artifact["Sha256"]:
            raise EvidenceError(f"Retained artifact hash mismatch: {path}")
    return manifest


def artifact_path(manifest: dict[str, object], role: str) -> Path:
    matches = [item for item in manifest["Artifacts"] if item["Role"] == role]
    if len(matches) != 1:
        raise EvidenceError(f"Expected exactly one retained artifact with role {role}.")
    return REFERENCE_ROOT / str(matches[0]["RelativePath"])


def classify_source_file(name: str) -> str:
    suffix = PurePosixPath(name).suffix.lower()
    if name.startswith("JSON/"):
        return "STATIC_JSON_DATA"
    if suffix == ".cs":
        return "CSHARP_SOURCE"
    if suffix == ".xml":
        return "UI_LAYOUT_OR_CONFIG"
    if suffix == ".png":
        return "UI_TEXTURE"
    if suffix == ".wav":
        return "SOUND_ASSET"
    if suffix in {".csproj", ".sln", ".config"} or name == "packages.config":
        return "BUILD_OR_DEPENDENCY_METADATA"
    return "REPOSITORY_METADATA"


def validate_item_rows(value: object, label: str) -> list[dict[str, object]]:
    if not isinstance(value, list):
        raise EvidenceError(f"{label} must be a JSON array.")
    validated = []
    for index, row in enumerate(value):
        if not isinstance(row, dict) or set(row) != {"Key", "Value"}:
            raise EvidenceError(f"{label}[{index}] has a non-canonical record shape.")
        key = row["Key"]
        stats = row["Value"]
        if not isinstance(key, dict) or set(key) != {"LowId", "HighId", "LowQl", "HighQl", "Tags", "Name"}:
            raise EvidenceError(f"{label}[{index}].Key has a non-canonical shape.")
        if any(not isinstance(key[field], int) or isinstance(key[field], bool) for field in ("LowId", "HighId", "LowQl", "HighQl")):
            raise EvidenceError(f"{label}[{index}] contains a non-integer item field.")
        if key["LowId"] <= 0 or key["HighId"] <= 0 or key["LowQl"] <= 0 or key["HighQl"] <= 0:
            raise EvidenceError(f"{label}[{index}] contains a non-positive item field.")
        if key["LowQl"] > key["HighQl"]:
            raise EvidenceError(f"{label}[{index}] has a reversed QL interval.")
        if not isinstance(key["Name"], str) or not isinstance(key["Tags"], list) or any(not isinstance(tag, str) for tag in key["Tags"]):
            raise EvidenceError(f"{label}[{index}] contains malformed text fields.")
        if not isinstance(stats, list) or any(not isinstance(stat, int) or isinstance(stat, bool) for stat in stats):
            raise EvidenceError(f"{label}[{index}] contains malformed stat tags.")
        validated.append(row)
    return validated


def duplicate_summary(records: list[dict[str, object]]) -> dict[str, object]:
    pairs: dict[tuple[int, int], list[dict[str, object]]] = {}
    fingerprints: dict[str, int] = {}
    for record in records:
        key = record["Key"]
        pair = (int(key["LowId"]), int(key["HighId"]))
        pairs.setdefault(pair, []).append(record)
        fingerprint = json.dumps(record, sort_keys=True, separators=(",", ":"))
        fingerprints[fingerprint] = fingerprints.get(fingerprint, 0) + 1
    conflicts = []
    for pair, rows in sorted(pairs.items()):
        canonical = {json.dumps(row, sort_keys=True, separators=(",", ":")) for row in rows}
        if len(canonical) > 1:
            conflicts.append({"LowId": pair[0], "HighId": pair[1], "RecordCount": len(rows), "DistinctRecords": len(canonical)})
    return {
        "ConflictingTemplatePairs": conflicts,
        "ExactDuplicateRecordExcess": sum(count - 1 for count in fingerprints.values() if count > 1),
        "RepeatedTemplatePairExcess": sum(len(rows) - 1 for rows in pairs.values() if len(rows) > 1),
        "UniqueTemplatePairs": len(pairs),
    }


def parse_mission_levels(value: object) -> list[list[int]]:
    if not isinstance(value, list) or len(value) != 220:
        raise EvidenceError("Malis MissionLevels.json must contain exactly 220 character-level rows.")
    rows: list[list[int]] = []
    for level, row in enumerate(value, start=1):
        if not isinstance(row, list) or not 1 <= len(row) <= 11:
            raise EvidenceError(f"Malis mission-level row {level} has an invalid shape.")
        if any(not isinstance(cell, int) or isinstance(cell, bool) or cell < 1 or cell > 250 for cell in row):
            raise EvidenceError(f"Malis mission-level row {level} contains an invalid QL.")
        if any(row[index] > row[index + 1] for index in range(len(row) - 1)):
            raise EvidenceError(f"Malis mission-level row {level} decreases.")
        rows.append(row)
    return rows


def parse_playfield_ids(source: str) -> list[int]:
    match = re.search(r"_pfIds\s*=\s*new int\[\]\s*\{([^}]*)\}", source)
    if match is None:
        raise EvidenceError("Malis playfield ID array was not found.")
    values = [int(token) for token in re.findall(r"\d+", match.group(1))]
    if len(values) != len(set(values)):
        raise EvidenceError("Malis playfield ID array contains duplicates.")
    return values


def parse_mission_icon_filters(source: str) -> list[tuple[str, int]]:
    matches = re.findall(
        r"SettingsView\.MissionTypes\.(\w+)\.Tag\s*&&\s*missionInfo\.MissionIcon\s*==\s*(\d+)",
        source,
    )
    if len(matches) != 5:
        raise EvidenceError("Expected exactly five Malis mission-icon filters.")
    return [(name, int(icon)) for name, icon in matches]


def parse_aosharp_members(source: str) -> list[dict[str, object]]:
    pattern = re.compile(
        r"\[AoMember\((\d+)([^\]]*)\)\]\s*public\s+([A-Za-z0-9_<>\[\].]+)\s+([A-Za-z0-9_]+)\s*\{\s*get;\s*set;\s*\}",
        re.MULTILINE,
    )
    rows = []
    for ordinal, options, data_type, name in pattern.findall(source):
        fixed = re.search(r"FixedSizeLength\s*=\s*(\d+)", options)
        rows.append(
            {
                "AoMemberOrdinal": int(ordinal),
                "DataType": data_type,
                "FixedSizeLength": int(fixed.group(1)) if fixed else None,
                "Property": name,
            }
        )
    return rows


def load_aorebirth_level_rows() -> list[list[int]]:
    path = REPOSITORY_ROOT / "AORebirth" / "Server" / "ZoneEngine" / "XML Data" / "MissionLevels.csv"
    with path.open("r", encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream)
        rows = [[int(row[f"Q{index}"]) for index in range(11)] for row in reader]
    if len(rows) != 220:
        raise EvidenceError("AORebirth canonical mission-level table is incomplete.")
    return rows


def normalized_name(value: str) -> str:
    return " ".join(value.casefold().split())


def build_outputs(output_root: Path) -> None:
    manifest = verify_source_manifest()
    source_zip = artifact_path(manifest, "MALIS_EXACT_SOURCE_TREE")
    release_zip = artifact_path(manifest, "MALIS_PUBLIC_TOOLKIT_RELEASE")
    aosharp_zip = artifact_path(manifest, "AOSHARP_PUBLIC_SOURCE_CORRELATION")
    nupkg_path = artifact_path(manifest, "AOSHARP_SDK_EXACT_PACKAGE")
    source = ZipTree(source_zip, f"malis-{MALIS_COMMIT[:8]}")
    release = ZipTree(release_zip, "Mali's AO Toolkit", allow_root_files=True)
    aosharp = ZipTree(aosharp_zip, f"aosharp-{AOSHARP_COMMIT[:8]}")

    source_inventory = []
    for name in source.names():
        data = source.read(name)
        source_inventory.append(
            {
                "ByteLength": len(data),
                "Path": name,
                "Role": classify_source_file(name),
                "Sha256": sha256_bytes(data),
            }
        )
    write_json(output_root / "source-file-inventory.json", source_inventory)

    item_records: list[dict[str, object]] = []
    dataset_inventory: list[dict[str, object]] = []
    item_dataset_counts: dict[str, int] = {}
    for category, source_name, _ in ITEM_FILES:
        data = source.read(source_name)
        rows = validate_item_rows(load_json_bytes(data, source_name), source_name)
        for row in rows:
            item_records.append({"Category": category, **row})
        item_dataset_counts[category] = len(rows)
        dataset_inventory.append(
            {
                "AppearsGeneratedOrMaintained": "STATIC_EXPORT_WITH_MANUAL_HISTORY_AMENDMENTS",
                "ByteLength": len(data),
                "Dataset": category,
                "Path": source_name,
                "Purpose": "SEARCHABLE_ITEM_TEMPLATE_CATALOG_NOT_ROLLABILITY_DATA",
                "RecordCount": len(rows),
                "RuntimeConsumer": "Main.Run -> Extensions.FormatItemDb -> ItemDisplayView search and roll-list entry creation",
                "Schema": "Array<KeyValuePair<ItemInfo,List<Stat>>>; Key={LowId,HighId,LowQl,HighQl,Tags,Name}",
                "Sha256": sha256_bytes(data),
                "SourceProvenance": "UNKNOWN; commit history documents manual additions and corrections but names no upstream dataset",
            }
        )

    levels_data = source.read("JSON/MissionLevels.json")
    levels = parse_mission_levels(load_json_bytes(levels_data, "JSON/MissionLevels.json"))
    level_anomalies = [
        {"CharacterLevel": level, "PresentDifficultySlots": len(row), "MissingDifficultySlots": list(range(len(row) + 1, 12))}
        for level, row in enumerate(levels, start=1)
        if len(row) != 11
    ]
    dataset_inventory.append(
        {
            "AppearsGeneratedOrMaintained": "MANUALLY_MAINTAINED_LOOKUP_TABLE",
            "ByteLength": len(levels_data),
            "Dataset": "mission_levels",
            "Path": "JSON/MissionLevels.json",
            "Purpose": "CLIENT_SIDE_CHARACTER_LEVEL_AND_DIFFICULTY_TO_REQUESTED_MISSION_QL",
            "RecordCount": len(levels),
            "RuntimeConsumer": "MainWindow and RollEntryProcessor",
            "Schema": "220 arrays intended to contain 11 ordered mission QLs; 13 rows are short in current source",
            "Sha256": sha256_bytes(levels_data),
            "SourceProvenance": "UNKNOWN; source history contains five point corrections without an upstream citation",
        }
    )

    mod_tags_data = source.read("JSON/ModTags.json")
    mod_tags = load_json_bytes(mod_tags_data, "JSON/ModTags.json")
    if not isinstance(mod_tags, dict) or any(not isinstance(value, list) for value in mod_tags.values()):
        raise EvidenceError("Malis ModTags.json has an invalid schema.")
    dataset_inventory.append(
        {
            "AppearsGeneratedOrMaintained": "MANUALLY_MAINTAINED_SEARCH_SYNONYMS",
            "ByteLength": len(mod_tags_data),
            "Dataset": "mod_tags",
            "Path": "JSON/ModTags.json",
            "Purpose": "ITEM_BROWSER_STAT_AND_PROFESSION_TEXT_FILTERING",
            "RecordCount": len(mod_tags),
            "RuntimeConsumer": "ItemDisplayView.SearchQuery",
            "Schema": "Object<StatOrNumericKey,string[]>",
            "Sha256": sha256_bytes(mod_tags_data),
            "SourceProvenance": "UNKNOWN",
        }
    )

    settings_data = source.read("JSON/Default_Settings.json")
    settings = load_json_bytes(settings_data, "JSON/Default_Settings.json")
    if not isinstance(settings, dict) or not isinstance(settings.get("Locations"), dict):
        raise EvidenceError("Malis default settings lack the location map.")
    dataset_inventory.append(
        {
            "AppearsGeneratedOrMaintained": "MANUALLY_MAINTAINED_DEFAULT_CONFIGURATION",
            "ByteLength": len(settings_data),
            "Dataset": "default_settings",
            "Path": "JSON/Default_Settings.json",
            "Purpose": "MISSION_TYPE_FILTERS_SLIDERS_DATABASE_SELECTION_LOCATION_ALLOWLIST_AND_UI_DEFAULTS",
            "RecordCount": len(settings),
            "RuntimeConsumer": "Main.Run and Settings/Views",
            "Schema": {key: len(value) if isinstance(value, dict) else type(value).__name__ for key, value in settings.items()},
            "Sha256": sha256_bytes(settings_data),
            "SourceProvenance": "MALIS_PROJECT_DEFAULTS; AO playfield names are resolved from the client at runtime",
        }
    )

    playfield_source = source.read("Views/PlayfieldView.cs").decode("utf-8-sig")
    playfield_ids = parse_playfield_ids(playfield_source)
    location_names = list(settings["Locations"])
    if len(playfield_ids) != len(location_names):
        raise EvidenceError("Malis playfield IDs and default location names do not align.")
    dataset_inventory.extend(
        [
            {
                "AppearsGeneratedOrMaintained": "HARDCODED_MANUAL_ARRAY",
                "Dataset": "mission_destination_playfield_allowlist",
                "Path": "Views/PlayfieldView.cs",
                "Purpose": "ENABLE_DISABLE_AND_OPTIONAL_XZ_BOUND_FILTERING",
                "RecordCount": len(playfield_ids),
                "RuntimeConsumer": "PlayfieldView and MainWindow.RollMatchCheck",
                "Schema": "int[] playfield IDs paired by order with Default_Settings.Locations",
                "Sha256": sha256_bytes(source.read("Views/PlayfieldView.cs")),
                "SourceProvenance": "UNKNOWN",
            },
            {
                "AppearsGeneratedOrMaintained": "HARDCODED_CLIENT_RESPONSE_CLASSIFICATION",
                "Dataset": "mission_type_icons",
                "Path": "MainWindow.cs",
                "Purpose": "POST_OFFER_MISSION_TYPE_FILTERING",
                "RecordCount": 5,
                "RuntimeConsumer": "MainWindow.RollMatchCheck",
                "Schema": "MissionIcon integer to UI filter",
                "Sha256": sha256_bytes(source.read("MainWindow.cs")),
                "SourceProvenance": "MALIS_SOURCE; independently bridged by capture-backed AORebirth MissionTypeCatalog",
            },
            {
                "AppearsGeneratedOrMaintained": "HARDCODED_UI_SHAPE",
                "Dataset": "five_offer_slots",
                "Path": "Views/MissionView.cs",
                "Purpose": "DISPLAY_AND_PROCESS_ONE_SERVER_RESPONSE_COHORT",
                "RecordCount": 5,
                "RuntimeConsumer": "MissionView and MainWindow.RollMatchCheck",
                "Schema": "Five ordered MissionInfo entries",
                "Sha256": sha256_bytes(source.read("Views/MissionView.cs")),
                "SourceProvenance": "MALIS_SOURCE_AND_AOSHARP_SERVER_RESPONSE_ARRAY",
            },
        ]
    )
    write_json(output_root / "static-dataset-inventory.json", dataset_inventory)

    ao_levels = load_aorebirth_level_rows()
    differences = []
    exact_cells = 0
    missing_cells = 0
    value_differences = 0
    exact_rows = 0
    for level, ao_row in enumerate(ao_levels, start=1):
        malis_row = levels[level - 1]
        row_exact = len(malis_row) == 11
        for index, ao_value in enumerate(ao_row):
            if index >= len(malis_row):
                missing_cells += 1
                row_exact = False
                differences.append(
                    {
                        "AORebirthQl": ao_value,
                        "CharacterLevel": level,
                        "DifficultyWireValue": index + 1,
                        "Kind": "MALIS_MISSING_SLOT",
                        "MalisQl": None,
                    }
                )
            elif malis_row[index] != ao_value:
                value_differences += 1
                row_exact = False
                differences.append(
                    {
                        "AORebirthQl": ao_value,
                        "CharacterLevel": level,
                        "DifficultyWireValue": index + 1,
                        "Kind": "VALUE_DIFFERENCE",
                        "MalisQl": malis_row[index],
                    }
                )
            else:
                exact_cells += 1
        if row_exact:
            exact_rows += 1

    ql200_mapping = []
    for level in range(201, 221):
        row = levels[level - 1]
        candidates = [(index + 1, quality) for index, quality in enumerate(row) if quality >= 200]
        if not candidates:
            raise EvidenceError(f"Malis >200 QL200 special case has no >=200 mission QL at level {level}.")
        difficulty, mission_ql = candidates[0]
        ql200_mapping.append({"CharacterLevel": level, "DifficultyWireValue": difficulty, "RequestedMissionQl": mission_ql})

    history = json.loads((REFERENCE_ROOT / "raw" / "malis-commit-history.json").read_text(encoding="utf-8"))
    history_by_sha = {entry["Sha"]: entry for entry in history}
    table_history = [
        {"After": 93, "Before": 92, "CharacterLevel": 52, "Commit": "6543610386e87a99243d12282a7ba474995d710d", "DifficultyWireValue": 11},
        {"After": 94, "Before": 95, "CharacterLevel": 53, "Commit": "6543610386e87a99243d12282a7ba474995d710d", "DifficultyWireValue": 11},
        {"After": 96, "Before": 97, "CharacterLevel": 54, "Commit": "6543610386e87a99243d12282a7ba474995d710d", "DifficultyWireValue": 11},
        {"After": 107, "Before": 108, "CharacterLevel": 60, "Commit": "ec4416970f5f38854e5a547a90ae44f40acc6b63", "DifficultyWireValue": 11},
        {"After": 143, "Before": 144, "CharacterLevel": 80, "Commit": LEVEL_80_COMMIT, "DifficultyWireValue": 11},
    ]
    for finding in table_history:
        finding["CommitDate"] = history_by_sha[finding["Commit"]]["CommitDate"]
        finding["Subject"] = history_by_sha[finding["Commit"]]["Subject"]
    mission_level_comparison = {
        "AORebirthCanonicalTable": "AORebirth/Server/ZoneEngine/XML Data/MissionLevels.csv",
        "CellSummary": {
            "AORebirthExpectedCells": 220 * 11,
            "ExactCells": exact_cells,
            "MalisPresentCells": sum(len(row) for row in levels),
            "MissingMalisCells": missing_cells,
            "ValueDifferences": value_differences,
        },
        "Differences": differences,
        "ExactRows": exact_rows,
        "HistoricalPointCorrections": table_history,
        "Level80Finding": {
            "AORebirthCanonicalValue": ao_levels[79][10],
            "CorrectedMalisValue": levels[79][10],
            "OldMalisValue": 144,
            "Conclusion": "The final difficulty slot changed 144 to 143; no issue, comment, formula, or upstream citation explains why.",
        },
        "MalisRowShapeAnomalies": level_anomalies,
        "Ql200Above200ClientSpecialCase": {
            "Architecture": "CLIENT_SIDE_SEARCH_AND_DIFFICULTY_SELECTION",
            "Mappings": ql200_mapping,
            "Rule": "For playerLevel > 200, itemQL == 200, and a non-nano name, choose the first table mission QL >= 200.",
            "ServerRuleProven": False,
        },
        "SliderMapping": {
            "AutoSelection": "IndexOf(selected mission QL) + 1; duplicate QLs choose the first difficulty slot",
            "DefaultDifficultyWireValue": 6,
            "DifficultyWireValues": "1..11, passed unchanged as a byte to AOSharp MissionTerminal.RequestMissions",
            "OtherSliderCentering": "GoodBad, OrderChaos, OpenHidden, PhysicalMystical, HeadonStealth, and CreditsXp map UI value 0 to signed -1 encoded as byte 255; difficulty is not remapped",
            "TableIndex": "difficulty wire value - 1",
        },
    }
    write_json(output_root / "mission-level-comparison.json", mission_level_comparison)
    with (output_root / "character-level-mission-ql.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(["character_level", *[f"difficulty_{index}" for index in range(1, 12)]])
        for level, row in enumerate(levels, start=1):
            writer.writerow([level, *row, *([""] * (11 - len(row)))])

    all_item_rows = [record for record in item_records if int(record["Key"]["LowId"]) != PSEUDO_REWARD_ID]
    duplicates = duplicate_summary(all_item_rows)
    malis_pairs = {(int(row["Key"]["LowId"]), int(row["Key"]["HighId"])) for row in all_item_rows}
    malis_endpoints = {value for pair in malis_pairs for value in pair}
    malis_names = {normalized_name(str(row["Key"]["Name"])) for row in all_item_rows}
    clicksaver_path = REPOSITORY_ROOT / "docs" / "generated" / "missions" / "arpa3" / "clicksaver-item-catalog.csv"
    with clicksaver_path.open("r", encoding="utf-8", newline="") as stream:
        clicksaver = list(csv.DictReader(stream))
    clicksaver_ids = {int(row["clicksaver_item_id"]) for row in clicksaver}
    endpoint_matches = 0
    pair_matches = 0
    interpolation_only = 0
    for row in clicksaver:
        item_id = int(row["clicksaver_item_id"])
        if item_id in malis_endpoints:
            endpoint_matches += 1
        if row["group_low_id"] and row["group_high_id"]:
            pair = (int(row["group_low_id"]), int(row["group_high_id"]))
            if pair in malis_pairs:
                pair_matches += 1
                if item_id not in malis_endpoints and pair[0] != pair[1]:
                    interpolation_only += 1

    aorebirth_ids = set()
    item_projection = REPOSITORY_ROOT / "docs" / "reference" / "missions" / "aorebirth-item-templates.jsonl"
    with item_projection.open("r", encoding="utf-8") as stream:
        for line in stream:
            if line.strip():
                aorebirth_ids.add(int(json.loads(line)["item_id"]))
    semantic_comparison = {}
    for category, source_name, ao_name in ITEM_FILES:
        malis_value = load_json_bytes(source.read(source_name), source_name)
        ao_path = REPOSITORY_ROOT / "AORebirth" / "Server" / "ZoneEngine" / "XML Data" / "MissionRewards" / ao_name
        ao_value = json.loads(ao_path.read_text(encoding="utf-8-sig"))
        semantic_comparison[category] = {
            "AORebirthByteSha256": sha256_file(ao_path),
            "MalisByteSha256": sha256_bytes(source.read(source_name)),
            "SemanticEquality": malis_value == ao_value,
        }
    unresolved_name = "pill with fling shot proficiency"
    item_comparison = {
        "AORebirthEndpointIdentityMatches": len(malis_endpoints & aorebirth_ids),
        "AORebirthEndpointIdentityMissing": sorted(malis_endpoints - aorebirth_ids),
        "AORebirthRewardCatalogSemanticComparison": semantic_comparison,
        "ClickSaver": {
            "ExactEndpointIdentityMatches": endpoint_matches,
            "ExactTemplatePairMatches": pair_matches,
            "InterpolatedRelationMembersBeyondEndpoints": interpolation_only,
            "ItemIdentities": len(clicksaver_ids),
            "ItemIdentitiesWithoutMalisEndpoint": len(clicksaver_ids - malis_endpoints),
            "MalisEndpointsAbsentFromClickSaver": len(malis_endpoints - clicksaver_ids),
        },
        "Duplicates": duplicates,
        "Malis": {
            "ActualItemRows": len(all_item_rows),
            "CategoryCountsIncludingPseudoEntry": item_dataset_counts,
            "EndpointIdentities": len(malis_endpoints),
            "PseudoRewardSearchEntries": len(item_records) - len(all_item_rows),
            "RowsIncludingPseudoEntry": len(item_records),
            "UniqueTemplatePairs": len(malis_pairs),
        },
        "UnresolvedHistoricalItem89622": {
            "ExactEndpointMatch": 89622 in malis_endpoints,
            "ExactTemplatePairMatch": any(
                row["clicksaver_item_id"] == "89622"
                and row["group_low_id"]
                and (int(row["group_low_id"]), int(row["group_high_id"])) in malis_pairs
                for row in clicksaver
            ),
            "NormalizedNameDiagnosticMatch": unresolved_name in malis_names,
            "Resolution": "UNRESOLVED",
        },
    }
    write_json(output_root / "item-comparison.json", item_comparison)

    clicksaver_playfields_path = REPOSITORY_ROOT / "docs" / "generated" / "missions" / "arpa3" / "clicksaver-playfield-catalog.json"
    clicksaver_playfields = json.loads(clicksaver_playfields_path.read_text(encoding="utf-8"))
    clicksaver_by_id = {int(row["playfield_id"]): row for row in clicksaver_playfields}
    playfield_rows = []
    exact_name_matches = 0
    normalized_name_matches = 0
    for playfield_id, name in zip(playfield_ids, location_names):
        historical = clicksaver_by_id.get(playfield_id)
        historical_names = [] if historical is None else [value for value in (historical.get("all_name"), historical.get("tiny_name")) if value]
        exact = name in historical_names
        normalized = normalized_name(name) in {normalized_name(value) for value in historical_names}
        exact_name_matches += int(exact)
        normalized_name_matches += int(normalized)
        playfield_rows.append(
            {
                "ClickSaverNames": historical_names,
                "IdPresentInClickSaver": historical is not None,
                "MalisName": name,
                "NameExactMatch": exact,
                "NameNormalizedDiagnosticMatch": normalized,
                "PlayfieldId": playfield_id,
            }
        )
    intersection = set(playfield_ids) & set(clicksaver_by_id)
    playfield_comparison = {
        "Counts": {
            "ClickSaverPlayfields": len(clicksaver_by_id),
            "ExactIdAndNameMatches": exact_name_matches,
            "IdMatches": len(intersection),
            "IdMatchesWithNameDisagreement": sum(1 for row in playfield_rows if row["IdPresentInClickSaver"] and not row["NameExactMatch"]),
            "MalisPlayfields": len(playfield_ids),
            "MissingHistoricalPlayfields": len(set(clicksaver_by_id) - set(playfield_ids)),
            "NewMalisPlayfields": len(set(playfield_ids) - set(clicksaver_by_id)),
            "NormalizedNameDiagnosticMatches": normalized_name_matches,
        },
        "Rows": playfield_rows,
        "Semantics": "Malis list is a client-side inclusion/filter list, not destination eligibility or weighting evidence.",
    }
    write_json(output_root / "playfield-comparison.json", playfield_comparison)

    main_window_source = source.read("MainWindow.cs").decode("utf-8-sig")
    icon_filters = parse_mission_icon_filters(main_window_source)
    type_mapping = {
        "ReturnItem": ("Return Item", "FindItemReturn", "RETURN_ITEM", "Return Item", "0x2C41"),
        "KillTarget": ("Kill Target", "KillPerson", "KILL_PERSON", "Kill Person", "0x2C42"),
        "FindTarget": ("Find Target", "FindPerson", "FIND_PERSON", "Find Person", "0x2C47"),
        "FindItem": ("Find Item", "FindItem", "FIND_ITEM", "Find Item", "0x2C49"),
        "UseItem": ("Use Item", "RepairMachine", "REPAIR", "Repair", "0x2C4E"),
    }
    mission_types = []
    for setting_name, icon in icon_filters:
        display, aorebirth_type, canonical_type, canonical_display, clicksaver_code = type_mapping[setting_name]
        mission_types.append(
            {
                "AORebirthCaptureBackedType": aorebirth_type,
                "CanonicalMissionType": canonical_type,
                "CanonicalDisplayName": canonical_display,
                "CanonicalNameSource": "https://forums.funcom.com/t/rubi-ka-mission-settings-101/6664",
                "ClickSaverWireCode": clicksaver_code,
                "MalisDisplayName": display,
                "MalisFilterSetting": setting_name,
                "MissionIcon": icon,
                "Representation": "SERVER_RESPONSE_MISSION_ICON_FILTERED_BY_MALIS",
            }
        )
    write_json(output_root / "mission-type-catalog.json", mission_types)

    aosharp_paths = {
        "QuestAlternativeMessage": "AOSharp.Common/SmokeLounge/AOtomation/Messaging/Messages/N3Messages/QuestAlternativeMessage.cs",
        "MissionInfo": "AOSharp.Common/SmokeLounge/AOtomation/Messaging/GameData/MissionInfo.cs",
        "MissionItemReward": "AOSharp.Common/SmokeLounge/AOtomation/Messaging/GameData/MissionItemReward.cs",
        "MissionSliders": "AOSharp.Common/SmokeLounge/AOtomation/Messaging/GameData/MissionSliders.cs",
    }
    usage = {
        "MissionIdentity": "accept and map-ping identity",
        "Title": "display",
        "Description": "string matching for nanos and objective/find-item-like text",
        "Credits": "display and combined reward value",
        "XpReward": "display",
        "MissionItemData": "reward display, value calculation, and exact ID/QL matching",
        "MissionIcon": "five-type filtering and display",
        "Playfield": "destination allowlist and display-name lookup",
        "Location": "optional X/Z bounds filtering",
        "LowId": "reward identity and single-template matching",
        "HighId": "reward identity and primary template-pair matching",
        "Ql": "reward quality matching and dummy-item construction",
        "Difficulty": "request difficulty forwarded by AOSharp",
        "GoodBad": "request slider forwarded by AOSharp",
        "OrderChaos": "request slider forwarded by AOSharp",
        "OpenHidden": "request slider forwarded by AOSharp",
        "PhysicalMystical": "request slider forwarded by AOSharp",
        "HeadonStealth": "request slider forwarded by AOSharp",
        "CreditsXp": "request slider forwarded by AOSharp",
        "MissionDetails": "ordered offer cohort delivered to Malis",
    }
    field_catalog = []
    for type_name, path in aosharp_paths.items():
        members = parse_aosharp_members(aosharp.read(path).decode("utf-8-sig"))
        if not members:
            raise EvidenceError(f"No AOSharp members parsed for {type_name}.")
        for member in members:
            member.update(
                {
                    "AOSharpType": type_name,
                    "MalisUse": usage.get(str(member["Property"]), "unused/unknown in Malis"),
                    "Origin": "QuestAlternative packet field" if type_name in {"QuestAlternativeMessage", "MissionInfo", "MissionItemReward", "MissionSliders"} else "AOSharp derived",
                    "SourcePath": path,
                }
            )
            field_catalog.append(member)

    field_availability = [
        ("mission identity", "MissionInfo.MissionIdentity", "DIRECT_SERVER_RESPONSE"),
        ("mission QL", None, "NOT_EXPOSED_BY_AOSHARP_MISSIONINFO_1_0_106"),
        ("reward low ID", "MissionItemReward.LowId", "DIRECT_SERVER_RESPONSE"),
        ("reward high ID", "MissionItemReward.HighId", "DIRECT_SERVER_RESPONSE"),
        ("reward QL", "MissionItemReward.Ql", "DIRECT_SERVER_RESPONSE"),
        ("objective item", "MissionInfo.Description only; accepted Mission.Actions later exposes identities", "NOT_A_TYPED_OFFER_FIELD"),
        ("objective item QL", None, "NOT_EXPOSED"),
        ("mission type", "MissionInfo.MissionIcon; accepted Mission.Actions later exposes action enum", "ICON_DIRECT_ACTION_POST_ACCEPT"),
        ("destination playfield", "MissionInfo.Playfield", "DIRECT_SERVER_RESPONSE"),
        ("destination coordinates", "MissionInfo.Location Vector3", "DIRECT_SERVER_RESPONSE"),
        ("credits", "MissionInfo.Credits", "DIRECT_SERVER_RESPONSE"),
        ("XP reward", "MissionInfo.XpReward", "DIRECT_SERVER_RESPONSE"),
        ("token reward", None, "NOT_EXPOSED"),
        ("faction", None, "NOT_EXPOSED"),
        ("mission description", "MissionInfo.Description", "DIRECT_SERVER_RESPONSE"),
        ("mission icon", "MissionInfo.MissionIcon", "DIRECT_SERVER_RESPONSE"),
        ("mission entrance identity", None, "NOT_EXPOSED_IN_OFFER"),
        ("terminal identity", "MissionInfo.TerminalIdentity and QuestAlternativeMessage.Terminal", "DIRECT_SERVER_RESPONSE"),
        ("offer slot", "MissionInfo[] array index", "DERIVED_FROM_ORDER"),
        ("five-offer cohort identity", None, "ONE_EVENT_ARRAY_WITH_NO_EXPLICIT_COHORT_ID"),
    ]
    aosharp_catalog = {
        "ExactPackage": {
            "NuGetSha256": sha256_file(nupkg_path),
            "Version": manifest["Acquisition"]["AOSharpNuGetVersion"],
        },
        "FieldAvailability": [
            {"Field": field, "AOSharpRepresentation": representation, "Status": status}
            for field, representation, status in field_availability
        ],
        "OfferFieldCatalog": field_catalog,
        "PostAcceptanceApi": {
            "Actions": ["FindPerson(0x10)", "FindItem(0x0F)", "UseItemOnItem(0x08)", "KillPerson(0x01)"],
            "Fields": ["Identity", "DisplayName", "Source", "PlayfieldInstance", "Location", "Actions"],
            "MalisUse": "Malis calls only static Mission.UploadToMap for an offered mission identity; it does not inspect accepted Mission.Actions.",
        },
        "SourceCorrelation": {
            "Commit": AOSHARP_COMMIT,
            "ExactPackageToCommitBridge": "UNKNOWN; the NuGet package has no repository commit metadata",
            "Finding": "The relevant public source files at this same-day commit are unchanged through the inspected public HEAD and agree with exact package metadata.",
        },
        "TransportPath": [
            "AO server returns QuestAlternative N3 message",
            "AO client receives the message",
            "AOSharp deserializes QuestAlternativeMessage and calls Mission.OnRollListChanged",
            "Malis receives RollListChangedArgs.MissionDetails in array order",
            "Malis displays and filters each MissionInfo; it never predicts a server offer",
        ],
    }
    write_json(output_root / "aosharp-mission-field-catalog.json", aosharp_catalog)

    static_candidates = [
        name for name in source.names()
        if name.startswith(("JSON/", "UI/", "Sound/")) or name in {"packages.config", "app.config"}
    ]
    release_member_prefix = "lib/MissionRoller/"
    release_members = [name for name in release.names() if name.startswith(release_member_prefix)]
    release_map = {name[len(release_member_prefix):]: name for name in release_members}
    comparisons = []
    for source_name in static_candidates:
        release_name = "Malis Mission Roller 2.dll.config" if source_name == "app.config" else source_name
        present = release_name in release_map
        same = present and source.read(source_name) == release.read(release_map[release_name])
        comparisons.append({"ByteIdentical": same, "PresentInRelease": present, "ReleasePath": release_name, "SourcePath": source_name})
    matched_release_names = {row["ReleasePath"] for row in comparisons if row["PresentInRelease"]}
    release_only = sorted(set(release_map) - matched_release_names)
    release_inventory = []
    for relative, archive_name in sorted(release_map.items()):
        data = release.read(archive_name)
        release_inventory.append({"ByteLength": len(data), "Path": relative, "Sha256": sha256_bytes(data)})
    release_comparison = {
        "AdditionalMissionDatasetsAbsentFromSource": [],
        "MissionRollerMembers": release_inventory,
        "ReleaseOnlyMembers": release_only,
        "SourceStaticComparison": comparisons,
        "Summary": {
            "ByteIdenticalStaticFiles": sum(1 for row in comparisons if row["ByteIdentical"]),
            "MissingSourceStaticFilesInRelease": sum(1 for row in comparisons if not row["PresentInRelease"]),
            "MismatchedStaticFiles": sum(1 for row in comparisons if row["PresentInRelease"] and not row["ByteIdentical"]),
            "ReleaseOnlyMissionRollerMembers": len(release_only),
        },
        "Safety": "The archive was inspected without executing bundled EXE or DLL files.",
    }
    write_json(output_root / "release-comparison.json", release_comparison)

    history_findings = {
        "CommitCount": len(history),
        "Issues": json.loads((REFERENCE_ROOT / "raw" / "gitlab-issues.json").read_text(encoding="utf-8")),
        "MergeRequests": json.loads((REFERENCE_ROOT / "raw" / "gitlab-merge-requests.json").read_text(encoding="utf-8")),
        "RelevantCommits": [
            {
                **history_by_sha[LEVEL_80_COMMIT],
                "FilesChanged": ["JSON/MissionLevels.json"],
                "Finding": "Character level 80, difficulty wire 11 changed from QL144 to QL143.",
                "PublicCommitComments": json.loads((REFERENCE_ROOT / "raw" / f"gitlab-{LEVEL_80_COMMIT}-comments.json").read_text(encoding="utf-8")),
                "Provenance": "UNKNOWN_BEYOND_COMMIT_SUBJECT",
            },
            {
                **history_by_sha[QL200_COMMIT],
                "FilesChanged": ["MainWindow.cs", "Malis Mission Roller 2.csproj", "RollEntryProcessor.cs"],
                "Finding": "Introduced client-side eligibility and difficulty selection for non-nano QL200 search entries when player level is strictly greater than 200.",
                "PublicCommitComments": json.loads((REFERENCE_ROOT / "raw" / f"gitlab-{QL200_COMMIT}-comments.json").read_text(encoding="utf-8")),
                "Provenance": "PROVEN_FROM_SOURCE; AO_SERVER_BEHAVIOR_PROVENANCE_UNKNOWN",
            },
            {
                **history_by_sha[SLIDER_CENTER_COMMIT],
                "FilesChanged": ["UI/Views/SliderView.xml", "Views/SliderView.cs"],
                "Finding": "Remapped neutral UI value 0 to byte 255 for the six non-difficulty sliders; difficulty remains a direct one-based value.",
                "Provenance": "PROVEN_FROM_SOURCE_AND_MERGE_REQUEST_TITLE; AO_SERVER_RATIONALE_NOT_DOCUMENTED",
            },
        ],
        "TableCorrectionHistory": table_history,
        "UpstreamReferencesFound": {"ARPA": False, "ClickSaver": False, "FormulaOrTableCitation": False},
    }
    write_json(output_root / "source-history-findings.json", history_findings)

    gap_matrix = [
        ("A", "Complete item -> mission QL eligibility", "DOES_NOT_FILL", "Search catalogs contain item templates only; no eligibility rows."),
        ("B", "Complete observed item -> mission QL matrix", "DOES_NOT_FILL", "No repeated offer observations or item/mission-QL occurrence records."),
        ("C", "Reward vs objective-item eligibility matrix", "DOES_NOT_FILL", "Malis checks reward arrays and description text after offers but ships no channel matrix."),
        ("D", "Observation counts", "DOES_NOT_FILL", "No observation corpus or counters are shipped."),
        ("E", "Reward frequency", "DOES_NOT_FILL", "No frequency data are shipped."),
        ("F", "Generator weighting", "DOES_NOT_FILL", "Malis requests and filters server-generated offers."),
        ("G", "Reward QL distribution", "STRUCTURAL_EVIDENCE_ONLY", "Reward QL is exposed and matched; the >200 feature targets QL200 non-nanos but preserves no distribution."),
        ("H", "Character level -> available mission QLs", "FILLS_PARTIALLY", "A 220-row client table exists, but 13 difficulty slots are missing and 55 present cells differ from AORebirth's canonical table."),
        ("I", "Difficulty slider -> mission QL", "FILLS_PARTIALLY", "Malis proves its one-based table-index/request mapping, not the Funcom implementation; table anomalies remain."),
        ("J", "Mission type generation structure", "STRUCTURAL_EVIDENCE_ONLY", "Five response icons/types are filtered; no generation weights or constraints are supplied."),
        ("K", "Destination eligibility", "STRUCTURAL_EVIDENCE_ONLY", "Forty-six client-side allowed destinations and X/Z bounds are present; no server eligibility/weighting table exists."),
        ("L", "Five-offer cohort behavior", "STRUCTURAL_EVIDENCE_ONLY", "One ordered array is processed as five offers; no cohort ID, independence, duplicates, or weighting rule is exposed."),
    ]
    write_json(
        output_root / "arpa-gap-matrix.json",
        [{"Gap": gap, "Question": question, "Classification": classification, "Evidence": evidence} for gap, question, classification, evidence in gap_matrix],
    )

    readiness = [
        ("character level -> mission QL", "READY", "AORebirth already has a governed exact 220-row table; Malis corroborates most cells but is not the authority."),
        ("difficulty slider mapping", "READY", "One-based difficulty 1..11 to table index 0..10 is source- and capture-backed."),
        ("mission type representation", "READY", "Five icons bridge exactly across Malis, ClickSaver codes, AOSharp actions, and AORebirth captures."),
        ("AOSharp offer field representation", "READY", "Exact typed response fields and cohort transport are source/package backed."),
        ("QL200 reward behavior above level 200", "NOT_READY", "Malis implements a filter assumption with an uncertainty comment; no retained offer corpus proves the server boundary."),
        ("reward eligibility", "NOT_READY", "No complete eligibility matrix."),
        ("reward weighting", "NOT_READY", "No frequency or true weight evidence."),
        ("objective-item selection", "NOT_READY", "Description matching is post-offer filtering, not selection evidence."),
        ("destination selection", "NOT_READY", "The 46-entry list is a user filter, not a server destination pool."),
        ("five-offer generation", "NOT_READY", "Representation is known; cross-offer constraints and selection law are not."),
    ]
    analysis_summary = {
        "Architecture": "SERVER_OFFER_FILTERING",
        "ArpaEquivalentClassification": "NO_EQUIVALENT_DATA",
        "GeneratorReadiness": [{"Subsystem": subsystem, "Status": status, "Basis": basis} for subsystem, status, basis in readiness],
        "HighestValueTargetedCaptures": [
            "At character levels 200, 201, and 220, request every difficulty detent while targeting the same scalable non-nano QL200 item; retain every reward low/high ID and reward QL.",
            "Repeat the >200 comparison for a QL200 nano and a scalable item with distinct low/high endpoints to separate cap, nano, and interpolation behavior.",
            "Capture level 80 difficulty 11 and the divergent/missing Malis cells (levels 12, 13, 209-219 difficulty 11) to distinguish table defects from server behavior.",
            "For exact target items, retain complete five-offer cohorts and accepted-mission actions so reward-array matches and objective-description matches become separate channels.",
            "If probabilities are a later target, retain ordered no-success-filter cohorts with terminal, character, sliders, costs, all five offers, and repeated requests; do not sample only matches.",
        ],
        "ItemExistenceIsEligibility": False,
        "MalisCommit": MALIS_COMMIT,
        "MalisMissionKnowledge": [
            "A client-side 220-row, 11-position-intended mission-level table and one-based difficulty selection path",
            "Typed AOSharp QuestAlternative offer fields and ordered cohort handling",
            "Five mission icon/type filters",
            "Post-offer reward/template/QL matching plus description-string matching",
            "A 46-playfield client-side destination allowlist with optional X/Z bounds",
            "A special non-nano QL200 search path for characters strictly above level 200",
        ],
        "NoObservationCounts": True,
        "NoRewardFrequencies": True,
        "NoRuntimeDependencyAdded": True,
        "NoRuntimeMissionLogicChanged": True,
        "ProvenanceClassification": {
            "AOSharpTypedFields": "PROVEN_FROM_SOURCE_AND_EXACT_PACKAGE_METADATA",
            "ItemCatalogs": "PROVEN_FROM_STATIC_DATA; upstream provenance UNKNOWN",
            "Level80Change": "PROVEN_FROM_SOURCE_HISTORY; rationale UNKNOWN",
            "MissionLevelTable": "PROVEN_FROM_STATIC_DATA; upstream provenance UNKNOWN",
            "Ql200SpecialCase": "PROVEN_FROM_SOURCE_AS_CLIENT_BEHAVIOR; AO server rule HYPOTHESIS",
        },
        "RuntimeMissionLogicChanged": False,
    }
    write_json(output_root / "analysis-summary.json", analysis_summary)

    generated_files = sorted(path for path in output_root.iterdir() if path.is_file() and path.name != "evidence-manifest.json")
    generated_manifest = {
        "Generator": "Tools/malis_mission_evidence.py",
        "GeneratorVersion": GENERATOR_VERSION,
        "MalisSourceCommit": MALIS_COMMIT,
        "Outputs": [
            {
                "ByteLength": path.stat().st_size,
                "RelativePath": "docs/generated/missions/malis/" + path.name,
                "Sha256": sha256_file(path),
            }
            for path in generated_files
        ],
        "RuntimeMissionLogicChanged": False,
    }
    write_json(output_root / "evidence-manifest.json", generated_manifest)


def self_test() -> None:
    valid = [{"Key": {"LowId": 1, "HighId": 2, "LowQl": 1, "HighQl": 200, "Tags": ["x"], "Name": "X"}, "Value": [1]}]
    validate_item_rows(valid, "fixture")
    malformed = [{"Key": {"LowId": 1}, "Value": []}]
    try:
        validate_item_rows(malformed, "malformed")
        raise EvidenceError("Malformed item fixture was accepted.")
    except EvidenceError:
        pass
    duplicate = valid + valid + [{"Key": {"LowId": 1, "HighId": 2, "LowQl": 2, "HighQl": 200, "Tags": ["x"], "Name": "X"}, "Value": [1]}]
    summary = duplicate_summary(duplicate)
    if summary["ExactDuplicateRecordExcess"] != 1 or len(summary["ConflictingTemplatePairs"]) != 1:
        raise EvidenceError("Duplicate/conflict self-test failed.")
    synthetic_levels = [[level] * 11 for level in range(1, 221)]
    synthetic_levels[11] = synthetic_levels[11][:-1]
    parsed = parse_mission_levels(synthetic_levels)
    if len(parsed[11]) != 10:
        raise EvidenceError("Mission-level shape self-test failed.")
    if parse_playfield_ids("private readonly int[] _pfIds = new int[] { 1, 2, 3 };") != [1, 2, 3]:
        raise EvidenceError("Playfield parser self-test failed.")
    icon_fixture = "\n".join(
        f"SettingsView.MissionTypes.Type{index}.Tag && missionInfo.MissionIcon == {icon}"
        for index, icon in enumerate((11329, 11330, 11335, 11337, 11342), start=1)
    )
    if parse_mission_icon_filters(icon_fixture) != [
        ("Type1", 11329),
        ("Type2", 11330),
        ("Type3", 11335),
        ("Type4", 11337),
        ("Type5", 11342),
    ]:
        raise EvidenceError("Mission-icon filter self-test failed.")
    manifest = verify_source_manifest()
    if manifest["Acquisition"]["MalisCommit"] != MALIS_COMMIT:
        raise EvidenceError("Provenance-retention self-test failed.")
    with tempfile.TemporaryDirectory() as temporary:
        candidate = Path(temporary) / "generated"
        candidate.mkdir()
        build_outputs(candidate)
        level = json.loads((candidate / "mission-level-comparison.json").read_text(encoding="utf-8"))
        level_80 = level["Level80Finding"]
        if (level_80["OldMalisValue"], level_80["CorrectedMalisValue"]) != (144, 143):
            raise EvidenceError("Level-80 historical comparison self-test failed.")
        ql200 = level["Ql200Above200ClientSpecialCase"]
        if ql200["ServerRuleProven"] or ql200["Mappings"][0] != {
            "CharacterLevel": 201,
            "DifficultyWireValue": 6,
            "RequestedMissionQl": 201,
        } or ql200["Mappings"][-1] != {
            "CharacterLevel": 220,
            "DifficultyWireValue": 6,
            "RequestedMissionQl": 220,
        }:
            raise EvidenceError(">200/QL200 client-special-case self-test failed.")
        items = json.loads((candidate / "item-comparison.json").read_text(encoding="utf-8"))
        if items["AORebirthEndpointIdentityMatches"] != 43075:
            raise EvidenceError("Item-ID join self-test failed.")
        if items["UnresolvedHistoricalItem89622"]["Resolution"] != "UNRESOLVED":
            raise EvidenceError("Historical unresolved-item self-test failed.")
        playfields = json.loads((candidate / "playfield-comparison.json").read_text(encoding="utf-8"))
        if playfields["Counts"]["IdMatches"] != 46 or playfields["Counts"]["NewMalisPlayfields"] != 0:
            raise EvidenceError("Playfield-ID join self-test failed.")
        history = json.loads((candidate / "source-history-findings.json").read_text(encoding="utf-8"))
        relevant_shas = {row["Sha"] for row in history["RelevantCommits"]}
        if not {LEVEL_80_COMMIT, QL200_COMMIT, SLIDER_CENTER_COMMIT}.issubset(relevant_shas):
            raise EvidenceError("Historical-provenance self-test failed.")


def check_generated() -> None:
    with tempfile.TemporaryDirectory() as temporary:
        candidate = Path(temporary) / "generated"
        candidate.mkdir()
        build_outputs(candidate)
        current_files = sorted(path.name for path in GENERATED_ROOT.iterdir() if path.is_file())
        candidate_files = sorted(path.name for path in candidate.iterdir() if path.is_file())
        if current_files != candidate_files:
            raise EvidenceError("Generated Malis evidence file set is stale.")
        for name in current_files:
            if (GENERATED_ROOT / name).read_bytes() != (candidate / name).read_bytes():
                raise EvidenceError(f"Generated Malis evidence is stale: {name}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_mutually_exclusive_group()
    actions.add_argument("--check", action="store_true")
    actions.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        print("Malis evidence parser self-tests passed.")
        return 0
    if args.check:
        check_generated()
        print("Malis evidence generated artifacts are deterministic and current.")
        return 0
    GENERATED_ROOT.mkdir(parents=True, exist_ok=True)
    build_outputs(GENERATED_ROOT)
    print(f"Malis evidence generated at {MALIS_COMMIT}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EvidenceError, OSError, KeyError, TypeError, ValueError, zipfile.BadZipFile) as error:
        print(f"Malis evidence generation failed: {error}")
        raise SystemExit(1)
