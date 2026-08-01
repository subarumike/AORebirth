# -*- coding: utf-8 -*-
import csv, pathlib, binascii

for cap in ["20260730-234537", "20260730-222816", "20260730-164552"]:
    p = pathlib.Path(rf"tools-temp/AOSharpLiveCapture/bin/Debug/captures/{cap}/raw-packets.csv")
    if not p.exists():
        print("missing", cap)
        continue
    needles = [b"Charge!", b"follow you wherever", b"I will wait here", b"protect you to the best", b"Bureaucrat Worker: Health"]
    hits = 0
    print("====", cap)
    with p.open(encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            hx = (row.get("RawHex") or row.get("Hex") or "").replace(" ", "")
            if not hx or len(hx) < 20:
                continue
            try:
                raw = binascii.unhexlify(hx)
            except Exception:
                continue
            for n in needles:
                i = raw.find(n)
                if i < 0:
                    continue
                hits += 1
                start = max(0, i - 40)
                chunk = raw[start:i+len(n)+8]
                print("n3", row.get("N3TypeName"), "proto", row.get("Protocol") or row.get("Channel") or row.get("PacketType"))
                print("chunkhex", chunk.hex())
                print("ascii", "".join(chr(b) if 32 <= b < 127 else "." for b in chunk))
                # find packet start 00 24 or similar
                for j in range(max(0, i-50), i):
                    if raw[j:j+2] in (b"\x00\x24", b"\x00\x25", b"\x00\x23"):
                        print("hdr", raw[j:i+4].hex())
                break
            if hits >= 4:
                break
    print("hits", hits)
