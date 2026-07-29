# Build 1419349 shape NPCs + patch doors/combat/corpse/loot from 20260725-185432 (+184103 target).
from __future__ import print_function
import csv
import os
import re
import struct

ROOT = r"C:\Users\nermi\source\repos\AORebirth"
CAP = os.path.join(ROOT, r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-185432")
CAP184 = os.path.join(ROOT, r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-184103")


def parse_meshes(s):
    # "0:40629:0:4|1:99154:0:2"
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 4:
            out.append([int(bits[0]), int(bits[1]), int(bits[2]), int(bits[3])])
    return out or None


def parse_tex(s):
    # "0:0:0|1:42243:0|..."
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 2:
            out.append([int(bits[0]), int(bits[1])])
    return out or None


def csharp_arr2(arr):
    if not arr:
        return "null"
    parts = []
    for a in arr:
        parts.append("new[] { %s }" % ", ".join(str(x) for x in a))
    return "new[] { " + ", ".join(parts) + " }"


def load_scfu(path):
    rows = []
    p = os.path.join(path, "scfu-appearance.csv")
    if not os.path.exists(p):
        return rows
    with open(p, encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            name = row.get("Name") or ""
            if not name or name == "Getkeep":
                continue
            try:
                x = float(row.get("PositionX") or 0)
                y = float(row.get("PositionY") or 0)
                z = float(row.get("PositionZ") or 0)
            except Exception:
                continue
            # interior cluster for this shape
            if y > 20 or x < 0 or x > 160 or z < 0 or z > 200:
                continue
            rows.append(row)
    # unique by name+approx xyz
    seen = set()
    uniq = []
    for row in rows:
        key = (
            row.get("Name"),
            round(float(row.get("PositionX") or 0), 1),
            round(float(row.get("PositionZ") or 0), 1),
        )
        if key in seen:
            continue
        seen.add(key)
        uniq.append(row)
    return uniq


npcs = {}
for row in load_scfu(CAP) + load_scfu(CAP184):
    name = row.get("Name")
    if name in ("Garbage Flea",):
        # keep flea as trash too
        pass
    if name == "Female Guard":
        continue
    # prefer 185432 row when duplicate names
    if name in npcs and "185432" not in (row.get("CapturedUtc") or ""):
        # keep existing if already from later capture
        continue
    npcs[name] = row

# Ensure FindTarget Levi — from 184103 if missing mesh
if "Levi McDannold" not in npcs:
    npcs["Levi McDannold"] = {
        "Name": "Levi McDannold",
        "PositionX": "81.30",
        "PositionY": "5.12",
        "PositionZ": "130.90",
        "HeadingX": "0",
        "HeadingY": "0",
        "HeadingZ": "0",
        "HeadingW": "1",
        "MonsterData": "26097",
        "Level": "5",
        "Health": "120",
        "MonsterScale": "93",
        "HeadMesh": "40103",
        "Meshes": "0:40103:0:4",
        "Textures": "0:0:0|1:81911:0|2:81913:0|3:81908:0|4:81916:0",
    }

# Marksman from corpse knowledge if missing in SCFU cluster
if "Fresh Marksman" not in npcs:
    npcs["Fresh Marksman"] = {
        "Name": "Fresh Marksman",
        "PositionX": "85.48",
        "PositionY": "5.01",
        "PositionZ": "95.30",
        "HeadingX": "0",
        "HeadingY": "0",
        "HeadingZ": "0",
        "HeadingW": "1",
        "MonsterData": "26103",
        "Level": "5",
        "Health": "115",
        "MonsterScale": "93",
        "HeadMesh": "40209",
        "Meshes": "0:40209:0:4|1:7777:0:2",
        "Textures": "0:0:0|1:40903:0|2:42241:0|3:42242:0|4:0:0",
    }

if "Fresh Clan Lookout" not in npcs:
    npcs["Fresh Clan Lookout"] = {
        "Name": "Fresh Clan Lookout",
        "PositionX": "85.85",
        "PositionY": "5.01",
        "PositionZ": "114.01",
        "HeadingX": "0",
        "HeadingY": "0",
        "HeadingZ": "0",
        "HeadingW": "1",
        "MonsterData": "26074",
        "Level": "5",
        "Health": "115",
        "MonsterScale": "93",
        "HeadMesh": "40691",
        "Meshes": "0:40691:0:4|1:7777:0:2",
        "Textures": "0:0:0|1:22571:0|2:45792:0|3:42254:0|4:42251:0",
    }

blocks = []
# Levi first as FindTarget
order = ["Levi McDannold"] + sorted([n for n in npcs if n != "Levi McDannold"])
for name in order:
    row = npcs[name]
    role = "FindTarget" if name == "Levi McDannold" else "Trash"
    meshes = parse_meshes(row.get("Meshes") or "")
    tex = parse_tex(row.get("Textures") or "")
    hx = float(row.get("HeadingX") or 0)
    hy = float(row.get("HeadingY") or 0)
    hz = float(row.get("HeadingZ") or 0)
    hw = float(row.get("HeadingW") or 1)
    head = int(float(row.get("HeadMesh") or 0) or 0)
    md = int(float(row.get("MonsterData") or 0) or 0)
    lvl = int(float(row.get("Level") or 1) or 1)
    hp = int(float(row.get("Health") or 50) or 50)
    scale = int(float(row.get("MonsterScale") or 100) or 100)
    x = float(row.get("PositionX") or 0)
    y = float(row.get("PositionY") or 5.01)
    z = float(row.get("PositionZ") or 0)
    is_grey = "true" if (tex is None or all(t[1] == 0 for t in tex)) and name != "Levi McDannold" else "false"
    block = """                new MissionNpc
                {
                    Name = %s,
                    Role = MissionNpcRole.%s,
                    Level = %d, Health = %d, MonsterData = %d, Scale = %d, HeadMesh = %d,
                    X = %sf, Y = %sf, Z = %sf,
                    Hx = %sf, Hy = %sf, Hz = %sf, Hw = %sf,
                    Textures = %s,
                    Meshes = %s,%s
                }""" % (
        '"%s"' % name.replace('"', ""),
        role,
        lvl,
        hp,
        md,
        scale,
        head,
        ("%.6f" % x).rstrip("0").rstrip(".") if False else "%.6f" % x,
        "%.6f" % y,
        "%.6f" % z,
        "%.9f" % hx,
        "%.9f" % hy,
        "%.9f" % hz,
        "%.9f" % hw,
        csharp_arr2(tex),
        csharp_arr2(meshes),
        ("\n                    IsGrey = true," if is_grey == "true" else ""),
    )
    blocks.append(block)

npc_cs = ",\n".join(blocks)
frag = """            Npcs = new[]
            {
%s
            },
""" % npc_cs
open(os.path.join(ROOT, r"tools-temp\_tmp_npcs_1419349_185432.csfrag"), "w", encoding="utf-8", newline="\n").write(frag)
print("npc count", len(blocks))
print("wrote npc frag")

# --- patch shape catalog Npcs for 1419349 ---
shape_path = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Playfields\MissionInstanceShapeCatalog.cs")
st = open(shape_path, encoding="utf-8").read()
# Replace from SpawnZ line through end of Npcs array before closing of this MissionShape
# Find start marker
start = st.find("        // Shape playfield 1419349 from capture 20260725-184103")
if start < 0:
    raise SystemExit("shape 1419349 marker missing")
# find Npcs = new[] after start
n0 = st.find("            Npcs = new[]", start)
if n0 < 0:
    raise SystemExit("Npcs missing")
# find matching close: next "        }," that ends this shape - after Npcs `},` then `},` for shape
# locate end of Npcs by brace depth from n0
i = st.find("{", n0)
depth = 0
end = None
for j in range(i, len(st)):
    if st[j] == "{":
        depth += 1
    elif st[j] == "}":
        depth -= 1
        if depth == 0:
            end = j + 1
            break
if end is None:
    raise SystemExit("Npcs end not found")
# include trailing comma if present
if end < len(st) and st[end] == ",":
    end += 1
st = st[:n0] + frag.rstrip() + "\n" + st[end:]
# also update comment
st = st.replace(
    "        // Shape playfield 1419349 from capture 20260725-184103 (fog ACG D7418B)",
    "        // Shape playfield 1419349 from capture 20260725-185432 (mobs/doors) + 184103 enter/fog",
    1,
)
open(shape_path, "w", encoding="utf-8", newline="\n").write(st)
print("patched shape NPCs")

# --- doors from 185432 ---
doors_frag = open(os.path.join(ROOT, r"tools-temp\_tmp_doors_1419349_185432.csfrag"), encoding="utf-8").read().strip()
chests_frag = open(os.path.join(ROOT, r"tools-temp\_tmp_chests_1419349_185432.csfrag"), encoding="utf-8").read().strip()
dynel = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceDynelCapture.cs")
dt = open(dynel, encoding="utf-8").read()
pat = re.compile(
    r"        // Capture 20260725-184103 PF 1419349 fog gold \(ACG D7418B\)\r?\n"
    r"        public static readonly string\[\] Doors_1419349 =[\s\S]*?"
    r"        public static readonly string\[\] Chests_1419349 =[\s\S]*?"
    r"        \};",
    re.M,
)
repl = (
    "        // Capture 20260725-185432 PF 1419349 (doors/chests during clear)\n"
    + doors_frag
    + "\n\n"
    + chests_frag
)
dt2, n = pat.subn(repl, dt, count=1)
if n != 1:
    raise SystemExit("dynel doors replace failed n=%d" % n)
open(dynel, "w", encoding="utf-8", newline="\n").write(dt2)
print("patched doors/chests")

# --- loot catalog add 124444 ---
# already will patch via separate edit
print("done generate")
