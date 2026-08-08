# -*- coding: utf-8 -*-
from __future__ import print_function
import json
import os

day10 = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Content\Daily\day10-profession-nanos.json"
out = r"C:\Users\nermi\Desktop\zadaily\nanos.txt"

order = [
    (1, "Soldier"),
    (2, "Martial Artist"),
    (3, "Engineer"),
    (4, "Fixer"),
    (5, "Agent"),
    (6, "Adventurer"),
    (7, "Trader"),
    (8, "Bureaucrat"),
    (9, "Enforcer"),
    (10, "Doctor"),
    (11, "Nano-Technician"),
    (12, "Meta-Physicist"),
    (14, "Keeper"),
    (15, "Shade"),
]

with open(day10, encoding="utf-8") as f:
    data = json.load(f)

os.makedirs(os.path.dirname(out), exist_ok=True)

lines = []
lines.append("Profession Nano Crystals from capture 20260808-043332 (shop-updates)")
lines.append("")

for pid, name in order:
    entries = data["professions"].get(str(pid), [])
    ids = sorted(set(e["itemId"] for e in entries))
    lines.append(name)
    lines.append(" ".join(str(i) for i in ids))
    lines.append("")
    print("%s: %d ids" % (name, len(ids)))

with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines).rstrip() + "\n")

print("WROTE " + out)
