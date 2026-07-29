from pathlib import Path
import re
import struct

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260720-204431/packets.hex.log")
text = cap.read_text(encoding="utf-8", errors="ignore")

# Find lines / packets containing Supreme name hex
name = bytes("Supreme Collector of Waste", "ascii").hex().upper()
waste = bytes("Waste Collector", "ascii").hex().upper()
mat22 = bytes("Material #22", "ascii").hex().upper()


def extract_override_blob(packet_hex: str, label: str):
    u = packet_hex.upper().replace(" ", "")
    # Material # pattern in AO ExtTex
    idx = u.find("4D6174657269616C2023")  # "Material #"
    if idx < 0:
        print(label, "no Material #")
        return
    # dump 64 bytes around
    start = max(0, idx - 8)
    blob = bytes.fromhex(u[start : start + 128])
    print(label, "override vicinity:", blob)
    # decode name
    try:
        s = blob.decode("ascii", errors="replace")
        print(label, "ascii:", repr(s))
    except Exception:
        pass
    # texture id often after padded name at offset
    # known Waste format: Material #22 then zeros then 00 43 A6
    m = re.search(rb"Material #[0-9]+", blob)
    if m:
        print(label, "material", m.group(0))


found_s = 0
found_w = 0
for line in text.splitlines():
    u = line.upper()
    if name in u and found_s < 2:
        # strip non-hex prefix if any
        hx = re.sub(r"[^0-9A-Fa-f]", "", line)
        # better: find hex body
        m = re.search(r"([0-9A-Fa-f]{80,})", line)
        if m:
            extract_override_blob(m.group(1), "SUPREME")
            found_s += 1
    if waste in u and "SUPREME" not in u and found_w < 2:
        # waste name alone - avoid supreme which also contains? Supreme has "of Waste" not "Waste Collector"
        if waste in u:
            m = re.search(r"([0-9A-Fa-f]{80,})", line)
            if m and name not in m.group(1).upper():
                extract_override_blob(m.group(1), "WASTE")
                found_w += 1

print("found supreme", found_s, "waste", found_w)
