# Capture 20260727-055715: Karli Cappelleri + alien XP/loot
from __future__ import print_function
import csv
import json
import re
import struct
from collections import Counter, defaultdict
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-055715")
out = Path(r"tools-temp/_tmp_karli_055715.txt")
lines = []

def add(s=""):
    lines.append(s)

NAME = "Karli"
# --- dossier / CHAR-SEEN ---
add("=== Karli identity / spawn ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "Karli" in ln or "Cappelleri" in ln:
        add(ln[:700])
add()

# --- dialogue / knubot ---
add("=== chat-dialogue Karli / Knubot / Append ===")
for ln in (cap / "chat-dialogue.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if any(k in ln for k in ("Karli", "Knubot", "Append", "Answer", "Quest", "NCU", "XP", "buff", "will")):
        add(ln[:600])
add()

add("=== npc-interactions Karli ===")
for ln in (cap / "npc-interactions.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if any(k in ln for k in ("Karli", "Knubot", "Append", "AnswerList", "Quest", "Cappell")):
        add(ln[:700])
add()

# --- mission flow / tips ---
add("=== mission-flow ===")
for ln in (cap / "mission-flow.log").read_text(encoding="utf-8", errors="replace").splitlines():
    add(ln[:500])
add()

# --- inventory for buff item / NCU ---
add("=== inventory-updates (templates) ===")
with (cap / "inventory-updates.csv").open(encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    n = 0
    for row in r:
        blob = " ".join((row.get(k) or "") for k in row)
        if any(k in blob for k in ("NCU", "Template", "LowId", "HighId", "Add", "buff", "Will", "XP")):
            add(str({k: row[k] for k in list(row.keys())[:18]})[:450])
            n += 1
            if n > 80:
                break
add()

# --- system messages ---
add("=== system-messages XP/AIXP/NCU/quest ===")
for ln in (cap / "system-messages.log").read_text(encoding="utf-8", errors="replace").splitlines():
    low = ln.lower()
    if any(k in low for k in ("xp", "alien", "aixp", "ncu", "quest", "buff", "will", "experience", "you receive", "you gain", "nano")):
        add(ln[:450])
add()

# --- enemy movement for Karli path ---
add("=== enemy-movement Karli ===")
em = cap / "enemy-movement.csv"
if em.exists():
    with em.open(encoding="utf-8-sig", errors="replace") as f:
        r = csv.DictReader(f)
        for i, row in enumerate(r):
            blob = " ".join((row.get(k) or "") for k in row)
            if "Karli" in blob or "Cappell" in blob:
                add(str(row)[:400])
                if i > 200:
                    break
add()

# --- loot alien ---
add("=== corpse-loot alien ===")
with (cap / "corpse-loot-observations.csv").open(encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    for row in r:
        name = row.get("EnemyName") or ""
        if any(k in name for k in ("Alien", "Spider", "Scout", "Specialist", "Minibull", "Saltworm", "Harvey", "Roller")):
            add("%s md=%s lvl=%s cred=%s items=%s" % (
                name, row.get("MonsterData"), row.get("EnemyLevel"),
                row.get("CorpseCredits"), row.get("Items")))
add()

# --- XP/AIXP stats ---
add("=== enemy-stat-updates XP/AlienXP ===")
with (cap / "enemy-stat-updates.csv").open(encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    for row in r:
        st = row.get("Stat") or ""
        if st in ("XP", "AlienXP"):
            add("%s %s role=%s id=%s val=%s" % (
                row.get("CapturedUtc"), st, row.get("IdentityRole"),
                row.get("Identity"), row.get("Value")))
add()

# --- FormatFeedback / NCU ---
add("=== events FormatFeedback / NCU / Quest / TemplateAction ===")
for ln in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if any(k in ln for k in ("FormatFeedback", "NCU", "QuestFull", "TemplateAction", "AddTemplate", "ContainerAdd", "Will", "XP Boost", "Experience")):
        if "Karli" in ln or "NCU" in ln or "Quest" in ln or "FormatFeedback" in ln or "Will" in ln or "AlienXP" in ln or "AddTemplate" in ln:
            add(ln[:650])
add()

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines))
