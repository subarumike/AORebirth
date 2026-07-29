import csv
import re

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-230406\scfu-appearance.csv"
spawn = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\NascenceLifeSpawn.cs"

# Load capture NPCInfo only
cap_npcs = []
seen = set()
with open(cap, newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("CharacterInfoType") or "").strip() != "NPCInfo":
            continue
        name = (r.get("Name") or "").strip()
        ident = r.get("Identity") or ""
        if not name or ident in seen:
            continue
        seen.add(ident)
        cap_npcs.append(
            {
                "name": name,
                "pf": int(r["PlayfieldId"]),
                "x": float(r["PositionX"]),
                "y": float(r["PositionY"]),
                "z": float(r["PositionZ"]),
                "ident": ident,
                "md": r.get("MonsterData"),
            }
        )

# Parse spawn entries
text = open(spawn, encoding="utf-8").read()
blocks = re.findall(
    r"new LifeNpc\s*\{(.*?)\n\s*\},",
    text,
    re.S,
)
spawned = []
for b in blocks:
    def grab(key, default="0"):
        m = re.search(r"%s\s*=\s*([^,\n]+)" % key, b)
        return m.group(1).strip().rstrip("f") if m else default

    name = grab("Name", '""').strip('"')
    spawned.append(
        {
            "name": name,
            "pf": int(grab("PlayfieldId")),
            "x": float(grab("X")),
            "y": float(grab("Y")),
            "z": float(grab("Z")),
        }
    )

print("capture_npcs", len(cap_npcs), "spawned", len(spawned))
missing = []
for c in cap_npcs:
    matched = False
    for s in spawned:
        if s["pf"] != c["pf"] or s["name"] != c["name"]:
            continue
        dx = s["x"] - c["x"]
        dy = s["y"] - c["y"]
        dz = s["z"] - c["z"]
        if dx * dx + dy * dy + dz * dz < 4.0:  # within 2m
            matched = True
            break
    if not matched:
        missing.append(c)

print("missing_or_far", len(missing))
for m in sorted(missing, key=lambda x: (x["pf"], x["name"])):
    print(
        "{pf} {name} {ident} md={md} @ {x:.2f},{y:.2f},{z:.2f}".format(**m)
    )
