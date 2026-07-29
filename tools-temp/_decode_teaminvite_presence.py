import csv
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")

for name in ("20260729-011305", "20260729-011333", "20260729-010948", "20260729-010949"):
    cap = root / name
    rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print("====", name, "====")
    for idx, r in enumerate(rows):
        n3 = r.get("N3TypeName") or ""
        d = r.get("Direction") or ""
        if n3 == "TeamInvite":
            print("#%d %s TeamInvite id=%s hexlen=%s" % (idx, d, r.get("IdentityInstance"), len((r.get("RawHex") or "").replace(" ", "")) // 2))
            continue
        if n3 != "CharacterAction" or not r.get("RawHex"):
            continue
        b = bytes.fromhex(r["RawHex"].replace(" ", ""))
        i = b.find(bytes.fromhex("5E477770"))
        if i < 0:
            continue
        rest = b[i + 4 :]
        if len(rest) < 33:
            continue
        a = int.from_bytes(rest[9:13], "big")
        if a not in (0x1A, 0x1C, 0x15, 0xA9, 0x23):
            continue
        tt = int.from_bytes(rest[17:21], "big")
        ti = int.from_bytes(rest[21:25], "big")
        p1 = int.from_bytes(rest[25:29], "big")
        p2 = int.from_bytes(rest[29:33], "big")
        print("#%d %s CA act=0x%X tgt=%X:%X p1=%d p2=%d" % (idx, d, a, tt, ti, p1, p2))
