# -*- coding: utf-8 -*-
import binascii, struct, csv
from pathlib import Path
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-234537/raw-packets.csv")
with p.open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        if (row.get("N3TypeName") or "") != "PetCommand":
            continue
        hx = (row.get("RawHex") or "").replace(" ","")
        raw = binascii.unhexlify(hx)
        print("utc", row.get("CapturedUtc"), "len", len(raw))
        print("hex", hx)
        # dump as BE ints from offset after header
        # N3 header typically ~16+ bytes
        for off in range(0, min(len(raw), 80), 4):
            pass
        # print all BE uint32
        vals = []
        for i in range(0, len(raw)-3, 4):
            vals.append((i, struct.unpack(">I", raw[i:i+4])[0]))
        print("ints", vals)
        print("---")
