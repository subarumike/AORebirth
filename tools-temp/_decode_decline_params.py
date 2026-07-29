import csv
from pathlib import Path

caps = [
    "20260727-065826",
    "20260727-071217",
    "20260728-234012",
    "20260729-011305",
    "20260729-010948",
]

def ca(raw):
    b = bytes.fromhex(raw.replace(" ", ""))
    i = b.find(bytes.fromhex("5E477770"))
    if i < 0 or len(b) < i + 37:
        return None
    rest = b[i + 4 :]
    a = int.from_bytes(rest[9:13], "big")
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big")
    p2 = int.from_bytes(rest[29:33], "big")
    if p2 >= 0x80000000:
        p2 = p2 - 0x100000000
    return a, tt, ti, p1, p2

for cap in caps:
    p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures") / cap / "raw-packets.csv"
    if not p.exists():
        print("missing", cap)
        continue
    rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))
    print("====", cap)
    for i, r in enumerate(rows):
        if (r.get("Direction") or "") != "OUT":
            continue
        if (r.get("N3TypeName") or "") != "CharacterAction" or not r.get("RawHex"):
            continue
        dec = ca(r["RawHex"])
        if not dec:
            continue
        a, tt, ti, p1, p2 = dec
        if a in (0x1A, 0x1C, 0x15, 0x18, 0x20, 0x23):
            print("  #%d act=0x%X tgt=%X:%X p1=%d p2=%d" % (i, a, tt, ti, p1, p2))
