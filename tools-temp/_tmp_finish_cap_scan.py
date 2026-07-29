# -*- coding: utf-8 -*-
import csv
import re
from pathlib import Path

text = Path(r"AORebirth/Server/ZoneEngine/Core/Playfields/AndromedaIccHqSpawn.cs").read_text(
    encoding="utf-8"
)
spawned = set(re.findall(r'Name = "([^"]+)"', text))
print("AndromedaIccHqSpawn names", len(spawned))

cap_names = set()
cap_rows = []
with open(
    r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-finish/scfu-appearance.csv",
    encoding="utf-8-sig",
    newline="",
) as f:
    for row in csv.DictReader(f):
        if row.get("PlayfieldId") != "655":
            continue
        n = row.get("Name") or ""
        if not n:
            continue
        if n.startswith("Gribas") or n in (
            "Mrmrsol",
            "Myengineer01",
            "Nicehere",
            "Sleeplessman",
        ):
            continue
        cap_names.add(n)
        cap_rows.append(row)

print("FINISH pf655 unique", len(cap_names))
print("MISSING from spawn:", sorted(cap_names - spawned))
print("extra in spawn sample:", sorted(spawned - cap_names)[:20])

# first player snap on 655
lines = Path(
    r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-finish/events.log"
).read_text(encoding="utf-8", errors="replace").splitlines()
for i, l in enumerate(lines):
    if "PLAYFIELD-INIT] 655" in l or "PLAYFIELD-INIT pf=655" in l:
        print("INIT", l[:160])
        for j in range(i, min(i + 40, len(lines))):
            if "SNAPSHOT" in lines[j] and "player=True" in lines[j]:
                print("SNAP", lines[j][:280])
                break
            if "pos=(" in lines[j] and "Mrmrsol" in lines[j] and "PLAYFIELD" not in lines[j]:
                if "CHAR-SEEN" in lines[j] or "SNAPSHOT" in lines[j]:
                    print("CHAR", lines[j][:280])
                    break
"done"
