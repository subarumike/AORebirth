from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

for name in ("20260728-234012", "20260728-232300"):
    cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures") / name
    if not (cap / "raw-packets.csv").exists():
        print("missing", name)
        continue
    rows = list(csv.DictReader((cap/"raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print(f"\n======== {name} TeamMember decode ========")
    magic = bytes.fromhex("46312D2E")
    for idx, r in enumerate(rows):
        if (r.get("N3TypeName") or "") != "TeamMember":
            continue
        b = bytes.fromhex(r["RawHex"].strip())
        i = b.find(magic)
        body = b[i+4:]
        # Identity viewer already in N3 header before magic... body starts with Identity (viewer in header)
        # After type: Identity(8) Member(8) Team(8) Unknown4(4) Level(4) Unknown5(2) NameLen(4) Name
        # Actually N3Message: after type comes Identity+Unknown of N3 header then AoMembers
        # Wire: ...46312D2E + Identity(viewer 8) + unk? 
        # From hex: 46312D2E 0000C350 765A6D34 0000C350 765A6D34 0000DEA9 0281D103 FFFFFFFF 0000003C 0003 00000008 Engynera
        # So after type: viewerId, memberId, teamId, unk4, level, unk5, namelen, name
        viewer_t = int.from_bytes(body[0:4], "big")
        viewer_i = int.from_bytes(body[4:8], "big")
        mem_t = int.from_bytes(body[8:12], "big")
        mem_i = int.from_bytes(body[12:16], "big")
        team_t = int.from_bytes(body[16:20], "big")
        team_i = int.from_bytes(body[20:24], "big")
        unk4 = int.from_bytes(body[24:28], "big", signed=True)
        level = int.from_bytes(body[28:32], "big")
        unk5 = int.from_bytes(body[32:34], "big")
        nlen = int.from_bytes(body[34:38], "big")
        name_s = body[38:38+nlen].decode("ascii", errors="replace")
        print(f"{idx} {r['Direction']} mem={mem_i:X} lvl={level} u5={unk5} name={name_s} team={team_i:X}")

print("\n=== Stat 6 anywhere in 234012? ===")
rows = list(csv.DictReader(Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012/raw-packets.csv").open(encoding="utf-8-sig", newline="")))
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "Stat":
        continue
    b = bytes.fromhex(r["RawHex"].strip())
    i = b.find(bytes.fromhex("2B333D6E"))
    body = b[i+4:]
    sid = int.from_bytes(body[13:17], "big")
    if sid == 6 or sid == 587:
        val = int.from_bytes(body[17:21], "big", signed=True)
        print(idx, "Stat", sid, "=", val)
