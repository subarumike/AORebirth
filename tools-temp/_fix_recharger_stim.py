# -*- coding: utf-8 -*-
from __future__ import print_function
import json
import os

paths = [
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\rewards.json",
    r"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json",
    r"C:\xampp\htdocs\daily\rewards.json",
]

with open(paths[0], encoding="utf-8") as f:
    data = json.load(f)

days = data["days"]

# Seed QL1 endpoints so scaling always has a proper low/high span:
# Recharger: 291082 (ql1) .. 293297 (ql400)
# Stim: 291043 (ql1) .. 291045 (ql400)
days["3"].update({
    "itemId": 291082,
    "amount": 50,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": "Health and Nano Recharger",
    "note": "x50; ql=character level (Relations 291082-293297)",
    "image": "assets/cells/day3-293297-1.png",
})
days["4"].update({
    "itemId": 291043,
    "amount": 25,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": "Health and Nano Stim",
    "note": "x25; ql=character level (Relations 291043-291045)",
    "image": "assets/cells/day4-291045-1.png",
})
days["17"].update({
    "itemId": 291082,
    "amount": 50,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": "Health and Nano Recharger",
    "note": "same as day3: x50; ql=character level",
    "image": "assets/cells/day17-293297-1.png",
})
days["26"].update({
    "itemId": 291043,
    "amount": 25,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": "Health and Nano Stim",
    "note": "same as day4: x25; ql=character level",
    "image": "assets/cells/day26-291045-1.png",
})

data["evidence"] = (
    data.get("evidence", "")
    + " | day3/17 recharger x50 ql=char; day4/26 stim x25 ql=char (low-ID seeds)."
)

for p in paths:
    with open(p, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    print("WROTE", p)

for d in ("3", "4", "17", "26"):
    e = days[d]
    print("day%s id=%s amount=%s mode=%s" % (d, e["itemId"], e["amount"], e.get("qualityMode")))
