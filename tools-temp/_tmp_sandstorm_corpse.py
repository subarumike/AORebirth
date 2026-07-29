# -*- coding: utf-8 -*-
"""Extract SANDSTORM Marauder corpse packet from capture and note length/name."""
import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/raw-packets.csv")
name_hex = "Remains of SANDSTORM Marauder".encode().hex().upper()
found = []
with cap.open(encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    for row in r:
        if row.get("N3TypeName") != "CorpseFullUpdate":
            continue
        payload = (row.get("PayloadHex") or "").replace(" ", "").upper()
        if name_hex in payload:
            found.append(row)

print("corpses", len(found))
for i, row in enumerate(found[:3]):
    payload = row["PayloadHex"].replace(" ", "").upper()
    print(i, "utc", row["CapturedUtc"], "len", row["PacketLength"], "payload_bytes", len(payload)//2)
    # find name offset
    idx = payload.find(name_hex)
    print("  name at", idx//2, "prev16", payload[idx-32:idx])
    # find CATMesh / monster-ish ints near textures
    b = bytes.fromhex(payload)
    # dump ints that look like mesh ids around name
    name_off = idx // 2
    print("  around name+len", b[name_off-4:name_off+len(name_hex)//2+8].hex())

# save first corpse hex
if found:
    Path("tools-temp/_tmp_sandstorm_corpse.hex").write_text(found[0]["PayloadHex"].replace(" ",""))
    print("wrote tools-temp/_tmp_sandstorm_corpse.hex")
