# -*- coding: utf-8 -*-
from __future__ import print_function
import json, os, math

base = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260726-spawn-mob-tll-alien"
out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_tll_alien_slots.csfrag"

with open(os.path.join(base, "enemy-dossier.json"), "r", encoding="utf-8-sig") as f:
    d = json.load(f)

# Existing oasis rollerrat slots (approx) — skip near-duplicates for alien runtime rats
oasis_rats = [
    (3423.55225, 691.272949), (3392.27124, 755.309631), (3392.08887, 680.059800),
    (3417.22949, 718.296631), (3379.18262, 662.932200), (3431.68200, 664.818054),
    (3384.95166, 639.372200), (3457.39575, 746.963300), (3387.48000, 744.358000),
    (3396.06787, 721.528900),  # gnarl old
]

def near_oasis_rat(x, z, r=4.0):
    for ox, oz in oasis_rats:
        if (x-ox)**2 + (z-oz)**2 < r*r:
            return True
    return False

WANT = {
    "Angry Minibull": ("Minibull", 30360, 42, 105),
    "Harvey the Bully": ("Minibull", 30360, 3, 100),
    "Alien Spider - Zix": ("Spider", 247728, 220, None),  # scale from entry
    "Saltworm": ("Saltworm", 17712, 58, 75),
    "Rollerrat": ("Rollerrat", 17687, 55, 125),
}

# runspeed from capture by level bands - use observed
slots = []
seen = []

def dedupe(x, y, z, name, r=3.0):
    for sx, sy, sz, sn in seen:
        if sn == name and (x-sx)**2 + (z-sz)**2 < r*r:
            return True
    seen.append((x, y, z, name))
    return False

for e in d["enemies"]:
    name = e.get("name") or ""
    if name not in WANT:
        continue
    pos = e.get("position") or {}
    x = float(pos.get("x") or 0)
    y = float(pos.get("y") or 0)
    z = float(pos.get("z") or 0)
    if name == "Rollerrat" and near_oasis_rat(x, z):
        continue
    if dedupe(x, y, z, name):
        continue
    kind, md, fam, default_scale = WANT[name]
    lvl = int(e.get("level") or 1)
    hp = int(e.get("maxHealth") or 1)
    scale = e.get("monsterScale")
    try:
        scale = int(scale) if scale not in (None, "", "1234567890") else (default_scale or 100)
    except Exception:
        scale = default_scale or 100
    run = e.get("runSpeed")
    try:
        run = int(run) if run not in (None, "", "1234567890") else 30
    except Exception:
        run = 30
    # AOS: minibulls/harvey/rats yes; spiders unknown -> AOS 12m like rats; saltworm passive until attacked
    if kind in ("Minibull", "Rollerrat", "Spider"):
        ai = "NpcAiProfile.Aggressive"
        aggro = "15.0f" if kind != "Spider" else "12.0f"
    else:
        ai = "NpcAiProfile.Passive"
        aggro = "0.0f"
    slots.append((kind, name, md, lvl, hp, fam, scale, run, ai, aggro, x, y, z))

# Sort by kind then x
order = {"Spider": 0, "Saltworm": 1, "Rollerrat": 2, "Minibull": 3}
slots.sort(key=lambda t: (order.get(t[0], 9), t[10], t[12]))

lines = []
lines.append("// Capture 20260726-spawn-mob-tll-alien enemy-dossier clustered slots")
lines.append("private static readonly MobSlot[] Slots =")
lines.append("{")
for kind, name, md, lvl, hp, fam, scale, run, ai, aggro, x, y, z in slots:
    lines.append(
        '                new MobSlot("%s", MobKind.%s, %d, %d, %d, %d, %d, %d, %s, %s, %.3ff, %.3ff, %.3ff),'
        % (name, kind, md, lvl, hp, fam, scale, run, ai, aggro, x, y, z)
    )
lines.append("            };")
lines.append("// count=%d" % len(slots))

with open(out, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print("wrote", out, "slots", len(slots))
for k in sorted(set(s[0] for s in slots)):
    print(k, sum(1 for s in slots if s[0]==k))
