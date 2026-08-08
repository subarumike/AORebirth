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
    d14 = data["days"]["14"]
    d14["quality"] = 0
    d14["qualityMode"] = "characterLevelPlusMinus"
    d14["qualityDelta"] = 10
    d14["itemName"] = "Prototype Symbiant (random)"
    d14["note"] = "Random Prototype Symbiant; ql = character level +-10"
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print("updated", path, "mode", d14["qualityMode"], "delta", d14["qualityDelta"])
