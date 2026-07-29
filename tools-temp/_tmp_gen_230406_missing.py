import csv
import re

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-230406\scfu-appearance.csv"
spawn = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\NascenceLifeSpawn.cs"
out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_230406_missing_blocks.txt"

text = open(spawn, encoding="utf-8").read()
blocks = re.findall(r"new LifeNpc\s*\{(.*?)\n\s*\},", text, re.S)
spawned = []
for b in blocks:
    def grab(key, default="0"):
        m = re.search(r"%s\s*=\s*([^,\n]+)" % key, b)
        return m.group(1).strip().rstrip("f") if m else default

    spawned.append(
        {
            "name": grab("Name", '""').strip('"'),
            "pf": int(grab("PlayfieldId")),
            "x": float(grab("X")),
            "y": float(grab("Y")),
            "z": float(grab("Z")),
        }
    )


def parse_tex(s):
    out = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        place, tex = int(f[0]), int(f[1])
        if tex > 0:
            out.append((place, tex))
    return out


def parse_mesh(s):
    out = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        out.append(tuple(int(x) for x in f[:4]))
    return out


def csharp_tex(texs):
    if not texs:
        return "null"
    return "new[] { " + ", ".join("new[] { %d, %d }" % t for t in texs) + " }"


def csharp_mesh(meshes):
    if not meshes:
        return "null"
    return "new[] { " + ", ".join("new[] { %d, %d, %d, %d }" % m for m in meshes) + " }"


cap_npcs = []
seen = set()
skipped_playerlike = 0
with open(cap, newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("CharacterInfoType") or "").strip() != "NPCInfo":
            continue
        name = (r.get("Name") or "").strip()
        ident = r.get("Identity") or ""
        if not name or ident in seen:
            continue
        md = int(r.get("MonsterData") or "0")
        vf = int(r.get("VisualFlags") or "31")
        # Never spawn player-like SCFUs (Thrak garden Executron mistake).
        if md <= 0 or vf == 127:
            skipped_playerlike += 1
            continue
        seen.add(ident)
        c = {
            "name": name,
            "pf": int(r["PlayfieldId"]),
            "x": float(r["PositionX"]),
            "y": float(r["PositionY"]),
            "z": float(r["PositionZ"]),
            "r": r,
        }
        matched = False
        for s in spawned:
            if s["pf"] != c["pf"] or s["name"] != c["name"]:
                continue
            dx = s["x"] - c["x"]
            dy = s["y"] - c["y"]
            dz = s["z"] - c["z"]
            if dx * dx + dy * dy + dz * dz < 4.0:
                matched = True
                break
        if not matched:
            cap_npcs.append(c)

lines = []
lines.append("// missing_scfu=%d skipped_playerlike=%d" % (len(cap_npcs), skipped_playerlike))
for c in sorted(cap_npcs, key=lambda x: (x["pf"], x["name"], x["r"].get("Identity"))):
    r = c["r"]
    name = r["Name"].replace("\\", "\\\\").replace('"', '\\"')
    texs = parse_tex(r.get("Textures"))
    meshes = parse_mesh(r.get("Meshes"))
    lines.append(
        """            new LifeNpc
            {
                PlayfieldId = %s,
                Name = "%s",
                Level = %s, Health = %s, MonsterData = %s, Scale = %s, VisualFlags = %s, HeadMesh = %s,
                X = %sf, Y = %sf, Z = %sf,
                Hx = %sf, Hy = %sf, Hz = %sf, Hw = %sf,
                Textures = %s,
                Meshes = %s,
                CaptureFolder = "20260718-230406",
            },"""
        % (
            c["pf"],
            name,
            r.get("Level") or 1,
            r.get("Health") or 1,
            r.get("MonsterData"),
            r.get("MonsterScale") or 100,
            r.get("VisualFlags") or 31,
            r.get("HeadMesh") or 0,
            r.get("PositionX"),
            r.get("PositionY"),
            r.get("PositionZ"),
            r.get("HeadingX") or 0,
            r.get("HeadingY") or 0,
            r.get("HeadingZ") or 0,
            r.get("HeadingW") or 1,
            csharp_tex(texs),
            csharp_mesh(meshes),
        )
    )

# Drake: NPCInfo lifecycle only (no SCFU in this capture — already in-world).
# player=False npc=True monsterData=26092 visualFlags=31 — NOT player-like.
lines.append(
    """            new LifeNpc
            {
                PlayfieldId = 4310,
                Name = "Scientist Drake Rodriguez",
                Level = 200, Health = 164773, MonsterData = 26092, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                X = 854.5125f, Y = 34.405f, Z = 958.5875f,
                Hx = 0f, Hy = -0.9730012f, Hz = 0f, Hw = 0.23080005f,
                Textures = null,
                Meshes = null,
                CaptureFolder = "20260718-230406",
            },"""
)

open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", out, "blocks", len(cap_npcs) + 1)
