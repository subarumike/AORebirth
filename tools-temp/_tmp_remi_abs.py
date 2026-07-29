# -*- coding: utf-8 -*-
"""Inspect Remi tip AbsoluteTime vs TipClientClockBase and structure around playfield."""
from pathlib import Path
import re

src = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
code_hex = re.search(r'QuellTipHex =\s*"([0-9A-Fa-f]+)"', src).group(1)
b = bytes.fromhex(code_hex)

tip_base = 1_201_445_827
duration = 48 * 3600
capture_exp = 0x6E697200
print("capture AbsoluteTime", capture_exp, hex(capture_exp))
print("tipBase+duration", tip_base + duration, hex(tip_base + duration))
print("capture - tipBase", capture_exp - tip_base, "days", (capture_exp - tip_base)/86400)

# Dump ints near end of packet for structure
print("--- tail ints ---")
for i in range(600, len(b)-3, 4):
    v = int.from_bytes(b[i:i+4], "big")
    print(f"{i:4d} {v:10d} 0x{v:08X}")

# Find Playfield2:1999 = type? Identity playfield
# 1999 = 0x000007CF
idx = b.find(bytes.fromhex("000007CF"))
print("07CF at", idx)
idx = b.find(bytes.fromhex("07CF"))
print("07CF any at", idx)

# Compare kneecapping AbsoluteTime leave-alone approach works with D2FC1C
kneec = bytes.fromhex(re.search(r'KneecappingQfuHex =\s*"([0-9A-Fa-f]+)"', Path(r"AORebirth/Server/ZoneEngine/Core/FlintKneecappingTipWire.cs").read_text()).group(1))
j = kneec.find(bytes.fromhex("d2fc1c"))
print("kneec D2FC1C at", j, kneec[j:j+8].hex(), "exp", int.from_bytes(kneec[j+3:j+7],"big"))
