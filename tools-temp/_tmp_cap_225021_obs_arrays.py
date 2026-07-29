# -*- coding: utf-8 -*-
import csv
from datetime import datetime
from collections import defaultdict

p = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260723-225021\enemy-combat.csv"
out = r"tools-temp\_tmp_cap_225021_obs_arrays.txt"
rows = list(csv.DictReader(open(p, encoding="utf-8-sig")))

def parse(s):
    s = s.rstrip("Z")
    if "." in s:
        b, f = s.split(".", 1)
        f = (f + "000000")[:6]
        return datetime.strptime(b + "." + f, "%Y-%m-%dT%H:%M:%S.%f")
    return datetime.strptime(s, "%Y-%m-%dT%H:%M:%S")

ev = defaultdict(list)
for r in rows:
    if r["SourceRole"] != "enemy":
        continue
    if r["MessageType"] in ("SpecialAttackWeapon", "AttackInfo"):
        ev[r["SourceIdentity"]].append(r)

am = []
starts = []
firsts = []
lands = []
for sid, rs in sorted(ev.items()):
    saw = None
    last = None
    for r in rs:
        t = parse(r["CapturedUtc"])
        if r["MessageType"] == "SpecialAttackWeapon":
            saw = t
            last = None
        elif r["MessageType"] == "AttackInfo" and saw is not None:
            amt = int(r["Amount"])
            am.append(amt)
            if last is None:
                starts.append(0.0)
                firsts.append(round((t - saw).total_seconds(), 6))
            else:
                lands.append(round((t - last).total_seconds(), 6))
            last = t

lines = []
lines.append("MIN=%d MAX=%d" % (min(am), max(am)))
lines.append("AM=" + ",".join(map(str, am)))
lines.append("STARTS=" + ",".join("%.6f" % x for x in starts))
lines.append("FIRSTS=" + ",".join("%.6f" % x for x in firsts))
lines.append("LANDS=" + ",".join("%.6f" % x for x in lands))
lines.append("RECHARGE0=%.6f" % lands[0])
open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("\n".join(lines))
