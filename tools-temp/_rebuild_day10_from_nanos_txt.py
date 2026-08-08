# -*- coding: utf-8 -*-
"""Rebuild Content/Daily/day10-profession-nanos.json from Desktop nanos.txt + shop QLs."""
from __future__ import print_function
import json
import os
import re

txt = r"C:\Users\nermi\Desktop\zadaily\nanos.txt"
shop_json = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_day10_vendor_nanos.json"
out = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Content\Daily\day10-profession-nanos.json"

name_to_id = {
    "soldier": 1,
    "martial artist": 2,
    "engineer": 3,
    "fixer": 4,
    "agent": 5,
    "adventurer": 6,
    "trader": 7,
    "bureaucrat": 8,
    "enforcer": 9,
    "doctor": 10,
    "nano-technician": 11,
    "meta-physicist": 12,
    "keeper": 14,
    "shade": 15,
}

# Build id -> quality from shop capture if available
ql_map = {}
if os.path.exists(shop_json):
    with open(shop_json, encoding="utf-8") as f:
        shop = json.load(f)
    for vendor, items in shop.get("vendors", {}).items():
        for it in items:
            ql_map.setdefault(it["lowId"], it["quality"])

professions = {}
current = None
with open(txt, encoding="utf-8") as f:
    for raw in f:
        line = raw.strip()
        if not line or line.lower().startswith("profession nano"):
            continue
        key = line.lower()
        if key in name_to_id:
            current = name_to_id[key]
            professions.setdefault(str(current), [])
            continue
        if current is None:
            continue
        for tok in re.split(r"[\s,;]+", line):
            if not tok.isdigit():
                continue
            iid = int(tok)
            if iid == 302080:
                continue
            professions[str(current)].append({
                "itemId": iid,
                "quality": int(ql_map.get(iid, 0)),
            })

# dedupe
for k in list(professions.keys()):
    seen = {}
    for e in professions[k]:
        if e["itemId"] not in seen:
            seen[e["itemId"]] = e
    professions[k] = [seen[i] for i in sorted(seen)]

payload = {
    "evidence": "Desktop/zadaily/nanos.txt (from capture 20260808-043332 shops)",
    "qualityDelta": 10,
    "note": "1 random nano for character profession; QL within character level +-10",
    "professions": professions,
}
with open(out, "w", encoding="utf-8") as f:
    json.dump(payload, f, indent=2)
for k in sorted(professions, key=int):
    print(k, len(professions[k]))
print("WROTE", out)
