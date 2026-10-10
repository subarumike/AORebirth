"""Project exact captured conditions and WorldPos into the existing destination catalog.

Offline only. Reads immutable repository Git blobs; runtime reads the compact output.
Selection sets retain joint conditions, never Cartesian products or frequency weights.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import gzip
import hashlib
import json
from pathlib import Path
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE_SHA = "f07bb3c1a99218433e7d459c95ba29ccb22c36b2"
DATA = "AORebirth/GameData/Missions/Destinations/"
FOUNDATION = ("MissionDestinationCatalogManifest.json", "MissionEntrancePlacements.json", "ObservedMissionDestinations.json")
OUTPUT = DATA + "MissionDestinationSelection.json"
GENERATOR = "Tools/mission_destination_selection.py"
ELIGIBILITY = "docs/generated/missions/destination-eligibility-analysis/mission-offer-analysis-inventory.jsonl.gz"
OFFERS = "docs/generated/missions/destination-duplicate-audit/offer-audit-inventory.jsonl.gz"
REQUESTS = "docs/generated/missions/destination-duplicate-audit/request-audit-inventory.jsonl.gz"
SLIDERS = ("good_bad", "order_chaos", "open_hidden", "physical_mystical", "headon_stealth", "credits_xp")
MISSION_TYPES = {"FIND_ITEM", "FIND_PERSON", "KILL_PERSON", "REPAIR", "RETURN_ITEM"}
POLICY = "UNIFORM_DISTINCT_DESTINATIONS_WITH_REPLACEMENT_NOT_RETAIL_WEIGHTED"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical(value: object) -> bytes:
    # Payload has only ASCII identifiers, integers and arrays. Alphabetical keys
    # match the catalog DataContractJsonSerializer's default member order exactly.
    return json.dumps(value, sort_keys=True, ensure_ascii=True, separators=(",", ":"), allow_nan=False).encode("ascii")


def blob(path: str) -> bytes:
    return subprocess.check_output(["git", "show", EVIDENCE_SHA + ":" + path], cwd=ROOT)


def offer_key(row: dict) -> tuple:
    return tuple(row[k] for k in ("session_id", "source_line", "request_id", "cohort_id", "offer_index"))


def indexed(rows: list[dict], key, label: str) -> dict:
    result = {}
    for row in rows:
        identity = key(row)
        require(identity not in result, "Duplicate " + label + ": " + str(identity))
        result[identity] = row
    return result


def identity(row: dict, instance: str = "IdentityInstance", type_name: str = "IdentityType") -> tuple[int, int]:
    return row[type_name], row[instance]


def project() -> tuple[bytes, dict]:
    foundation = {}
    for name in FOUNDATION:
        expected = blob(DATA + name)
        actual = (ROOT / DATA / name).read_bytes()
        require(actual == expected, "Foundation payload changed from pinned evidence: " + name)
        foundation[name] = expected
    placement_document = json.loads(foundation["MissionEntrancePlacements.json"])
    placements = indexed(placement_document["Placements"], identity, "placement identity")
    require(len(placements) == 2242, "Expected 2242 exact client placements")
    coordinate_keys = {(r["PlayfieldId"], r["LocalXBits"], r["LocalYBits"], r["LocalZBits"]) for r in placements.values()}
    require(len(coordinate_keys) == 2242, "Exact placement coordinate collision")
    observed_document = json.loads(foundation["ObservedMissionDestinations.json"])
    observed = indexed(observed_document["Destinations"], identity, "observed identity")
    require(len(observed) == 812 and set(observed) <= set(placements), "Expected 812 observed catalog identities")

    inputs = {path: blob(path) for path in (ELIGIBILITY, OFFERS, REQUESTS)}
    def read_rows(path: str) -> list[dict]:
        return [json.loads(line) for line in gzip.decompress(inputs[path]).splitlines()]
    eligibility = indexed(read_rows(ELIGIBILITY), offer_key, "eligibility offer")
    offers = indexed(read_rows(OFFERS), offer_key, "audited offer")
    requests = indexed(read_rows(REQUESTS), lambda r: r["request_id"], "request")
    require(len(offers) == 93185 and set(offers) == set(eligibility), "Complete offer provenance join mismatch")
    require(len(requests) == 18642, "Request inventory count mismatch")

    condition_sets = defaultdict(set)
    world_positions = {}
    identity_observations = Counter()
    condition_rows = {}
    request_conditions = set()
    exact = missing = 0
    for key, offer in offers.items():
        row = eligibility[key]
        if not offer["raw_byte_range_verified"]:
            require(offer["destination_identity"] is None and row["destination_identity"] is None,
                    "Missing raw offer was promoted")
            missing += 1
            continue
        require(row["population"] == "RAW_BACKED_EXACT_DESTINATION", "Raw offer population mismatch")
        require(offer["offer_start"] < offer["offer_end"] and len(offer["raw_offer_hash"]) == 64,
                "Missing audited raw offer ownership")
        selected = identity(offer["destination_identity"], "instance", "type")
        require(selected == identity(row["destination_identity"], "instance_uint32", "type") and selected in observed,
                "Complete destination identity join mismatch")
        placement = placements[selected]
        world = offer["destination_worldpos"]
        raw = bytes.fromhex(world["raw_hex"])
        require(len(raw) == 28, "WorldPos must contain the proven 28 bytes")
        pf_type, pf, offset_x, offset_z, x_bits, y_bits, z_bits = struct.unpack(">IIiiIII", raw)
        require((pf_type, pf) == (world["playfield_identity"]["type"], world["playfield_identity"]["instance"])
                and pf_type == 40016 and pf == placement["PlayfieldId"] == row["destination_playfield"] == offer["destination_playfield"],
                "WorldPos playfield mismatch")
        require((x_bits, y_bits, z_bits) == (placement["LocalXBits"], placement["LocalYBits"], placement["LocalZBits"]),
                "WorldPos does not exactly resolve the selected placement")
        require(raw[16:].hex() == world["local_float32_hex"] == offer["destination_local_xyz_binary32_hex"],
                "WorldPos raw coordinate representation mismatch")
        expected_xyz = struct.unpack(">fff", raw[16:])
        require(tuple(world["local_position"]) == tuple(offer["destination_local_xyz"]) == tuple(row["destination_local_xyz"]) == expected_xyz,
                "WorldPos local coordinate mismatch")
        require([offset_x, offset_z] == world["playfield_origin_integer_xz"] == row["destination_world_offsets_xz"],
                "WorldPos offset mismatch")
        position = (pf_type, offset_x, offset_z)
        require(selected not in world_positions or world_positions[selected] == position, "Conflicting per-identity WorldPos offsets")
        world_positions[selected] = position

        request = requests[row["request_id"]]
        require(len(request["contexts"]) == 1 and request["header_count"] == 1, "Ambiguous request condition")
        context = request["contexts"][0]
        require(row["character_level"] == offer["character_level"], "Audited character level mismatch")
        for field in ("character_level", "breed_raw", "profession_raw", "faction_side_raw"):
            require(context["character_metadata"][field] == row[field] and type(row[field]) is int,
                    "Request/offer metadata mismatch: " + field)
        require(context["expected_ql"] == row["static_expected_mission_ql"] == offer["static_expected_mission_ql"]
                and row["analysis_mission_ql_source"] == "STATIC_EXPECTED_MISSION_QL", "Expected QL provenance mismatch")
        require(row["live_decoded_mission_ql"] is None, "Unexpected live QL promotion")
        require(row["raw_sliders"] == context["sliders"] == offer["raw_sliders"], "Raw slider mismatch")
        require(row["difficulty_detent"] == row["raw_sliders"]["difficulty"], "Difficulty mismatch")
        terminal = row["mission_terminal_identity"]
        terminal_pf = row["terminal_playfield"]["instance_uint32"]
        require(terminal == offer["mission_terminal_identity"]
                and terminal["type"] == context["terminal_identity"]["type"]
                and terminal["instance_uint32"] == context["terminal_identity"]["instance"] & 0xffffffff
                and terminal_pf == context["terminal_playfield"]["instance"] & 0xffffffff
                and row["terminal_playfield"] == offer["terminal_playfield"], "Terminal condition mismatch")
        sliders = tuple(row["raw_sliders"][s] for s in SLIDERS)
        require(all(type(s) is int and 0 <= s <= 255 for s in sliders), "Invalid raw slider byte")
        mission_type = row["mission_type"]
        require(mission_type in MISSION_TYPES and mission_type == offer["mission_type"], "Mission type mismatch")
        condition = {
            "CharacterLevel": row["character_level"], "ExpectedMissionQl": row["static_expected_mission_ql"],
            "DifficultyDetent": row["difficulty_detent"], "FactionSide": row["faction_side_raw"],
            "Breed": row["breed_raw"], "Profession": row["profession_raw"],
            "TerminalPlayfieldId": terminal_pf, "TerminalIdentityType": terminal["type"],
            "TerminalIdentityInstance": terminal["instance_uint32"], "SecondarySliderBytes": list(sliders),
            "MissionType": mission_type,
        }
        condition_key = canonical(condition)
        condition_rows[condition_key] = condition
        condition_sets[condition_key].add(selected)
        request_conditions.add(canonical({k: v for k, v in condition.items() if k != "MissionType"}))
        identity_observations[selected] += 1
        exact += 1

    require(exact == 92830 and missing == 355, "Raw-backed population mismatch")
    require(set(world_positions) == set(observed) and len(world_positions) == 812, "WorldPos observation coverage mismatch")
    require(all(identity_observations[k] == r["ObservationCount"] for k, r in observed.items()), "Per-identity observation mismatch")
    require(len(condition_sets) == 547 and len(request_conditions) == 180, "Joint captured condition count mismatch")
    require(sum(map(len, condition_sets.values())) == 25296, "Joint condition/destination association count mismatch")
    conditions = []
    for key in sorted(condition_rows):
        conditions.append({**condition_rows[key], "DestinationIdentities": [
            {"IdentityType": t, "IdentityInstance": i} for t, i in sorted(condition_sets[key])
        ]})
    positions = [
        {"IdentityType": t, "IdentityInstance": i, "PlayfieldIdentityType": values[0],
         "WorldOffsetX": values[1], "WorldOffsetZ": values[2]}
        for (t, i), values in sorted(world_positions.items())
    ]
    payload = {"SchemaVersion": 1, "CatalogKind": "CAPTURED_MISSION_DESTINATION_SELECTION", "SelectionPolicy": POLICY,
               "RawBackedObservationCount": exact, "WorldPositions": positions, "Conditions": conditions}
    manifest = {
        "SchemaVersion": 1, "SourceEvidenceCommit": EVIDENCE_SHA, "PayloadSha256": sha256(canonical(payload)),
        "FoundationFiles": [{"Path": name, "Sha256": sha256(foundation[name])} for name in sorted(foundation)],
        "Sources": [{"Path": path, "Sha256": sha256(inputs[path])} for path in sorted(inputs)],
        "Generator": {"Path": GENERATOR, "Sha256": sha256((ROOT / GENERATOR).read_bytes())},
    }
    stats = {"raw_backed_observations": exact, "excluded_missing_raw": missing, "world_positions": len(positions),
             "request_conditions": len(request_conditions), "condition_type_buckets": len(conditions),
             "condition_destination_associations": sum(map(len, condition_sets.values()))}
    return canonical({"Manifest": manifest, "Payload": payload}) + b"\n", stats


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("generate",))
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content, stats = project()
    target = ROOT / OUTPUT
    if args.check:
        require(target.read_bytes() == content, "Stale captured destination selection content")
    else:
        target.write_bytes(content)
    print(json.dumps({"result": "PASS", "mode": "check" if args.check else "generate", "output": OUTPUT,
                      "sha256": sha256(content), **stats}, sort_keys=True))


if __name__ == "__main__":
    main()
