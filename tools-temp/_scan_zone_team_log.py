from pathlib import Path
from datetime import datetime

p = Path(r"AORebirth/Built/Debug/ZoneEngineLog.txt")
lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
print("lines", len(lines), "mtime", datetime.fromtimestamp(p.stat().st_mtime))

keys = (
    "CharacterAction action=",
    "Team invite",
    "TeamRequest",
    "InfoRequest",
    "LFT:",
    "LftSearch",
    "Seed",
    "Aminasol",
    "Fixer",
    "declined",
    "pending",
)
hits = []
for i, line in enumerate(lines):
    if any(k in line for k in keys):
        hits.append((i, line))

print("hits", len(hits))
for i, line in hits[-80:]:
    print("%d %s" % (i, line[:300]))
