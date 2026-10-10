"""Deterministic offline migration to two editable mission destination files.

Research inputs and migration checks stay outside runtime GameData. Historical
QL labels preserve every accepted QL pool; current-lookup differences are
reported, never silently applied to this migration.
"""
import argparse
from collections import defaultdict
import gzip
import hashlib
import io
import json
from pathlib import Path
import struct
import subprocess


ROOT = Path(__file__).resolve().parents[1]
SOURCE_SHA = "0721fa8844407bf6e1462341bf46d122bda44437"
DATA = "AORebirth/GameData/Missions/Destinations/"
DISTRIBUTION = "docs/generated/missions/destination-zone-distribution/"
OLD_FILES = ("MissionEntrancePlacements.json", "ObservedMissionDestinations.json",
             "MissionDestinationCatalogManifest.json", "MissionDestinationSelection.json")
RECEIPT = "docs/generated/missions/destination-runtime-migration/migration-receipt.json"
PHYSICAL_FIELDS = ("IdentityType", "IdentityInstance", "PlayfieldId", "DisplayName", "RawNameHex",
                   "LocalX", "LocalY", "LocalZ", "LocalXBits", "LocalYBits", "LocalZBits",
                   "RotationComponent0", "RotationComponent1", "RotationComponent2", "RotationComponent3")
WORLD_FIELDS = ("PlayfieldIdentityType", "WorldOffsetX", "WorldOffsetZ")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True, allow_nan=False).encode()


def document_bytes(value):
    return (json.dumps(value, indent=2, sort_keys=True, ensure_ascii=True, allow_nan=False) + "\n").encode()


def identity(row):
    return row["IdentityType"], row["IdentityInstance"]


def identity_rows(values):
    return [{"IdentityType": kind, "IdentityInstance": instance} for kind, instance in sorted(values)]


def index(rows, key, label):
    result = {}
    for row in rows:
        row_key = key(row)
        require(row_key not in result, "Duplicate " + label)
        result[row_key] = row
    return result


def project():
    input_hashes = {}

    def blob(path):
        value = subprocess.check_output(["git", "show", f"{SOURCE_SHA}:{path}"], cwd=ROOT)
        input_hashes[path] = digest(value)
        return value

    old_bytes = {name: blob(DATA + name) for name in OLD_FILES}
    old = {name: json.loads(value) for name, value in old_bytes.items()}
    manifest = old["MissionDestinationCatalogManifest.json"]
    for file in manifest["Files"]:
        require(digest(old_bytes[file["Path"]]) == file["Sha256"], "Pinned foundation hash mismatch")
    selection = old["MissionDestinationSelection.json"]
    for file in selection["Manifest"]["FoundationFiles"]:
        require(digest(old_bytes[file["Path"]]) == file["Sha256"], "Pinned selection foundation hash mismatch")
    require(digest(canonical(selection["Payload"])) == selection["Manifest"]["PayloadSha256"],
            "Pinned selection payload hash mismatch")
    payload = selection["Payload"]
    placements = index(old["MissionEntrancePlacements.json"]["Placements"], identity, "physical identity")
    observations = index(old["ObservedMissionDestinations.json"]["Destinations"], identity, "observed identity")
    world_positions = index(payload["WorldPositions"], identity, "captured WorldPos identity")
    require(len(placements) == 2242 and len(observations) == 812 and set(observations) == set(world_positions),
            "Pinned physical/observed/WorldPos population mismatch")
    require(set(world_positions) <= set(placements), "WorldPos identity lacks a physical placement")

    old_pools, old_terminal_pools = defaultdict(set), defaultdict(set)
    for condition in payload["Conditions"]:
        ql, terminal = condition["ExpectedMissionQl"], condition["TerminalPlayfieldId"]
        refs = {identity(row) for row in condition["DestinationIdentities"]}
        require(refs <= set(world_positions), "Old pool references an identity without captured WorldPos")
        old_pools[ql].update(refs)
        old_terminal_pools[(terminal, ql)].update(refs)

    distribution_manifest = json.loads(blob(DISTRIBUTION + "artifact-manifest.json"))
    ledger_bytes = blob(DISTRIBUTION + "offer-records.jsonl.gz")
    expected_ledger = next(row for row in distribution_manifest["files"] if row["path"] == "offer-records.jsonl.gz")
    require(digest(ledger_bytes) == expected_ledger["sha256"] and len(ledger_bytes) == expected_ledger["bytes"],
            "Completed distribution ledger hash/size mismatch")
    pools, current_pools = defaultdict(set), defaultdict(set)
    seen_offers, resolved, unresolved, changed_label_offers = set(), 0, 0, 0
    with gzip.GzipFile(fileobj=io.BytesIO(ledger_bytes)) as stream:
        for line in stream:
            row = json.loads(line)
            offer = tuple(row[field] for field in ("session_id", "source_line", "request_id", "cohort_id", "offer_index"))
            require(offer not in seen_offers, "Duplicate completed distribution offer")
            seen_offers.add(offer)
            if row["population"] == "NO_RAW_DESTINATION_UNRESOLVED":
                require(row["destination_identity"] is None, "Missing-raw offer was promoted")
                unresolved += 1
                continue
            require(row["population"] == "RAW_BACKED_EXACT_DESTINATION", "Unknown distribution population")
            resolved += 1
            ql, terminal = row["original_expected_ql"], row["terminal_playfield"]
            require(type(ql) is int and ql > 0 and type(terminal) is int and terminal > 0,
                    "Resolved observation lacks original QL/terminal geography")
            destination = row["destination_identity"]
            key = destination["type"], destination["instance_uint32"]
            require(key in world_positions, "Distribution identity lacks captured WorldPos")
            placement, world = placements[key], world_positions[key]
            require(row["destination_playfield"] == placement["PlayfieldId"]
                    and row["destination_xyz_bits"] == [placement[field] for field in ("LocalXBits", "LocalYBits", "LocalZBits")]
                    and row["destination_local_xyz"] == [placement[field] for field in ("LocalX", "LocalY", "LocalZ")]
                    and row["world_offsets_xz"] == [world["WorldOffsetX"], world["WorldOffsetZ"]],
                    "Completed observation does not match exact physical/WorldPos fields")
            pools[(terminal, ql)].add(key)
            current_pools[row["current_expected_ql"]].add(key)
            changed_label_offers += ql != row["current_expected_ql"]
    require(len(seen_offers) == 93185 and resolved == 92830 and unresolved == 355, "Distribution population mismatch")
    require(dict(pools) == dict(old_terminal_pools), "Original-label terminal pools differ from accepted runtime evidence")

    new_pools = defaultdict(set)
    for (_, ql), refs in pools.items():
        new_pools[ql].update(refs)
    require(dict(new_pools) == dict(old_pools) and len(new_pools) == 45, "Accepted expected-QL pools changed")
    require(set().union(*pools.values()) == set(world_positions), "Observed identity coverage changed")
    require(new_pools[25] == old_pools[25] and len(new_pools[25]) == 124 and (800, 25) not in pools,
            "Working Borealis QL25 union fallback changed")

    physical_rows = []
    for key, prior in sorted(placements.items()):
        current = {field: prior[field] for field in PHYSICAL_FIELDS}
        for axis in ("X", "Y", "Z"):
            value = struct.unpack(">I", struct.pack(">f", current["Local" + axis]))[0]
            require(value == prior["Local" + axis + "Bits"], "Physical coordinate representation changed")
        for component in range(4):
            field = "RotationComponent" + str(component)
            require(struct.pack(">f", current[field]) == struct.pack(">f", prior[field]), "Raw rotation component changed")
        current["WorldPos"] = {field: world_positions[key][field] for field in WORLD_FIELDS} if key in world_positions else None
        physical_rows.append(current)
    physical = {"SchemaVersion": 2, "CatalogKind": "MISSION_ENTRANCE_PLACEMENTS", "Placements": physical_rows}
    destinations = {"SchemaVersion": 1, "CatalogKind": "MISSION_DESTINATIONS", "Pools": [
        {"TerminalPlayfieldId": terminal, "ExpectedMissionQl": ql, "DestinationIdentities": identity_rows(refs)}
        for (terminal, ql), refs in sorted(pools.items())]}
    outputs = {DATA + "MissionEntrancePlacements.json": document_bytes(physical),
               DATA + "MissionDestinations.json": document_bytes(destinations)}

    ql_proofs = [{"ExpectedMissionQl": ql, "OldCount": len(old_pools[ql]), "NewCount": len(new_pools[ql]),
                 "OldIdentitySetSha256": digest(canonical(identity_rows(old_pools[ql]))),
                 "NewIdentitySetSha256": digest(canonical(identity_rows(new_pools[ql]))), "ExactSetPreserved": True}
                for ql in sorted(old_pools)]
    projection_differences = [{"ExpectedMissionQl": ql, "PreservedCount": len(old_pools[ql]),
                               "CurrentLookupProjectionCount": len(current_pools.get(ql, set())),
                               "RemovedByCurrentLookup": identity_rows(old_pools[ql] - current_pools.get(ql, set())),
                               "AddedByCurrentLookup": identity_rows(current_pools.get(ql, set()) - old_pools[ql])}
                              for ql in sorted(old_pools) if old_pools[ql] != current_pools.get(ql, set())]
    receipt = {"SchemaVersion": 1, "SourceCommit": SOURCE_SHA,
               "Migration": "TWO_EDITABLE_RUNTIME_DESTINATION_FILES",
               "QlProjection": "ORIGINAL_CAPTURE_EXPECTED_QL_PRESERVES_ACCEPTED_RUNTIME_POOLS",
               "QlProjectionReason": "Current-lookup regrouping would silently remove three identities from QL22; no such content change is authorized.",
               "Sources": [{"Path": path, "Sha256": value} for path, value in sorted(input_hashes.items())],
               "Generator": {"Path": "Tools/mission_destination_runtime_migration.py", "Sha256": digest(Path(__file__).read_bytes())},
               "Outputs": [{"Path": path, "Sha256": digest(value), "Bytes": len(value)} for path, value in sorted(outputs.items())],
               "Validation": {"ClientPlacements": len(physical_rows), "CapturedWorldPos": len(world_positions),
                              "NullWorldPos": len(physical_rows) - len(world_positions), "RetainedOffers": len(seen_offers),
                              "ResolvedOffers": resolved, "UnresolvedOffersExcluded": unresolved, "ExpectedQls": len(new_pools),
                              "TerminalQlPools": len(pools), "PhysicalFieldsPreserved": True, "WorldPosPreserved": True,
                              "AllQlPoolsPreserved": True, "AllTerminalQlPoolsPreserved": True,
                              "HistoricalQlLabelDifferenceOffers": changed_label_offers},
               "ExpectedQlPoolProofs": ql_proofs,
               "TerminalQlPoolProofs": [{"TerminalPlayfieldId": terminal, "ExpectedMissionQl": ql, "Count": len(refs),
                                         "IdentitySetSha256": digest(canonical(identity_rows(refs)))}
                                        for (terminal, ql), refs in sorted(pools.items())],
               "CurrentLookupProjectionDifferencesNotApplied": projection_differences,
               "TerminalGeographyExampleQl29": {"ExpectedMissionQl": 29, "PreviousQlWideCount": len(old_pools[29]),
                                               "PreservedQlWideFallbackCount": len(new_pools[29]),
                                               "PreferredTerminalPools": [{"TerminalPlayfieldId": terminal, "Count": len(refs)}
                                                                          for (terminal, ql), refs in sorted(pools.items()) if ql == 29]},
               "WorkingFallback": {"TerminalPlayfieldId": 800, "ExpectedMissionQl": 25,
                                   "TerminalPoolPresent": False, "QlWideFallbackCount": len(new_pools[25])},
               "Boundaries": ["Terminal geography is an explicit provisional selection policy, not a proven exclusion rule.",
                              "Fallback unions terminal pools at exactly the requested QL; no nearest-QL or unobserved entrance fallback.",
                              "Uniform distinct-identity selection with replacement remains a policy, not inferred retail weights.",
                              "Hashes, counts, capture conditions and source provenance stay in this offline receipt, outside runtime data.",
                              "Old selection, observed-metadata and manifest files are not deleted by this generator."]}
    outputs[RECEIPT] = document_bytes(receipt)
    return outputs, receipt["Validation"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["generate"])
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    outputs, validation = project()
    for relative, value in outputs.items():
        path = ROOT / relative
        if args.check:
            require(path.is_file() and path.read_bytes() == value, "Stale migration output: " + relative)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(value)
    print(json.dumps({"mode": "check" if args.check else "generate", "outputs": len(outputs), "validation": validation}, sort_keys=True))


if __name__ == "__main__":
    main()
