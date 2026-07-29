# -*- coding: utf-8 -*-
"""Compare Remi Quell tip hex in code vs capture raw packet."""
import csv
import re
from pathlib import Path

src = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
code_hex = re.search(r'QuellTipHex =\s*"([0-9A-Fa-f]+)"', src).group(1).upper()
code = bytes.fromhex(code_hex)
print("code tip len", len(code))

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/raw-packets.csv")
# Find QuestFullUpdate containing 556B5E53
needle = "556B5E53"
found = []
with cap.open(encoding="utf-8", errors="replace", newline="") as f:
    reader = csv.DictReader(f)
    for row in reader:
        payload = (row.get("payload_hex") or row.get("PayloadHex") or row.get("hex") or "").replace(" ", "").upper()
        if not payload:
            # try any column
            for k, v in row.items():
                if v and needle in str(v).upper().replace(" ", ""):
                    payload = str(v).upper().replace(" ", "")
                    break
        if needle in payload and "5175656C6C696E67" in payload:  # "Quelling"
            found.append((row, payload))

print("found rows", len(found))
if not found:
    # dump headers / sample
    with cap.open(encoding="utf-8", errors="replace", newline="") as f:
        reader = csv.DictReader(f)
        print("cols", reader.fieldnames)
        for i, row in enumerate(reader):
            if i > 2:
                break
            print({k: (v[:80] + "...") if v and len(v) > 80 else v for k, v in row.items()})
else:
    for i, (row, payload) in enumerate(found[:3]):
        print("--- match", i)
        meta = {k: row[k] for k in row if k and "hex" not in k.lower() and "payload" not in k.lower()}
        print(meta)
        print("payload len bytes", len(payload)//2)
        # find tip-like start 000A0001 or 0A0001
        idx = payload.find("000A0001")
        print("idx 000A0001", idx)
        # compare trailing structure
        if payload.endswith(code_hex) or code_hex in payload:
            print("code hex contained in payload:", code_hex in payload)
            print("exact equal full payload?", payload == code_hex)
            if code_hex in payload:
                start = payload.index(code_hex)
                print("code starts at nibble", start, "prefix", payload[:start][:40])
        # try match without first 4 bytes (size)
        for skip in (0, 2, 4, 8):
            sub = payload[skip*2:]
            if sub.startswith(code_hex[:40]) or code_hex.startswith(sub[:40]):
                print("align skip", skip, "sublen", len(sub)//2, "codelen", len(code))
                if sub == code_hex:
                    print("EXACT match with skip", skip)
                else:
                    # first diff
                    bl = bytes.fromhex(sub[: min(len(sub), len(code_hex))])
                    bc = code
                    for j in range(min(len(bl), len(bc))):
                        if bl[j] != bc[j]:
                            print("first diff at", j, "cap", bl[j:j+8].hex(), "code", bc[j:j+8].hex())
                            break
                    else:
                        print("prefix equal; len cap", len(bl), "code", len(bc), "extra cap", sub[len(code_hex):len(code_hex)+20], "extra code", code_hex[len(sub):len(sub)+20])
