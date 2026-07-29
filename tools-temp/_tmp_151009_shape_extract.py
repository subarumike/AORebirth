# Extract doors/chests/NPC spawn list + gen from 20260725-151009 for shape 1441800
import csv, os, struct, json

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-151009"
OUT = r"tools-temp/_tmp_151009_shape_extract.txt"
lines = []

def p(s=""):
    lines.append(s)

def be_f(h, off):
    return struct.unpack(">f", bytes.fromhex(h[off*2:(off+4)*2]))[0]

doors = []
chests = []
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        nt = row.get("N3TypeName") or ""
        d = row.get("Direction") or ""
        if not d.startswith("IN"):
            continue
        hx = (row.get("RawHex") or "").replace(" ", "").upper()
        if nt == "DoorFullUpdate" and hx:
            # store body without seq pad if starts with size
            doors.append(hx)
        if nt == "ChestFullUpdate" and hx:
            chests.append(hx)

# unique by identity instance if possible
p("doors=%d chests=%d" % (len(doors), len(chests)))
# unique door identities - look for C76A door type
seen = set()
uniq_doors = []
for hx in doors:
    # identity often at offset after header - use full hex as key truncated identity region
    key = hx[40:80] if len(hx) > 80 else hx
    if key in seen:
        continue
    seen.add(key)
    uniq_doors.append(hx)
p("uniq_doors=%d" % len(uniq_doors))

seen = set()
uniq_chests = []
for hx in chests:
    key = hx[40:80] if len(hx) > 80 else hx
    if key in seen:
        continue
    seen.add(key)
    uniq_chests.append(hx)
p("uniq_chests=%d" % len(uniq_chests))

# write csharp fragments
def to_cs_array(name, hexes):
    parts = ["        public static readonly string[] %s =" % name, "        {"]
    for h in hexes:
        # strip leading packet length/seq if present - keep as used by other doors
        # existing format is full hex strings
        parts.append('            "' + h + '",')
    parts.append("        };")
    return "\n".join(parts)

open(r"tools-temp/_tmp_doors_1441800.csfrag", "w").write(to_cs_array("Doors_1441800", uniq_doors))
open(r"tools-temp/_tmp_chests_1441800.csfrag", "w").write(to_cs_array("Chests_1441800", uniq_chests))

# NPC list from scfu-appearance
npcs = []
with open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        name = row.get("Name") or ""
        if name in ("Getkeep", "") or "Getkeep" in name:
            continue
        try:
            x = float(row.get("PositionX") or 0)
            y = float(row.get("PositionY") or 0)
            z = float(row.get("PositionZ") or 0)
            md = int(float(row.get("MonsterData") or 0))
            lvl = int(float(row.get("Level") or 1))
            meshes = row.get("Meshes") or ""
            npcs.append((name, x, y, z, md, lvl, meshes, row.get("Identity")))
        except Exception:
            pass

# unique by identity
seen = set()
uniq = []
for n in npcs:
    if n[7] in seen:
        continue
    seen.add(n[7])
    uniq.append(n)
p("\nNPCs uniq=%d" % len(uniq))
for n in uniq:
    p("  %s xyz=(%.1f,%.1f,%.1f) md=%d lvl=%d mesh=%s" % (n[0], n[1], n[2], n[3], n[4], n[5], (n[6] or "")[:60]))

# player spawn from first Getkeep or PAF
p("\nPAF gen building")
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") == "PlayfieldAnarchyF" and (row.get("Direction") or "").startswith("IN"):
            hx = (row.get("RawHex") or "").replace(" ", "").upper()
            bidx = hx.find("0000C79F")
            if bidx >= 0:
                gen = hx[bidx:]
                # until end or next marker
                p("gen_len_guess=%d building=%s" % (len(gen)//2, hx[bidx+8:bidx+16]))
                open(r"tools-temp/_tmp_151009_gen.hex", "w").write(gen[:246])  # 123 bytes
            break

open(OUT, "w", encoding="utf-8").write("\n".join(lines))
print("wrote", OUT)
print("doors frag", len(uniq_doors), "chests", len(uniq_chests), "npcs", len(uniq))
