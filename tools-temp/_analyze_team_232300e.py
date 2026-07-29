from pathlib import Path
import csv
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-232300/raw-packets.csv")
rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))

print("=== packets 65-100 ===")
for idx in range(65, 100):
    r = rows[idx]
    name = r.get("N3TypeName") or ""
    d = r.get("Direction")
    hx = (r.get("RawHex") or "").strip()
    extra = ""
    if name == "CharacterAction":
        b = bytes.fromhex(hx)
        i = b.find(bytes.fromhex("5E477770"))
        body = b[i + 4 :]
        act = int.from_bytes(body[9:13], "big")
        p1 = int.from_bytes(body[25:29], "big", signed=True)
        p2 = int.from_bytes(body[29:33], "big", signed=True)
        extra = f" act=0x{act:X} p1={p1} p2={p2}"
    elif name == "Stat":
        b = bytes.fromhex(hx)
        i = b.find(bytes.fromhex("2B333D6E"))
        body = b[i + 4 :]
        sid = int.from_bytes(body[13:17], "big")
        val = int.from_bytes(body[17:21], "big", signed=True)
        extra = f" stat={sid} val={val} unk={body[8]}"
    elif name == "OrgServer":
        extra = f" hex={hx[32:96]}"
    print(f"{idx:3d} {d:3s} {name}{extra}")

# events around team for chat text
ev = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-232300/events.log").read_text(encoding="utf-8", errors="replace")
for line in ev.splitlines():
    if any(x in line for x in ("CHAT]", "TeamMember", "AcceptTeam", "TeamRequest", "TeamSide", "Social", "ChannelList", "joined", "invite")):
        print(line[:220])
