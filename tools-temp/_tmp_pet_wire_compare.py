# -*- coding: utf-8 -*-
"""Decode FormatFeedback report from capture 20260731-005116 and compare templates."""
import binascii
from pathlib import Path
import csv

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260731-005116/raw-packets.csv")
needles = [b"TkDG", b"gM*@", b"$*)e"]
with cap.open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        hx = (row.get("RawHex") or "").replace(" ", "")
        if not hx:
            continue
        raw = binascii.unhexlify(hx)
        for n in needles:
            i = raw.find(n)
            if i < 0:
                continue
            # find start of ~&
            start = raw.rfind(b"~&", 0, i)
            if start < 0:
                start = max(0, i - 20)
            # payload after N3 header-ish: dump from ~& to end of packet minus checksum maybe
            # Find end: after robot name or end
            end = len(raw)
            chunk = raw[start:end]
            print("===", n, "utc", row.get("CapturedUtc"), "plen", row.get("PacketLength"))
            print("ascii", "".join(chr(b) if 32 <= b < 127 else "." for b in chunk))
            print("hex", chunk.hex())
            # also show ints after name
            # after TkDG or gM*@ comes s then length byte
            break

# Reconstruct MsgSystem expected bytes for first pet line
text = "Catcraty's pet, Bureaucrat Worker: Hello master. I'm ready to obey your commands..."
tb = text.encode("utf-8")
slen = len(tb)
# type 36 string-only
payload = bytes([(slen >> 8) & 0xFF, slen & 0xFF]) + tb
pkt = bytes([0, 36, (len(payload) >> 8) & 0xFF, len(payload) & 0xFF]) + payload
print("\n=== MsgSystem string-only expected ===")
print("len", len(pkt), "hex", pkt.hex())

# with trailing Unk2=1 byte
payload2 = payload + bytes([1])
pkt2 = bytes([0, 36, (len(payload2) >> 8) & 0xFF, len(payload2) & 0xFF]) + payload2
print("\n=== MsgSystem string+Unk2=1 expected ===")
print("len", len(pkt2), "hex", pkt2.hex())

# with Unk1 u16=0 before string (wrong prior theory as int32)
payload3 = bytes([0, 0]) + payload + bytes([0, 1])  # unk1 u16 + string + unk2 u16?
pkt3 = bytes([0, 36, (len(payload3) >> 8) & 0xFF, len(payload3) & 0xFF]) + payload3
print("\n=== alt unk1u16 + string + unk2u16 ===")
print("len", len(pkt3), "hex", pkt3.hex())
