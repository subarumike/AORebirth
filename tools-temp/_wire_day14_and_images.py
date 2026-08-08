# -*- coding: utf-8 -*-
from __future__ import print_function
import json
from pathlib import Path

proto = Path(r"C:\Users\nermi\Desktop\zadaily\prototyprsymb.txt")
# Prefer QL1 seeds so Zone Relations scale to QL250 (Mike: get just ql=250).
ql1_ids = []
for line in proto.read_text(encoding="utf-8", errors="replace").splitlines():
    parts = line.replace("\t", " ").split()
    nums = [int(x) for x in parts if x.isdigit()]
    if len(nums) >= 2 and nums[0] == 1:
        ql1_ids.append(nums[1])
    elif len(nums) >= 2 and nums[-2] == 1:
        ql1_ids.append(nums[-1])

seen = set()
pool = []
for i in ql1_ids:
    if i not in seen:
        seen.add(i)
        pool.append(i)

print("day14 ql1 seed count", len(pool), "sample", pool[:6])

paths = [
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\rewards.json"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json"),
    Path(r"C:\xampp\htdocs\daily\rewards.json"),
]

btn_days = {
    "3": ("291082", 50, "Health and Nano Recharger"),
    "4": ("291043", 25, "Health and Nano Stim"),
    "17": ("291082", 50, "Health and Nano Recharger"),
    "26": ("291043", 25, "Health and Nano Stim"),
}

for path in paths:
    data = json.loads(path.read_text(encoding="utf-8"))
    days = data["days"]
    for day, (item_id, amount, name) in btn_days.items():
        e = days[day]
        e["itemId"] = int(item_id)
        e["amount"] = amount
        e["qualityMode"] = "characterLevel"
        e["itemName"] = name
        # Use button art (cells for new IDs/amounts are missing → black popup).
        e["image"] = "assets/buttons/btn-%02d.png" % int(day)
        e["buttonImage"] = "assets/buttons/btn-%02d.png" % int(day)

    d14 = days["14"]
    d14["itemId"] = pool[0] if pool else 0
    d14["amount"] = 1
    d14["quality"] = 250
    d14["qualityMode"] = "fixed"
    d14["itemName"] = "Prototype Symbiant (random QL 250)"
    d14["randomItemIds"] = pool
    d14["note"] = "Random Prototype Symbiant at QL 250 from prototyprsymb.txt"
    d14["image"] = "assets/buttons/btn-14.png"
    d14["buttonImage"] = "assets/buttons/btn-14.png"
    if "qualityDelta" in d14:
        del d14["qualityDelta"]

    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print("wrote", path)
