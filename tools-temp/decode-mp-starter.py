import re
import struct

CAPTURE = r"c:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260711-215715\packets.hex.log"
ITEMNAMES = r"c:\Users\nermi\source\repos\AORebirth\AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql"


def wire_id(buf, offset):
    return (buf[offset] << 16) | (buf[offset + 1] << 8) | buf[offset + 2]


def load_fullcharacter():
    with open(CAPTURE, encoding="utf-8-sig") as handle:
        for line in handle:
            if "n3=FullCharacter" in line:
                return bytes.fromhex(line.split("hex=")[1].strip())
    raise RuntimeError("FullCharacter packet not found")


def parse_inventory(body):
    inv_count = (struct.unpack_from("<H", body, 35)[0] // 0x3F1) - 1
    slots = []
    offset = 40
    for _ in range(inv_count):
        placement = struct.unpack_from("<h", body, offset)[0]
        flags = struct.unpack_from("<h", body, offset + 2)[0]
        count = struct.unpack_from("<h", body, offset + 4)[0]
        low = wire_id(body, offset + 14)
        high = wire_id(body, offset + 18)
        quality = struct.unpack_from("<i", body, offset + 22)[0]
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
    return slots, offset


def parse_uploaded_nanos(body, offset):
    marker = struct.unpack_from("<i", body, offset)[0]
    if marker != 0x3F1:
        return [], offset
    count = (struct.unpack_from("<i", body, offset + 4)[0] // 0x3F1) - 1
    offset += 8
    nano_ids = list(struct.unpack_from("<" + "i" * count, body, offset)) if count else []
    return nano_ids, offset + count * 4


def load_item_names():
    text = open(ITEMNAMES, encoding="utf-8", errors="replace").read()
    names = {}
    for match in re.finditer(r"\((\d+),\s*'((?:''|[^'])*)'", text):
        names[int(match.group(1))] = match.group(2).replace("''", "'")
    return names


def main():
    body = load_fullcharacter()
    slots, offset = parse_inventory(body)
    nano_ids, _ = parse_uploaded_nanos(body, offset)
    names = load_item_names()

    print("INVENTORY_SLOTS", len(slots))
    for slot in slots:
        low_name = names.get(slot["low"], "UNKNOWN")
        high_name = names.get(slot["high"], "UNKNOWN")
        print(
            f"placement={slot['placement']} flags=0x{slot['flags']:04X} count={slot['count']} "
            f"low={slot['low']} ({low_name}) high={slot['high']} ({high_name}) ql={slot['quality']}"
        )

    print("UPLOADED_NANOS", len(nano_ids))
    if nano_ids:
        print(nano_ids)


if __name__ == "__main__":
    main()
