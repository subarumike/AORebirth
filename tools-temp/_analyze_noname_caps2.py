import csv
from pathlib import Path

root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")

# Cross-zone inviter: does InfoRequest happen before 0x1A?
# Decode SCFU identities around invite
for name in ("20260729-010948", "20260729-011305"):
    p = root / name / "raw-packets.csv"
    rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))
    print("===", name, "===")
    for idx, r in enumerate(rows):
        n3 = r.get("N3TypeName") or ""
        d = r.get("Direction") or ""
        it = r.get("IdentityType") or ""
        ii = r.get("IdentityInstance") or ""
        if n3 in ("SimpleCharFullUpdate", "CharInPlay", "InfoPacket", "LookAt"):
            print("#%d %s %s idType=%s idInst=%s" % (idx, d, n3, it, ii))
            continue
        if n3 != "CharacterAction" or not r.get("RawHex"):
            continue
        b = bytes.fromhex(r["RawHex"].strip())
        i = b.find(bytes.fromhex("5E477770"))
        if i < 0:
            continue
        rest = b[i + 4 :]
        if len(rest) < 25:
            continue
        a = int.from_bytes(rest[9:13], "big")
        tt = int.from_bytes(rest[17:21], "big")
        ti = int.from_bytes(rest[21:25], "big")
        print("#%d %s CA act=0x%X tgt=%X:%X" % (idx, d, a, tt, ti))
