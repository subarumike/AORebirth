# Brief Elysium capture 20260727-193914
import csv
import os
import sys
from collections import Counter, OrderedDict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-193914"
print("files", sorted(os.listdir(cap)))

by_id = OrderedDict()
sides = Counter()
names = Counter()
with open(os.path.join(cap, "scfu-appearance.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        if row.get("CharacterInfoType") != "NPCInfo":
            continue
        name = (row.get("Name") or "").strip()
        if not name:
            continue
        by_id[row["Identity"]] = row
        sides[row.get("Side") or "?"] += 1
        names[name] += 1

print("unique npc", len(by_id))
print("sides", dict(sides))
print("distinct names", len(names))
for n, c in names.most_common(60):
    print(" ", c, n)

name_side = {}
for row in by_id.values():
    name_side.setdefault(row.get("Name"), Counter())[row.get("Side") or "?"] += 1

print("\n=== Side by name ===")
for n in sorted(name_side):
    print(n, dict(name_side[n]))

print("\n=== Nontrivial tex/mesh ===")
shown = 0
for row in by_id.values():
    tex = row.get("Textures") or ""
    mesh = row.get("Meshes") or ""
    texov = row.get("TextureOverrides") or ""
    nontrivial = bool(mesh) or bool(texov)
    if tex and any(len(p.split(":")) >= 2 and p.split(":")[1] not in ("0", "") for p in tex.split("|")):
        nontrivial = True
    if not nontrivial:
        continue
    print(
        row.get("Name"),
        "Side",
        row.get("Side"),
        "pos",
        row.get("PositionX"),
        row.get("PositionY"),
        row.get("PositionZ"),
        "tex",
        tex[:100],
        "mesh",
        (mesh or "")[:100],
        "texov",
        (texov or "")[:80],
    )
    shown += 1
    if shown >= 40:
        break

# compare name set to known Elysium wildlife
print("\nnames only:", ", ".join(sorted(names)))
