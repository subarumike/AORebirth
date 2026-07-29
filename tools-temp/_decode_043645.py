import csv
import json
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-043645")
print("exists", cap.exists())
info = cap / "capture_info.json"
if info.exists():
    d = json.loads(info.read_text(encoding="utf-8-sig"))
    print("char", d.get("characterName"), "pf", d.get("playfieldId"))
    print("counts", json.dumps(d.get("packetCounts", {}), indent=2)[:800])
    print("start", d.get("captureStartUtc"), "end", d.get("captureEndUtc") or d.get("captureFinalizedUtc"))

rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
print("rows", len(rows))

def ca(raw):
    b = bytes.fromhex(raw.replace(" ", ""))
    i = b.find(bytes.fromhex("5E477770"))  # CharacterAction N3
    if i < 0 or len(b) < i + 37:
        return None
    rest = b[i + 4 :]
    a = int.from_bytes(rest[9:13], "big")
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big")
    p2 = int.from_bytes(rest[29:33], "big")
    if p2 >= 0x80000000:
        p2 -= 0x100000000
    return a, tt, ti, p1, p2

interesting = {
    "TeamInvite", "TeamMember", "TeamMemberInfo", "CharacterAction",
    "InfoPacket", "SimpleCharFullUpdate", "CharInPlay", "LookAt", "Feedback", "ChatText"
}

print("==== timeline ====")
for idx, r in enumerate(rows):
    n3 = r.get("N3TypeName") or ""
    d = r.get("Direction") or ""
    if n3 not in interesting:
        continue
    extra = ""
    if n3 == "CharacterAction" and r.get("RawHex"):
        dec = ca(r["RawHex"])
        if not dec:
            continue
        a, tt, ti, p1, p2 = dec
        # keep invite/info/look related
        if a not in (0x1A, 0x1C, 0x15, 0xA9, 0x23, 0x69, 0x20, 0x18, 0x14):
            continue
        extra = " act=0x%X tgt=%X:%X p1=%d p2=%d" % (a, tt, ti, p1, p2)
    elif n3 in ("InfoPacket", "SimpleCharFullUpdate", "TeamInvite", "TeamMember"):
        extra = " id=%s" % r.get("IdentityInstance")
    print("#%d utc=%s %s %s%s" % (idx, r.get("CapturedUtc"), d, n3, extra))

# events.log team-related
ev = cap / "events.log"
if ev.exists():
    print("==== events filtered ====")
    for line in ev.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        low = line.lower()
        if any(k in low for k in ("team", "info", "invite", "lookat", "feedback", "too high", "level")):
            print(line[:300])
