#!/usr/bin/env python3
"""Decode generic NPC corpse/lifecycle evidence from an AOSharpLiveCapture folder."""

import argparse
import csv
import datetime as dt
import json
import math
import re
import struct
from pathlib import Path


CORPSE_FULL_UPDATE = 0x4F474E05
MONSTER_DATA_SUFFIX_OFFSET = 72
TAIL_DEAD_NPC_TYPE_SUFFIX_OFFSET = 80
TAIL_DEAD_NPC_INSTANCE_SUFFIX_OFFSET = 84

PACKET_RE = re.compile(
    r"^(?P<timestamp>\S+)\s+(?P<direction>IN|OUT)\s+#(?P<sequence>\d+)\s+"
    r"len=(?P<length>\d+)\s+n3=(?P<message>\S+)\s+hex=(?P<hex>[0-9A-Fa-f]+)$"
)

CSV_HEADER = [
    "CapturedUtc", "Direction", "Sequence", "ReceiverInstance", "CorpseType",
    "CorpseInstance", "CorpseIdentity", "CorpseName", "PlayfieldId", "PositionX",
    "PositionY", "PositionZ", "MonsterScale", "Sex", "Breed", "Race",
    "DeadNpcType", "DeadNpcInstance", "DeadNpcIdentity", "DeadNpcName",
    "CorpseCatMesh", "CorpseCredits", "CorpseMonsterData", "TailDeadNpcType",
    "TailDeadNpcInstance", "TailDeadNpcIdentity", "PacketLength", "RawHex",
]

RESPAWN_CSV_HEADER = [
    "GeneratedUtc", "Status", "DeathIdentity", "Name", "MonsterData",
    "NpcFamily", "DeathUtc", "DeathX", "DeathY", "DeathZ",
    "CorpseIdentity", "CorpseSeenUtc", "RespawnIdentity", "RespawnUtc",
    "RespawnDelaySeconds", "RespawnX", "RespawnY", "RespawnZ",
    "PositionDelta", "ElapsedAfterDeathSeconds", "CandidateCount", "Detail",
]

RESPAWN_CANDIDATE_EVENTS = {"spawn", "population", "respawn"}
SAME_POSITION_THRESHOLD = 2.0


def u32(data, offset):
    return struct.unpack_from(">I", data, offset)[0]


def i32(data, offset):
    return struct.unpack_from(">i", data, offset)[0]


def f32(data, offset):
    return struct.unpack_from(">f", data, offset)[0]


def identity(identity_type, instance):
    names = {0xC350: "SimpleChar", 0xC76A: "Corpse"}
    return f"({names.get(identity_type, f'0x{identity_type:08X}')}:{instance:08X})"


def decode_corpse_full_update(match):
    raw_hex = match.group("hex").upper()
    data = bytes.fromhex(raw_hex)
    if len(data) < 231 or u32(data, 16) != CORPSE_FULL_UPDATE:
        raise ValueError("packet is not a CorpseFullUpdate")

    name_offset = data.find(b"Remains of ")
    if name_offset < 4:
        raise ValueError("CorpseFullUpdate has no encoded Remains name marker")
    encoded_name_length = i32(data, name_offset - 4)
    suffix_offset = name_offset + encoded_name_length
    monster_data_offset = suffix_offset + MONSTER_DATA_SUFFIX_OFFSET
    tail_type_offset = suffix_offset + TAIL_DEAD_NPC_TYPE_SUFFIX_OFFSET
    tail_instance_offset = suffix_offset + TAIL_DEAD_NPC_INSTANCE_SUFFIX_OFFSET
    if (
        encoded_name_length <= 0
        or suffix_offset > len(data)
        or monster_data_offset < suffix_offset
        or tail_instance_offset + 4 > len(data)
    ):
        raise ValueError(
            f"invalid layout len={len(data)} nameLength={encoded_name_length} "
            f"monsterOffset={monster_data_offset} tailOffset={tail_instance_offset}"
        )

    corpse_type = u32(data, 20)
    corpse_instance = u32(data, 24)
    dead_npc_type = u32(data, 183)
    dead_npc_instance = u32(data, 191)
    tail_type = u32(data, tail_type_offset)
    tail_instance = u32(data, tail_instance_offset)
    corpse_name = data[name_offset:name_offset + encoded_name_length].rstrip(b"\0").decode("ascii")

    return [
        match.group("timestamp"),
        match.group("direction"),
        int(match.group("sequence")),
        u32(data, 12),
        f"0x{corpse_type:08X}",
        f"0x{corpse_instance:08X}",
        identity(corpse_type, corpse_instance),
        corpse_name,
        i32(data, 73),
        format(f32(data, 45), ".9g"),
        format(f32(data, 49), ".9g"),
        format(f32(data, 53), ".9g"),
        i32(data, 143),
        i32(data, 159),
        i32(data, 167),
        i32(data, 175),
        f"0x{dead_npc_type:08X}",
        f"0x{dead_npc_instance:08X}",
        identity(dead_npc_type, dead_npc_instance),
        "",
        i32(data, 199),
        i32(data, 207),
        i32(data, monster_data_offset),
        f"0x{tail_type:08X}",
        f"0x{tail_instance:08X}",
        identity(tail_type, tail_instance),
        len(data),
        raw_hex,
    ]


def count_event_lines(events_path):
    counts = {
        "deathActions": 0,
        "corpseSeen": 0,
        "corpseGone": 0,
        "corpseUses": 0,
        "lootMoveRequests": 0,
        "lootMoveResults": 0,
        "corpseDespawns": 0,
    }
    if not events_path.exists():
        return counts

    for line in events_path.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        counts["deathActions"] += "Action=99" in line
        counts["corpseSeen"] += "[CORPSE-SEEN]" in line
        counts["corpseGone"] += "[CORPSE-GONE]" in line
        counts["corpseUses"] += "Target=(Corpse:" in line and "GenericCmd" in line
        counts["lootMoveRequests"] += "ClientMoveItemToInventory" in line
        counts["lootMoveResults"] += "ContainerAddItem" in line
        counts["corpseDespawns"] += "type=Despawn identity=(Corpse:" in line
    return counts


def count_corpse_inventory_updates(path):
    if not path.exists():
        return 0
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return sum(
            1
            for row in csv.DictReader(handle)
            if row.get("InventoryIdentity", "").startswith("(Corpse:")
        )


def parse_timestamp(value):
    if not value:
        return None
    try:
        return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None


def parse_float(value):
    if value in (None, ""):
        return None
    try:
        return float(value)
    except ValueError:
        return None


def position(row):
    x = parse_float(row.get("x"))
    y = parse_float(row.get("y"))
    z = parse_float(row.get("z"))
    if x is None or y is None or z is None:
        return None
    return x, y, z


def position_delta(first, second):
    first_pos = position(first)
    second_pos = position(second)
    if first_pos is None or second_pos is None:
        return None
    return math.dist(first_pos, second_pos)


def load_enemy_state_rows(path):
    rows = []
    skipped = 0
    if not path.exists():
        return rows, skipped

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            if (
                row.get("timestamp")
                and row.get("entityId")
                and row.get("eventType")
                and parse_timestamp(row.get("timestamp")) is not None
            ):
                rows.append(row)
            else:
                skipped += 1
    rows.sort(key=lambda row: parse_timestamp(row["timestamp"]))
    return rows, skipped


def load_enemy_profiles(path):
    profiles = {}
    if not path.exists():
        return profiles

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            identity_value = row.get("Identity", "")
            if not identity_value:
                continue
            profiles[identity_value] = {
                "Name": row.get("Name", ""),
                "MonsterData": row.get("MonsterData", ""),
                "NpcFamily": row.get("NPCFamily", ""),
            }
    return profiles


def load_corpse_by_dead_npc(path):
    corpses = {}
    if not path.exists():
        return corpses

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            dead_identity = row.get("DeadNpcIdentity", "")
            if not dead_identity:
                continue
            corpses[dead_identity] = {
                "CorpseIdentity": row.get("CorpseIdentity", ""),
                "CorpseSeenUtc": row.get("CapturedUtc", ""),
            }
    return corpses


def same_profile(dead_profile, candidate_profile):
    required_fields = ("Name", "MonsterData", "NpcFamily")
    return all(
        dead_profile.get(field)
        and candidate_profile.get(field)
        and dead_profile.get(field) == candidate_profile.get(field)
        for field in required_fields
    )


def build_enemy_respawns(capture, corpse_csv):
    state_rows, skipped_state_rows = load_enemy_state_rows(capture / "enemy-state.csv")
    profiles = load_enemy_profiles(capture / "enemy-full-updates.csv")
    corpses = load_corpse_by_dead_npc(corpse_csv)
    capture_end = parse_timestamp(state_rows[-1]["timestamp"]) if state_rows else None
    generated_utc = dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")
    observations = []

    for death in [row for row in state_rows if row.get("eventType") == "death"]:
        dead_identity = death.get("entityId", "")
        dead_profile = profiles.get(dead_identity, {})
        death_time = parse_timestamp(death.get("timestamp"))
        if death_time is None:
            continue

        candidates = []
        for candidate in state_rows:
            candidate_time = parse_timestamp(candidate.get("timestamp"))
            if candidate_time is None or candidate_time <= death_time:
                continue
            if candidate.get("entityId") == dead_identity:
                continue
            if candidate.get("eventType") not in RESPAWN_CANDIDATE_EVENTS:
                continue
            candidate_profile = profiles.get(candidate.get("entityId", ""), {})
            if not same_profile(dead_profile, candidate_profile):
                continue
            delta = position_delta(death, candidate)
            if delta is None or delta > SAME_POSITION_THRESHOLD:
                continue
            candidates.append((candidate_time, delta, candidate))

        candidates.sort(key=lambda item: item[0])
        selected = candidates[0] if candidates else None
        status = "complete" if selected else "incomplete"
        if len(candidates) > 1 and (candidates[1][0] - selected[0]).total_seconds() <= 2.0:
            status = "ambiguous"

        corpse = corpses.get(dead_identity, {})
        selected_time = selected[0] if selected else None
        selected_delta = selected[1] if selected else None
        selected_row = selected[2] if selected else {}
        elapsed_end = capture_end or death_time
        observations.append({
            "GeneratedUtc": generated_utc,
            "Status": status,
            "DeathIdentity": dead_identity,
            "Name": dead_profile.get("Name", ""),
            "MonsterData": dead_profile.get("MonsterData", ""),
            "NpcFamily": dead_profile.get("NpcFamily", ""),
            "DeathUtc": death.get("timestamp", ""),
            "DeathX": death.get("x", ""),
            "DeathY": death.get("y", ""),
            "DeathZ": death.get("z", ""),
            "CorpseIdentity": corpse.get("CorpseIdentity", ""),
            "CorpseSeenUtc": corpse.get("CorpseSeenUtc", ""),
            "RespawnIdentity": selected_row.get("entityId", "") if selected else "",
            "RespawnUtc": selected_row.get("timestamp", "") if selected else "",
            "RespawnDelaySeconds": (
                f"{max(0, (selected_time - death_time).total_seconds()):.3f}"
                if selected_time
                else ""
            ),
            "RespawnX": selected_row.get("x", "") if selected else "",
            "RespawnY": selected_row.get("y", "") if selected else "",
            "RespawnZ": selected_row.get("z", "") if selected else "",
            "PositionDelta": f"{selected_delta:.3f}" if selected_delta is not None else "",
            "ElapsedAfterDeathSeconds": f"{max(0, (elapsed_end - death_time).total_seconds()):.3f}",
            "CandidateCount": str(len(candidates)),
            "Detail": (
                "Matched later same-name/same-monsterData/same-position spawn."
                if selected
                else "No later same-name/same-monsterData/same-position spawn was observed before capture stop/crash."
            ),
        })

    output_csv = capture / "enemy-respawns.csv"
    with output_csv.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=RESPAWN_CSV_HEADER)
        writer.writeheader()
        writer.writerows(observations)

    return {
        "outputCsv": output_csv,
        "rows": len(observations),
        "completeRows": sum(1 for row in observations if row["Status"] == "complete"),
        "ambiguousRows": sum(1 for row in observations if row["Status"] == "ambiguous"),
        "incompleteRows": sum(1 for row in observations if row["Status"] == "incomplete"),
        "skippedStateRows": skipped_state_rows,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("capture_folder", type=Path)
    args = parser.parse_args()

    capture = args.capture_folder.resolve()
    packets_path = capture / "packets.hex.log"
    if not packets_path.exists():
        raise SystemExit(f"missing {packets_path}")

    rows = []
    errors = []
    for line_number, line in enumerate(
        packets_path.read_text(encoding="utf-8-sig", errors="replace").splitlines(), 1
    ):
        match = PACKET_RE.match(line)
        if not match or match.group("message") != "CorpseFullUpdate":
            continue
        try:
            rows.append(decode_corpse_full_update(match))
        except Exception as exc:
            errors.append({"line": line_number, "error": str(exc)})

    output_csv = capture / "corpse-full-updates.csv"
    with output_csv.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(CSV_HEADER)
        writer.writerows(rows)

    event_counts = count_event_lines(capture / "events.log")
    corpse_inventory_updates = count_corpse_inventory_updates(capture / "inventory-updates.csv")
    respawn_summary = build_enemy_respawns(capture, output_csv)
    corpse_evidence_observed = bool(
        rows
        or event_counts["corpseSeen"]
        or event_counts["corpseUses"]
        or corpse_inventory_updates
    )
    processing_allowed = not errors and (not corpse_evidence_observed or bool(rows))
    summary = {
        "captureFolder": str(capture),
        "corpseFullUpdateRows": len(rows),
        "corpseFullUpdateDecodeErrors": errors,
        "corpseInventoryUpdateRows": corpse_inventory_updates,
        "enemyRespawnRows": respawn_summary["rows"],
        "enemyRespawnCompleteRows": respawn_summary["completeRows"],
        "enemyRespawnAmbiguousRows": respawn_summary["ambiguousRows"],
        "enemyRespawnIncompleteRows": respawn_summary["incompleteRows"],
        "enemyStateSkippedRows": respawn_summary["skippedStateRows"],
        "lifecycleCounts": event_counts,
        "processingAllowed": processing_allowed,
        "outputs": {
            "corpseFullUpdatesCsv": str(output_csv),
            "enemyRespawnsCsv": str(respawn_summary["outputCsv"]),
        },
    }
    summary_path = capture / "npc-lifecycle-summary.json"
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    print(f"corpseFullUpdateRows={len(rows)} decodeErrors={len(errors)}")
    print(
        "enemyRespawnRows="
        f"{respawn_summary['rows']} complete={respawn_summary['completeRows']} "
        f"ambiguous={respawn_summary['ambiguousRows']} "
        f"incomplete={respawn_summary['incompleteRows']} "
        f"skippedStateRows={respawn_summary['skippedStateRows']}"
    )
    print(f"processingAllowed={str(processing_allowed).lower()}")
    print(output_csv)
    print(respawn_summary["outputCsv"])
    print(summary_path)
    if not processing_allowed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
