# Extract player SCFU/Appearance around Hellfyre equip for weapon meshes
from pathlib import Path
import re

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902")
# AppearanceUpdate for player 7996C028
for name in ["packets.hex.log", "events.log"]:
    p = cap / name
    hits = []
    for i, line in enumerate(p.read_text(encoding="utf-8", errors="replace").splitlines()):
        if "7996C028" in line and ("AppearanceUpdate" in line or "SimpleCharFullUpdate" in line):
            hits.append((i, line[:300]))
    print("====", name, "hits", len(hits))
    for i, l in hits[:30]:
        print(i, l)
    print()

# Decode AppearanceUpdate hex for player after zone with hellfyre (18:54:22)
for line in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "18:54:22.718" in line and "AppearanceUpdate" in line:
        print("AU line", line[:250])
        hx = line.split("hex=")[-1].strip()
        raw = bytes.fromhex(hx)
        # search for 264083 = 0x000407D3
        for needle in [264083, 9013, 1006, 295757]:
            b = needle.to_bytes(4, "big")
            print(" find", needle, raw.find(b))
        # also little endian
        for needle in [264083, 9013]:
            b = needle.to_bytes(4, "little")
            print(" findLE", needle, raw.find(b))
