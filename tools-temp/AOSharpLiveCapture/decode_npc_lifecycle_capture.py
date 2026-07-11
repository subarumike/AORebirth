#!/usr/bin/env python3
"""Decode generic NPC corpse/lifecycle evidence from an AOSharpLiveCapture folder."""

import argparse
import csv
import json
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
        "lifecycleCounts": event_counts,
        "processingAllowed": processing_allowed,
        "outputs": {"corpseFullUpdatesCsv": str(output_csv)},
    }
    summary_path = capture / "npc-lifecycle-summary.json"
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    print(f"corpseFullUpdateRows={len(rows)} decodeErrors={len(errors)}")
    print(f"processingAllowed={str(processing_allowed).lower()}")
    print(output_csv)
    print(summary_path)
    if not processing_allowed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
