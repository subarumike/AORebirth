# -*- coding: utf-8 -*-
import re
from pathlib import Path

src = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
m = re.search(r'QuellTipHex =\s*"([0-9A-Fa-f]+)"', src)
hexs = m.group(1)
b = bytes.fromhex(hexs)
print("len", len(b))
for needle in [bytes.fromhex("d2fc1c"), bytes.fromhex("d2f14d"), bytes.fromhex("6e697200")]:
    i = 0
    while True:
        j = b.find(needle, i)
        if j < 0:
            break
        print(needle.hex(), "at", j, "bytes", b[j : j + 8].hex())
        i = j + 1
for off in (625, 673):
    print("claimed off", off, b[off : off + 4].hex(), "prev", b[off - 3 : off].hex())

# Compare ShinySword structure around D2F14D
src2 = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/ShinySwordTipSender.cs").read_text(encoding="utf-8")
m2 = re.search(r'TipHex =\s*"([0-9A-Fa-f]+)"', src2)
b2 = bytes.fromhex(m2.group(1))
j = b2.find(bytes.fromhex("d2f14d"))
print("shiny D2F14D at", j, "bytes", b2[j : j + 12].hex(), "len", len(b2))
print("shiny claimed 383", b2[383:387].hex(), "prev", b2[380:383].hex())
print("shiny claimed 431", b2[431:435].hex(), "prev", b2[428:431].hex())
