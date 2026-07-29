import json
import collections
import os

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260720-212302"
with open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig") as f:
    d = json.load(f)

skip_exact = {
    "Alex Gibbs",
    "Marcus Stone",
    "Flint Novak",
    "Shipping Manifest Terminal",
    "Wounded Dockworker",
    "Rex Larsson",
    "Stan Larsson",
    "Bart Moss",
    "Natalia",
}
skip_substr = ("Terminal", "Vendor", "Shop", "Statue")

names = collections.Counter()
mobs = []
for e in d["enemies"]:
    n = e.get("name") or ""
    names[n] += 1
    if n in skip_exact:
        continue
    if any(s in n for s in skip_substr):
        continue
    # keep hostiles / robots / fleas
    mobs.append(e)

print("ALL NAMES:")
for n, c in names.most_common():
    print("  %3d %s" % (c, n))

print("\nHOSTILE CANDIDATES:", len(mobs))
by = collections.defaultdict(list)
for e in mobs:
    by[e["name"]].append(e)

for n, lst in sorted(by.items()):
    print("\n=== %s x%d ===" % (n, len(lst)))
    e0 = lst[0]
    print(
        "  monsterData",
        e0.get("monsterData"),
        "level",
        e0.get("level"),
        "hp",
        e0.get("maxHealth"),
        "scale",
        e0.get("monsterScale"),
        "npcFamily",
        e0.get("npcFamily"),
        "run",
        e0.get("runSpeed"),
    )
    for e in lst:
        p = e["position"]
        print(
            "  %s hp=%s/%s pos=(%.4f,%.4f,%.4f) death=%s"
            % (
                e["identity"],
                e.get("currentHealth"),
                e.get("maxHealth"),
                p["x"],
                p["y"],
                p["z"],
                e.get("deathObserved"),
            )
        )

# SCFU appearance if present
scfu = os.path.join(cap, "scfu-appearance.csv")
if os.path.isfile(scfu):
    import csv

    print("\nSCFU rows:")
    with open(scfu, encoding="utf-8") as f:
        r = csv.DictReader(f)
        seen = set()
        for row in r:
            key = (row.get("Name") or row.get("name") or "", row.get("MonsterData") or "")
            if key in seen:
                continue
            seen.add(key)
            print(" ", key, {k: row[k] for k in row if row[k] and k not in ("CapturedUtc",)})
