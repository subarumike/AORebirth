import csv
from pathlib import Path

# Decode 0x05DD LFT replies from 20260727-lft-list-search
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-lft-list-search/raw-packets.csv")
rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))
print("rows", len(rows), "cols", list(rows[0].keys())[:15])
# chat packets may not have N3TypeName
for idx, r in enumerate(rows):
    hx = (r.get("RawHex") or "").replace(" ", "")
    if not hx:
        continue
    b = bytes.fromhex(hx)
    # look for chat type 1501 = 05DD
    if len(b) < 4:
        continue
    # various wrappers — search for 05 DD
    if b"\x05\xdd" not in b and b"\x05\xDD" not in b:
        # also try big-endian length framed
        pass
    pos = b.find(b"\x05\xdd")
    if pos < 0:
        pos = b.find(bytes([0x05, 0xDD]))
    if pos < 0:
        continue
    body = b[pos:]
    if len(body) < 8:
        continue
    mode = body[2] if len(body) > 2 else -1
    # actually after type u16be 05DD, length u16be, then mode
    # PPJ: type is first
    typ = int.from_bytes(body[0:2], "big")
    ln = int.from_bytes(body[2:4], "big")
    payload = body[4:4+max(0, ln-4)] if ln > 4 else body[4:]
    if not payload:
        continue
    mode = payload[0]
    print("idx", idx, "dir", r.get("Direction"), "typ", hex(typ), "mode", mode, "paylen", len(payload), "hex", payload[:40].hex())
