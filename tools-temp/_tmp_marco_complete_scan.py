# -*- coding: utf-8 -*-
import pathlib, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
for cap in ["20260730-212921", "20260730-212713"]:
    p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures") / cap
    print("====", cap, "exists", p.exists())
    if not p.exists():
        continue
    for name in ["mission-flow.log", "system-messages.log", "inventory-updates.csv", "events.log"]:
        f = p / name
        if not f.exists():
            continue
        print("---", name)
        n = 0
        for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
            low = line.lower()
            if any(k in low for k in ["223373", "555be9f4", "attribute", "2581", "1160", "received reward", "quest", "templateaction", "overflow", "248257", "delete"]):
                print(line[:350])
                n += 1
                if n >= 40:
                    print("...trunc")
                    break
