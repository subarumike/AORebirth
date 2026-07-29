# -*- coding: utf-8 -*-
"""Decode Barking Chimera SAW specials + AttackInfo hit type from capture."""
import csv
import re
import os

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260723-225021"
out = r"tools-temp\_tmp_cap_225021_saw_decode.txt"
lines = []

# Prefer hex log for SAW packets from enemy 798C1F4F / any Chimera
# Also parse Detail more fully from enemy-combat.csv
combat = list(csv.DictReader(open(os.path.join(cap, "enemy-combat.csv"), encoding="utf-8-sig")))

# Find first SAW with full detail - Detail may be truncated. Use raw-packets or packets.hex
# Search raw-packets for SpecialAttackWeapon from known identities

raw = list(csv.DictReader(open(os.path.join(cap, "raw-packets.csv"), encoding="utf-8-sig")))
lines.append("raw columns: %s" % list(raw[0].keys()) if raw else "empty")

# Sample a few SAW rows
saw_rows = [r for r in combat if r["MessageType"] == "SpecialAttackWeapon" and r["SourceRole"] == "enemy"]
lines.append("enemy SAW count=%d" % len(saw_rows))
for r in saw_rows[:3]:
    lines.append("seq=%s id=%s detail=%s" % (r["Sequence"], r["SourceIdentity"], r["Detail"]))

# Look in packets.hex.log for SAW message type marker - AttackInfo weapon instances as ASCII?
# Parse SpecialAttackInfo from Detail if longer in fight events

fe = open(os.path.join(cap, "enemy-fight-events.log"), encoding="utf-8", errors="replace").read()
# find SpecialAttackWeapon with Specials for 798C1F4F
idx = fe.find("798C1F4F) SpecialAttackWeapon")
if idx < 0:
    idx = fe.find("SpecialAttackWeaponMessage { Specials=count=5")
lines.append("\n=== fight-events SAW snippet ===")
if idx >= 0:
    lines.append(fe[idx:idx+2500])

# AttackInfo HitType - already Normal. Check numeric from Detail HitType=
hit_types = []
unk1 = []
for r in combat:
    if r["MessageType"] != "AttackInfo" or r["SourceRole"] != "enemy":
        continue
    d = r["Detail"]
    m = re.search(r"HitType=(\w+)", d)
    u = re.search(r"Unk1=(\d+)", d)
    if m:
        hit_types.append(m.group(1))
    if u:
        unk1.append(u.group(1))
from collections import Counter
lines.append("\nHitType %s" % Counter(hit_types))
lines.append("Unk1 %s" % Counter(unk1))

# Parse SAW unknown ints from truncated details - need hex
# Find N3 SpecialAttackWeapon in packets.hex.log by scanning for identity bytes of 798C1F4F = 4F1F8C79 little endian
ident = bytes([0x4F, 0x1F, 0x8C, 0x79])
# Also try reading movement or a dedicated SAW export

# Use python to find in hex log lines containing SpecialAttackWeapon decoded elsewhere
# Check if analyzer wrote scfu or other

# Grep-like: read enemy-combat Detail for SAW that might include SpecialAttackInfo fields if we re-dump
# Check system messages

# From known AO SAW structure: Unknown1-5 after specials count
# Extract from Amount column was wrong (48/52/56/60) - those might be Unknown1 from Amount field mis-map
lines.append("\nSAW Amount column values (may be Unknown1):")
from collections import Counter as C
lines.append(str(C(r.get("Amount") or "" for r in saw_rows)))

# Try decode SpecialAttackInfo tags from hex packet after SAW type
# Message type SpecialAttackWeapon = 0x355C1D3C or similar from audit: 1D3C0F1C
# Search packets.hex.log for lines with 798C1F4F and nearby

hexpath = os.path.join(cap, "packets.hex.log")
hits = []
with open(hexpath, encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f):
        if "798C1F4F" in line.upper().replace("0x", "") or "4f1f8c79" in line.lower():
            hits.append((i, line[:300]))
            if len(hits) > 5:
                break
lines.append("\nhexlog identity hits (first): %d" % len(hits))
for h in hits[:5]:
    lines.append("L%d %s" % h)

# Better: use raw-packets MessageType column
if raw:
    types = C(r.get("MessageType") or r.get("Type") or "" for r in raw)
    lines.append("\nraw message types top: %s" % types.most_common(20))
    # find SAW
    for key in raw[0].keys():
        if "type" in key.lower() or "hex" in key.lower() or "pay" in key.lower():
            lines.append("col %s sample=%s" % (key, (raw[0].get(key) or "")[:80]))

open(out, "w", encoding="utf-8").write("\n".join(lines))
print("wrote", out)
print("\n".join(lines[:40]))
