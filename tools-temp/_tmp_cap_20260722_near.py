# -*- coding: utf-8 -*-
from __future__ import print_function
import json, os, collections, math

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-134750"
d = json.load(open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig"))

# Marcus-pad focus box around wounded/marcus
CX, CZ = 3600, 850
R = 120

players = {"Getkeep", "Sunnbeem", "Meninblack07"}
pets = {"Engineer Automaton I"}

by_name = collections.defaultdict(list)
for e in d["enemies"]:
    name = e.get("name") or ""
    if name in players or name in pets:
        continue
    p = e["position"]
    dist = math.hypot(p["x"] - CX, p["z"] - CZ)
    by_name[name].append((dist, e))

print("=== WITHIN %dm of Marcus pad ===" % R)
for name in sorted(by_name, key=lambda n: (-len([x for x in by_name[n] if x[0] <= R]), n)):
    near = [e for dist, e in by_name[name] if dist <= R]
    if not near:
        continue
    print("%3d %s" % (len(near), name))
    for e in sorted(near, key=lambda x: (x["position"]["x"], x["position"]["z"])):
        p = e["position"]
        print("    %s md=%s hp=%s/%s (%.2f, %.2f, %.2f)" % (
            e["identity"], e.get("monsterData"), e.get("currentHealth"), e.get("maxHealth"),
            p["x"], p["y"], p["z"]))
