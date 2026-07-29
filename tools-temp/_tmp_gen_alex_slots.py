# -*- coding: utf-8 -*-
"""Emit unique Alex-area MobSlot positions from capture 20260722-cap-mob-drop-cred."""
from __future__ import print_function
import json, os, math, collections

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-cap-mob-drop-cred"
d = json.load(open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig"))

# Focus combat mobs near Alex (~3520,5,860) and waste pile north
FOCUS = {
    "32-V Docker": ("Docker", 17649, 3, 35, 1019, 110, 11, "Passive", 0.0),
    "Waste Collector": ("WasteCollector", 17714, 2, 29, 1019, 75, 12, "Passive", 0.0),
    "Garbage Flea": ("GarbageFlea", 17657, 2, 24, 25, 125, 8, "Aggressive", 1.0),
    "Cleanmeister Intelligence Robot": ("CleaningRobot", 297023, 2, 180, 1019, 100, 13, "Passive", 0.0),
    "IIV-X Advanced Docker": ("Docker", 17649, 4, 323, 1019, 110, 15, "Passive", 0.0),
    "Supreme Collector of Waste": ("WasteCollector", 17714, 4, 60, 1019, 75, 12, "Passive", 0.0),
}

# Cluster radius meters (XZ)
R = 3.0

def cluster(points):
    kept = []
    for p in sorted(points, key=lambda t: (t[0], t[2])):
        if any(math.hypot(p[0]-k[0], p[2]-k[2]) < R for k in kept):
            continue
        kept.append(p)
    return kept

by = collections.defaultdict(list)
for e in d["enemies"]:
    name = e.get("name") or ""
    if name not in FOCUS:
        continue
    p = e["position"]
    # skip far oasis fleas (y~0.01 and x<3460)
    if name == "Garbage Flea" and p["x"] < 3460:
        continue
    by[name].append((p["x"], p["y"], p["z"], e.get("level") or FOCUS[name][2], e.get("maxHealth") or FOCUS[name][3]))

print("// Auto from capture 20260722-cap-mob-drop-cred (cluster %.1fm)" % R)
for name in ("32-V Docker", "Waste Collector", "Garbage Flea", "Cleanmeister Intelligence Robot",
             "IIV-X Advanced Docker", "Supreme Collector of Waste"):
    meta = FOCUS[name]
    kind, md, lvl, hp, fam, scale, spd, ai, aggro = meta
    pts = cluster([(x,y,z,lv,h) for x,y,z,lv,h in by[name]])
    print("// %s n=%d" % (name, len(pts)))
    for x,y,z,lv,h in pts:
        # use dossier level/hp when present
        use_lvl = int(lv) if lv else lvl
        use_hp = int(h) if h else hp
        # Supreme/IIV keep capture names
        print('                new MobSlot("%s", MobKind.%s, %d, %d, %d, %d, %d, %d, NpcAiProfile.%s, %.1ff, %.3ff, %.3ff, %.3ff),' % (
            name, kind, md, use_lvl, use_hp, fam, scale, spd, ai, aggro, x, y, z))
