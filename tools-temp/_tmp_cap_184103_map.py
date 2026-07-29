# Map extract from 20260725-184103 (new mesh / PF 1419349)
from __future__ import print_function
import csv
import collections
import os
import struct

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-184103"
OUT = r"tools-temp/_tmp_cap_184103_map.txt"


def w(lines, s=""):
    lines.append(s)
    print(s)


lines = []
w(lines, "=== mission-flow teleport/paf ===")
mf = os.path.join(CAP, "mission-flow.log")
with open(mf, encoding="utf-8", errors="replace") as f:
    for line in f:
        if any(k in line for k in ("TELEPORT", "PLAYFIELD", "N3-TELEPORT", "PAF")):
            w(lines, line.rstrip()[:260])

doors = []
chests = []
paf_rows = []
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        nt = row.get("N3TypeName") or ""
        d = row.get("Direction") or ""
        if not d.startswith("IN"):
            continue
        hx = (row.get("RawHex") or "").replace(" ", "").upper()
        if nt == "PlayfieldAnarchyF":
            paf_rows.append(hx)
        if nt == "DoorFullUpdate":
            doors.append(hx)
        if nt == "ChestFullUpdate":
            chests.append(hx)

w(lines, "")
w(lines, "=== PAF count=%d ===" % len(paf_rows))
for hx in paf_rows[:2]:
    raw = bytes.fromhex(hx)
    body = raw[16:] if len(raw) > 16 and raw[2:4] == b"\x00\x0A" else raw
    idx = body.find(bytes.fromhex("0000C79F"))
    w(lines, "PAF bytes=%d C79F@%d" % (len(hx) // 2, idx))
    if idx >= 0:
        bldg = struct.unpack_from(">I", body, idx + 4)[0]
        w(lines, "building=%08X" % bldg)
    off = 4 + 8 + 1 + 4
    coords = struct.unpack_from(">fff", body, off)
    w(lines, "CharacterCoordinates=%s" % (coords,))
    off2 = 4 + 8 + 1 + 4 + 12 + 1 + 8 + 4 + 4 + 8
    payload = body[off2:]
    w(lines, "payload_len=%d head=%s" % (len(payload), payload[:20].hex().upper() if payload else None))
    if payload and idx >= 0:
        open(r"tools-temp/_tmp_184103_gen.hex", "w").write(payload.hex().upper())
        w(lines, "wrote tools-temp/_tmp_184103_gen.hex")

seen = set()
uniq_doors = []
for h in doors:
    key = h[40:80] if len(h) > 80 else h
    if key in seen:
        continue
    seen.add(key)
    uniq_doors.append(h)

seen = set()
uniq_chests = []
for h in chests:
    key = h[40:80] if len(h) > 80 else h
    if key in seen:
        continue
    seen.add(key)
    uniq_chests.append(h)

w(lines, "")
w(lines, "doors=%d uniq=%d chests=%d uniq=%d" % (len(doors), len(uniq_doors), len(chests), len(uniq_chests)))
pfs = collections.Counter()
for h in uniq_doors:
    if "0015A855" in h:
        pfs["15A855"] += 1
    if "00160008" in h:
        pfs["160008"] += 1
w(lines, "door pf markers %s" % dict(pfs))

# door xyz sample
def parse_door_xyz(hx):
    b = bytes.fromhex(hx)
    for i in range(0, len(b) - 28):
        if b[i : i + 3] != b"\x00\x00\xC7":
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
        if 1 < y < 40 and -50 < x < 800 and 0 < z < 800:
            return x, y, z
    return None


w(lines, "door xyz samples:")
for h in uniq_doors[:8]:
    xyz = parse_door_xyz(h)
    w(lines, "  %s" % (xyz,))

door_cs = ["        public static readonly string[] Doors_1419349 =", "        {"]
for h in uniq_doors:
    door_cs.append('            "' + h + '",')
door_cs.append("        };")
open(r"tools-temp/_tmp_doors_1419349.csfrag", "w", encoding="utf-8", newline="\n").write(
    "\n".join(door_cs) + "\n"
)

chest_cs = ["        public static readonly string[] Chests_1419349 =", "        {"]
for h in uniq_chests:
    chest_cs.append('            "' + h + '",')
chest_cs.append("        };")
open(r"tools-temp/_tmp_chests_1419349.csfrag", "w", encoding="utf-8", newline="\n").write(
    "\n".join(chest_cs) + "\n"
)
w(lines, "wrote door/chest csfrags")

# NPC spawn sample from scfu
w(lines, "")
w(lines, "=== SCFU NPCs (uniq) ===")
npcs = []
path = os.path.join(CAP, "scfu-appearance.csv")
if os.path.exists(path):
    with open(path, encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            name = row.get("Name") or ""
            if not name or "Getkeep" in name:
                continue
            try:
                npcs.append(
                    (
                        name,
                        float(row.get("PositionX") or 0),
                        float(row.get("PositionY") or 0),
                        float(row.get("PositionZ") or 0),
                        row.get("Identity"),
                        row.get("MonsterData"),
                        row.get("Level"),
                    )
                )
            except Exception:
                pass
seen = set()
for n in npcs:
    if n[4] in seen:
        continue
    seen.add(n[4])
    w(lines, "  %s xyz=(%.2f,%.2f,%.2f) md=%s lvl=%s" % (n[0], n[1], n[2], n[3], n[5], n[6]))

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
w(lines, "wrote " + OUT)
