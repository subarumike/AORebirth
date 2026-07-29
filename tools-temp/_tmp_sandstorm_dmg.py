# -*- coding: utf-8 -*-
"""Summarize SANDSTORM fight damage + quest handoff from capture."""
from pathlib import Path
import re

log = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/events.log").read_text(encoding="utf-8", errors="replace")
# Focus on first marauder fight window
start = log.find("18:53:0")
chunk = log[start:start+200000] if start>=0 else log

# HealthDamage to marauders
for m in re.finditer(r"HealthDamageMessage \{([^}]+)\}", chunk):
    s = m.group(1)
    if "FireAC" in s or "799F" in s or "Amount=-" in s:
        # find preceding identity line
        print("HD", s[:180])

print("--- AttackInfo ---")
for m in re.finditer(r"AttackInfoMessage \{([^}]+)\}", chunk):
    print(m.group(1)[:160])

print("--- handoff ---")
for line in chunk.splitlines():
    if any(k in line for k in ("556B5E59", "556B5E53", "Return to Remi", "Action=59", "QuestFullUpdate")) and "IN-" in line:
        if "SimpleCharFullUpdate" in line: continue
        print(line[:220])
