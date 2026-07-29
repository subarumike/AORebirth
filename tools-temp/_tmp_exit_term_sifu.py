# -*- coding: utf-8 -*-
from pathlib import Path
import re
cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-loralei/events.log")
lines = cap.read_text(encoding="utf-8-sig", errors="ignore").splitlines()
for i, line in enumerate(lines):
    if "574187C3" in line:
        # print nearby SimpleItemFullUpdate
        for j in range(max(0, i-5), min(len(lines), i+15)):
            if "SimpleItem" in lines[j] or "574187C3" in lines[j] or "Exit Arete" in lines[j]:
                print(f"{j+1}: {lines[j][:500]}")
        print("---")
# also scan raw packets for identity after spawn
raw = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-loralei/raw-packets.csv")
# find hex containing 574187C3 and ACGItem
count=0
with raw.open(encoding="utf-8-sig", errors="ignore") as f:
    for line in f:
        if "574187C3" in line.upper().replace(" ",""):
            count += 1
            if count <= 3:
                print("RAW", line[:400])
print("raw hits", count)
