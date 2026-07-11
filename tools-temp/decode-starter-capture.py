import json
import re
import struct
import sys

ITEMNAMES = r"c:\Users\nermi\source\repos\AORebirth\AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql"


def wire_id(buf, offset):
    return (buf[offset] << 16) | (buf[offset + 1] << 8) | buf[offset + 2]


def load_fullcharacter(capture_dir):
    path = capture_dir + r"\packets.hex.log"
    with open(path, encoding="utf-8-sig") as handle:
        for line in handle:
            if "n3=FullCharacter" in line:
                return bytes.fromhex(line.split("hex=")[1].strip())
    raise RuntimeError("FullCharacter packet not found")


def parse_inventory(body):
    inv_count = (struct.unpack_from(">H", body, 35)[0] // 0x3F1) - 1
    slots = []
    offset = 40
    for _ in range(inv_count):
        placement = struct.unpack_from("<h", body, offset)[0]
        flags = struct.unpack_from("<h", body, offset + 2)[0]
        count = struct.unpack_from("<h", body, offset + 4)[0]
        low = wire_id(body, offset + 14)
        high = wire_id(body, offset + 18)
        quality = struct.unpack_from("<i", body, offset + 22)[0]
        if placement < 64 or placement > 120:
            break
        slots.append(
            {
                "placement": placement,
                "flags": flags,
                "count": count,
                "low": low,
                "high": high,
                "quality": quality,
            }
        )
        offset += 32
    return slots


def load_item_names():
    text = open(ITEMNAMES, encoding="utf-8", errors="replace").read()
    names = {}
    for match in re.finditer(r"\(\s*(\d+)\s*,\s*'((?:''|[^'])*)'", text):
        names[int(match.group(1))] = match.group(2).replace("''", "'")
    return names


def parse_stats(body):
    wanted = {1: "life", 27: "health", 214: "currentnano", 221: "maxnano", 54: "level", 60: "profession"}
    found = {}
    for i in range(300, len(body) - 8):
        sid = struct.unpack_from("<i", body, i)[0]
        if sid in wanted and sid not in found:
            found[sid] = struct.unpack_from("<I", body, i + 4)[0]
    return {wanted[k]: v for k, v in found.items()}


def main():
    capture_dir = sys.argv[1]
    body = load_fullcharacter(capture_dir)
    slots = parse_inventory(body)
    names = load_item_names()
    info_path = capture_dir + r"\capture_info.json"
    with open(info_path, encoding="utf-8-sig") as handle:
        info = json.load(handle)

    print("CHARACTER", info.get("characterName"), "PF", info.get("playfieldId"))
    print("STATS", parse_stats(body))
    print("ITEMS", len(slots))
    for slot in slots:
        print(
            f"placement={slot['placement']} count={slot['count']} low={slot['low']} "
            f"high={slot['high']} ql={slot['quality']} name={names.get(slot['low'], 'UNKNOWN')}"
        )


if __name__ == "__main__":
    main()
