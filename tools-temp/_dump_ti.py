import csv
from pathlib import Path

for name in ("20260729-010949", "20260729-010948"):
    cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures") / name
    rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print("====", name, "====")
    for i, r in enumerate(rows):
        if (r.get("N3TypeName") or "") != "TeamInvite":
            continue
        h = (r.get("RawHex") or "").replace(" ", "")
        print(i, r.get("Direction"), "id", r.get("IdentityInstance"), "len", len(h) // 2)
        print(h)
        b = bytes.fromhex(h)
        # find N3 header after possible wrappers
        magic = bytes.fromhex("4D2A313B")  # TeamInvite type LE? actually BE in AO
        # N3MessageType TeamInvite = 0x4d2a313b big-endian in packet
        idx = b.find(bytes.fromhex("4D2A313B"))
        print("type_at", idx)
        if idx >= 0:
            rest = b[idx:]
            print("from_type", rest[:64].hex())
