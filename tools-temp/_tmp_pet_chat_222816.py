# -*- coding: utf-8 -*-
import pathlib, csv, sys, binascii, struct
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-222816")
print("=== chat-dialogue")
for line in (p/"chat-dialogue.log").read_text(encoding="utf-8-sig", errors="replace").splitlines():
    if "SystemMessage" in line or "pet" in line.lower():
        print(line[:280])

print("\n=== PetCommand from raw-packets")
# PetCommand N3 type - look for name
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        name = (row.get("N3TypeName") or "")
        if name != "PetCommand":
            continue
        hx = row.get("RawHex") or ""
        try:
            raw = binascii.unhexlify(hx)
        except Exception:
            continue
        # Unknown2 command id often near end as int32 BE after identities
        # Print last 16 bytes as ints
        tail = raw[-20:]
        ints = []
        for i in range(0, len(tail)-3, 4):
            ints.append(struct.unpack(">I", tail[i:i+4])[0])
        print(row.get("CapturedUtc"), "len", len(raw), "tail ints", ints, "hex", hx[-40:])
