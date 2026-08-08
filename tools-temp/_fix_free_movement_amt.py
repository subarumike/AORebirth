# -*- coding: utf-8 -*-
from __future__ import print_function
import json
from pathlib import Path

paths = [
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\rewards.json"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json"),
    Path(r"C:\xampp\htdocs\daily\rewards.json"),
]

for path in paths:
    data = json.loads(path.read_text(encoding="utf-8"))
    for day, note in (
        ("9", "x25; ql=character level"),
        ("23", "same as day9: x25; ql=character level"),
    ):
        e = data["days"][day]
        e["itemId"] = 204103
        e["amount"] = 25
        e["qualityMode"] = "characterLevel"
        e["itemName"] = "Free Movement"
        e["note"] = note
        e["image"] = "assets/buttons/btn-%02d.png" % int(day)
        e["buttonImage"] = "assets/buttons/btn-%02d.png" % int(day)
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print("updated", path, "d9", data["days"]["9"]["amount"], "d23", data["days"]["23"]["amount"])
