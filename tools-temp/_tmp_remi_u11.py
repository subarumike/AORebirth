# -*- coding: utf-8 -*-
"""Locate Quest.Unknown11 AbsoluteTime vs D2FC1C marker AbsoluteTime in Remi tip."""
from pathlib import Path
import re

src = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
b = bytes.fromhex(re.search(r'QuellTipHex =\s*"([0-9A-Fa-f]+)"', src).group(1))

# After LongInfo ends: find Remi NPC identity C35078E0FC75
npc = b.find(bytes.fromhex("C35078E0FC75"))
print("Remi NPC at", npc)

# Mission icon 00002C42 = 11330
icon = b.find(bytes.fromhex("00002C42"))
print("icon 11330 at", icon, "context", b[icon-20:icon+24].hex())

# Unknown11 should be shortly before UnknownHash / MissionIcon
# Look for UXIR = 55584952
uxir = b.find(b"UXIR")
print("UXIR at", uxir, "before", b[uxir-32:uxir].hex())

# In Quest: after MissionItemData comes Unknown11
# Find pattern of empty X3F1 then AbsoluteTime candidate
# Capture AbsoluteTime 6E697200 - ALL occurrences
i = 0
while True:
    j = b.find(bytes.fromhex("6E697200"), i)
    if j < 0:
        break
    print("expiry at", j, "prev8", b[j-8:j].hex(), "next8", b[j+4:j+12].hex())
    i = j + 1

# ShinySword: find AbsoluteTime relative to MissionIcon and D2F14D
ss = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/ShinySwordTipSender.cs").read_text(encoding="utf-8")
b2 = bytes.fromhex(re.search(r'TipHex =\s*"([0-9A-Fa-f]+)"', ss).group(1))
exp = bytes.fromhex("5DF2C300")
i = 0
while True:
    j = b2.find(exp, i)
    if j < 0:
        break
    print("shiny expiry at", j, "prev8", b2[j-8:j].hex())
    i = j + 1
icon2 = b2.find(bytes.fromhex("000111D3"))  # often near actions
print("shiny len", len(b2))

# In typed Flint, Unknown11 is BEFORE hash and MissionIconId
# Parse Remi from after LongInfo null-terminated short + len long
# Print dword stream from npc to D2FC1C with annotations
print("--- dwords from NPC to end ---")
start = npc
for off in range(start, len(b)-3, 4):
    v = int.from_bytes(b[off:off+4], "big")
    mark = ""
    if v == 0x6E697200:
        mark = " << AbsoluteTime candidate"
    if v == 0x0000D2FC or (off <= 622 <= off+3):
        mark += " << near D2FC"
    if b[off:off+4] == b"UXIR":
        mark = " << UXIR"
    if v == 11330:
        mark = " << icon"
    if v == 0x7996C028:
        mark = " << player"
    print(f"{off:4d} 0x{v:08X}{mark}")
    if off > 620 and off < 630:
        # also print unaligned
        pass

print("byte 622-640", b[622:640].hex())
