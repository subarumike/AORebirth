# Decode capture ContainerAdd + compare icon 264797 vs sunglasses icons
import struct
from pathlib import Path

hexes = {
"overflow": "01DE000A0001003100000DC17996C02847537A240000C3507996C028000000006E000000000000006E7996C0280000006F",
"equip": None,
}

# find equip ContainerAdd hex
for line in Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "18:50:10.275" in line and "ContainerAddItem" in line:
        print(line[:220])
        hx = line.split("hex=")[-1].strip()
        hexes["equip"] = hx
        break

for name, hx in hexes.items():
    if not hx:
        print(name, "MISSING")
        continue
    raw = bytes.fromhex(hx)
    print(name, "len", len(raw))
    # find N3 payload after header - rough: look for C350
    i = raw.find(bytes.fromhex("C3507996C028"))
    print(" after identity", raw[i:].hex())
