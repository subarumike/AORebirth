import csv
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Same-day invite/accept golds that completed the LFT fix after list capture
root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")
for name in (
    "20260728-234012",
    "20260729-011305",
    "20260729-011333",
    "20260729-010948",
    "20260729-010949",
):
    cap = root / name
    if not cap.exists():
        print(name, "MISSING")
        continue
    rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print("\n====", name, "rows", len(rows), "====")
    for idx, r in enumerate(rows):
        n3 = r.get("N3TypeName") or ""
        d = r.get("Direction") or ""
        if n3 not in ("CharacterAction", "TeamInvite", "TeamMember", "TeamMemberInfo"):
            continue
        act = ""
        tgt = ""
        p1 = p2 = ""
        if n3 == "CharacterAction" and r.get("RawHex"):
            b = bytes.fromhex(r["RawHex"].replace(" ", ""))
            i = b.find(bytes.fromhex("5E477770"))
            if i >= 0:
                rest = b[i + 4 :]
                if len(rest) >= 25:
                    a = int.from_bytes(rest[9:13], "big")
                    act = hex(a)
                    tt = int.from_bytes(rest[17:21], "big")
                    ti = int.from_bytes(rest[21:25], "big")
                    tgt = "%X:%X" % (tt, ti)
                    # params after target in CA layout — check length
                    if len(rest) >= 33:
                        p1 = str(int.from_bytes(rest[25:29], "big"))
                        p2 = str(int.from_bytes(rest[29:33], "big"))
                    if a not in (0x1A, 0x1C, 0x15, 0xA9, 0x23, 0x20, 0x18, 0x69):
                        continue
        print("#%d %s %s act=%s tgt=%s p1=%s p2=%s id=%s" % (
            idx, d, n3, act, tgt, p1, p2, r.get("IdentityInstance")))
