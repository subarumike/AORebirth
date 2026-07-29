from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

CAPS = [
    Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-003944"),
    Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-003950"),
]

def parse_ca(hx):
    b = bytes.fromhex(hx.strip())
    i = b.find(bytes.fromhex("5E477770"))
    if i < 0:
        return None
    rest = b[i+4:]
    if len(rest) < 33:
        return None
    unk = rest[8]
    act = int.from_bytes(rest[9:13], "big")
    unk1 = int.from_bytes(rest[13:17], "big")
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big", signed=True)
    p2 = int.from_bytes(rest[29:33], "big", signed=True)
    return unk, act, unk1, tt, ti, p1, p2

TEAM_ACTS = {0x15, 0x18, 0x1A, 0x1B, 0x1C, 0x19, 0x20, 0x23, 0xA9, 0x1D, 0x1E}

for cap in CAPS:
    print("\n========", cap.name, "========")
    print("files:", [p.name for p in cap.iterdir()][:15])
    csvp = cap / "raw-packets.csv"
    if not csvp.exists():
        print("NO raw-packets.csv")
        continue
    rows = list(csv.DictReader(csvp.open(encoding="utf-8-sig", newline="")))
    print("rows", len(rows))

    # TeamInvite
    for idx, r in enumerate(rows):
        hx = (r.get("RawHex") or "").strip()
        if not hx:
            continue
        b = bytes.fromhex(hx)
        if b.find(bytes.fromhex("4D2A313B")) >= 0:
            print(f"{idx} {r.get('Direction')} TeamInvite N3={r.get('N3TypeName')} {hx}")

    print("--- CharacterActions team-related ---")
    for idx, r in enumerate(rows):
        if (r.get("N3TypeName") or "") != "CharacterAction":
            continue
        p = parse_ca(r["RawHex"])
        if not p:
            continue
        unk, act, unk1, tt, ti, p1, p2 = p
        if act not in TEAM_ACTS:
            continue
        print(f"{idx} {r.get('Direction')} act=0x{act:X} tgt={tt:X}:{ti:X}({ti}) p1={p1} p2={p2}")

    print("--- TeamMember / TeamMemberInfo / Stat teamish ---")
    for idx, r in enumerate(rows):
        n3 = r.get("N3TypeName") or ""
        d = r.get("Direction")
        if n3 in ("TeamMember", "TeamMemberInfo"):
            print(f"{idx} {d} {n3}")
        elif n3 == "Stat":
            b = bytes.fromhex(r["RawHex"].strip())
            i = b.find(bytes.fromhex("2B333D6E"))
            if i < 0:
                continue
            body = b[i+4:]
            sid = int.from_bytes(body[13:17], "big")
            val = int.from_bytes(body[17:21], "big", signed=True)
            if sid in (6, 213, 521, 587, 54):
                print(f"{idx} {d} Stat {sid}={val}")

    print("--- Feedback ---")
    for idx, r in enumerate(rows):
        if (r.get("N3TypeName") or "") == "Feedback":
            print(f"{idx} {r.get('Direction')} Feedback {r.get('RawHex','')[:80]}")
