"""Decode perk CharacterAction packets from capture 20260715-194155."""
from __future__ import annotations

import csv
import struct
from pathlib import Path

CAPTURE = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260715-194155"
)

ACTION_NAMES = {
    0xB4: "Action180_AddPerkAction?",
    0xBB: "TrainPerk",
    0xBC: "?",
    0xBD: "?",
    0xBE: "?",
    0xBF: "?",
}


def find_action_offset(payload: bytes) -> int | None:
    # N3 payload after identity commonly has action int; search for known perk actions.
    for off in range(0, min(len(payload) - 4, 64)):
        (action,) = struct.unpack_from(">I", payload, off)
        if action in (0xB4, 0xBB, 0x70, 0x84, 0xA4, 0xAA) or 0xB0 <= action <= 0xC5:
            return off
    return None


def decode_character_action(hexblob: str) -> dict | None:
    data = bytes.fromhex(hexblob)
    # Find N3 marker 5E477770 (N3MessageType CharacterAction = 0x5E477770)
    marker = bytes.fromhex("5E477770")
    idx = data.find(marker)
    if idx < 0:
        return None
    # After marker: unknown(2?) + identity(8) + unknown(4) + action(4) ...
    # From known TrainPerk OUT:
    # ...5E4777700000C350 7966F05B 00000000 BB000000 00000000 00000000 00000000 00000000 000000FA 0000
    body = data[idx + 4 :]
    if len(body) < 28:
        return None
    # common pattern after type: 0000 C350 | identity type+inst | unknown | action
    # body starts with 0000C350 for these
    # Skip 4 (0000C350?), then identity 8, unknown 4, action 4
    # Actually: 0000 C350 7966F05B 00000000 BB...
    # Looking at dump: after 5E477770: 0000C350 7966F05B 00000000 BB000000 ...
    # So: U16?+U16? or Int32 clientInst then identity
    off = 0
    # Try: Int32 unknown/header, Identity (Int32 type, Int32 instance), Int32 unknown1, Int32 action
    if len(body) < 40:
        return None
    # Pattern from serializer typical: Identity of message already before type in outer frame.
    # For N3 body after type dword:
    # [AoMember] Unknown (int) + Identity + Unknown1 + Action + Target + Param1 + Param2 + Unknown2
    unknown, id_type, id_inst, unk1, action = struct.unpack_from(">IIIII", body, 0)
    target_type, target_inst, p1, p2 = struct.unpack_from(">IIII", body, 20)
    unk2 = struct.unpack_from(">H", body, 36)[0] if len(body) >= 38 else None
    p2_ascii = ""
    try:
        p2_ascii = struct.pack(">I", p2).decode("ascii")
        if not p2_ascii.isprintable():
            p2_ascii = ""
    except Exception:
        p2_ascii = ""
    return {
        "unknown": unknown,
        "id": f"{id_type:X}:{id_inst:X}",
        "unk1": unk1,
        "action": action,
        "action_name": ACTION_NAMES.get(action, f"0x{action:X}"),
        "target": f"{target_type:X}:{target_inst:X}",
        "p1": p1,
        "p2": p2,
        "p2_ascii": p2_ascii,
        "unk2": unk2,
    }


def main() -> None:
    path = CAPTURE / "raw-packets.csv"
    rows = []
    with path.open(newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            msg = row.get("messageType") or row.get("MessageType") or ""
            # flexible column names
            if not msg:
                # try by position keys
                vals = list(row.values())
                joined = ",".join(vals)
                if "CharacterAction" not in joined:
                    continue
                hexblob = vals[-1]
                direction = vals[2] if len(vals) > 2 else "?"
                ts = vals[0]
            else:
                if "CharacterAction" not in msg and "CharacterAction" not in str(row):
                    continue
                hexblob = row.get("hex") or row.get("payload") or list(row.values())[-1]
                direction = row.get("direction") or row.get("Direction") or "?"
                ts = row.get("timestampUtc") or row.get("timestamp") or "?"

            hexblob = hexblob.strip().strip('"')
            if not hexblob or "5E477770" not in hexblob.upper():
                # still try
                pass
            decoded = decode_character_action(hexblob)
            if not decoded:
                continue
            if decoded["action"] not in (
                0xB4,
                0xBB,
                0xBC,
                0xBD,
                0xBE,
                0xBF,
                0xC0,
                0xC1,
                0xC2,
                0xC3,
                0xC4,
                0xC5,
            ) and not (0xB0 <= decoded["action"] <= 0xC8):
                # keep perk-ish range only
                if decoded["action"] not in (0x84, 0xA4, 0xAA):
                    continue
            rows.append((ts, direction, decoded, hexblob))

    # If DictReader column names wrong, fall back
    if not rows:
        with path.open(newline="", encoding="utf-8") as f:
            reader = csv.reader(f)
            header = next(reader)
            print("CSV header:", header)
            for cols in reader:
                if len(cols) < 12:
                    continue
                if "CharacterAction" not in cols[7]:
                    continue
                hexblob = cols[-1].strip('"')
                decoded = decode_character_action(hexblob)
                if not decoded:
                    continue
                a = decoded["action"]
                if a < 0xB0 or a > 0xC8:
                    continue
                rows.append((cols[0], cols[2], decoded, hexblob))

    print(f"perk-ish CharacterAction count: {len(rows)}")
    for ts, direction, d, _ in rows:
        p2s = f" '{d['p2_ascii']}'" if d["p2_ascii"] else ""
        print(
            f"{ts} {direction} action={d['action']}({d['action_name']}) "
            f"target={d['target']} p1={d['p1']} p2={d['p2']}{p2s} id={d['id']}"
        )

    # Known target IDs -> check Perks.xml AOIDs nearby
    print("\nTarget instance decimals:")
    seen = set()
    for _, _, d, _ in rows:
        inst = int(d["target"].split(":")[1], 16)
        if inst and inst not in seen:
            seen.add(inst)
            print(f"  0x{inst:X} = {inst}")


if __name__ == "__main__":
    main()
