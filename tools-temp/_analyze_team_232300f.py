from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
rows=list(csv.DictReader(Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-232300/raw-packets.csv").open(encoding="utf-8-sig",newline="")))
# Feedback
b=bytes.fromhex(rows[70]["RawHex"].strip())
print("feedback", rows[70]["RawHex"])
# find type 50544D19
i=b.find(bytes.fromhex("50544D19"))
body=b[i+4:]
print("fb body", body.hex())
# CharacterAction 0xA9
b=bytes.fromhex(rows[72]["RawHex"].strip())
i=b.find(bytes.fromhex("5E477770"))
body=b[i+4:]
print("A9 body", body.hex())
print("compare invite OUT 71")
b=bytes.fromhex(rows[71]["RawHex"].strip())
i=b.find(bytes.fromhex("5E477770"))
print(b[i+4:].hex())
print("OUT 73 p2=1")
b=bytes.fromhex(rows[73]["RawHex"].strip())
i=b.find(bytes.fromhex("5E477770"))
print(b[i+4:].hex())
