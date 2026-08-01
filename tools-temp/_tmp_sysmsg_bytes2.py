# -*- coding: utf-8 -*-
import csv, pathlib, binascii

p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-222816/raw-packets.csv")
needles = [b"Charge!", b"follow you wherever", b"I will wait here", b"protect you to the best", b"stay out of it"]
hits = 0
with p.open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        hx = (row.get("RawHex") or "").replace(" ", "")
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
            # dump surrounding 60 bytes before
            start = max(0, i - 60)
            chunk = raw[start:i+len(n)+8]
            print("---", row.get("CapturedUtc"), "dir", row.get("Direction"), "n3", row.get("N3TypeName"), "proto", row.get("Protocol") or row.get("Channel"))
            print("offset", i, "chunkhex", chunk.hex())
            # show printable
            print("ascii", "".join(chr(b) if 32 <= b < 127 else "." for b in chunk))
            # look for 00 24 nearby
            for j in range(max(0, i-30), i):
                if raw[j] == 0x00 and j+1 < len(raw) and raw[j+1] == 0x24:
                    print("00 24 at", j, "full from type:", raw[j:i+len(n)+4].hex())
            break
        if hits >= 6:
            break
print("total hits printed", hits)
