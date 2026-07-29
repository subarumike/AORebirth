#!/usr/bin/env python3
import csv
import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
CAP = pathlib.Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture"
    r"\bin\Debug\captures\20260715-Recive-mail-datetime-stamp"
)
out = []

# list all unique message types mentioning mail or 333b
with (CAP / "raw-packets.csv").open(newline="", encoding="utf-8-sig", errors="replace") as f:
    r = csv.DictReader(f)
    cols = r.fieldnames
    out.append("cols=" + str(cols))
    types = {}
    mailish = []
    for i, row in enumerate(r):
        t = row.get("MessageType") or row.get("PacketType") or row.get("Name") or ""
        types[t] = types.get(t, 0) + 1
        blob = " ".join(str(v) for v in row.values() if v)
        if "333B2867" in blob.upper() or "67283B33" in blob.upper() or "Mail" in t or "mail" in blob.lower()[:200]:
            mailish.append((i, t, {k: (v[:80] + "...") if v and len(v) > 80 else v for k, v in row.items()}))
    out.append("type counts (top):")
    for t, c in sorted(types.items(), key=lambda x: -x[1])[:40]:
        out.append(f"  {c:5d} {t}")
    out.append(f"mailish rows {len(mailish)}")
    for item in mailish[:30]:
        out.append(str(item))

# search packets.hex.log for 67 28 3B 33 and nearby
hexlog = (CAP / "packets.hex.log").read_text(encoding="utf-8", errors="replace")
out.append(f"hexlog size {len(hexlog)}")
for pat in ["67283B33", "67 28 3B 33", "333B2867", "Mail"]:
    out.append(f"count {pat}={hexlog.upper().count(pat.upper())}")

# sample first 2000 chars
out.append("hexlog head:\n" + hexlog[:1500])

# events around mailbox open with unknown N3
ev = (CAP / "events.log").read_text(encoding="utf-8", errors="replace")
for i, line in enumerate(ev.splitlines(), 1):
    if "IN-N3" in line and ("10:00:3" in line or "10:00:4" in line or "10:00:5" in line):
        if "GenericCmd" in line or "Stat" in line or "Mail" in line or "Unknown" in line or "N3MessageType" in line:
            out.append(f"L{i}: {line[:350]}")

path = CAP / "_mail_scan.txt"
path.write_text("\n".join(out), encoding="utf-8")
print("wrote", path)
