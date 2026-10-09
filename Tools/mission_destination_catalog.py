"""Build lookup-only mission destination content from the pinned evidence corpus."""
from __future__ import annotations

import argparse
from collections import defaultdict
import gzip
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE_EVIDENCE_SHA = "a259df4b118ee6074dd17c1da91d650d95fb5ea1"
SCHEMA_VERSION = 1
ACG = Path("docs/generated/missions/acgentrance-reconstruction")
ELIGIBILITY = Path("docs/generated/missions/destination-eligibility-analysis")
AUDIT = Path("docs/generated/missions/destination-duplicate-audit")
OUT = Path("AORebirth/GameData/Missions/Destinations")
PLACEMENTS_FILE = "MissionEntrancePlacements.json"
OBSERVED_FILE = "ObservedMissionDestinations.json"
MANIFEST_FILE = "MissionDestinationCatalogManifest.json"
CLIENT_CLASS = "CLIENT_ACGENTRANCE_PLACEMENT"
OBSERVED_CLASS = "OBSERVED_RANDOM_MISSION_DESTINATION"
UNOBSERVED_CLASS = "CLIENT_ACGENTRANCE_NOT_YET_OBSERVED_IN_RANDOM_MISSION_CAPTURE"
REPORTS = (
    "ACGENTRANCE_REGISTRY_AND_MISSION_DESTINATION_RECONSTRUCTION.md",
    "MISSION_DESTINATION_ELIGIBILITY_FROM_RESOLVED_CAPTURE_CORPUS.md",
    "MISSION_DESTINATION_DUPLICATE_AUDIT.md",
)


def serialize(value):
    return (json.dumps(value, sort_keys=True, ensure_ascii=True, separators=(",", ":"),
                       allow_nan=False) + "\n").encode("utf-8")


def file_hash(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_jsonl(path):
    opener = gzip.open if str(path).endswith(".gz") else open
    decoder = json.JSONDecoder()
    with opener(path, "rt", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            try:
                yield decoder.decode(line)
            except (ValueError, TypeError) as exc:
                raise ValueError(f"Invalid JSON at {path}:{line_number}") from exc


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def offer_key(row):
    return tuple(row[field] for field in
                 ("session_id", "source_line", "request_id", "cohort_id", "offer_index"))


def placement_identity(row):
    return row["IdentityType"], row["IdentityInstance"]


def coordinate_key(row):
    return row["PlayfieldId"], row["LocalXBits"], row["LocalYBits"], row["LocalZBits"]


def source_identity(value):
    if value is None:
        return None
    return value["type"], value.get("instance", value.get("instance_uint32"))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def exact_binary32_bytes(value):
    require(type(value) in (int, float) and math.isfinite(value), "Nonfinite or nonnumeric binary32 value")
    try:
        encoded = struct.pack("<f", value)
    except (OverflowError, struct.error) as error:
        raise ValueError("Value is outside binary32 range") from error
    require(struct.unpack("<f", encoded)[0] == value, "Value requires binary32 rounding")
    return encoded


def verify_evidence_sources(root):
    """Pin manifests to the requested commit, then verify every output they govern.

    No original client files, capture journals or private GameData are required.
    Hashing a historical artifact here does not promote all of its fields.
    """
    sources = {}
    manifests = (
        (ACG / "mission-location-evidence-manifest.json", "artifact_sha256"),
        (ELIGIBILITY / "mission-destination-eligibility-manifest.json", "generated_outputs"),
        (AUDIT / "duplicate-audit-manifest.json", "outputs"),
    )
    pinned = [path for path, _ in manifests]
    pinned += [ACG / "acgentrance-source-manifest.json"]
    pinned += [Path("docs/evidence") / name for name in REPORTS]
    for relative in pinned:
        path = root / relative
        expected = subprocess.check_output(
            ["git", "show", f"{SOURCE_EVIDENCE_SHA}:{relative.as_posix()}"], cwd=root)
        require(path.read_bytes() == expected, f"PINNED_EVIDENCE_CHANGED: {relative}")
        sources[relative.as_posix()] = hashlib.sha256(expected).hexdigest()
    for relative, field in manifests:
        for name, metadata in read_json(root / relative)[field].items():
            artifact = Path(name) if name.startswith("docs/") else relative.parent / name
            require(not artifact.is_absolute() and ".." not in artifact.parts,
                    f"Non-repository source path: {artifact}")
            expected = metadata["sha256"] if isinstance(metadata, dict) else metadata
            actual = file_hash(root / artifact)
            require(actual == expected, f"EVIDENCE_HASH_MISMATCH: {artifact}")
            sources[artifact.as_posix()] = actual
    return sources


def project_placement(row, observed=False):
    raw = bytes.fromhex(row["raw_transform_bytes"])
    require(len(raw) == 28, "Placement transform must contain seven binary32 values")
    values = struct.unpack("<7f", raw)
    bits = struct.unpack("<3I", raw[:12])
    require(all(math.isfinite(value) for value in values), "Nonfinite source transform")
    require(tuple(row["raw_position_components"]) == values[:3] and
            tuple(row["raw_rotation_components"]) == values[3:], "Contradictory source transform")
    require(row["operational_entrance_key"] is None and row["stat_0xBD_effective_value"] is None,
            "This evidence version has no resolved operational key or effective stat BD")
    require(row["raw_scale_components"] is None, "No scale is established by this evidence version")
    raw_name = bytes.fromhex(row["display_name_raw_bytes"])
    require(raw_name.decode("latin-1") == row["display_name_exact"], "Contradictory raw name")
    return {
        "IdentityType": row["identity_type"], "IdentityInstance": row["identity_instance_uint32"],
        "PlayfieldId": row["explicit_playfield_id"],
        "LocalX": values[0], "LocalY": values[1], "LocalZ": values[2],
        "LocalXBits": bits[0], "LocalYBits": bits[1], "LocalZBits": bits[2],
        **{f"RotationComponent{index}": values[index + 3] for index in range(4)},
        "DisplayName": row["display_name_exact"], "RawNameHex": raw_name.hex(),
        "NameProvenance": {
            "Encoding": row["text_encoding"], "ResolutionPath": row["name_resolution_path"],
            "ResourceType": row["string_resource_type"],
            "ResourceInstance": row["string_resource_instance"],
            "SourceOffset": row["string_source_offset"],
        },
        "TemplateInstance": row["template_identity_instance"],
        "SourcePlacementContainer": {"ResourceType": row["parent_resource_type"],
                                     "ResourceInstance": row["parent_resource_instance"]},
        "SourceRecordOffset": row["source_record_offset"],
        "SourceRecordLength": row["source_record_length"],
        "SourceRecordSha256": row["source_record_sha256"],
        "SourceDatabaseSha256": row["source_database_sha256"],
        "Classification": CLIENT_CLASS,
        "ObservationClassification": OBSERVED_CLASS if observed else UNOBSERVED_CLASS,
        "OperationalEntranceKey": None, "EffectiveStatBd": None,
    }


def aggregate_observations(root, placements):
    by_identity = {placement_identity(row): row for row in placements}
    require(len(by_identity) == len(placements), "Duplicate placement identity")
    metadata = {}
    fields = ("destination_identity", "population", "static_expected_mission_ql", "character_level",
              "mission_type", "terminal_playfield", "faction_side", "faction_side_raw")
    for row in read_jsonl(root / ELIGIBILITY / "mission-offer-analysis-inventory.jsonl.gz"):
        key = offer_key(row)
        require(key not in metadata, f"Duplicate eligibility offer key: {key}")
        metadata[key] = {field: row[field] for field in fields}
    groups = defaultdict(lambda: {"count": 0, "requests": set(), "cohorts": set(), "sessions": set(),
                                 "qls": set(), "levels": set(), "types": set(), "terminals": set(),
                                 "sides": set(), "side_values": set()})
    missing = 0
    total = 0
    for row in read_jsonl(root / AUDIT / "offer-audit-inventory.jsonl.gz"):
        key = offer_key(row)
        require(key in metadata, f"Unmatched or duplicate audit offer key: {key}")
        meta = metadata.pop(key)
        total += 1
        identity = source_identity(row["destination_identity"])
        require(identity == source_identity(meta["destination_identity"]), "Contradictory destination assignment")
        if not row["raw_byte_range_verified"]:
            require(identity is None and row["raw_offer_hash"] is None and
                    meta["population"] == "NO_RAW_DESTINATION_UNRESOLVED",
                    "Missing-raw evidence must not promote a destination")
            missing += 1
            continue
        require(identity in by_identity and row["raw_offer_hash"] and row["raw_response_sha256"] and
                row["offer_end"] > row["offer_start"] >= 51 and
                meta["population"] == "RAW_BACKED_EXACT_DESTINATION", "Incomplete raw-backed assignment")
        placement = by_identity[identity]
        coordinate_bits = struct.unpack(">3I", bytes.fromhex(row["destination_local_xyz_binary32_hex"]))
        require((row["destination_playfield"], *coordinate_bits) == coordinate_key(placement),
                "Audited destination does not match exact placement bits")
        for field in ("static_expected_mission_ql", "character_level", "mission_type", "terminal_playfield"):
            require(row[field] == meta[field], f"Contradictory observed metadata: {field}")
        require(meta["faction_side"] == "Omni" and meta["faction_side_raw"] == 2,
                "Unexpected faction evidence in the pinned corpus")
        group = groups[identity]
        group["count"] += 1
        group["requests"].add((row["session_id"], row["request_id"]))
        group["cohorts"].add((row["session_id"], row["source_line"], row["request_id"], row["cohort_id"]))
        group["sessions"].add(row["session_id"])
        group["qls"].add(meta["static_expected_mission_ql"])
        group["levels"].add(meta["character_level"])
        group["types"].add(meta["mission_type"])
        group["terminals"].add(source_identity(meta["terminal_playfield"])[1])
        group["sides"].add(meta["faction_side"])
        group["side_values"].add(meta["faction_side_raw"])
    require(not metadata, "Eligibility inventory contains offers absent from the audit")
    require((total, missing) == (93185, 355), "Pinned offer population counts changed")
    observations = []
    for identity, group in sorted(groups.items()):
        observations.append({
            "IdentityType": identity[0], "IdentityInstance": identity[1], "Classification": OBSERVED_CLASS,
            "ObservationCount": group["count"], "RequestCount": len(group["requests"]),
            "CohortCount": len(group["cohorts"]), "SessionCount": len(group["sessions"]),
            "ObservedExpectedMissionQls": sorted(group["qls"]),
            "ObservedCharacterLevels": sorted(group["levels"]),
            "ObservedMissionTypes": sorted(group["types"]),
            "ObservedTerminalPlayfields": sorted(group["terminals"]),
            "ObservedFactionSides": sorted(group["sides"]),
            "ObservedFactionSideValues": sorted(group["side_values"]),
            "FactionEvidenceClassification": "OBSERVED_WITH_OMNI",
        })
    aggregate = {placement_identity(row): row for row in observations}
    checked = set()
    for row in read_jsonl(root / AUDIT / "destination-observation-counts.jsonl.gz"):
        identity = source_identity(row["destination_identity"])
        require(identity not in checked and identity in aggregate, "Duplicate or unexpected destination aggregate")
        checked.add(identity)
        actual = aggregate[identity]
        for field, historical in (("ObservationCount", "observation_count"), ("RequestCount", "request_count"),
                                  ("CohortCount", "cohort_count"), ("SessionCount", "session_count"),
                                  ("ObservedExpectedMissionQls", "expected_qls")):
            require(actual[field] == row[historical], f"Destination aggregate mismatch: {identity} {field}")
    require(checked == set(aggregate), "Destination aggregate inventory is incomplete")
    return observations, missing


def validate_catalog(placements_doc, observed_doc, manifest=None, strict_counts=True):
    require(placements_doc["SchemaVersion"] == SCHEMA_VERSION and observed_doc["SchemaVersion"] == SCHEMA_VERSION,
            "Unsupported schema version")
    require(placements_doc["CatalogKind"] == "CLIENT_ACGENTRANCE_PLACEMENTS" and
            observed_doc["CatalogKind"] == "OBSERVED_RANDOM_MISSION_DESTINATIONS", "Contradictory catalog kind")
    placements, observed = placements_doc["Placements"], observed_doc["Destinations"]
    identities, coordinates = {}, set()
    for row in placements:
        identity, coordinate = placement_identity(row), coordinate_key(row)
        require(identity not in identities, "Duplicate complete placement identity")
        require(coordinate not in coordinates, "Ambiguous exact coordinate key")
        identities[identity] = row
        coordinates.add(coordinate)
        require(all(type(value) is int and 0 < value <= 0xFFFFFFFF for value in identity), "Invalid placement identity")
        require(type(row["PlayfieldId"]) is int and 0 < row["PlayfieldId"] <= 0x7FFFFFFF, "Invalid playfield")
        require(row["Classification"] == CLIENT_CLASS and row["ObservationClassification"] in
                (OBSERVED_CLASS, UNOBSERVED_CLASS), "Invalid evidence classification")
        require(row["OperationalEntranceKey"] is None and row["EffectiveStatBd"] is None,
                "Fabricated operational entrance key or effective stat BD")
        for axis in "XYZ":
            value, bits = row["Local" + axis], row["Local" + axis + "Bits"]
            require(type(bits) is int and 0 <= bits <= 0xFFFFFFFF, "Invalid coordinate bits")
            require(exact_binary32_bytes(value) == struct.pack("<I", bits),
                    "Contradictory coordinate value/bits")
        for index in range(4):
            exact_binary32_bytes(row[f"RotationComponent{index}"])
        require(bytes.fromhex(row["RawNameHex"]).decode("latin-1") == row["DisplayName"], "Contradictory name bytes")
    observed_ids = set()
    observed_playfields = set()
    for row in observed:
        identity = placement_identity(row)
        require(identity not in observed_ids and identity in identities, "Duplicate or absent observed identity")
        observed_ids.add(identity)
        observed_playfields.add(identities[identity]["PlayfieldId"])
        require(row["Classification"] == OBSERVED_CLASS, "Invalid observed classification")
        require(0 < row["SessionCount"] <= row["RequestCount"] <= row["ObservationCount"] and
                row["RequestCount"] == row["CohortCount"], "Invalid observation counts")
        for field in ("ObservedExpectedMissionQls", "ObservedCharacterLevels", "ObservedMissionTypes",
                      "ObservedTerminalPlayfields", "ObservedFactionSides", "ObservedFactionSideValues"):
            require(row[field] and row[field] == sorted(set(row[field])), f"Invalid observed metadata set: {field}")
        require(row["ObservedFactionSides"] == ["Omni"] and row["ObservedFactionSideValues"] == [2] and
                row["FactionEvidenceClassification"] == "OBSERVED_WITH_OMNI", "Invalid faction evidence")
        require(all(type(value) is int and 1 <= value <= 250 for value in row["ObservedExpectedMissionQls"]),
                "Invalid expected mission QL")
    for identity, row in identities.items():
        require((row["ObservationClassification"] == OBSERVED_CLASS) == (identity in observed_ids),
                "Contradictory placement observation classification")
    counts = {"FullPlacementCount": len(placements), "ObservedDestinationCount": len(observed),
              "ObservedPlayfieldCount": len(observed_playfields),
              "UnobservedPlacementCount": len(placements) - len(observed),
              "RawBackedObservationCount": sum(row["ObservationCount"] for row in observed)}
    if strict_counts:
        require(counts == {"FullPlacementCount": 2242, "ObservedDestinationCount": 812,
                           "ObservedPlayfieldCount": 22, "UnobservedPlacementCount": 1430,
                           "RawBackedObservationCount": 92830}, "Catalog evidence counts changed")
    if manifest is not None:
        require(manifest["SchemaVersion"] == SCHEMA_VERSION and
                manifest["SourceEvidenceCommit"] == SOURCE_EVIDENCE_SHA, "Invalid manifest evidence version")
        require(all(manifest[field] == count for field, count in counts.items()), "Manifest row counts mismatch")
        require(manifest["DestinationUniquenessWithinCohortRequired"] is False and
                manifest["OperationalEntranceKeysResolved"] == 0 and
                manifest["MissingRawOffersPromoted"] == 0, "Invalid manifest evidence boundary")
        if strict_counts:
            require(manifest["MissingRawOffersExcluded"] == 355, "Missing-raw count mismatch")
        documents = {PLACEMENTS_FILE: placements_doc, OBSERVED_FILE: observed_doc}
        require(len(manifest["Files"]) == 2, "Unexpected catalog file count")
        require({item["Path"] for item in manifest["Files"]} == set(documents), "Invalid catalog file paths")
        for item in manifest["Files"]:
            document = documents[item["Path"]]
            rows = document["Placements"] if item["Path"] == PLACEMENTS_FILE else document["Destinations"]
            require(item["RowCount"] == len(rows) and
                    item["Sha256"] == hashlib.sha256(serialize(document)).hexdigest(), "Catalog content hash mismatch")
    return counts


def build_catalog(root=ROOT):
    root = Path(root)
    sources = verify_evidence_sources(root)
    summary = read_json(root / AUDIT / "duplicate-audit-summary.json")
    for field in ("ISSUE_COUNT", "PARSER_DUPLICATION_BUGS", "PROPOSED_REMOVABLE_OFFER_POSITIONS",
                  "DUPLICATE_COMPLETE_PLACEMENT_IDENTITIES", "EXACT_SAME_PLAYFIELD_XYZ_COLLISIONS"):
        require(summary[field] == 0, f"Contradictory duplicate audit: {field}")
    placements = sorted((project_placement(row) for row in read_jsonl(root / ACG / "acgentrance-records.jsonl")),
                        key=placement_identity)
    observations, missing = aggregate_observations(root, placements)
    observed_ids = {placement_identity(row) for row in observations}
    for row in placements:
        if placement_identity(row) in observed_ids:
            row["ObservationClassification"] = OBSERVED_CLASS
    placements_doc = {"SchemaVersion": SCHEMA_VERSION, "CatalogKind": "CLIENT_ACGENTRANCE_PLACEMENTS",
                      "Placements": placements}
    observed_doc = {"SchemaVersion": SCHEMA_VERSION, "CatalogKind": "OBSERVED_RANDOM_MISSION_DESTINATIONS",
                    "Destinations": observations}
    counts = validate_catalog(placements_doc, observed_doc)
    documents = {PLACEMENTS_FILE: placements_doc, OBSERVED_FILE: observed_doc}
    manifest = {"SchemaVersion": SCHEMA_VERSION, "SourceEvidenceCommit": SOURCE_EVIDENCE_SHA, **counts,
                "MissingRawOffersExcluded": missing, "MissingRawOffersPromoted": 0,
                "DestinationUniquenessWithinCohortRequired": False, "OperationalEntranceKeysResolved": 0,
                "Files": [{"Path": name, "Sha256": hashlib.sha256(serialize(document)).hexdigest(),
                           "RowCount": len(document.get("Placements", document.get("Destinations")))}
                          for name, document in sorted(documents.items())],
                "Sources": [{"Path": path, "Sha256": digest} for path, digest in sorted(sources.items())],
                "Generator": {"Path": "Tools/mission_destination_catalog.py",
                              "Sha256": file_hash(root / "Tools/mission_destination_catalog.py")}}
    validate_catalog(placements_doc, observed_doc, manifest)
    for path, expected in sources.items():
        require(file_hash(root / path) == expected, f"EVIDENCE_CHANGED_DURING_GENERATION: {path}")
    documents[MANIFEST_FILE] = manifest
    return documents


def write_or_check(path, data, check=False):
    path = Path(path)
    if check:
        require(path.exists() and path.read_bytes() == data, f"STALE_ARTIFACT: {path}")
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


def generate(check=False, root=ROOT):
    documents = build_catalog(root)
    for name, document in sorted(documents.items()):
        write_or_check(Path(root) / OUT / name, serialize(document), check)
    manifest = documents[MANIFEST_FILE]
    result = {key: manifest[key] for key in ("FullPlacementCount", "ObservedDestinationCount",
              "ObservedPlayfieldCount", "UnobservedPlacementCount", "RawBackedObservationCount",
              "MissingRawOffersExcluded", "MissingRawOffersPromoted")}
    result["GenerateCheck"] = "PASS" if check else "GENERATED"
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("generate", "test"))
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    if args.command == "test":
        require(not args.check, "--check applies only to generate")
        suite = unittest.defaultTestLoader.discover(str(ROOT / "Tools"), "test_mission_destination_catalog.py")
        if not unittest.TextTestRunner(verbosity=2).run(suite).wasSuccessful():
            return 1
        return subprocess.call(["cmd", "/d", "/c", "Tools\\MissionDestinationCatalog.Tests\\run-tests.cmd"], cwd=ROOT)
    print(json.dumps(generate(args.check), sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, KeyError, TypeError, subprocess.CalledProcessError) as error:
        print(f"MISSION_DESTINATION_CATALOG_FAILED: {error}", file=sys.stderr)
        sys.exit(1)
