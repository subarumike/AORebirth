# -*- coding: utf-8 -*-
import csv
import re
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-finish")
term = "574187C3"
# search events and raw for terminal
for name in ["events.log", "npc-lifecycle.csv", "raw-packets.csv"]:
    p = cap / name
    if not p.exists():
        continue
    hits = 0
    with p.open(encoding="utf-8-sig", errors="replace") as f:
        for i, line in enumerate(f, 1):
            if term in line or "574187c3" in line.lower():
                hits += 1
                if hits <= 8:
                    print(f"{name}:{i}: {line[:300].rstrip()}")
    print(f"{name} hits={hits}")

# also find SIFU terminal near Vaughn pad
for name in ["events.log"]:
    p = cap / name
    with p.open(encoding="utf-8-sig", errors="replace") as f:
        for i, line in enumerate(f, 1):
            if "Terminal:" in line and ("SIFU" in line or "SimpleItemFullUpdate" in line or "DYNEL-SPAWNED" in line):
                if "336" in line or "828" in line or "835" in line or "Vaughn" in line or "574187C3" in line:
                    print(f"term-cand:{i}: {line[:350].rstrip()}")
