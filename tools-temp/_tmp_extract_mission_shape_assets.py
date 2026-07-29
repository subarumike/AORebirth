# Extract PlayfieldAnarchyF + first player pos per mission instance + door/chest hex catalogs.
from __future__ import print_function
import csv, os, collections

CAP = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-5-different-shape-fo-mish"
OUT_DIR = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_mission_shapes_assets"
os.makedirs(OUT_DIR, exist_ok=True)

PF_HEX = {0x15A82E: 1419310, 0x15A876: 1419382, 0x15A847: 1419335}
windows = [
    (1419310, "2026-07-19T03:33:19", "2026-07-19T03:37:12"),
    (1419382, "2026-07-19T03:37:26", "2026-07-19T03:40:38"),
    (1419335, "2026-07-19T03:40:38", "2026-07-19T03:46:46"),
]

def pf_for_utc(utc):
    for pf, s, e in windows:
        if s <= utc <= e:
            return pf
    return None

# PlayfieldAnarchyF bodies
paf = collections.defaultdict(list)
doors = collections.defaultdict(list)
chests = collections.defaultdict(list)
with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("Direction") or "").upper() != "IN":
            continue
        n3 = (r.get("N3TypeName") or "").strip()
        hx = (r.get("RawHex") or "").strip().upper()
        utc = r.get("CapturedUtc") or ""
        if n3 == "PlayfieldAnarchyF":
            pf = None
            for needle, pfi in PF_HEX.items():
                if ("%08X" % needle) in hx:
                    pf = pfi
                    break
            if pf is None:
                pf = pf_for_utc(utc)
            if pf:
                paf[pf].append(hx)
        elif n3 in ("DoorFullUpdate", "ChestFullUpdate"):
            pf = None
            for needle, pfi in PF_HEX.items():
                if ("%08X" % needle) in hx:
                    pf = pfi
                    break
            if pf is None:
                pf = pf_for_utc(utc)
            if pf is None:
                continue
            # unique by last 40 bytes
            key = hx[-80:]
            bucket = doors if n3 == "DoorFullUpdate" else chests
            if any(x.endswith(key) for x in bucket[pf]):
                continue
            bucket[pf].append(hx)

summary = []
for pf in sorted(PF_HEX.values()):
    summary.append("PF %d PAF=%d doors=%d chests=%d" % (pf, len(paf[pf]), len(doors[pf]), len(chests[pf])))
    open(os.path.join(OUT_DIR, "paf_%d.hex" % pf), "w").write("\n".join(paf[pf]))
    open(os.path.join(OUT_DIR, "doors_%d.hex" % pf), "w").write("\n".join(doors[pf]))
    open(os.path.join(OUT_DIR, "chests_%d.hex" % pf), "w").write("\n".join(chests[pf]))

# Player SCFU first position in each instance
player_pos = {}
with open(os.path.join(CAP, "scfu-appearance.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        pf = int(r.get("PlayfieldId") or 0)
        if pf not in PF_HEX.values():
            continue
        if (r.get("CharacterInfoType") or "") != "PlayerInfo":
            continue
        if pf in player_pos:
            continue
        player_pos[pf] = (r.get("PositionX"), r.get("PositionY"), r.get("PositionZ"), r.get("Name"))

for pf, pos in sorted(player_pos.items()):
    summary.append("player_spawn PF%s %s @ %s,%s,%s" % (pf, pos[3], pos[0], pos[1], pos[2]))

# Generate C# spawn catalog from SCFU
def parse_tex(s):
    out = []
    for part in (s or "").split("|"):
        if not part: continue
        f = part.split(":")
        place, tex = int(f[0]), int(f[1])
        if tex > 0:
            out.append((place, tex))
    return out

def parse_mesh(s):
    out = []
    for part in (s or "").split("|"):
        if not part: continue
        f = part.split(":")
        if len(f) >= 4 and int(f[1]) > 0:
            out.append(tuple(int(x) for x in f[:4]))
    return out

def csharp_tex(texs):
    if not texs: return "null"
    return "new[] { " + ", ".join("new[] { %d, %d }" % t for t in texs) + " }"

def csharp_mesh(meshes):
    if not meshes: return "null"
    return "new[] { " + ", ".join("new[] { %d, %d, %d, %d }" % m for m in meshes) + " }"

NAMED_FIND = {"Berneice Cornelius", "Nichole Orender", "Chae Aronstein"}
KILL_BOSS = {"Carlo Pinnetti"}

shapes = {}
with open(os.path.join(CAP, "scfu-appearance.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        pf = int(r.get("PlayfieldId") or 0)
        if pf not in PF_HEX.values():
            continue
        if (r.get("CharacterInfoType") or "") != "NPCInfo":
            continue
        name = (r.get("Name") or "").strip()
        md = int(r.get("MonsterData") or 0)
        vf = int(r.get("VisualFlags") or 0)
        if not name or md <= 0 or vf == 127:
            continue
        ident = r.get("Identity")
        shapes.setdefault(pf, {})
        if ident in shapes[pf]:
            continue
        role = "Trash"
        if name in NAMED_FIND:
            role = "FindTarget"
        elif name in KILL_BOSS:
            role = "KillBoss"
        elif name == "CEO Guardian":
            role = "KillGuard"
        shapes[pf][ident] = dict(r, _role=role)

# write csharp fragment
cs = []
cs.append("// AUTO from capture 20260719-5-different-shape-fo-mish")
for pf in sorted(shapes.keys()):
    cs.append("        // Shape playfield %d (%d npcs)" % (pf, len(shapes[pf])))
    cs.append("        new MissionShape")
    cs.append("        {")
    cs.append("            CapturedPlayfieldId = %d," % pf)
    spawn = player_pos.get(pf, ("5", "5.01", "100", ""))
    cs.append("            SpawnX = %sf, SpawnY = %sf, SpawnZ = %sf," % (spawn[0], spawn[1], spawn[2]))
    cs.append("            Npcs = new[]")
    cs.append("            {")
    for ident, r in sorted(shapes[pf].items(), key=lambda kv: (kv[1].get("Name"), kv[0])):
        name = r["Name"].replace("\\", "\\\\").replace('"', '\\"')
        texs = parse_tex(r.get("Textures"))
        meshes = parse_mesh(r.get("Meshes"))
        role = r["_role"]
        cs.append("                new MissionNpc")
        cs.append("                {")
        cs.append('                    Name = "%s",' % name)
        cs.append("                    Role = MissionNpcRole.%s," % role)
        cs.append("                    Level = %s, Health = %s, MonsterData = %s, Scale = %s, HeadMesh = %s," % (
            r.get("Level") or 1, r.get("Health") or 1, r.get("MonsterData"), r.get("MonsterScale") or 100, r.get("HeadMesh") or 0))
        cs.append("                    X = %sf, Y = %sf, Z = %sf," % (r.get("PositionX"), r.get("PositionY"), r.get("PositionZ")))
        hx = r.get("HeadingX") or 0; hy = r.get("HeadingY") or 0; hz = r.get("HeadingZ") or 0; hw = r.get("HeadingW") or 1
        cs.append("                    Hx = %sf, Hy = %sf, Hz = %sf, Hw = %sf," % (hx, hy, hz, hw))
        cs.append("                    Textures = %s," % csharp_tex(texs))
        cs.append("                    Meshes = %s," % csharp_mesh(meshes))
        cs.append("                },")
    cs.append("            },")
    cs.append("        },")

open(os.path.join(OUT_DIR, "shapes_fragment.cs"), "w", encoding="utf-8", newline="\n").write("\n".join(cs) + "\n")
open(os.path.join(OUT_DIR, "summary.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(summary) + "\n")
print("\n".join(summary))
print("wrote", OUT_DIR)
