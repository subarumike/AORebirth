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
    data["freeTestMode"] = False
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print("freeTestMode", data["freeTestMode"], path)
