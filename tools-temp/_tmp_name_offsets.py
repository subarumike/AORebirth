# -*- coding: utf-8 -*-
from pathlib import Path
import re

# Parse CorpseFullUpdate.cs templates for name offsets
src = Path("AORebirth/Server/ZoneEngine/Core/Packets/CorpseFullUpdate.cs").read_text(encoding="utf-8")

def find_name(label, const_name):
    m = re.search(const_name + r'\s*=\s*HexToBytes\(\s*"([^"]+)"(?:\s*\+\s*"([^"]+)")*', src)
    # get all hex parts for this template - simpler: find const and following strings until );
    m = re.search(const_name + r'\s*=\s*HexToBytes\(\s*((?:\"[0-9A-Fa-f]+\"\s*\+?\s*)+)\);', src, re.S)
    if not m:
        print(label, "not found")
        return
    hexs = "".join(re.findall(r'"([0-9A-Fa-f]+)"', m.group(1)))
    b = bytes.fromhex(hexs)
    # find Remains
    idx = b.find(b"Remains")
    print(label, "len", len(b), "Remains at", idx, "lenField", int.from_bytes(b[idx-4:idx],"big") if idx>=4 else None)

find_name("thief", "CapturedSubwayThiefTemplate")
find_name("minibull", "CapturedAreteMinibullTemplate")
find_name("rhinoman", "Template")

# sandstorm capture
h = open("tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/packets.hex.log", encoding="utf-8").read()
m = re.search(r"len=425 n3=CorpseFullUpdate hex=([0-9A-Fa-f]+)", h)
b = bytes.fromhex(m.group(1))
idx = b.find(b"Remains")
print("sandstorm Remains at", idx, "lenField", int.from_bytes(b[idx-4:idx],"big"))
print("shared NameOffset 231 content", b[231:245].hex(), b[231:245])
print("MD offset", b.find((265822).to_bytes(4,"big")))
print("tail dead", 353)
# CATMesh 0x40E5B = 265819?
print("catmesh", int.from_bytes(b[199:203],"big"))
print("cash?", int.from_bytes(b[207:211],"big"))
print("scale at 143", int.from_bytes(b[143:147],"big"))

# Pad like minibull? capture starts 0886 - replace with 0000 for template
padded = bytes.fromhex("0000" + m.group(1)[4:])  # replace seq with 0000
# Or 0000000A like minibull - capture is 0886000A...
print("start", m.group(1)[:20])
