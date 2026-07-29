# -*- coding: utf-8 -*-
"""Extract Barking Chimera fight + loot evidence from capture 20260723-225021."""
import csv
import collections
import json
import os

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260723-225021"
out = r"tools-temp\_tmp_cap_225021_chimera.txt"
lines = []

lines.append("=== LOOT OBSERVATIONS ===")
loot_rows = list(csv.DictReader(open(os.path.join(cap, "corpse-loot-observations.csv"), encoding="utf-8-sig")))
lines.append("corpses=%d" % len(loot_rows))
empty = [r for r in loot_rows if int(r["ItemCount"] or 0) == 0]
with_items = [r for r in loot_rows if int(r["ItemCount"] or 0) > 0]
lines.append("empty=%d with_items=%d credits_all=%s" % (
    len(empty), len(with_items), sorted({r["CorpseCredits"] for r in loot_rows})))
item_freq = collections.Counter()
for r in loot_rows:
    lines.append("  %s lvl=%s items=%s count=%s" % (
        r["DeadNpcIdentity"], r["EnemyLevel"], r["Items"] or "(empty)", r["ItemCount"]))
    if not r["Items"]:
        continue
    for part in r["Items"].split(";"):
        a = part.split(":")
        item_freq[(a[0], a[1], a[2])] += 1
lines.append("ITEM_FREQ low:high:ql count")
for (lo, hi, ql), n in item_freq.most_common():
    lines.append("  %s:%s ql%s x%d" % (lo, hi, ql, n))

lines.append("\n=== CORPSE FULL UPDATES (unique dead ids) ===")
seen = set()
with open(os.path.join(cap, "corpse-full-updates.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        did = row.get("DeadNpcIdentity") or ""
        if did in seen:
            continue
        seen.add(did)
        lines.append(
            "%s catMesh=%s md=%s credits=%s scale=%s name=%s pos=(%s,%s,%s)"
            % (
                did,
                row.get("CorpseCatMesh"),
                row.get("CorpseMonsterData"),
                row.get("CorpseCredits"),
                row.get("MonsterScale"),
                row.get("CorpseName"),
                row.get("PositionX"),
                row.get("PositionY"),
                row.get("PositionZ"),
            )
        )

lines.append("\n=== COMBAT MESSAGE TYPE COUNTS (enemy-related) ===")
combat = list(csv.DictReader(open(os.path.join(cap, "enemy-combat.csv"), encoding="utf-8-sig")))
mt = collections.Counter()
enemy_attack_info = []
for r in combat:
    key = (r["MessageType"], r["SourceRole"], r.get("Action") or "")
    mt[key] += 1
    if r["MessageType"] == "AttackInfo" and r["SourceRole"] == "enemy":
        enemy_attack_info.append(r)
    if r["MessageType"] in ("Attack", "SpecialAttackWeapon", "CharacterAction", "LookAt") and r["SourceRole"] == "enemy":
        enemy_attack_info.append(r)
for k, v in mt.most_common(50):
    lines.append("  %s = %d" % (k, v))

lines.append("\n=== ENEMY ATTACK / SAW / ATTACKINFO SAMPLES ===")
for r in combat:
    if r["SourceRole"] != "enemy":
        continue
    if r["MessageType"] not in ("Attack", "AttackInfo", "SpecialAttackWeapon", "CharacterAction", "CombatMode", "FollowTarget"):
        continue
    lines.append(
        "seq=%s %s action=%s src=%s tgt=%s amount=%s detail=%s"
        % (
            r["Sequence"],
            r["MessageType"],
            r.get("Action") or "",
            r.get("SourceIdentity") or "",
            r.get("TargetIdentity") or "",
            r.get("Amount") or "",
            (r.get("Detail") or "")[:220],
        )
    )
    if len([x for x in lines if x.startswith("seq=")]) > 80:
        lines.append("...truncated...")
        break

lines.append("\n=== FIGHT EVENTS (first 80 lines) ===")
fe = open(os.path.join(cap, "enemy-fight-events.log"), encoding="utf-8", errors="replace").read().splitlines()
lines.extend(fe[:80])

lines.append("\n=== STAT UPDATES mentioning damage / health (sample) ===")
with open(os.path.join(cap, "enemy-stat-updates.csv"), encoding="utf-8-sig", newline="") as f:
    for i, row in enumerate(csv.DictReader(f)):
        if i > 40:
            lines.append("...truncated stats...")
            break
        lines.append(
            "seq=%s id=%s stat=%s val=%s"
            % (row.get("Sequence"), row.get("Identity") or row.get("SourceIdentity"), row.get("Stat") or row.get("StatId"), row.get("Value") or row.get("Amount"))
        )

# dossier barkers
lines.append("\n=== DOSSIER Barking Chimera ===")
d = json.load(open(os.path.join(cap, "enemy-dossier.json"), encoding="utf-8-sig"))
for e in d.get("enemies", []):
    if e.get("name") != "Barking Chimera":
        continue
    lines.append(
        "%s lvl=%s md=%s hm=%s flags=%s health=%s pos=(%.3f,%.3f,%.3f)"
        % (
            e.get("identity"),
            e.get("level"),
            e.get("monsterData"),
            e.get("headMesh"),
            e.get("characterFlags"),
            e.get("health") or e.get("maxHealth"),
            e["position"]["x"],
            e["position"]["y"],
            e["position"]["z"],
        )
    )

open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", out, "lines", len(lines))
