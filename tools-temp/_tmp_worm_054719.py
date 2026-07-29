# Capture 20260727-054719: Saltworm texture / fight / loot / XP
from __future__ import print_function
import csv
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-054719")
out = Path(r"tools-temp/_tmp_worm_054719.txt")
lines = []

def add(s=""):
    lines.append(s)

# --- enemy dossier / state ---
for name in ("enemy-dossier.json", "enemy-state.json", "capture_info.json"):
    p = cap / name
    if not p.exists():
        continue
    add("=== %s ===" % name)
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
        text = json.dumps(data, indent=2)
        # keep focused
        for key in ("Saltworm", "worm", "Worm", "17712"):
            if key.lower() in text.lower():
                add("contains %s" % key)
        # print names if list
        if isinstance(data, dict):
            for k, v in list(data.items())[:40]:
                add("%s: %s" % (k, str(v)[:200]))
        elif isinstance(data, list):
            for e in data[:30]:
                add(str(e)[:300])
    except Exception as ex:
        add("err %s" % ex)
    add()

# --- scfu appearance ---
scfu = cap / "scfu-appearance.csv"
if scfu.exists():
    add("=== scfu-appearance (worm-ish) ===")
    with scfu.open(encoding="utf-8", errors="replace") as f:
        r = csv.DictReader(f)
        for row in r:
            blob = " ".join(row.values())
            if any(k in blob for k in ("Salt", "worm", "Worm", "17712", "95955", "ExtTex", "Material")):
                add(str(row)[:500])
    add()

# --- enemy full updates ---
efu = cap / "enemy-full-updates.csv"
if efu.exists():
    add("=== enemy-full-updates Saltworm ===")
    with efu.open(encoding="utf-8", errors="replace") as f:
        r = csv.DictReader(f)
        for row in r:
            blob = " ".join((row.get(k) or "") for k in row)
            if "Salt" in blob or "worm" in blob.lower() or "17712" in blob:
                keys = ["Name", "Identity", "MonsterData", "Level", "Flags", "CatMesh", "Scale", "Hp", "MaxHp"]
                add(" | ".join("%s=%s" % (k, row.get(k, "")) for k in keys if k in row or True)[:400])
                add("  raw keys sample: " + ",".join(list(row.keys())[:25]))
                break
    # dump first matching full row keys
    with efu.open(encoding="utf-8", errors="replace") as f:
        r = csv.DictReader(f)
        for row in r:
            name = row.get("Name") or row.get("name") or ""
            if "Salt" in name or "worm" in name.lower():
                add("FULL ROW:")
                for k, v in row.items():
                    if v and v not in ("0", "False", ""):
                        add("  %s=%s" % (k, v[:200] if isinstance(v, str) else v))
                break
    add()

# --- combat ---
combat = cap / "enemy-combat.csv"
if combat.exists():
    add("=== enemy-combat (anim/attack) ===")
    anim = Counter()
    attackers = Counter()
    with combat.open(encoding="utf-8", errors="replace") as f:
        r = csv.DictReader(f)
        cols = None
        for row in r:
            if cols is None:
                cols = list(row.keys())
                add("cols: " + ",".join(cols))
            blob = " ".join((row.get(k) or "") for k in row)
            # filter later
            anim_key = row.get("Animation") or row.get("Anim") or row.get("Action") or row.get("AttackType") or ""
            attacker = row.get("AttackerName") or row.get("Name") or row.get("SourceName") or ""
            anim[anim_key or blob[:40]] += 1
            attackers[attacker] += 1
        add("attackers top: " + str(attackers.most_common(15)))
        add("anim top: " + str(anim.most_common(30)))
    add()

# --- fight events ---
fel = cap / "enemy-fight-events.log"
if fel.exists():
    add("=== enemy-fight-events (Saltworm / AttackInfo / VZCX / special) ===")
    for ln in fel.read_text(encoding="utf-8", errors="replace").splitlines():
        if any(k in ln for k in ("Salt", "worm", "Worm", "AttackInfo", "special", "Anim", "VZCX", "CKHC", "DXZJ", "XP", "AIXP", "damage")):
            if "Salt" in ln or "worm" in ln.lower() or "AttackInfo" in ln or "XP" in ln or "AIXP" in ln:
                add(ln[:500])
    add()

# --- corpse loot ---
for name in ("corpse-loot-observations.csv", "corpse-full-updates.csv"):
    p = cap / name
    if not p.exists():
        continue
    add("=== %s ===" % name)
    with p.open(encoding="utf-8", errors="replace") as f:
        r = csv.DictReader(f)
        for i, row in enumerate(r):
            blob = " ".join((row.get(k) or "") for k in row)
            if "Salt" in blob or "worm" in blob.lower() or i < 3:
                add(str({k: row.get(k) for k in list(row)[:20]})[:500])
    add()

# --- system messages XP ---
sysm = cap / "system-messages.log"
if sysm.exists():
    add("=== system-messages XP/AIXP ===")
    for ln in sysm.read_text(encoding="utf-8", errors="replace").splitlines():
        if any(k in ln.lower() for k in ("xp", "experience", "alien", "aixp", "points", "credit", "you gain", "you received")):
            add(ln[:400])
    add()

# --- inventory updates around loot ---
inv = cap / "inventory-updates.csv"
if inv.exists():
    add("=== inventory-updates (sample non-empty) ===")
    with inv.open(encoding="utf-8", errors="replace") as f:
        r = csv.DictReader(f)
        n = 0
        for row in r:
            blob = " ".join((row.get(k) or "") for k in row)
            if any(k in blob for k in ("LowId", "HighId", "Template", "QL", "Add", "loot")):
                add(str(row)[:400])
                n += 1
                if n > 40:
                    break
    add()

# --- events CHAR-SEEN Saltworm + ExtTex ---
ev = cap / "events.log"
if ev.exists():
    add("=== events CHAR-SEEN / ExtTex / Death / XP ===")
    for ln in ev.read_text(encoding="utf-8", errors="replace").splitlines():
        if "Saltworm" in ln or ("worm" in ln.lower() and ("CHAR" in ln or "ExtTex" in ln or "Death" in ln)):
            add(ln[:600])
        elif "you receive" in ln.lower() or "XP" in ln or "AIXP" in ln or "Alien XP" in ln:
            add(ln[:400])
    add()

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines))
