# -*- coding: utf-8 -*-
"""Extract Brawl / Dimach / MA perk evidence from capture 20260724-001643."""
import csv
import json
import os
from collections import Counter, defaultdict

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-001643"
out = r"tools-temp\_tmp_cap_001643_specials.txt"
lines = []

info = json.load(open(os.path.join(cap, "capture_info.json"), encoding="utf-8-sig"))
lines.append("=== CAPTURE INFO ===")
for k in sorted(info.keys()):
    v = info[k]
    if isinstance(v, (list, dict)) and len(str(v)) > 200:
        lines.append("%s: <%s len=%s>" % (k, type(v).__name__, len(v)))
    else:
        lines.append("%s: %s" % (k, v))

# combat message types
combat_path = os.path.join(cap, "enemy-combat.csv")
if os.path.getsize(combat_path) > 50:
    combat = list(csv.DictReader(open(combat_path, encoding="utf-8-sig")))
    lines.append("\n=== ENEMY-COMBAT rows=%d ===" % len(combat))
    mt = Counter((r.get("MessageType"), r.get("SourceRole"), r.get("Action") or "") for r in combat)
    for k, v in mt.most_common(60):
        lines.append("  %s = %d" % (k, v))
else:
    lines.append("\n=== ENEMY-COMBAT empty/small ===")
    combat = []

# raw packet N3 types
raw = list(csv.DictReader(open(os.path.join(cap, "raw-packets.csv"), encoding="utf-8-sig")))
lines.append("\n=== RAW PACKET N3 TYPES (top) ===")
nt = Counter(r.get("N3TypeName") or "" for r in raw)
for k, v in nt.most_common(40):
    lines.append("  %s = %d" % (k, v))

# Focus specials / perks
wanted = (
    "SpecialAttack", "CharSecSpecAttack", "SpecialAttackInfo", "SpecialAttackWeapon",
    "CharacterAction", "CastNanoSpell", "Attack", "AttackInfo", "MissedAttackInfo",
    "HealthDamage", "Perk", "Buff", "SpecialUsed", "SpecialAvailable"
)
lines.append("\n=== SPECIAL / PERK / BRAWL SAMPLES ===")
count = 0
for r in combat:
    mt = r.get("MessageType") or ""
    action = r.get("Action") or ""
    detail = r.get("Detail") or ""
    blob = (mt + " " + action + " " + detail).lower()
    if any(w.lower() in blob for w in ("brawl", "dimach", "perk", "special", "martial", "flurry", "evoke", "nuke")) \
       or mt in wanted or action in ("SpecialUsed", "SpecialAvailable", "SpecialUnavailable"):
        lines.append(
            "seq=%s dir=%s %s action=%s src=%s tgt=%s amount=%s detail=%s"
            % (
                r.get("Sequence"),
                r.get("Direction"),
                mt,
                action,
                r.get("SourceIdentity") or r.get("SourceRole"),
                r.get("TargetIdentity") or r.get("TargetRole"),
                r.get("Amount"),
                detail[:280],
            )
        )
        count += 1
        if count > 120:
            lines.append("...truncated combat samples...")
            break

# fight events keyword scan
lines.append("\n=== FIGHT EVENTS matching specials/perks ===")
fe_path = os.path.join(cap, "enemy-fight-events.log")
keys = ("Brawl", "Dimach", "Special", "Perk", "Martial", "CharSec", "SpecialAttackInfo", "SpecialUsed", "Flurry", "Nuke")
with open(fe_path, encoding="utf-8", errors="replace") as f:
    n = 0
    for line in f:
        if any(k.lower() in line.lower() for k in keys):
            lines.append(line.rstrip()[:400])
            n += 1
            if n > 100:
                lines.append("...truncated fight events...")
                break

# system messages
lines.append("\n=== SYSTEM MESSAGES (head) ===")
sm = os.path.join(cap, "system-messages.log")
if os.path.exists(sm):
    with open(sm, encoding="utf-8", errors="replace") as f:
        for i, line in enumerate(f):
            if i > 80:
                break
            lines.append(line.rstrip()[:300])

# raw SpecialAttackInfo / CharSecSpecAttack hex samples
lines.append("\n=== RAW SpecialAttackInfo / CharSecSpecAttack / CharacterAction ===")
for name in ("SpecialAttackInfo", "CharSecSpecAttack", "SpecialAttackWeapon", "CharacterAction", "CastNanoSpell"):
    rows = [r for r in raw if r.get("N3TypeName") == name]
    lines.append("%s count=%d" % (name, len(rows)))
    for r in rows[:8]:
        lines.append(
            "  seq=%s dir=%s id=%s len=%s hex=%s"
            % (r.get("Sequence"), r.get("Direction"), r.get("IdentityInstance"), r.get("PacketLength"), (r.get("RawHex") or "")[:180])
        )

# chat dialogue for perk names
lines.append("\n=== CHAT / DIALOGUE hits ===")
for fname in ("chat-dialogue.log", "events.log", "npc-interactions.log"):
    p = os.path.join(cap, fname)
    if not os.path.exists(p):
        continue
    with open(p, encoding="utf-8", errors="replace") as f:
        hits = 0
        for line in f:
            low = line.lower()
            if any(k in low for k in ("brawl", "dimach", "perk", "martial", "flurry", "special")):
                lines.append("[%s] %s" % (fname, line.rstrip()[:300]))
                hits += 1
                if hits > 40:
                    break

open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", out, "lines", len(lines))
