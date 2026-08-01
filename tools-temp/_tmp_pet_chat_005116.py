# -*- coding: utf-8 -*-
import csv, binascii, pathlib, re

cap = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260731-005116")
needles = [
    b"Hello master",
    b"Charge!",
    b"I will follow",
    b"I will wait here",
    b"protect you to the best",
    b"stay out of it",
    b"TkDG",
    b"gM*@",
    b"Catcraty",
]

print("=== raw-packets.csv ===")
hits = 0
with (cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        hx = (row.get("RawHex") or "").replace(" ", "")
        if not hx:
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
            start = max(0, i - 48)
            chunk = raw[start : i + len(n) + 16]
            print("---", row.get("CapturedUtc"), "dir", row.get("Direction"), "n3", row.get("N3TypeName"), "plen", row.get("PacketLength"))
            print("off", i, "chunk", chunk.hex())
            print("ascii", "".join(chr(b) if 32 <= b < 127 else "." for b in chunk))
            for j in range(max(0, i - 48), i):
                if j + 1 < len(raw) and raw[j] == 0x00 and raw[j + 1] in (0x24, 0x36, 0x23):
                    print("hdr", format(raw[j + 1], "02x"), "at", j, "fromhdr", raw[j : i + len(n) + 8].hex())
            break
        if hits >= 20:
            break
print("raw hits", hits)

print("\n=== packets.hex.log scan ===")
hexlog = cap / "packets.hex.log"
if hexlog.exists():
    text_hits = 0
    with hexlog.open("r", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            if any(x in line for x in ("Hello master", "Charge!", "Catcraty", "0024", "SystemMessage", "follow you")):
                print(line[:400].rstrip())
                text_hits += 1
                if text_hits >= 30:
                    break
    print("hexlog text hits", text_hits)
else:
    print("no packets.hex.log")

print("\n=== events.log CHAT SystemMessage lines ===")
with (cap / "events.log").open("r", encoding="utf-8", errors="replace") as fh:
    for line in fh:
        if "SystemMessage" in line or ("CHAT" in line and ("pet" in line.lower() or "Hello" in line or "Charge" in line)):
            print(line[:500].rstrip())
