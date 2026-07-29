import csv
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")

def dump(name, start=0, end=120):
    cap = root / name
    rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print("====", name, "rows", len(rows), "====")
    for idx, r in enumerate(rows):
        if idx < start or idx > end:
            continue
        n3 = r.get("N3TypeName") or ""
        d = r.get("Direction") or ""
        raw = (r.get("RawHex") or "").replace(" ", "")
        interesting = n3 in (
            "TeamInvite", "TeamMember", "TeamMemberInfo", "CharacterAction",
            "ChatText", "Feedback", "SimpleCharFullUpdate", "Despawn", "InfoPacket"
        )
        if not interesting:
            continue
        extra = ""
        if n3 == "CharacterAction" and raw:
            b = bytes.fromhex(raw)
            i = b.find(bytes.fromhex("5E477770"))
            if i >= 0 and len(b) >= i + 37:
                rest = b[i + 4 :]
                a = int.from_bytes(rest[9:13], "big")
                tt = int.from_bytes(rest[17:21], "big")
                ti = int.from_bytes(rest[21:25], "big")
                p1 = int.from_bytes(rest[25:29], "big")
                p2 = int.from_bytes(rest[29:33], "big")
                extra = " act=0x%X tgt=%X:%X p1=%d p2=%d" % (a, tt, ti, p1, p2)
                if a not in (0x1A, 0x1C, 0x15, 0xA9, 0x23, 0x20):
                    continue
        if n3 == "TeamInvite":
            extra = " id=%s" % r.get("IdentityInstance")
        if n3 in ("TeamMember", "TeamMemberInfo"):
            extra = " id=%s hex=%d" % (r.get("IdentityInstance"), len(raw)//2)
        if n3 == "ChatText" and raw:
            # skip spam unless short
            if len(raw) > 200:
                continue
        print("#%d %s %s%s" % (idx, d, n3, extra))

dump("20260729-010949", 50, 100)
dump("20260729-010948", 75, 160)
