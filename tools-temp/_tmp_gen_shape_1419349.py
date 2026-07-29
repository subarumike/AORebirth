# Extract containers + spawn + generate shape 1419349 with textures/meshes.
from __future__ import print_function
import csv, collections, os, re

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-mission-find-person"
PF = "1419349"
OUT_SHAPE = r"tools-temp\_tmp_shape_1419349.csfrag"
OUT_LOOT = r"tools-temp\_tmp_loot_1419349.csfrag"
OUT_NOTES = r"tools-temp\_tmp_find_person_layout.txt"

def f(v, default=0.0):
    try:
        return float(v)
    except Exception:
        return default

def i(v, default=0):
    try:
        return int(float(v))
    except Exception:
        return default

def parse_tex(s):
    # "0:9418:0|1:8729:0|..." -> [[0,9418],[1,8729],...]
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) < 2:
            continue
        place, tid = i(bits[0]), i(bits[1])
        if tid == 0 and place == 0 and all(i(b) == 0 for b in bits[1:]):
            # keep zeros only if any non-zero elsewhere later
            out.append([place, tid])
            continue
        out.append([place, tid])
    # drop all-zero textures (grey shell)
    if not out or all(t[1] == 0 for t in out):
        return None
    return out

def parse_mesh(s):
    # "0:20065:0:2|0:40251:0:4|1:15839:0:2"
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) < 4:
            continue
        out.append([i(bits[0]), i(bits[1]), i(bits[2]), i(bits[3])])
    return out if out else None

def cs_arr2(arr):
    if not arr:
        return "null"
    parts = []
    for row in arr:
        parts.append("new[] { %s }" % ", ".join(str(x) for x in row))
    return "new[] { %s }" % ", ".join(parts)

# containers from events
props = []
seen_prop = set()
with open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace") as fh:
    for line in fh:
        m = re.search(r"identity=\(Container:([0-9A-F]+)\) name=([^=]+?) pos=\(([^)]+)\)", line)
        if not m:
            continue
        cid, name, xyz = m.group(1), m.group(2).strip(), m.group(3)
        parts = [p.strip() for p in xyz.split(",")]
        if len(parts) != 3:
            continue
        key = (name, parts[0], parts[2])
        if key in seen_prop:
            continue
        seen_prop.add(key)
        locked = "(Locked)" in name or "Locked" in line
        clean = name.replace(" (Locked)", "").strip()
        props.append((clean, f(parts[0]), f(parts[1]), f(parts[2]), locked, cid))

# also DoorFullUpdate hex count
door_n = 0
chest_n = 0
with open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace") as fh:
    for line in fh:
        if "DoorFullUpdate" in line:
            door_n += 1
        if "ChestFullUpdate" in line:
            chest_n += 1

# spawn from movement Current*
mov = list(csv.DictReader(open(os.path.join(CAP, "movement-packets.csv"), encoding="utf-8-sig")))
spawn = None
for r in mov:
    x, y, z = f(r.get("CurrentX"), None), f(r.get("CurrentY"), None), f(r.get("CurrentZ"), None)
    if x is None or y is None or z is None:
        # try Destination
        x, y, z = f(r.get("DestinationX"), None), f(r.get("DestinationY"), None), f(r.get("DestinationZ"), None)
    if x is None:
        continue
    if y > 20:
        continue
    # first interior-ish
    spawn = (x, y, z)
    break

# SCFU mobs
rows = list(csv.DictReader(open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig")))
mobs = []
find = None
seen_id = set()
for r in rows:
    if (r.get("PlayfieldId") or "") != PF:
        continue
    name = (r.get("Name") or "").strip()
    if not name or name in ("Carlo Pinnetti", "CEO Guardian", "Corporate Guardian", "Cratonera"):
        continue
    if f(r.get("PositionY")) > 20:
        continue
    ident = r.get("Identity") or ""
    if not ident or ident in seen_id:
        continue
    seen_id.add(ident)
    if name == "Gary Arnall":
        find = r
        continue
    mobs.append(r)

sx, sy, sz = spawn if spawn else (190.0, 5.01, 160.0)
# Prefer first SetPos / early Follow with coords near entrance - use min Z cluster?
# From find-item pattern spawn was entrance; here first mov may be mid-fight.
# Use movement-summary or earliest Current near Gary? Capture playfield enter ~09:43:12
# Use average of lowest-Z or check dossier positions - look at movement summary json

import json
ms = json.load(open(os.path.join(CAP, "movement-summary.json"), encoding="utf-8-sig"))
notes = []
notes.append("movement-summary keys=%s" % list(ms.keys())[:40])
notes.append(json.dumps(ms, indent=2)[:2500])

# Heuristic: spawn near lowest Z among early Follow destinations if available
early = []
for r in mov[:40]:
    x = f(r.get("CurrentX"), None)
    if x is None:
        x = f(r.get("DestinationX"), None)
        y = f(r.get("DestinationY"), 5.01)
        z = f(r.get("DestinationZ"), None)
    else:
        y = f(r.get("CurrentY"), 5.01)
        z = f(r.get("CurrentZ"), None)
    if x is not None and z is not None and y < 20:
        early.append((x, y, z))
if early:
    # take first
    sx, sy, sz = early[0]
    notes.append("spawn_from_early_mov=%s" % ((sx, sy, sz),))

# Write layout notes
notes.append("PF=%s doors=%d chests=%d props=%d trash=%d find=%s" % (
    PF, door_n, chest_n, len(props), len(mobs), find.get("Name") if find else None))
notes.append("COLORED (token %+): Neutral/Clan with non-zero textures")
notes.append("GREY (token 0%): Monster side tex all zero — Cyborgs/Hellhounds/Medusas")
colored = 0
grey = 0
for r in mobs:
    tex = parse_tex(r.get("Textures"))
    if tex:
        colored += 1
    else:
        grey += 1
notes.append("trash colored=%d grey=%d" % (colored, grey))
notes.append("PROPS:")
for p in props:
    notes.append("  %s @ (%.3f,%.3f,%.3f) locked=%s id=%s" % (p[0], p[1], p[2], p[3], p[4], p[5]))

# Generate shape CS
lines = []
lines.append("        // Shape playfield 1419349 from capture 20260724-mission-find-person (%d trash + FindPerson)" % len(mobs))
lines.append("        new MissionShape")
lines.append("        {")
lines.append("            CapturedPlayfieldId = 1419349,")
lines.append("            SpawnX = %sf, SpawnY = %sf, SpawnZ = %sf," % (sx, sy, sz))
lines.append("            Npcs = new[]")
lines.append("            {")

def emit_npc(r, role, name_override=None):
    name = (name_override or r.get("Name") or "Trash").replace('"', '\\"')
    tex = parse_tex(r.get("Textures"))
    mesh = parse_mesh(r.get("Meshes"))
    # Capture rule: grey (no non-zero textures) = 0% token; colored = %+
    is_grey = role == "Trash" and tex is None
    lines.append("                new MissionNpc")
    lines.append("                {")
    lines.append('                    Name = "%s",' % name)
    lines.append("                    Role = MissionNpcRole.%s," % role)
    lines.append("                    Level = %d, Health = %d, MonsterData = %d, Scale = %d, HeadMesh = %d," % (
        i(r.get("Level"), 150), i(r.get("Health"), 10000), i(r.get("MonsterData"), 26137),
        i(r.get("MonsterScale"), 100), i(r.get("HeadMesh"), 0)))
    lines.append("                    X = %sf, Y = %sf, Z = %sf," % (
        f(r.get("PositionX")), f(r.get("PositionY")), f(r.get("PositionZ"))))
    lines.append("                    Hx = %sf, Hy = %sf, Hz = %sf, Hw = %sf," % (
        f(r.get("HeadingX")), f(r.get("HeadingY")), f(r.get("HeadingZ")), f(r.get("HeadingW"), 1.0)))
    lines.append("                    Textures = %s," % cs_arr2(tex))
    lines.append("                    Meshes = %s," % cs_arr2(mesh))
    if is_grey:
        lines.append("                    IsGrey = true,")
    lines.append("                },")

if find:
    emit_npc(find, "FindTarget")
else:
    # fallback fictional at bag-ish center
    lines.append("                new MissionNpc")
    lines.append("                {")
    lines.append('                    Name = "Gary Arnall",')
    lines.append("                    Role = MissionNpcRole.FindTarget,")
    lines.append("                    Level = 187, Health = 18885, MonsterData = 26151, Scale = 120, HeadMesh = 40171,")
    lines.append("                    X = 190.01f, Y = 5.01000166f, Z = 106.5f,")
    lines.append("                    Hx = 0f, Hy = -0.9948113f, Hz = 0f, Hw = 0.1017406f,")
    lines.append("                    Textures = new[] { new[] { 1, 81911 }, new[] { 2, 81913 }, new[] { 3, 81908 }, new[] { 4, 81916 } },")
    lines.append("                    Meshes = new[] { new[] { 0, 40171, 0, 4 } },")
    lines.append("                },")

for r in mobs:
    emit_npc(r, "Trash")

lines.append("            },")
lines.append("        },")

open(OUT_SHAPE, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")

pl = []
pl.append("        // Capture 20260724-mission-find-person PF 1419349 containers")
pl.append("        internal static readonly LootProp[] Props_1419349 =")
pl.append("        {")
for name, x, y, z, locked, cid in props:
    pl.append('            new LootProp { Name = "%s", X = %sf, Y = %sf, Z = %sf, Locked = %s },' % (
        name.replace('"', '\\"'), x, y, z, "true" if locked else "false"))
pl.append("        };")
open(OUT_LOOT, "w", encoding="utf-8", newline="\n").write("\n".join(pl) + "\n")
open(OUT_NOTES, "w", encoding="utf-8", newline="\n").write("\n".join(notes) + "\n")
print("shape trash", len(mobs), "props", len(props), "spawn", sx, sy, sz, "doors", door_n, "chests", chest_n)
print("colored", colored, "grey", grey)
