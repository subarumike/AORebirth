import re
from pathlib import Path

log = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt")
lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
pat = re.compile(r"2026-07-16 05:")
keys = re.compile(r"connected|Disconnected|CharInPlay|resync|Loaded .*trained|trained perk|charactersperk|Error|Exception|TrainPerk|WritePerk", re.I)
for line in lines:
    if pat.search(line) and keys.search(line):
        print(line[-240:])
