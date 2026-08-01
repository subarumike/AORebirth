# -*- coding: utf-8 -*-
import pathlib, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
# Decode FormatFeedback from capture vs current
# Capture: ~&!!!":$'O"ui!!!?@i!!!.X~
# Current: ~&!!!":$'O"ui!!!?4i!!!/S~

# Search raw hex for QFU Buy Nano in capture
cap = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-212713")
# Find QuestFullUpdate in raw-packets
import csv
with (cap/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        if row.get("N3TypeName") == "QuestFullUpdate":
            hx = row.get("RawHex") or ""
            print("utc", row.get("CapturedUtc"), "len", len(hx)//2)
            print(hx[:400])
            # save full
            (cap/"_qfu_hex.txt").write_text(hx, encoding="utf-8")
            break

# Also look for Formatted feedback encoding patterns in code
print("feedback capture bytes attempt")
fb = '~&!!!":$\'O"ui!!!?@i!!!.X~'
print(repr(fb))
