# -*- coding: utf-8 -*-
from __future__ import print_function
import json
import os

nanos_json = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_day10_vendor_nanos.json"
out_path = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Content\Daily\day10-profession-nanos.json"

# Capture open order -> Profession enum int (SmokeLounge.AOtomation.Messaging.GameData.Profession)
# Identified from nanocrystal name samples in itemnames.sql against capture 20260808-043332.
VENDOR_PROFESSION = [
    ("(VendingMachine:12FE8DA2)", 6, "Adventurer"),
    ("(VendingMachine:12FE8DA1)", 5, "Agent"),
    ("(VendingMachine:12FE8DA0)", 8, "Bureaucrat"),
    ("(VendingMachine:12FE8D9F)", 10, "Doctor"),
    ("(VendingMachine:12FE8D9E)", 9, "Enforcer"),
    ("(VendingMachine:12FE8D9D)", 3, "Engineer"),
    ("(VendingMachine:12FE8D9C)", 4, "Fixer"),
    ("(VendingMachine:12FE8D95)", 7, "Trader"),
    ("(VendingMachine:12FE8D96)", 1, "Soldier"),
    ("(VendingMachine:12FE8D97)", 15, "Shade"),
    ("(VendingMachine:12FE8D98)", 11, "Nanotechnician"),
    ("(VendingMachine:12FE8D99)", 12, "Metaphysicist"),
    ("(VendingMachine:12FE8D9A)", 2, "MartialArtist"),
    ("(VendingMachine:12FE8D9B)", 14, "Keeper"),
]

with open(nanos_json, encoding="utf-8") as f:
    data = json.load(f)

professions = {}
meta = []
for vendor, prof_id, prof_name in VENDOR_PROFESSION:
    items = data["vendors"][vendor]
    # Dedup by itemId keep first quality (vendor listed)
    seen = {}
    for it in items:
        iid = it["lowId"]
        if iid not in seen:
            seen[iid] = it["quality"]
    entries = [{"itemId": iid, "quality": seen[iid]} for iid in sorted(seen)]
    professions[str(prof_id)] = entries
    meta.append({
        "vendor": vendor,
        "professionId": prof_id,
        "profession": prof_name,
        "count": len(entries)
    })
    print("%s (%d): %d nanos" % (prof_name, prof_id, len(entries)))

os.makedirs(os.path.dirname(out_path), exist_ok=True)
payload = {
    "evidence": "AOSharpLiveCapture/20260808-043332 profession nano vendors (shop-updates.csv)",
    "qualityDelta": 10,
    "note": "At claim: pick random nano for character profession with vendor QL within characterLevel +- qualityDelta.",
    "vendors": meta,
    "professions": professions
}
with open(out_path, "w", encoding="utf-8") as f:
    json.dump(payload, f, indent=2)
print("WROTE", out_path)
