# -*- coding: utf-8 -*-
from __future__ import print_function
import csv, json, os, collections, re, ast

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-cap-mob-drop-cred"

# --- loot summary by enemy ---
print("=== LOOT BY ENEMY (initial snapshots) ===")
by = collections.defaultdict(lambda: {"credits": [], "items": collections.Counter(), "n": 0})
with open(os.path.join(cap, "corpse-loot-observations.csv"), encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if r.get("InitialSnapshot", "").lower() != "true":
            continue
        name = r.get("EnemyName") or "?"
        by[name]["n"] += 1
        try:
            by[name]["credits"].append(int(r.get("CorpseCredits") or 0))
        except Exception:
            pass
        items = r.get("Items") or ""
        # Items may be JSON-ish
        for m in re.finditer(r"LowId[=:]?\s*(\d+)|Template[=:]?\s*(\d+)|Id[=:]?\s*(\d+)|QL[=:]?\s*(\d+)|Name[=:]?\s*([^,;|}]+)", items, re.I):
            pass
        # print raw items for first few of each
        if by[name]["n"] <= 3:
            print(" ", name, "credits=", r.get("CorpseCredits"), "itemCount=", r.get("ItemCount"), "items=", (items[:300] if items else ""))

print("\nCREDIT RANGES:")
for name, d in sorted(by.items()):
    creds = d["credits"]
    if not creds:
        continue
    print(" %-35s n=%d credits=%s avg=%.1f" % (name, d["n"], sorted(set(creds)), sum(creds)/float(len(creds))))

# --- hit messages ---
print("\n=== YOU GOT HIT / DAMAGE TEXT ===")
paths = [os.path.join(cap, "system-messages.log"), os.path.join(cap, "events.log"), os.path.join(cap, "chat-dialogue.log")]
hit_pat = re.compile(r"You got hit|you were hit|hit you|Damage|red|FormatFeedback|Feedback", re.I)
for p in paths:
    if not os.path.exists(p):
        continue
    hits = 0
    with open(p, encoding="utf-8-sig", errors="replace") as f:
        for line in f:
            if "You got hit" in line or "got hit" in line.lower() or "FormatFeedback" in line and "hit" in line.lower():
                print(os.path.basename(p), line.rstrip()[:260])
                hits += 1
                if hits > 30:
                    break
    print(os.path.basename(p), "hit-like lines shown", hits)

# Search packets for FormatFeedback containing hit
print("\n=== FormatFeedback in events ===")
ev = os.path.join(cap, "events.log")
with open(ev, encoding="utf-8-sig", errors="replace") as f:
    n = 0
    for line in f:
        if "FormatFeedback" in line or "You got hit" in line or "got hit for" in line.lower():
            print(line.rstrip()[:300])
            n += 1
            if n > 40:
                break
    print("total shown", n)

# AttackInfo / SpecialAttackWeapon
print("\n=== AttackInfo / SpecialAttackWeapon sample ===")
with open(os.path.join(cap, "enemy-combat.csv"), encoding="utf-8-sig") as f:
    rows = list(csv.DictReader(f))
for mt in ("AttackInfo", "SpecialAttackWeapon", "HealthDamage", "Feedback", "VicinityDamage"):
    subset = [r for r in rows if r.get("MessageType") == mt]
    print(mt, len(subset))
    for r in subset[:5]:
        print(" ", r.get("SourceIdentity"), "->", r.get("TargetIdentity"), "amt", r.get("Amount"), "detail", (r.get("Detail") or "")[:180])

# MessageType counts
print("\nMessageType counts:")
print(collections.Counter(r.get("MessageType") for r in rows).most_common(30))

# Exact spots for combat mobs (first position)
print("\n=== MOB FIRST POSITIONS (dossier) ===")
d = json.load(open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig"))
focus = {"Cleaning Robot", "Waste Collector", "Garbage Flea", "32-V Docker", "Cleanmeister Intelligence Robot",
         "Supreme Collector of Waste", "IIV-X Advanced Docker", "Burning Cleaning Robot", "Malfunctioning Cleaning Robot"}
for e in d["enemies"]:
    name = e.get("name") or ""
    if name not in focus:
        continue
    p = e["position"]
    print("%-35s %s hp=%s/%s pos=(%.3f, %.3f, %.3f) death=%s" % (
        name, e["identity"], e.get("currentHealth"), e.get("maxHealth"), p["x"], p["y"], p["z"], e.get("deathObserved")))
