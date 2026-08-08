# -*- coding: utf-8 -*-
from __future__ import print_function
import json
import os
import shutil

paths = [
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\rewards.json",
    r"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json",
    r"C:\xampp\htdocs\daily\rewards.json",
]
cells = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\assets\cells"
xampp_cells = [
    r"C:\xampp\htdocs\uwg.daily.icc-rk\assets\cells",
    r"C:\xampp\htdocs\daily\assets\cells",
]

with open(paths[0], encoding="utf-8") as f:
    data = json.load(f)

days = data["days"]
d3 = days["3"]
d4 = days["4"]
d5 = days["5"]
d7 = days["7"]
d8 = days["8"]
d9 = days["9"]

# Day17 = Day3
days["17"] = {
    "itemId": d3["itemId"],
    "amount": 1,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": d3["itemName"],
    "note": "same as day3; ql=character level",
    "image": "assets/cells/day17-293297-1.png",
    "buttonImage": "assets/buttons/btn-17.png",
}

days["19"] = {
    "itemId": 0,
    "amount": 1,
    "quality": 1,
    "itemName": "Empty (reserved)",
    "note": "Empty for now",
    "image": "assets/cells/day19-unknown.png",
    "buttonImage": "assets/buttons/btn-19.png",
}

days["20"] = {
    "itemId": d5["itemId"],
    "amount": 1,
    "quality": 1,
    "itemName": d5["itemName"],
    "note": "same as day5",
    "image": "assets/cells/day20-288792-1.png",
    "buttonImage": "assets/buttons/btn-20.png",
}

days["21"] = {
    "itemId": 0,
    "amount": 1,
    "quality": 1,
    "itemName": "Empty (reserved)",
    "note": "Empty for now",
    "image": "assets/cells/day21-unknown.png",
    "buttonImage": "assets/buttons/btn-21.png",
}

days["22"] = {
    "itemId": 288752,
    "amount": 1,
    "quality": 1,
    "itemName": "Victory Points",
    "note": "Victory Points item 288752",
    "image": "assets/cells/day22-288752-1.png",
    "buttonImage": "assets/buttons/btn-22.png",
}

days["23"] = {
    "itemId": d9["itemId"],
    "amount": 1,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": d9["itemName"],
    "note": "same as day9; ql=character level",
    "image": "assets/cells/day23-204103-1.png",
    "buttonImage": "assets/buttons/btn-23.png",
}

days["24"] = {
    "itemId": d7["itemId"],
    "amount": 1,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": d7["itemName"],
    "randomItemIds": list(d7["randomItemIds"]),
    "note": "same as day7; ql=character level",
    "image": "assets/cells/day24-124354-1.png",
    "buttonImage": "assets/buttons/btn-24.png",
}

days["25"] = {
    "itemId": 254328,
    "amount": 1,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": "Controller Recompiler Unit (random)",
    "randomItemIds": [254328, 254327, 254326],
    "note": "Controller Recompiler Unit; ql=character level",
    "image": "assets/cells/day25-254328-1.png",
    "buttonImage": "assets/buttons/btn-25.png",
}

days["26"] = {
    "itemId": d4["itemId"],
    "amount": 1,
    "quality": 0,
    "qualityMode": "characterLevel",
    "itemName": d4["itemName"],
    "note": "same as day4; ql=character level",
    "image": "assets/cells/day26-291045-1.png",
    "buttonImage": "assets/buttons/btn-26.png",
}

days["27"] = {
    "itemId": d8["itemId"],
    "amount": 1,
    "quality": 1,
    "itemName": d8["itemName"],
    "note": "same as day8",
    "image": "assets/cells/day27-288747-1.png",
    "buttonImage": "assets/buttons/btn-27.png",
}

data["evidence"] = (
    "Mike daily reward list updated 20260808 — day17=day3; "
    "day20=day5; day22 VP 288752; day23=day9; day24=day7; "
    "day25 Controller Recompiler 254328/327/326; day26=day4; day27=day8; "
    "day19/21 empty; day14 still needs IDs."
)
data["listTemplate"] = (
    "For each day set: itemId, itemName, amount, quality/qualityMode(optional), "
    "randomItemIds(optional), image(optional). qualityMode: characterLevel | "
    "characterLevelPlusMinus (+ qualityDelta) | professionVendorNano (day10)."
)

# Copy cell images from source days where available
copies = [
    ("day5-288792-1.png", "day20-288792-1.png"),
    ("day9-204103-1.png", "day23-204103-1.png"),
    ("day7-124354-1.png", "day24-124354-1.png"),
    ("day4-291045-1.png", "day26-291045-1.png"),
    ("day8-288747-1.png", "day27-288747-1.png"),
    ("day3-293297-1.png", "day17-293297-1.png"),
]
# day22/25: try unknown or source if missing — leave named path; copy from unknown as placeholder
placeholders = [
    ("day22-unknown.png", "day22-288752-1.png"),
    ("day25-unknown.png", "day25-254328-1.png"),
]

def copy_cell(src_name, dst_name):
    src = os.path.join(cells, src_name)
    if not os.path.isfile(src):
        print("MISSING", src)
        return
    dst = os.path.join(cells, dst_name)
    shutil.copy2(src, dst)
    for root in xampp_cells:
        if os.path.isdir(root):
            shutil.copy2(src, os.path.join(root, dst_name))
    print("copied", src_name, "->", dst_name)

for a, b in copies:
    copy_cell(a, b)
for a, b in placeholders:
    # prefer existing unknown as source for new named cell
    if os.path.isfile(os.path.join(cells, a)):
        copy_cell(a, b)

for p in paths:
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    print("WROTE", p)

for d in ["17", "19", "20", "21", "22", "23", "24", "25", "26", "27"]:
    e = days[d]
    print("day%s itemId=%s mode=%s name=%s" % (
        d, e.get("itemId"), e.get("qualityMode", "fixed"), e.get("itemName", "")))
