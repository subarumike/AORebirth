import csv
import collections
import os

base = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-230406\scfu-appearance.csv"
seen = {}
rows = []
players = set()
with open(base, newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        ct = (r.get("CharacterInfoType") or "").strip()
        name = (r.get("Name") or "").strip()
        pf = (r.get("PlayfieldId") or "").strip()
        ident = r.get("Identity") or ""
        if ct == "PlayerInfo":
            players.add((name, ident, pf))
            continue
        if ct != "NPCInfo" or not name:
            continue
        if ident in seen:
            continue
        seen[ident] = 1
        rows.append(
            {
                "pf": pf,
                "name": name,
                "ident": ident,
                "md": r.get("MonsterData"),
                "level": r.get("Level"),
                "x": r.get("PositionX"),
                "y": r.get("PositionY"),
                "z": r.get("PositionZ"),
                "family": r.get("NpcFamily"),
                "side": r.get("Side"),
                "flags": r.get("Flags"),
                "head": r.get("HeadMesh"),
                "scale": r.get("MonsterScale"),
                "vf": r.get("VisualFlags"),
                "health": r.get("Health"),
                "hx": r.get("HeadingX"),
                "hy": r.get("HeadingY"),
                "hz": r.get("HeadingZ"),
                "hw": r.get("HeadingW"),
                "textures": r.get("Textures"),
                "meshes": r.get("Meshes"),
            }
        )

print("npc_count", len(rows))
print("by_pf", dict(collections.Counter(r["pf"] for r in rows)))
print("players_skipped", len(players))
for p in sorted(players):
    print("PLAYER", p)
print("---NAMES---")
names = collections.Counter(r["name"] for r in rows)
for n, c in sorted(names.items(), key=lambda x: (-x[1], x[0])):
    print("%3d %s" % (c, n))
print("---DETAIL---")
for r in sorted(rows, key=lambda x: (x["pf"], x["name"], x["ident"])):
    print(
        "{pf}\t{name}\t{ident}\tmd={md}\tlv={level}\t{x},{y},{z}\tfam={family}\tside={side}".format(
            **r
        )
    )
