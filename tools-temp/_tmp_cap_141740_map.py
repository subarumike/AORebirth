# Map open/close + first segment: 20260725-141740
from __future__ import print_function
import csv, os, json, binascii, struct, collections

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-141740"
OUT = r"tools-temp/_tmp_cap_141740_map.txt"


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


def scfu_name(hx):
    b = binascii.unhexlify(hx.replace(" ", ""))
    # crude: look for printable ASCII runs length >= 3
    best = ""
    i = 0
    while i < len(b):
        if 32 <= b[i] < 127:
            j = i
            while j < len(b) and 32 <= b[j] < 127:
                j += 1
            s = b[i:j].decode("ascii", "replace")
            if len(s) > len(best) and " " in s or len(s) >= 4:
                if len(s) >= 4:
                    best = s if len(s) > len(best) else best
            i = j
        else:
            i += 1
    return best


lines = []


def w(*a):
    line = " ".join(str(x) for x in a)
    lines.append(line)
    print(line)


w("=== capture_info ===")
info = json.load(open(os.path.join(CAP, "capture_info.json"), encoding="utf-8-sig"))
for k in ("playfieldId", "characterName", "startedUtc", "endedUtc", "durationSeconds"):
    if k in info:
        w(k, info.get(k))
w("counts", info.get("packetCounts", {}))

path = os.path.join(CAP, "raw-packets.csv")
with open(path, encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    utc = r.fieldnames[0]
    rows = list(r)

w("=== all packets", len(rows), "fields", r.fieldnames)
type_counts = collections.Counter()
for row in rows:
    nt = row.get("N3TypeName") or ("N3_" + str(row.get("N3TypeValue")))
    d = (row.get("Direction") or "?")[:3]
    type_counts[d + " " + nt] += 1
for k, v in sorted(type_counts.items()):
    w(" ", k, v)

w("=== timeline ===")
for row in rows:
    nt = row.get("N3TypeName") or ("N3_" + str(row.get("N3TypeValue")))
    hx = (row.get("RawHex") or "").replace(" ", "")
    pos = None
    extra = ""
    if nt == "DoorFullUpdate":
        pos = parse_door_xyz(hx)
        extra = " xyz=%s" % (pos,)
    elif nt == "SimpleCharFullUpdate":
        extra = " name=%r" % (scfu_name(hx)[:40],)
    elif "9C50" in hx.upper():
        # playfield marker
        last = hx.upper().rfind("00009C50")
        if last >= 0 and last + 16 <= len(hx):
            pfhex = hx[last + 8 : last + 16]
            try:
                extra = " pf2=%s" % (hex(int(pfhex, 16)),)
            except Exception:
                pass
    w(
        row[utc][11:26],
        (row.get("Direction") or "?")[:3],
        nt,
        "len",
        len(hx) // 2,
        extra,
    )

# enemy state / npc lifecycle for map icons
for name in ("enemy-state.csv", "npc-lifecycle.csv", "scfu-appearance.csv", "mission-flow.log"):
    p = os.path.join(CAP, name)
    if not os.path.exists(p):
        continue
    w("===", name, "===")
    with open(p, encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f):
            if i > 60:
                w("...trunc")
                break
            w(line[:240].rstrip())

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", OUT)
