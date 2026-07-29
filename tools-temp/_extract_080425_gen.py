import csv
import os

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-080425"
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") == "PlayfieldAnarchyF" and (row.get("Direction") or "").startswith("IN"):
            hx = (row.get("RawHex") or "").replace(" ", "").upper()
            last = hx.rfind("00009C50")
            b = bytes.fromhex(hx)
            gen = b[last // 2 + 8 :]
            print("gen_len", len(gen))
            open(r"tools-temp/_tmp_080425_gen.hex", "w").write(gen.hex().upper())
            break
