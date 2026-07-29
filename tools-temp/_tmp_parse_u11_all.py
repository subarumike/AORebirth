# -*- coding: utf-8 -*-
"""Parse ShinySword Unknown11 vs Remi; find correct AbsoluteTime offset."""
from pathlib import Path
import re, struct

def be32(data, off):
    return struct.unpack_from(">I", data, off)[0]

def parse(name, hexs):
    b = bytes.fromhex(hexs)
    off = 16
    assert be32(b, off) == 0x465A4061
    off += 4
    off += 8  # identity
    off += 1  # unknown
    enc = be32(b, off); off += 4
    count = enc // 0x3F1 - 1
    # quest
    off += 8  # quest id
    off += 16  # u1-u4
    # short nt
    while b[off] != 0:
        off += 1
    off += 1
    ln = be32(b, off); off += 4 + ln
    off += 8  # unknownid1
    for _ in range(6):
        off += 4  # u5-u10
    enc = be32(b, off); off += 4
    mi = enc // 0x3F1 - 1
    # skip mission items (each 16 bytes)
    off += mi * 16
    u11 = be32(b, off)
    print(name, "Unknown11 offset", off, "value", hex(u11), u11, "ascii", bytes.fromhex(f"{u11:08x}"))
    return off, u11

ss = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/ShinySwordTipSender.cs").read_text(encoding="utf-8")
remi = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
parse("shiny", re.search(r'TipHex =\s*"([0-9A-Fa-f]+)"', ss).group(1))
parse("remi", re.search(r'QuellTipHex =\s*"([0-9A-Fa-f]+)"', remi).group(1))
parse("remi-return", re.search(r'ReturnTipHex =\s*"([0-9A-Fa-f]+)"', remi).group(1))

# Patrick insurance for comparison
ps = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/PatrickSunTipSender.cs").read_text(encoding="utf-8")
# first long hex in file
m = re.search(r'"([0-9A-Fa-f]{200,})"', ps)
parse("patrick0", m.group(1))
