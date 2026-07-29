from __future__ import print_function
import csv
import os

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-184103"
paf = None
doors = []
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        d = row.get("Direction") or ""
        nt = row.get("N3TypeName") or ""
        if not d.startswith("IN"):
            continue
        if nt == "PlayfieldAnarchyF" and paf is None:
            paf = row.get("Timestamp")
            print("PAF", paf)
        if paf is None:
            continue
        if nt == "DoorFullUpdate":
            doors.append(row.get("Timestamp"))
print("doors", len(doors))
for t in doors:
    print(t)
