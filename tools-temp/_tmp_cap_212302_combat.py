import csv
import collections
import os

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260720-212302"

# Combat damage by enemy name
dmg = collections.defaultdict(list)
with open(os.path.join(cap, "enemy-combat.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        name = row.get("EnemyName") or row.get("Name") or ""
        for k in ("Damage", "Unknown1", "damage"):
            if row.get(k):
                try:
                    dmg[name].append(int(float(row[k])))
                except Exception:
                    pass
                break

print("COMBAT damage samples:")
for n, vals in sorted(dmg.items()):
    if not vals:
        continue
    print("  %s n=%d min=%d max=%d vals=%s" % (n, len(vals), min(vals), max(vals), vals[:12]))

# Cleaning robot first-seen positions (prefer dossier living non-death, pad area)
import json
with open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig") as f:
    d = json.load(f)

robots = []
for e in d["enemies"]:
    if e.get("name") != "Cleaning Robot":
        continue
    p = e["position"]
    # pad cluster roughly y~5 and z 850-920 x 3540-3630
    robots.append((p["x"], p["y"], p["z"], e.get("maxHealth"), e.get("deathObserved"), e["identity"]))

print("\nCleaning Robot count", len(robots))
# Dedup by rounding to 1m
seen = set()
unique = []
for x, y, z, hp, death, ident in robots:
    key = (round(x), round(z))
    if key in seen:
        continue
    seen.add(key)
    unique.append((x, y, z, hp, death, ident))
print("Unique ~1m:", len(unique))
for u in unique:
    print("  %.4f, %.4f, %.4f hp=%s death=%s %s" % (u[0], u[1], u[2], u[3], u[4], u[5]))
