# -*- coding: utf-8 -*-
import binascii
import re
from pathlib import Path

cs = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
h = re.search(r'QuellTipHex =\s*"([0-9A-Fa-f]+)"', cs).group(1)
b = binascii.unhexlify(h)
for off in (620, 625, 668, 673):
    print(off, b[off - 8 : off + 12].hex(), repr(b[off - 8 : off + 12]))

# Simulate replace both expiries with a sample live expiry
live = 0x47C00000  # sample
pkt = bytearray(b)
from_b = bytes.fromhex("6E697200")
to_b = live.to_bytes(4, "big")
i = 0
while True:
    j = pkt.find(from_b, i)
    if j < 0:
        break
    pkt[j : j + 4] = to_b
    print("replaced at", j)
    i = j + 4

# Compare with Patrick tip AbsoluteTime locations
pcs = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/PatrickSunTipSender.cs").read_text(encoding="utf-8")
ph = re.search(r'TrySendWire\(\s*source,\s*"([0-9A-Fa-f]+)"\s*,\s*CapturedInsuranceExpiry', pcs, re.S)
if not ph:
    ph = re.search(r'"([0-9A-Fa-f]{80,})"', pcs)
# insurance tip is first long hex in SendInsuranceTip
ph = re.search(r'SendInsuranceTip.*?TrySendWire\(\s*source,\s*"([0-9A-Fa-f]+)"', pcs, re.S)
print("patrick found", bool(ph))
if ph:
    pb = binascii.unhexlify(ph.group(1))
    print("patrick len", len(pb), "be16", int.from_bytes(pb[0:2], "big"))
    exp = bytes.fromhex("6A651192")
    idx = 0
    while True:
        j = pb.find(exp, idx)
        if j < 0:
            break
        print("patrick expiry", j, pb[j - 8 : j + 8].hex())
        idx = j + 1
