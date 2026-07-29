import csv
from pathlib import Path

root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")


def analyze(name):
    p = root / name / "raw-packets.csv"
    if not p.exists():
        print(name, "MISSING")
        return
    rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))
    print("===", name, "rows", len(rows), "===")
    cols = rows[0].keys() if rows else []
    print("cols sample:", list(cols)[:12])
    for idx, r in enumerate(rows):
        n3 = (r.get("N3TypeName") or "")
        d = (r.get("Direction") or "")
        keep = n3 in (
            "SimpleCharFullUpdate",
            "CharInPlay",
            "CharacterAction",
            "TeamInvite",
            "InfoPacket",
            "Despawn",
            "LookAt",
        )
        act = ""
        tgt = ""
        if n3 == "CharacterAction" and r.get("RawHex"):
            b = bytes.fromhex(r["RawHex"].strip())
            i = b.find(bytes.fromhex("5E477770"))
            if i >= 0:
                rest = b[i + 4 :]
                if len(rest) >= 25:
                    a = int.from_bytes(rest[9:13], "big")
                    act = hex(a)
                    tt = int.from_bytes(rest[17:21], "big")
                    ti = int.from_bytes(rest[21:25], "big")
                    tgt = "%X:%X" % (tt, ti)
                    if a in (0x1A, 0x69, 0x1C, 0x15, 0xA9, 0x23):
                        keep = True
                    else:
                        keep = False
        if n3 == "SimpleCharFullUpdate" and r.get("RawHex"):
            # print identity from header if possible
            keep = True
        if not keep:
            continue
        print("#%d %s %s act=%s tgt=%s" % (idx, d, n3, act, tgt))


for n in ("20260729-010948", "20260729-010949", "20260729-011305", "20260729-011333"):
    analyze(n)
    print()
