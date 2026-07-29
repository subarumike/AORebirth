# Remi Gallois capture brief 20260727-204902
import csv
import json
import os
import re
from collections import Counter

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-204902"
print("files", sorted(os.listdir(cap)))

# session
for fn in ("capture-session.json", "capture_info.json"):
    p = os.path.join(cap, fn)
    if os.path.exists(p):
        print(fn, open(p, encoding="utf-8-sig").read()[:600])

# dialogue / npc interactions
for fn in ("chat-dialogue.log", "npc-interactions.log", "mission-flow.log", "system-messages.log"):
    p = os.path.join(cap, fn)
    if not os.path.exists(p):
        continue
    data = open(p, encoding="utf-8", errors="replace").read()
    print("\n====", fn, "len", len(data), "====")
    print(data[:8000] if len(data) < 20000 else data[:8000] + "\n...[trunc]...")

# scfu Remi
print("\n==== Remi SCFU ====")
with open(os.path.join(cap, "scfu-appearance.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        if "Remi" in (row.get("Name") or "") or "Gallois" in (row.get("Name") or ""):
            print(row.get("Identity"), row.get("Name"), "pf", row.get("PlayfieldId"),
                  "pos", row.get("PositionX"), row.get("PositionY"), row.get("PositionZ"),
                  "md", row.get("MonsterData"), "level", row.get("Level"))
