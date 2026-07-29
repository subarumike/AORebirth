import csv
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-030954")
print("exists", cap.exists())
info = cap / "capture_info.json"
if info.exists():
    print(info.read_text(encoding="utf-8-sig", errors="replace")[:2000])

rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
print("rows", len(rows))

def ca_act(raw):
    b = bytes.fromhex(raw.replace(" ", ""))
    i = b.find(bytes.fromhex("5E477770"))
    if i < 0 or len(b) < i + 37:
        return None
    rest = b[i + 4 :]
    a = int.from_bytes(rest[9:13], "big")
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big")
    p2 = int.from_bytes(rest[29:33], "big")
    return a, tt, ti, p1, p2

interesting = {
    "TeamInvite", "TeamMember", "TeamMemberInfo", "CharacterAction",
    "InfoPacket", "SimpleCharFullUpdate", "CharInPlay", "LookAt", "Feedback", "ChatText"
}

for idx, r in enumerate(rows):
    n3 = r.get("N3TypeName") or ""
    d = r.get("Direction") or ""
    if n3 not in interesting:
        continue
    extra = ""
    if n3 == "CharacterAction" and r.get("RawHex"):
        decoded = ca_act(r["RawHex"])
        if not decoded:
            continue
        a, tt, ti, p1, p2 = decoded
        if a not in (0x1A, 0x1C, 0x15, 0xA9, 0x23, 0x69, 0x20, 0x18):
            continue
        extra = " act=0x%X tgt=%X:%X p1=%d p2=%d" % (a, tt, ti, p1, p2)
    elif n3 in ("TeamInvite", "TeamMember", "InfoPacket", "SimpleCharFullUpdate"):
        extra = " id=%s" % r.get("IdentityInstance")
    print("#%d utc=%s %s %s%s" % (idx, r.get("CapturedUtc"), d, n3, extra))
