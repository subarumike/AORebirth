# -*- coding: utf-8 -*-
from pathlib import Path
import re

zone = Path(r"AORebirth/Built/Debug/ZoneEngineLog.txt")
chat = Path(r"AORebirth/Built/Debug/ChatEngineLog.txt")

print("=== ChatEngineLog key lines (last matches) ===")
pat = re.compile(r"ISCom|SystemChat|unpack|unhandled|Distribute|Vicinity|Pet|ready", re.I)
matches = []
with chat.open(encoding="utf-8", errors="replace") as fh:
    for line in fh:
        if pat.search(line):
            matches.append(line.rstrip())
print("total", len(matches))
for line in matches[-40:]:
    print(line)

print("\n=== ZoneEngineLog PetSystemChat/ISCom (last) ===")
pat2 = re.compile(r"PetSystemChat|ISCom", re.I)
matches2 = []
with zone.open(encoding="utf-8", errors="replace") as fh:
    for line in fh:
        if pat2.search(line):
            matches2.append(line.rstrip())
print("total", len(matches2))
for line in matches2[-50:]:
    print(line)
