# -*- coding: utf-8 -*-
from __future__ import print_function
import json
import collections
import os
import re

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-134750"
dossier = json.load(open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig"))
spawn_path = r"AORebirth\Server\ZoneEngine\Core\Playfields\AreteLandingSpawn.cs"
spawn_txt = open(spawn_path, encoding="utf-8-sig").read()

# Parse spawn names + positions for wounded
spawn_names = collections.Counter(re.findall(r'Name = "([^"]+)"', spawn_txt))
wounded_spawn = re.findall(
    r'Name = "Wounded Dockworker",.*?X = ([0-9.f]+)f, Y = ([0-9.f]+)f, Z = ([0-9.f]+)f',
    spawn_txt,
    re.S,
)

playerish = set()
by = collections.defaultdict(list)
for e in dossier["enemies"]:
    name = e.get("name") or ""
    # skip obvious players / pets when npc=False in events; dossier lacks flags — filter by monsterData 0
    md = str(e.get("monsterData") or "")
    if md in ("0", "1234567890") and name not in ("Shipping Manifest Terminal",):
        # still keep if named known NPC
        pass
    by[name].append(e)

print("=== CAPTURE UNIQUE NAMES ===")
for name in sorted(by, key=lambda n: (-len(by[n]), n)):
    es = by[name]
    hps = sorted({(e.get("currentHealth"), e.get("maxHealth")) for e in es})
    mds = sorted({str(e.get("monsterData")) for e in es})
    in_spawn = spawn_names.get(name, 0)
    print("%3d cap | %3d spawn | %-35s md=%s hp=%s" % (len(es), in_spawn, name, mds, hps))

print("\n=== WOUNDED CAPTURE POS ===")
for e in sorted(by.get("Wounded Dockworker", []), key=lambda x: (x["position"]["x"], x["position"]["z"])):
    p = e["position"]
    print("%s hp=%s/%s pos=(%.3f, %.3f, %.3f) scale=%s head=%s" % (
        e["identity"], e["currentHealth"], e["maxHealth"], p["x"], p["y"], p["z"],
        e.get("monsterScale"), e.get("headMesh")))

print("\n=== WOUNDED SPAWN POS ===")
for x, y, z in wounded_spawn:
    print("spawn (%.3f, %.3f, %.3f)" % (float(x.replace("f","")), float(y.replace("f","")), float(z.replace("f",""))))

print("\n=== MISSING FROM SPAWN (name count) ===")
for name, es in sorted(by.items()):
    if spawn_names.get(name, 0) == 0:
        # skip players heuristically: monsterData 0
        mds = {str(e.get("monsterData")) for e in es}
        if mds <= {"0"}:
            continue
        print("MISSING %s count=%d md=%s" % (name, len(es), sorted(mds)))

print("\n=== UNDERSPAWNED ===")
for name, es in sorted(by.items()):
    sc = spawn_names.get(name, 0)
    if sc > 0 and len(es) > sc:
        print("UNDER %s cap=%d spawn=%d" % (name, len(es), sc))
