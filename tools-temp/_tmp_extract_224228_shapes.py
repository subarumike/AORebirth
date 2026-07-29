# Extract PAF payloads + door/chest hex + NPC summary from 20260724-224228 for shape import.
from __future__ import print_function
import csv, collections, os, re, struct

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-224228"
OUTDIR = r"tools-temp\_tmp_cap_224228_assets"
os.makedirs(OUTDIR, exist_ok=True)

PFS = {
    1460226: {"hex": "00164802", "acg": "D79990", "enter": "2026-07-24T20:42:46", "exit": "2026-07-24T20:47:39"},
    1456133: {"hex": "00163805", "acg": "D79992", "enter": "2026-07-24T20:48:35", "exit": "2026-07-24T20:52:32"},
}

def extract_paf_payload(raw_hex):
    raw = bytes.fromhex(raw_hex.strip())
    # packets.hex.log lines often include full UDP? Our brief showed hex starting 0002000A...
    # Find N3 000A then type PlayfieldAnarchyF = 0x0000DB3? Actually from brief: n3=PlayfieldAnarchyF
    # Look for C79F ACGBuildingGeneratorData
    idx = raw.find(b"\x00\x00\xc7\x9f")
    if idx < 0:
        idx = raw.find(b"\x00\x00\xC7\x9F")
    if idx < 0:
        return None
    return raw[idx:]

hex_path = os.path.join(CAP, "packets.hex.log")
paf = {pf: None for pf in PFS}
doors = {pf: [] for pf in PFS}
chests = {pf: [] for pf in PFS}
door_ids = {pf: set() for pf in PFS}
chest_ids = {pf: set() for pf in PFS}

with open(hex_path, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        ts = line[:26] if len(line) > 26 else ""
        m = re.search(r"hex=([0-9A-Fa-f]+)", line)
        if not m:
            continue
        hx = m.group(1)
        for pf, meta in PFS.items():
            if ts < meta["enter"] or ts > meta["exit"]:
                continue
            if "PlayfieldAnarchyF" in line and meta["hex"].lower() in hx.lower():
                pl = extract_paf_payload(hx)
                if pl and (paf[pf] is None or len(pl) > len(paf[pf])):
                    paf[pf] = pl
            if "DoorFullUpdate" in line and meta["hex"].lower() in hx.lower():
                # identity after C748
                raw = bytes.fromhex(hx)
                # store full packet hex from first 000A or from start
                start = hx.upper().find("000A")
                if start < 0:
                    start = 0
                body = hx[start:].upper() if start >= 0 else hx.upper()
                # unique by door instance C748xxxxxxxx
                dm = re.search(r"0000C748([0-9A-F]{8})", body)
                if dm and dm.group(1) not in door_ids[pf]:
                    door_ids[pf].add(dm.group(1))
                    # Prefer body starting at 00..000A pattern used in existing captures
                    # Existing format is full N3 body hex strings in DynelCapture
                    # Use from IdentityType door onward? Existing doors are full packet bodies.
                    # Look at Doors_1419310 format - typically starts with sequence after transport
                    doors[pf].append(hx.upper())
            if "ChestFullUpdate" in line and meta["hex"].lower() in hx.lower():
                raw = bytes.fromhex(hx)
                body = hx.upper()
                cm = re.search(r"0000C74[89ABCDEF]([0-9A-F]{8})", body)
                key = cm.group(0) if cm else body[40:56]
                if key not in chest_ids[pf]:
                    chest_ids[pf].add(key)
                    chests[pf].append(hx.upper())

# SCFU NPCs
scfu = list(csv.DictReader(open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig")))
for pf in PFS:
    print("=== PF", pf, "ACG", PFS[pf]["acg"])
    pl = paf[pf]
    print(" PAF payload", len(pl) if pl else None, "head", pl[:16].hex() if pl else None)
    if pl:
        open(os.path.join(OUTDIR, "paf_%d.hex" % pf), "w").write(pl.hex().upper() + "\n")
        # also write csharp bytes
        parts = ["0x%02X" % b for b in pl]
        lines = []
        for i in range(0, len(parts), 8):
            lines.append(", ".join(parts[i:i+8]))
        open(os.path.join(OUTDIR, "paf_%d.csfrag" % pf), "w").write(",\n".join(lines) + "\n")
    print(" doors unique", len(doors[pf]), "chests", len(chests[pf]))
    open(os.path.join(OUTDIR, "doors_%d.txt" % pf), "w").write("\n".join(doors[pf][:80]) + "\n")
    open(os.path.join(OUTDIR, "chests_%d.txt" % pf), "w").write("\n".join(chests[pf][:40]) + "\n")

    by_id = {}
    for r in scfu:
        if (r.get("PlayfieldId") or "") != str(pf):
            continue
        ident = r.get("Identity") or ""
        if ident and ident not in by_id:
            by_id[ident] = r
    # player/pets skip
    npcs = []
    for ident, r in by_id.items():
        name = r.get("Name") or "?"
        if name in ("Cratonera", "Carlo Pinnetti", "CEO Guardian"):
            continue
        npcs.append(r)
    print(" npcs", len(npcs))
    # write compact shape npc lines
    with open(os.path.join(OUTDIR, "npcs_%d.txt" % pf), "w", encoding="utf-8") as out:
        for r in sorted(npcs, key=lambda x: x.get("Name") or ""):
            out.write("%s|%s|%s|%s|%s|%s|%s|%s|tex=%s|mesh=%s\n" % (
                r.get("Name"), r.get("Level"), r.get("MonsterData"), r.get("Side"),
                r.get("PositionX"), r.get("PositionY"), r.get("PositionZ"),
                r.get("HeadMesh"), (r.get("Textures") or "")[:80], (r.get("Meshes") or "")[:80]))

print("Wrote", OUTDIR)
