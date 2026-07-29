from pathlib import Path
import csv

# Decode feedback from 232300 and check if any Feedback looks like team-level
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-232300/raw-packets.csv")
rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "Feedback":
        continue
    b = bytes.fromhex((r.get("RawHex") or "").strip())
    i = b.find(bytes.fromhex("50544D19"))
    body = b[i+4:] if i >= 0 else b
    # identity(8)+unk(1)+u1(4)+cat(4)+msg(4)
    unk = body[8]
    u1 = int.from_bytes(body[9:13], "big", signed=True)
    cat = int.from_bytes(body[13:17], "big", signed=True)
    msg = int.from_bytes(body[17:21], "big", signed=True)
    print(f"{idx} {r.get('Direction')} unk={unk} u1={u1} cat={cat} msg={msg} (0x{msg:X})")
