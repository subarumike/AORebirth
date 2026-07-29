# -*- coding: utf-8 -*-
import csv
from datetime import datetime
from collections import defaultdict

p = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260723-225021\enemy-combat.csv"
out = r"tools-temp\_tmp_cap_225021_combat_timing.txt"
rows = list(csv.DictReader(open(p, encoding="utf-8-sig")))

def parse_ts(s):
    s = s.rstrip("Z")
    if "." in s:
        # trim to 6 fractional digits
        base, frac = s.split(".", 1)
        frac = (frac + "000000")[:6]
        return datetime.strptime(base + "." + frac, "%Y-%m-%dT%H:%M:%S.%f")
    return datetime.strptime(s, "%Y-%m-%dT%H:%M:%S")

ev = defaultdict(list)
for r in rows:
    if r["SourceRole"] != "enemy":
        continue
    if r["MessageType"] not in ("SpecialAttackWeapon", "Attack", "AttackInfo"):
        continue
    ev[r["SourceIdentity"]].append(r)

lines = []
amts = []
first_hit_delays = []
hit_intervals = []
saw_unknowns = []
weapon_slots = []
weapon_insts = []

for sid, rs in sorted(ev.items()):
    saw_t = None
    atk_t = None
    last_hit = None
    lines.append("=== %s ===" % sid)
    for r in rs:
        t = parse_ts(r["CapturedUtc"])
        mt = r["MessageType"]
        detail = r.get("Detail") or ""
        if mt == "SpecialAttackWeapon":
            saw_t = t
            # Unknown1 often in Amount field? extract used tgt=56 for SAW - that's Amount column wrongly labeled
            saw_unknowns.append(r.get("Amount") or r.get("TargetIdentity") or "")
            lines.append("  SAW t=%s amount=%s detail=%s" % (r["CapturedUtc"], r.get("Amount"), detail[:160]))
        elif mt == "Attack":
            atk_t = t
            lines.append("  ATK t=%s" % r["CapturedUtc"])
        elif mt == "AttackInfo":
            amt = int(r["Amount"] or 0)
            amts.append(amt)
            # parse weapon slot/instance from detail
            import re
            mslot = re.search(r"WeaponSlot=(\d+)", detail)
            minst = re.search(r"WeaponInstance=(\-?\d+)", detail)
            if mslot:
                weapon_slots.append(int(mslot.group(1)))
            if minst:
                weapon_insts.append(int(minst.group(1)))
            if saw_t and last_hit is None:
                first_hit_delays.append((t - saw_t).total_seconds())
            if last_hit is not None:
                hit_intervals.append((t - last_hit).total_seconds())
            last_hit = t
            lines.append("  HIT amt=%d slot=%s inst=%s dt_saw=%.3f" % (
                amt,
                mslot.group(1) if mslot else "?",
                minst.group(1) if minst else "?",
                (t - saw_t).total_seconds() if saw_t else -1))

lines.append("\n=== SUMMARY ===")
lines.append("damage min=%d max=%d n=%d" % (min(amts), max(amts), len(amts)))
lines.append("first_hit_after_SAW mean=%.3f min=%.3f max=%.3f n=%d" % (
    sum(first_hit_delays)/len(first_hit_delays), min(first_hit_delays), max(first_hit_delays), len(first_hit_delays)))
if hit_intervals:
    lines.append("hit_interval mean=%.3f min=%.3f max=%.3f n=%d" % (
        sum(hit_intervals)/len(hit_intervals), min(hit_intervals), max(hit_intervals), len(hit_intervals)))
from collections import Counter
lines.append("weapon_slots %s" % Counter(weapon_slots))
lines.append("weapon_insts %s" % Counter(weapon_insts))
lines.append("saw_amount_field %s" % Counter(saw_unknowns))

open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", out)
print("\n".join(lines[-12:]))
