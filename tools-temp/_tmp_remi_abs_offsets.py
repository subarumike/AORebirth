# -*- coding: utf-8 -*-
import binascii
import re
from pathlib import Path

cs = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
for name, const in [("Quell", "QuellTipHex"), ("Return", "ReturnTipHex")]:
    h = re.search(const + r' =\s*"([0-9A-Fa-f]+)"', cs).group(1)
    b = binascii.unhexlify(h)
    print("===", name, "len", len(b))
    # find D2F marker before AbsoluteTime
    for marker in (b"\xd2\xfc\x1c", b"\xd2\xf1\x4d", b"\xd2\xfc"):
        idx = 0
        while True:
            i = b.find(marker, idx)
            if i < 0:
                break
            print(" marker", marker.hex(), "at", i, "next4", b[i + len(marker) : i + len(marker) + 4].hex())
            idx = i + 1
    # all expiry-like
    exp = bytes.fromhex("6E697200" if name == "Quell" else "60EBA800")
    idx = 0
    while True:
        i = b.find(exp, idx)
        if i < 0:
            break
        print(" expiryval at", i, "ctx", b[i - 4 : i + 8].hex())
        idx = i + 1
