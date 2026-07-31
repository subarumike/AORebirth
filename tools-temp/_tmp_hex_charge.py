# -*- coding: utf-8 -*-
from pathlib import Path
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-222816/packets.hex.log")
needles = ["Charge!", "follow you wherever", "I will wait here", "protect you", "stay out"]
count = 0
with p.open(encoding="utf-8", errors="replace") as fh:
    for line in fh:
        for n in needles:
            if n in line:
                print(line[:500])
                count += 1
                break
        if count >= 8:
            break
print("count", count)
