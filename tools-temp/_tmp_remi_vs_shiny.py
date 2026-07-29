# -*- coding: utf-8 -*-
"""Compare Remi tip structure vs working ShinySword tip AbsoluteTime markers."""
import binascii
import re
from pathlib import Path

def load_hex(path, const_name):
    text = Path(path).read_text(encoding="utf-8")
    m = re.search(const_name + r'\s*=\s*"([0-9A-Fa-f]+)"', text)
    return binascii.unhexlify(m.group(1))

remi = load_hex("AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs", "QuellTipHex")
shiny = load_hex("AORebirth/Server/ZoneEngine/Core/Arete/Quests/ShinySwordTipSender.cs", "TipHex")

print("remi", len(remi), "shiny", len(shiny))
print("remi head", remi[:16].hex())
print("shiny head", shiny[:16].hex())

# Compare markers around AbsoluteTime
for name, b in [("remi", remi), ("shiny", shiny)]:
    for marker in (b"\xd2\xf1\x4d", b"\xd2\xfc\x1c"):
        i = b.find(marker)
        print(name, "marker", marker.hex(), "at", i)
    # find pattern 00000104 / 00000105 before abs
    for tag in (b"\x00\x00\x00\x01\x04", b"\x00\x00\x00\x01\x05"):
        i = b.find(tag)
        print(name, "tag", tag.hex(), "at", i, "next", b[i+5:i+9].hex() if i>=0 else None)

# Remi uses D2FC1C not D2F14D - is that wrong extraction?
# Check capture raw packet for same tip
import csv
cap = Path("tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/raw-packets.csv")
with cap.open(encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") == "QuestFullUpdate" and "18:49:28" in (row.get("CapturedUtc") or ""):
            hx = (row.get("RawHex") or "").replace(" ", "")
            print("capture equal tip", hx.lower() == remi.hex())
            # show markers in capture
            cb = binascii.unhexlify(hx)
            print("cap D2FC1C", cb.find(b"\xd2\xfc\x1c"), "D2F14D", cb.find(b"\xd2\xf1\x4d"))
            break

# Compare tip Unknown / message subtype after 000A0001
print("remi subtype", remi[4:8].hex(), remi[8:12].hex())
print("shiny subtype", shiny[4:8].hex(), shiny[8:12].hex())
