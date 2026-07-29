# PF map open/close + first door: 20260725-143126
from __future__ import print_function
import csv, os, json, binascii, struct, collections

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-143126"
OUT = r"tools-temp/_tmp_cap_143126_map.txt"


def parse_door_xyz(hx):
    b = binascii.unhexlify(hx.replace(" ", ""))
    for i in range(0, len(b) - 28):
        if b[i] != 0 or b[i + 1] != 0 or b[i + 2] != 0xC7:
            continue
        if b[i + 3] not in (0x48, 0x49, 0x3D):
            continue
        o = i + 8
        if b[o : o + 4] != b"\x00\x00\x00\x00":
            continue
        o += 5
        x = struct.unpack_from(">f", b, o + 8)[0]
        y = struct.unpack_from(">f", b, o + 12)[0]
        z = struct.unpack_from(">f", b, o + 16)[0]
        if 1 < y < 30 and -1 < x < 500 and 0 < z < 500:
            return x, y, z
    return None


lines = []


def w(*a):
    line = " ".join(str(x) for x in a)
    lines.append(line)
    print(line)


info = json.load(open(os.path.join(CAP, "capture_info.json"), encoding="utf-8-sig"))
w("=== capture_info ===")
w("pf", info.get("playfieldId"), "char", info.get("characterName"))
w("counts", info.get("packetCounts", {}))

path = os.path.join(CAP, "raw-packets.csv")
with open(path, encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    utc = r.fieldnames[0]
    rows = list(r)

w("=== all packets", len(rows))
type_counts = collections.Counter()
for row in rows:
    nt = row.get("N3TypeName") or ("N3_" + str(row.get("N3TypeValue")))
    d = (row.get("Direction") or "?")[:3]
    type_counts[d + " " + nt] += 1
for k, v in sorted(type_counts.items()):
    w(" ", k, v)

w("=== timeline ===")
spawn = None
paf = None
for row in rows:
    nt = row.get("N3TypeName") or ("N3_" + str(row.get("N3TypeValue")))
    hx = (row.get("RawHex") or "").replace(" ", "")
    d = (row.get("Direction") or "?")[:3]
    extra = ""
    if nt == "PlayfieldAnarchyF" and d.startswith("IN") and paf is None:
        paf = row
        b = binascii.unhexlify(hx)
        last = hx.upper().rfind("00009C50")
        if last >= 0:
            pfhex = hx[last + 8 : last + 16]
            try:
                extra += " pf2=%s" % (hex(int(pfhex, 16)),)
            except Exception:
                pass
            gen = b[last // 2 + 8 :]
            if len(gen) >= 8:
                bi = (gen[4] << 24) | (gen[5] << 16) | (gen[6] << 8) | gen[7]
                extra += " build=%s genLen=%d" % (hex(bi), len(gen))
        for i in range(0, min(100, len(b) - 12)):
            x, y, z = struct.unpack_from(">fff", b, i)
            if 200 < x < 400 and 1 < y < 20 and 150 < z < 350:
                spawn = (x, y, z)
                extra += " spawn=%s" % (spawn,)
                break
        # PlayfieldX/Z guess: scan ints near end of header
        w(row[utc][11:26], d, nt, "len", len(hx) // 2, extra)
        w("  PAF head", hx[:180])
        continue
    if nt == "DoorFullUpdate":
        pos = parse_door_xyz(hx)
        dist = ""
        if pos and spawn:
            dist = " dist=%.1f" % (((pos[0] - spawn[0]) ** 2 + (pos[2] - spawn[2]) ** 2) ** 0.5)
        extra = " xyz=%s%s" % (pos, dist)
    elif nt == "CharDCMove":
        b = binascii.unhexlify(hx) if hx else b""
        for i in range(0, max(0, len(b) - 12)):
            x, y, z = struct.unpack_from(">fff", b, i)
            if 180 < x < 320 and 1 < y < 20 and 180 < z < 280:
                extra = " move=(%.1f,%.2f,%.1f)" % (x, y, z)
                break
    w(row[utc][11:26], d, nt, "len", len(hx) // 2, extra)

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", OUT)
