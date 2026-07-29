# -*- coding: utf-8 -*-
from __future__ import print_function
import csv, collections, re, os, json

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-cap-mob-drop-cred"

print("=== AttackInfo targeting player ===")
player = "641D0C3C"
with open(os.path.join(cap, "enemy-combat.csv"), encoding="utf-8-sig") as f:
    rows = list(csv.DictReader(f))

ai = [r for r in rows if r.get("MessageType") == "AttackInfo"]
print("AttackInfo total", len(ai))
hit_player = [r for r in ai if player in (r.get("TargetIdentity") or "")]
print("AttackInfo vs player", len(hit_player))
# sample amounts / unknowns
for r in hit_player[:20]:
    print(" src=%s amt=%s u2=%s u3=%s u4=%s u5=%s u6=%s detail=%s" % (
        r.get("SourceIdentity"), r.get("Amount") or r.get("Unknown1"),
        r.get("Unknown2"), r.get("Unknown3"), r.get("Unknown4"), r.get("Unknown5"), r.get("Unknown6"),
        (r.get("Detail") or "")[:160]))

# Amount histogram for player hits
amts = []
for r in hit_player:
    d = r.get("Detail") or ""
    m = re.search(r"Unknown1=(\d+)", d)
    if m:
        amts.append(int(m.group(1)))
print("player hit Unknown1 amounts", collections.Counter(amts).most_common(20))

# SpecialAttackWeapon
saw = [r for r in rows if r.get("MessageType") == "SpecialAttackWeapon"]
print("\nSpecialAttackWeapon", len(saw))
for r in saw[:10]:
    print((r.get("Detail") or "")[:200])

# HealthDamage
hd = [r for r in rows if r.get("MessageType") == "HealthDamage"]
print("\nHealthDamage", len(hd))
for r in hd[:10]:
    print((r.get("Detail") or "")[:200])

# Loot items parse
print("\n=== LOOT ITEMS RAW ===")
with open(os.path.join(cap, "corpse-loot-observations.csv"), encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if r.get("InitialSnapshot","").lower() != "true":
            continue
        items = r.get("Items") or ""
        if not items or items in ("[]", ""):
            if int(r.get("ItemCount") or 0) == 0:
                print("%-30s credits=%s EMPTY" % (r.get("EnemyName"), r.get("CorpseCredits")))
            continue
        print("%-30s credits=%s count=%s items=%s" % (r.get("EnemyName"), r.get("CorpseCredits"), r.get("ItemCount"), items[:350]))
