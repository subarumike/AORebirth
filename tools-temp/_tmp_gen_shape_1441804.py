# Generate MissionShape CS for PF 1441804 from Find-Item capture.
from __future__ import print_function
import csv, os

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-mission-find-item"
OUT = r"tools-temp\_tmp_shape_1441804.csfrag"
PF = "1441804"

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

rows = list(csv.DictReader(open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig")))
mobs = []
seen = set()
player_spawn = None
for r in rows:
    if (r.get("PlayfieldId") or "") != PF:
        continue
    name = (r.get("Name") or "").strip()
    if name == "Cratonera":
        player_spawn = r
        continue
    if name in ("Carlo Pinnetti", "CEO Guardian", "Corporate Guardian", ""):
        continue
    key = (name, r.get("PositionX"), r.get("PositionZ"), r.get("MonsterData"))
    if key in seen:
        continue
    seen.add(key)
    # skip outdoor leftovers wrongly tagged
    y = f(r.get("PositionY"))
    if y > 20:
        continue
    mobs.append(r)

# Containers from earlier extract
props = [
    ("Treasure", 31.60001, 5.1, 193.2, True),   # BA0CF53 locked UseItemOnItem
    ("Broken Machine", 38.60001, 5.1, 216.4, False),
    ("Treasure", 46.60001, 5.1, 208.4, False),
    ("A blasted Skeleton", 29.5, 5.1, 238.4, False),
    ("Barrel", 57.70001, 5.1, 227.5, True),  # locked
    ("A Crashed Android", 53.29999, 5.1, 201.6, False),
    ("Broken Machine", 76.59999, 5.1, 150.3, False),
    ("Barrel", 59.2, 5.1, 198.9, False),
    ("Small Crate", 79.5, 5.1, 188.1, False),
    ("Treasure", 81.10001, 5.1, 167.7, False),
    ("Treasure", 21.1933, 5.1, 178.8232, False),
    ("Skeleton", 68.39999, 5.1, 208.01, False),
    ("Treasure", 48.2, 5.1, 124.4, False),
    ("Barrel", 65.01, 5.1, 125.4, False),
    ("Bottles and Garbage", 88.10001, 5.1, 158.3, False),
    ("Barrel", 78.60001, 5.1, 194.8, False),
    ("Treasure", 71.70001, 5.1, 191.5, False),
    ("A bag", 98.41754, 5.100035, 178.7181, False),
]

sx = f(player_spawn.get("PositionX"), 1.801025) if player_spawn else 1.801025
sy = f(player_spawn.get("PositionY"), 5.01) if player_spawn else 5.01
sz = f(player_spawn.get("PositionZ"), 205.01) if player_spawn else 205.01

lines = []
lines.append("        // Shape playfield 1441804 from capture 20260724-mission-find-item (%d trash)" % len(mobs))
lines.append("        new MissionShape")
lines.append("        {")
lines.append("            CapturedPlayfieldId = 1441804,")
lines.append("            SpawnX = %sf, SpawnY = %sf, SpawnZ = %sf," % (sx, sy, sz))
lines.append("            Npcs = new[]")
lines.append("            {")
# Find-item host at bag position (first container used for mission item hunting area)
lines.append("                new MissionNpc")
lines.append("                {")
lines.append('                    Name = "Mission Cube",')
lines.append("                    Role = MissionNpcRole.FindTarget,")
lines.append("                    Level = 1, Health = 999999, MonsterData = 26092, Scale = 40, HeadMesh = 0,")
lines.append("                    X = 98.41754f, Y = 5.100035f, Z = 178.7181f,")
lines.append("                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,")
lines.append("                    Textures = null,")
lines.append("                    Meshes = null,")
lines.append("                },")

for r in mobs:
    name = (r.get("Name") or "Trash").replace('"', '\\"')
    lines.append("                new MissionNpc")
    lines.append("                {")
    lines.append('                    Name = "%s",' % name)
    lines.append("                    Role = MissionNpcRole.Trash,")
    lines.append("                    Level = %d, Health = %d, MonsterData = %d, Scale = %d, HeadMesh = %d," % (
        i(r.get("Level"), 150), i(r.get("Health"), 10000), i(r.get("MonsterData"), 26137),
        i(r.get("MonsterScale"), 100), i(r.get("HeadMesh"), 0)))
    lines.append("                    X = %sf, Y = %sf, Z = %sf," % (
        f(r.get("PositionX")), f(r.get("PositionY")), f(r.get("PositionZ"))))
    lines.append("                    Hx = %sf, Hy = %sf, Hz = %sf, Hw = %sf," % (
        f(r.get("HeadingX")), f(r.get("HeadingY")), f(r.get("HeadingZ")), f(r.get("HeadingW"), 1.0)))
    lines.append("                    Textures = null,")
    lines.append("                    Meshes = null,")
    lines.append("                },")

lines.append("            },")
lines.append("        },")

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines) + "\n")

# Also emit loot props CS
prop_out = r"tools-temp\_tmp_loot_1441804.csfrag"
pl = []
pl.append("        // Capture 20260724-mission-find-item PF 1441804 containers")
pl.append("        internal static readonly LootProp[] Props_1441804 =")
pl.append("        {")
for name, x, y, z, locked in props:
    pl.append('            new LootProp { Name = "%s", X = %sf, Y = %sf, Z = %sf, Locked = %s },' % (
        name, x, y, z, "true" if locked else "false"))
pl.append("        };")
with open(prop_out, "w", encoding="utf-8") as f:
    f.write("\n".join(pl) + "\n")

print("mobs", len(mobs), "spawn", sx, sy, sz)
print("wrote", OUT)
print("wrote", prop_out)
