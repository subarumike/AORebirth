# -*- coding: utf-8 -*-
import csv, pathlib, binascii, struct

p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-222816")
log = (p / "chat-dialogue.log").read_text(encoding="utf-8-sig", errors="replace")
for line in log.splitlines():
    if "SystemMessage" in line or "pet" in line.lower() or "Charge" in line:
        print(line[:300])

print("\n=== looking for chat opcode 0x24 SystemMessage in raw packets ===")
csv_path = p / "raw-packets.csv"
hits = 0
with csv_path.open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        hx = (row.get("RawHex") or "").replace(" ", "")
        if not hx:
            continue
        try:
            raw = binascii.unhexlify(hx)
        except Exception:
            continue
        # Chat packets often start with length + type
        # Look for ASCII Charge! or follow
        if b"Charge!" in raw or b"follow you wherever" in raw or b"I will wait here" in raw:
            hits += 1
            print(row.get("CapturedUtc"), row.get("Direction"), row.get("N3TypeName"), "len", len(raw))
            # find message start
            for needle in (b"Charge!", b"follow you wherever", b"I will wait here", b"protect you", b"stay out"):
                i = raw.find(needle)
                if i >= 0:
                    start = max(0, i - 40)
                    end = min(len(raw), i + len(needle) + 20)
                    chunk = raw[start:end]
                    print("  needle", needle, "at", i, "context", chunk.hex())
                    # try parse as chat: look back for 00 24
                    for j in range(max(0, i - 80), i):
                        if raw[j:j+2] == b"\x00\x24":
                            print("  found 00 24 at", j, "payload", raw[j:i+len(needle)+5].hex())
                            break
            if hits >= 8:
                break
print("hits", hits)
