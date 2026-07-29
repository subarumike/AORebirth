# -*- coding: utf-8 -*-
from __future__ import print_function
import csv, json, os, collections, re

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-cap-mob-drop-cred"

print("=== CAPTURE INFO ===")
info = json.load(open(os.path.join(cap, "capture_info.json"), encoding="utf-8-sig"))
print("duration", info.get("sessionDurationSeconds"), "char", info.get("characterName"))
print("counts", {k: info.get("captureCounts", {}).get(k) for k in (
    "enemyDeathEvents","corpseLootObservationRows","enemyCombatRows","enemyFightCaptureStarted",
    "systemMessages","rawSimpleCharFullUpdatePackets")})

print("\n=== SYSTEM MSGS (hit/reward/credit) ===")
hit_re = re.compile(r"You got hit|Received reward|credits|damage|XP", re.I)
with open(os.path.join(cap, "system-messages.log"), encoding="utf-8-sig", errors="replace") as f:
    for line in f:
        if hit_re.search(line):
            print(line.rstrip()[:240])

print("\n=== CORPSE LOOT ===")
loot_path = os.path.join(cap, "corpse-loot-observations.csv")
with open(loot_path, encoding="utf-8-sig", errors="replace") as f:
    rows = list(csv.DictReader(f))
print("rows", len(rows))
if rows:
    print("cols", rows[0].keys())
    for r in rows[:40]:
        print({k: r.get(k) for k in list(r.keys())[:12]})

print("\n=== ENEMY COMBAT sample ===")
with open(os.path.join(cap, "enemy-combat.csv"), encoding="utf-8-sig", errors="replace") as f:
    rows = list(csv.DictReader(f))
print("rows", len(rows), "cols", rows[0].keys() if rows else None)
kinds = collections.Counter(r.get("EventType") or r.get("eventType") or r.get("Action") or "?" for r in rows)
print("kinds", kinds)
for r in rows[:15]:
    print(r)

print("\n=== FIGHT EVENTS head ===")
with open(os.path.join(cap, "enemy-fight-events.log"), encoding="utf-8-sig", errors="replace") as f:
    for i, line in enumerate(f):
        if i > 40:
            break
        print(line.rstrip()[:220])

print("\n=== DOSSIER combat-relevant ===")
d = json.load(open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig"))
players = set()
by = collections.defaultdict(list)
for e in d["enemies"]:
    name = e.get("name") or ""
    md = str(e.get("monsterData") or "")
    if md == "0":
        continue
    by[name].append(e)
for name in sorted(by, key=lambda n: (-len(by[n]), n)):
    es = by[name]
    death = sum(1 for e in es if e.get("deathObserved"))
    print("%3d death=%d %-35s md=%s" % (len(es), death, name, sorted({str(e.get("monsterData")) for e in es})))
