#!/usr/bin/env python3

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


PLAYFIELD_ID = 4582
EXPECTED_SOURCE_PLACEMENTS = 206
REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = REPOSITORY_ROOT / "docs/reference/pf4582/PlayfieldDistrictInfo.json"
DEFAULT_EVIDENCE_MAP = REPOSITORY_ROOT / "docs/reference/pf4582/runtime-evidence-map.json"
DEFAULT_RUNTIME_SOURCE = (
    REPOSITORY_ROOT
    / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs"
)
DEFAULT_OUTPUT = (
    REPOSITORY_ROOT
    / "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportPlacementCatalog.g.cs"
)
DEFAULT_REPORT = (
    REPOSITORY_ROOT
    / "docs/generated/pf4582_authoritative_placement_report.json"
)

SPAWN_FIELDS = {
    "Name",
    "NpcId",
    "TemplateHash",
    "BossMods",
    "SpawnHash",
    "Position",
    "SpawnRadius",
    "SpawnAngle",
    "SpawnAngleW",
    "MinLevel",
    "MaxLevel",
    "SpawnChance",
    "ExtraData",
    "ExFlags",
    "SpawnTime",
    "SpawnUnknowns",
    "SpawnPointFlags",
}
POSITION_FIELDS = {"X", "Y", "Z"}
INTEGER_FIELDS = {
    "NpcId",
    "TemplateHash",
    "SpawnHash",
    "MinLevel",
    "MaxLevel",
    "SpawnChance",
    "ExtraData",
    "ExFlags",
    "SpawnTime",
}
NUMBER_FIELDS = {"SpawnRadius", "SpawnAngle", "SpawnAngleW"}
STRING_FIELDS = {"Name", "BossMods", "SpawnPointFlags"}


class PlacementValidationError(ValueError):
    pass


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise PlacementValidationError(message)


def _is_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool)


def _is_number(value: Any) -> bool:
    return (isinstance(value, int) and not isinstance(value, bool)) or isinstance(
        value, float
    )


def _load_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise PlacementValidationError(f"cannot read valid JSON {path}: {exc}") from exc


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _template_tag(template_hash: int) -> str:
    try:
        tag = template_hash.to_bytes(4, byteorder="little", signed=False).decode(
            "ascii"
        )
    except (OverflowError, UnicodeDecodeError) as exc:
        raise PlacementValidationError(
            f"TemplateHash {template_hash} is not a four-byte ASCII tag"
        ) from exc
    _require(all(32 <= ord(character) <= 126 for character in tag),
             f"TemplateHash {template_hash} contains a non-printable tag")
    return tag


def validate_source(source: Any) -> list[dict[str, Any]]:
    _require(isinstance(source, dict), "source root must be an object")
    _require(set(source) == {str(PLAYFIELD_ID)},
             f"source root must contain only playfield {PLAYFIELD_ID}")
    playfield = source[str(PLAYFIELD_ID)]
    _require(isinstance(playfield, dict) and set(playfield) == {"Spawns"},
             "playfield object must contain only Spawns")
    spawns = playfield["Spawns"]
    _require(isinstance(spawns, list), "Spawns must be an array")
    _require(len(spawns) == EXPECTED_SOURCE_PLACEMENTS,
             f"expected {EXPECTED_SOURCE_PLACEMENTS} spawns, found {len(spawns)}")

    normalized: list[dict[str, Any]] = []
    npc_ids: set[int] = set()
    for index, raw in enumerate(spawns):
        prefix = f"Spawns[{index}]"
        _require(isinstance(raw, dict), f"{prefix} must be an object")
        _require(set(raw) == SPAWN_FIELDS,
                 f"{prefix} fields differ: missing={sorted(SPAWN_FIELDS - set(raw))} "
                 f"extra={sorted(set(raw) - SPAWN_FIELDS)}")
        for field in INTEGER_FIELDS:
            _require(_is_int(raw[field]), f"{prefix}.{field} must be an integer")
        for field in NUMBER_FIELDS:
            _require(_is_number(raw[field]), f"{prefix}.{field} must be numeric")
        for field in STRING_FIELDS:
            _require(isinstance(raw[field], str), f"{prefix}.{field} must be a string")

        position = raw["Position"]
        _require(isinstance(position, dict) and set(position) == POSITION_FIELDS,
                 f"{prefix}.Position must contain only X, Y, Z")
        for axis in sorted(POSITION_FIELDS):
            _require(_is_number(position[axis]),
                     f"{prefix}.Position.{axis} must be numeric")
            _require(math.isfinite(float(position[axis])),
                     f"{prefix}.Position.{axis} must be finite")
        for field in NUMBER_FIELDS:
            _require(math.isfinite(float(raw[field])),
                     f"{prefix}.{field} must be finite")

        unknowns = raw["SpawnUnknowns"]
        _require(isinstance(unknowns, list) and len(unknowns) == 4,
                 f"{prefix}.SpawnUnknowns must contain exactly four values")
        _require(all(_is_int(value) for value in unknowns),
                 f"{prefix}.SpawnUnknowns values must be integers")
        _require(raw["NpcId"] > 0, f"{prefix}.NpcId must be positive")
        _require(raw["TemplateHash"] > 0, f"{prefix}.TemplateHash must be positive")
        _require(raw["SpawnHash"] > 0, f"{prefix}.SpawnHash must be positive")
        _require(raw["MinLevel"] <= raw["MaxLevel"],
                 f"{prefix} has MinLevel greater than MaxLevel")
        _require(raw["NpcId"] not in npc_ids,
                 f"duplicate NpcId {raw['NpcId']}")
        npc_ids.add(raw["NpcId"])

        record = dict(raw)
        record["Position"] = dict(position)
        record["SpawnUnknowns"] = list(unknowns)
        record["TemplateTag"] = _template_tag(raw["TemplateHash"])
        normalized.append(record)

    return sorted(normalized, key=lambda record: record["NpcId"])


def validate_evidence_map(
    evidence_map: Any,
    source_sha256: str,
    records_by_id: dict[int, dict[str, Any]],
) -> tuple[list[dict[str, Any]], set[int], list[str]]:
    expected_fields = {
        "schemaVersion",
        "playfieldId",
        "sourceSha256",
        "placementSource",
        "runtimeEvidence",
        "runtimeMappings",
        "unresolvedDynamicSourceNameNpcIds",
        "unresolvedSemantics",
    }
    _require(isinstance(evidence_map, dict), "runtime evidence map must be an object")
    _require(set(evidence_map) == expected_fields,
             "runtime evidence map fields differ from the governed schema")
    _require(evidence_map["schemaVersion"] == 1, "unsupported evidence-map schema")
    _require(evidence_map["playfieldId"] == PLAYFIELD_ID,
             "evidence map targets the wrong playfield")
    _require(evidence_map["sourceSha256"] == source_sha256,
             "authoritative source SHA-256 differs from the evidence map")
    _require(isinstance(evidence_map["runtimeEvidence"], list)
             and all(isinstance(value, str) and value for value in evidence_map["runtimeEvidence"]),
             "runtimeEvidence must contain non-empty paths")

    mappings = evidence_map["runtimeMappings"]
    _require(isinstance(mappings, list), "runtimeMappings must be an array")
    mapped_ids: set[int] = set()
    for index, mapping in enumerate(mappings):
        prefix = f"runtimeMappings[{index}]"
        _require(isinstance(mapping, dict)
                 and set(mapping) == {"npcId", "sourceName", "runtimeProfile"},
                 f"{prefix} fields differ from the governed schema")
        npc_id = mapping["npcId"]
        _require(_is_int(npc_id), f"{prefix}.npcId must be an integer")
        _require(npc_id not in mapped_ids, f"duplicate runtime mapping NpcId {npc_id}")
        _require(npc_id in records_by_id, f"runtime mapping NpcId {npc_id} is absent")
        _require(mapping["sourceName"] == records_by_id[npc_id]["Name"],
                 f"runtime mapping NpcId {npc_id} source name conflicts")
        _require(isinstance(mapping["runtimeProfile"], str)
                 and mapping["runtimeProfile"].startswith("IccShuttleportSpawn:"),
                 f"{prefix}.runtimeProfile is not an explicit ICC profile")
        mapped_ids.add(npc_id)

    dynamic_ids = evidence_map["unresolvedDynamicSourceNameNpcIds"]
    _require(isinstance(dynamic_ids, list)
             and all(_is_int(value) for value in dynamic_ids),
             "unresolved dynamic-name IDs must be integers")
    dynamic_set = set(dynamic_ids)
    _require(len(dynamic_set) == len(dynamic_ids),
             "unresolved dynamic-name IDs must be unique")
    _require(dynamic_set.issubset(records_by_id),
             "unresolved dynamic-name IDs must exist in the source")
    _require(dynamic_set.isdisjoint(mapped_ids),
             "unresolved dynamic-name records cannot be runtime mapped")

    semantics = evidence_map["unresolvedSemantics"]
    _require(isinstance(semantics, list)
             and semantics == sorted(set(semantics))
             and all(isinstance(value, str) and value for value in semantics),
             "unresolvedSemantics must be a sorted unique string array")
    return mappings, dynamic_set, semantics


def parse_current_runtime_definitions(path: Path) -> list[dict[str, Any]]:
    text = path.read_text(encoding="utf-8")
    starts = list(re.finditer(r"new ShuttleportNpc\s*\{", text))
    records: list[dict[str, Any]] = []
    for index, start in enumerate(starts):
        end = starts[index + 1].start() if index + 1 < len(starts) else text.find(
            "public static void ClearPlayfield", start.end()
        )
        _require(end > start.end(), f"cannot bound runtime definition {index}")
        block = text[start.end():end]
        npc_match = re.search(r"SourceNpcId\s*=\s*(\d+)", block)
        name_match = re.search(r'Name\s*=\s*"([^"]+)"', block)
        position_match = re.search(
            r"X\s*=\s*(-?[0-9.]+)f,\s*Y\s*=\s*(-?[0-9.]+)f,\s*Z\s*=\s*(-?[0-9.]+)f,",
            block,
        )
        _require(npc_match is not None, f"runtime definition {index} lacks SourceNpcId")
        _require(name_match is not None, f"runtime definition {index} lacks Name")
        _require(position_match is not None, f"runtime definition {index} lacks position")
        records.append(
            {
                "NpcId": int(npc_match.group(1)),
                "Name": name_match.group(1),
                "Position": tuple(float(position_match.group(axis)) for axis in (1, 2, 3)),
            }
        )
    return records


def validate_current_runtime(
    runtime_definitions: list[dict[str, Any]],
    mappings: list[dict[str, Any]],
    records_by_id: dict[int, dict[str, Any]],
) -> list[dict[str, Any]]:
    runtime_by_id = {record["NpcId"]: record for record in runtime_definitions}
    _require(len(runtime_by_id) == len(runtime_definitions),
             "current runtime definitions contain duplicate SourceNpcId values")
    mapping_by_id = {mapping["npcId"]: mapping for mapping in mappings}
    _require(set(runtime_by_id) == set(mapping_by_id),
             "current runtime SourceNpcId values differ from the evidence map")

    matches: list[dict[str, Any]] = []
    for npc_id in sorted(runtime_by_id):
        runtime = runtime_by_id[npc_id]
        mapping = mapping_by_id[npc_id]
        source = records_by_id[npc_id]
        expected_runtime_name = mapping["runtimeProfile"].split(":", 1)[1]
        _require(runtime["Name"] == expected_runtime_name,
                 f"runtime NpcId {npc_id} profile name conflicts with evidence map")
        source_position = (
            float(source["Position"]["X"]),
            float(source["Position"]["Y"]),
            float(source["Position"]["Z"]),
        )
        distance = math.dist(runtime["Position"], source_position)
        _require(distance <= 5.0,
                 f"runtime NpcId {npc_id} is {distance:.3f} units from its source placement")
        matches.append(
            {
                "npcId": npc_id,
                "sourceName": source["Name"],
                "runtimeProfile": mapping["runtimeProfile"],
                "positionDelta": round(distance, 6),
            }
        )
    return matches


def build_model(
    source_path: Path = DEFAULT_SOURCE,
    evidence_map_path: Path = DEFAULT_EVIDENCE_MAP,
    runtime_source_path: Path = DEFAULT_RUNTIME_SOURCE,
) -> dict[str, Any]:
    source_sha256 = _sha256(source_path)
    records = validate_source(_load_json(source_path))
    records_by_id = {record["NpcId"]: record for record in records}
    evidence_map = _load_json(evidence_map_path)
    mappings, dynamic_ids, unresolved_semantics = validate_evidence_map(
        evidence_map, source_sha256, records_by_id
    )
    runtime_definitions = parse_current_runtime_definitions(runtime_source_path)
    existing_matches = validate_current_runtime(
        runtime_definitions, mappings, records_by_id
    )

    mapping_by_id = {mapping["npcId"]: mapping for mapping in mappings}
    mapped_profiles: dict[int, str] = {}
    for mapping in mappings:
        template_hash = records_by_id[mapping["npcId"]]["TemplateHash"]
        existing = mapped_profiles.get(template_hash)
        _require(existing is None or existing == mapping["runtimeProfile"],
                 f"TemplateHash {template_hash} maps to conflicting runtime profiles")
        mapped_profiles[template_hash] = mapping["runtimeProfile"]

    enriched: list[dict[str, Any]] = []
    for source in records:
        record = dict(source)
        npc_id = source["NpcId"]
        template_hash = source["TemplateHash"]
        runtime_mapping = mapping_by_id.get(npc_id)
        record.update(
            {
                "PlacementKnown": True,
                "TemplateMapped": template_hash in mapped_profiles,
                "RuntimeProfile": mapped_profiles.get(template_hash, ""),
                "BehaviorProven": runtime_mapping is not None,
                "RuntimeEligible": runtime_mapping is not None,
                "RuntimeActive": runtime_mapping is not None,
                "SourceNameInterpretation": (
                    "UnresolvedDynamic" if npc_id in dynamic_ids else "MetadataOnly"
                ),
            }
        )
        enriched.append(record)

    return {
        "sourceSha256": source_sha256,
        "records": enriched,
        "existingMatches": existing_matches,
        "unresolvedSemantics": unresolved_semantics,
        "runtimeEvidence": evidence_map["runtimeEvidence"],
    }


def _csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'


def _float_literal(value: Any) -> str:
    text = format(float(value), ".9g")
    if "e" not in text.lower() and "." not in text:
        text += ".0"
    return text + "f"


def render_catalog(model: dict[str, Any]) -> str:
    records = model["records"]
    mapped_hashes = {record["TemplateHash"] for record in records if record["TemplateMapped"]}
    behavior_proven = sum(1 for record in records if record["BehaviorProven"])
    runtime_eligible = sum(1 for record in records if record["RuntimeEligible"])
    runtime_active = sum(1 for record in records if record["RuntimeActive"])
    lines = [
        "// <auto-generated />",
        "// Generated by Tools/generate_pf4582_placements.py.",
        "// Placement metadata only. No source flag, name, movement, or respawn semantics are inferred.",
        "namespace AORebirth.Core.Playfields",
        "{",
        "    internal static partial class IccShuttleportPlacementCatalog",
        "    {",
        f"        internal const int SourcePlacementCount = {len(records)};",
        f"        internal const int UniqueTemplateHashCount = {len({record['TemplateHash'] for record in records})};",
        f"        internal const int MappedTemplateHashCount = {len(mapped_hashes)};",
        f"        internal const int BehaviorProvenPlacementCount = {behavior_proven};",
        f"        internal const int RuntimeEligiblePlacementCount = {runtime_eligible};",
        f"        internal const int RuntimeActivePlacementCount = {runtime_active};",
        f"        internal const string SourceSha256 = \"{model['sourceSha256']}\";",
        "",
        "        private static IccShuttleportPlacementRecord[] CreatePlacements()",
        "        {",
        "            return new[]",
        "            {",
    ]
    for record in records:
        unknowns = ", ".join(str(value) for value in record["SpawnUnknowns"])
        values = [
            str(record["NpcId"]),
            str(record["TemplateHash"]),
            _csharp_string(record["TemplateTag"]),
            _csharp_string(record["Name"]),
            _csharp_string(record["BossMods"]),
            str(record["SpawnHash"]),
            _float_literal(record["Position"]["X"]),
            _float_literal(record["Position"]["Y"]),
            _float_literal(record["Position"]["Z"]),
            _float_literal(record["SpawnRadius"]),
            _float_literal(record["SpawnAngle"]),
            _float_literal(record["SpawnAngleW"]),
            str(record["MinLevel"]),
            str(record["MaxLevel"]),
            str(record["SpawnChance"]),
            str(record["ExtraData"]),
            str(record["ExFlags"]),
            str(record["SpawnTime"]),
            f"new[] {{ {unknowns} }}",
            _csharp_string(record["SpawnPointFlags"]),
            _csharp_string(record["SourceNameInterpretation"]),
            str(record["PlacementKnown"]).lower(),
            _csharp_string(record["RuntimeProfile"]),
            str(record["BehaviorProven"]).lower(),
            str(record["RuntimeEligible"]).lower(),
            str(record["RuntimeActive"]).lower(),
        ]
        lines.append(
            "                new IccShuttleportPlacementRecord(" + ", ".join(values) + "),"
        )
    lines.extend(
        [
            "            };",
            "        }",
            "    }",
            "}",
            "",
        ]
    )
    return "\n".join(lines)


def render_report(model: dict[str, Any]) -> str:
    records = model["records"]
    positions: dict[tuple[Any, Any, Any], list[int]] = defaultdict(list)
    template_counts: Counter[int] = Counter()
    for record in records:
        position = record["Position"]
        positions[(position["X"], position["Y"], position["Z"])].append(record["NpcId"])
        template_counts[record["TemplateHash"]] += 1
    duplicate_groups = [
        {
            "position": {"x": position[0], "y": position[1], "z": position[2]},
            "npcIds": sorted(npc_ids),
        }
        for position, npc_ids in sorted(positions.items())
        if len(npc_ids) > 1
    ]
    mapped_hashes = sorted(
        {record["TemplateHash"] for record in records if record["TemplateMapped"]}
    )
    unresolved_hashes = sorted(
        {record["TemplateHash"] for record in records if not record["TemplateMapped"]}
    )
    by_hash = {record["TemplateHash"]: record for record in records}
    eligible_ids = sorted(record["NpcId"] for record in records if record["RuntimeEligible"])
    blocked_ids = sorted(record["NpcId"] for record in records if not record["RuntimeEligible"])
    matched_ids = {match["npcId"] for match in model["existingMatches"]}
    new_ids = sorted(record["NpcId"] for record in records if record["NpcId"] not in matched_ids)
    duplicate_record_count = sum(len(group["npcIds"]) for group in duplicate_groups)
    duplicate_excess = sum(len(group["npcIds"]) - 1 for group in duplicate_groups)

    report = {
        "schemaVersion": 1,
        "playfieldId": PLAYFIELD_ID,
        "authoritativeSource": "docs/reference/pf4582/PlayfieldDistrictInfo.json",
        "sourceSha256": model["sourceSha256"],
        "importer": "Tools/generate_pf4582_placements.py",
        "normalizedPlacementArtifact": "AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportPlacementCatalog.g.cs",
        "PF4582_SOURCE_PLACEMENTS": len(records),
        "PF4582_UNIQUE_NPC_IDS": len({record["NpcId"] for record in records}),
        "PF4582_DUPLICATE_NPC_IDS": len(records) - len({record["NpcId"] for record in records}),
        "PF4582_UNIQUE_TEMPLATE_HASHES": len(template_counts),
        "PF4582_EXISTING_MATCHED": len(model["existingMatches"]),
        "PF4582_EXISTING_NOT_MATCHED": 0,
        "PF4582_NEW_PLACEMENTS": len(new_ids),
        "PF4582_DUPLICATE_POSITION_RECORDS": duplicate_record_count,
        "PF4582_DUPLICATE_POSITION_GROUPS": len(duplicate_groups),
        "PF4582_DUPLICATE_POSITION_EXCESS": duplicate_excess,
        "PF4582_TEMPLATE_HASHES_MAPPED": len(mapped_hashes),
        "PF4582_TEMPLATE_HASHES_UNRESOLVED": len(unresolved_hashes),
        "PF4582_RUNTIME_ELIGIBLE": len(eligible_ids),
        "PF4582_RUNTIME_BLOCKED": len(blocked_ids),
        "existingMatches": model["existingMatches"],
        "existingNotMatched": [],
        "newPlacementNpcIds": new_ids,
        "duplicatePositionGroups": duplicate_groups,
        "mappedTemplateHashes": [
            {
                "templateHash": value,
                "templateTag": by_hash[value]["TemplateTag"],
                "runtimeProfile": by_hash[value]["RuntimeProfile"],
                "sourceSpawnCount": template_counts[value],
            }
            for value in mapped_hashes
        ],
        "unresolvedTemplateHashes": [
            {
                "templateHash": value,
                "templateTag": by_hash[value]["TemplateTag"],
                "sourceName": by_hash[value]["Name"],
                "sourceSpawnCount": template_counts[value],
            }
            for value in unresolved_hashes
        ],
        "runtimeEligibleNpcIds": eligible_ids,
        "runtimeBlockedNpcIds": blocked_ids,
        "PF4582_PLACEMENT_KNOWN": sum(1 for record in records if record["PlacementKnown"]),
        "PF4582_BEHAVIOR_PROVEN": sum(1 for record in records if record["BehaviorProven"]),
        "PF4582_RUNTIME_ACTIVE": sum(1 for record in records if record["RuntimeActive"]),
        "runtimeEvidence": model["runtimeEvidence"],
        "unresolvedDynamicSourceNames": [
            {
                "npcId": record["NpcId"],
                "sourceName": record["Name"],
                "templateHash": record["TemplateHash"],
            }
            for record in records
            if record["SourceNameInterpretation"] == "UnresolvedDynamic"
        ],
        "unresolvedSourceSemantics": model["unresolvedSemantics"],
        "rejectedPlacements": [],
        "invariants": {
            "NO_HAND_TRANSCRIPTION": "YES",
            "DUPLICATE_POSITIONS_PRESERVED": "YES",
            "UNKNOWN_METADATA_PRESERVED": "YES",
            "UNPROVEN_BEHAVIOR_INVENTED": "NO",
            "UNPROVEN_SPAWNS_ACTIVATED": "NO",
        },
    }
    return json.dumps(report, indent=2, ensure_ascii=False, sort_keys=False) + "\n"


def _write_or_check(path: Path, content: str, check: bool) -> None:
    if check:
        try:
            existing = path.read_text(encoding="utf-8")
        except OSError as exc:
            raise PlacementValidationError(f"missing generated artifact {path}") from exc
        _require(existing == content, f"generated artifact is stale: {path}")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate PF4582 placement artifacts")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--evidence-map", type=Path, default=DEFAULT_EVIDENCE_MAP)
    parser.add_argument("--runtime-source", type=Path, default=DEFAULT_RUNTIME_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    try:
        model = build_model(args.source, args.evidence_map, args.runtime_source)
        _write_or_check(args.output, render_catalog(model), args.check)
        _write_or_check(args.report, render_report(model), args.check)
    except PlacementValidationError as exc:
        print(f"PF4582 placement import failed: {exc}", file=sys.stderr)
        return 1
    action = "validated" if args.check else "generated"
    print(
        f"PF4582 placements {action}: source={len(model['records'])} "
        f"runtimeEligible={sum(1 for record in model['records'] if record['RuntimeEligible'])}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
