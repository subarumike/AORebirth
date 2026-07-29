from pathlib import Path
import csv
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012")
rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
print("rows", len(rows))
from collections import Counter
print(Counter((r.get("N3TypeName") or "?") for r in rows).most_common(25))
print("dir", Counter(r.get("Direction") for r in rows))

print("\n=== CharacterAction / Team* / Feedback / Stat(teamish) ===")
for idx, r in enumerate(rows):
    name = r.get("N3TypeName") or ""
    if name not in ("CharacterAction", "TeamMember", "TeamMemberInfo", "Feedback"):
        if name != "Stat":
            continue
        b = bytes.fromhex((r.get("RawHex") or "").strip())
        i = b.find(bytes.fromhex("2B333D6E"))
        if i < 0:
            continue
        body = b[i + 4 :]
        if len(body) < 21:
            continue
        sid = int.from_bytes(body[13:17], "big")
        if sid not in (6, 213, 521, 587):
            continue
        val = int.from_bytes(body[17:21], "big", signed=True)
        print(f"{idx:3d} {r.get('Direction'):3s} Stat id={sid} val={val} unk={body[8]}")
        continue

    hx = (r.get("RawHex") or "").strip()
    b = bytes.fromhex(hx)
    if name == "CharacterAction":
        i = b.find(bytes.fromhex("5E477770"))
        body = b[i + 4 :] if i >= 0 else b
        act = int.from_bytes(body[9:13], "big")
        p1 = int.from_bytes(body[25:29], "big", signed=True)
        p2 = int.from_bytes(body[29:33], "big", signed=True)
        tgt = f"{int.from_bytes(body[17:21],'big'):X}:{int.from_bytes(body[21:25],'big'):X}"
        ident = f"{int.from_bytes(body[0:4],'big'):X}:{int.from_bytes(body[4:8],'big'):X}"
        print(f"{idx:3d} {r.get('Direction'):3s} CA act=0x{act:X}({act}) id={ident} tgt={tgt} p1={p1} p2={p2}")
    elif name == "Feedback":
        i = b.find(bytes.fromhex("50544D19"))
        body = b[i + 4 :] if i >= 0 else b
        u1 = int.from_bytes(body[9:13], "big", signed=True)
        cat = int.from_bytes(body[13:17], "big", signed=True)
        msg = int.from_bytes(body[17:21], "big", signed=True)
        print(f"{idx:3d} {r.get('Direction'):3s} Feedback u1={u1} cat={cat} msg={msg} (0x{msg:X})")
    else:
        print(f"{idx:3d} {r.get('Direction'):3s} {name} rawlen={len(b)}")

print("\n=== events team/chat highlights ===")
ev = (cap / "events.log").read_text(encoding="utf-8", errors="replace")
for line in ev.splitlines():
    if any(
        k in line
        for k in (
            "Team",
            "Invite",
            "Accept",
            "ChannelList",
            "SocialStatus",
            "TeamSide",
            "Feedback",
            "CHAT]",
            "level",
            "Level",
            "too",
        )
    ):
        if "CharDCMove" in line or "FollowTarget" in line:
            continue
        print(line[:260])
