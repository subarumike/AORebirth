# Extract AIXP / alienxp from capture 20260726-230559
import csv
import json
import os
import re
from collections import defaultdict

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-230559"
OUT = r"tools-temp/_tmp_aixp_230559.txt"
lines = []

def w(s=""):
    lines.append(str(s))

# enemy-stat-updates for AlienXP / alienxp
w("=== enemy-stat-updates AlienXP-ish ===")
path = os.path.join(CAP, "enemy-stat-updates.csv")
with open(path, newline="", encoding="utf-8-sig") as f:
    rows = list(csv.DictReader(f))
w("fields: %s" % (list(rows[0].keys()) if rows else []))
alien_rows = []
for r in rows:
    blob = " ".join((r.get(k) or "") for k in r)
    if re.search(r"alien|aixp|axp", blob, re.I):
        alien_rows.append(r)
w("alien-ish rows: %d / %d" % (len(alien_rows), len(rows)))
for r in alien_rows[:40]:
    w(str({k: r[k] for k in r if r[k] and k != "RawHex"}))

# system-messages
w("\n=== system-messages alien ===")
sp = os.path.join(CAP, "system-messages.log")
if os.path.exists(sp):
    with open(sp, encoding="utf-8-sig", errors="replace") as f:
        for ln in f:
            if re.search(r"alien|aixp|axp|AI level|AI XP", ln, re.I):
                w(ln.rstrip()[:400])

# fight events Stat AlienXP
w("\n=== fight-events Stat alien ===")
with open(os.path.join(CAP, "enemy-fight-events.log"), encoding="utf-8-sig", errors="replace") as f:
    for ln in f:
        if re.search(r"AlienXP|AlienXp|alienxp|AIXP|AlienLevel|Alien Level", ln, re.I):
            w(ln.rstrip()[:500])

# raw packets Stat with Alien
w("\n=== raw-packets Stat mentioning Alien ===")
rp = os.path.join(CAP, "raw-packets.csv")
count = 0
with open(rp, newline="", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        blob = " ".join((row.get(k) or "") for k in ("N3TypeName", "PreservationStatus", "RawHex") if k in row)
        # look for Stat messages near kills - scan Detail if any
        detail = " ".join(str(v) for v in row.values() if v and len(str(v)) < 500)
        if re.search(r"AlienXP|AlienLevel|alienxp", detail, re.I):
            count += 1
            w(detail[:400])
            if count > 30:
                break
w("raw hits: %d" % count)

# Also scan packets.hex.log for AlienXP string
w("\n=== packets.hex.log text hits ===")
php = os.path.join(CAP, "packets.hex.log")
hits = 0
with open(php, encoding="utf-8", errors="replace") as f:
    for ln in f:
        if re.search(r"AlienXP|AlienLevel|alienxp|AIXP", ln, re.I):
            hits += 1
            if hits <= 40:
                w(ln.rstrip()[:500])
w("hexlog hits: %d" % hits)

# events.log
w("\n=== events.log ===")
ep = os.path.join(CAP, "events.log")
if os.path.exists(ep):
    with open(ep, encoding="utf-8-sig", errors="replace") as f:
        for ln in f:
            if re.search(r"alien|aixp|axp", ln, re.I):
                w(ln.rstrip()[:400])

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print("wrote", OUT, "lines", len(lines))
