# -*- coding: utf-8 -*-
import binascii
import re
from pathlib import Path

cs = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
m = re.search(r'QuellTipHex =\s*"([0-9A-Fa-f]+)"', cs)
h = m.group(1)
b = binascii.unhexlify(h)
print("len", len(b))
print("be16", int.from_bytes(b[0:2], "big"))

for name, tgt in [("expiry", "6E697200"), ("player", "7996C028"), ("mission", "556B5E53")]:
    needle = bytes.fromhex(tgt)
    idx = 0
    while True:
        i = b.find(needle, idx)
        if i < 0:
            break
        print(name, i)
        idx = i + 1

# Compare with capture raw QuestFullUpdate
cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/raw-packets.csv")
import csv
with cap.open(encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") == "QuestFullUpdate" and "18:49:28" in (row.get("CapturedUtc") or ""):
            hx = (row.get("RawHex") or "").replace(" ", "")
            print("capture len", len(hx) // 2)
            print("equal", hx.lower() == h.lower())
            # show header
            print("cap head", hx[:32])
            print("tip head", h[:32])
            break

# Check if dialogue identity resolves - remi file NpcIdentity
dlg = Path(r"AORebirth/Server/ZoneEngine/Content/Arete/flint-novak/dialogue/remi-gallois.dialogue.json").read_text(encoding="utf-8")
print("has remi_offer_001", "remi_offer_001" in dlg)
print("has SimpleChar:78E0FC75", "SimpleChar:78E0FC75" in dlg)
