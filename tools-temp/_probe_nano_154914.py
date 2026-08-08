# Dump nano 154914 formula + all packet types around finish from warp capture
import os
import struct
import json
from collections import Counter

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-Warp-single"

# 1) raw packet types around finish
rows = open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig", errors="replace").read().splitlines()
print("raw header:", rows[0][:300] if rows else "none")
print("raw rows", len(rows)-1)

# events full write utf-8
ev = open(os.path.join(CAP, "events.log"), encoding="utf-8-sig", errors="replace").read()
open(r"tools-temp\_warp_full_events.txt", "w", encoding="utf-8").write(ev)
print("events chars", len(ev))

# message type counts from events
types = Counter()
for line in ev.splitlines():
    if "type=" in line:
        import re
        m = re.search(r"type=(\w+)", line)
        if m:
            types[m.group(1)] += 1
print("event types:", types)

# Find nano db
candidates = []
for root, dirs, files in os.walk("AORebirth"):
    for f in files:
        fl = f.lower()
        if "nano" in fl and (fl.endswith(".dat") or fl.endswith(".db") or fl.endswith(".json") or fl.endswith(".bin") or fl.endswith(".xml")):
            candidates.append(os.path.join(root, f))
print("nano candidate files sample:", candidates[:40])
print("count", len(candidates))
