import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-061328")
rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))

print("==== session ====")
info = cap / "capture_info.json"
if info.exists():
    print(info.read_text(encoding="utf-8-sig")[:800])

print("==== OUT packets summary ====")
for i, r in enumerate(rows):
    if r.get("Direction") != "OUT":
        continue
    print(i, r.get("CapturedUtc"), r.get("N3TypeName"), "id", r.get("IdentityInstance"))

print("==== team/invite related IN+OUT ====")
for i, r in enumerate(rows):
    n3 = r.get("N3TypeName") or ""
    d = r.get("Direction") or ""
    if n3 in ("TeamInvite", "InfoPacket", "SimpleCharFullUpdate", "TeamMember", "TeamMemberInfo"):
        print(i, d, n3, "id", r.get("IdentityInstance"), "hexlen", len((r.get("RawHex") or "").replace(" ", "")) // 2)
        continue
    if n3 != "CharacterAction" or not r.get("RawHex"):
        continue
    b = bytes.fromhex(r["RawHex"].replace(" ", ""))
    idx = b.find(bytes.fromhex("5E477770"))
    if idx < 0:
        continue
    rest = b[idx + 4 :]
    if len(rest) < 33:
        continue
    a = int.from_bytes(rest[9:13], "big")
    if a not in (0x1A, 0x1C, 0x15, 0xA9, 0x23, 0x69):
        continue
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big")
    p2 = int.from_bytes(rest[29:33], "big")
    print("%d %s CA act=0x%X tgt=%X:%X p1=%d p2=%d" % (i, d, a, tt, ti, p1, p2))

print("==== system/chat hits ====")
for name in ("system-messages.log", "chat-dialogue.log", "events.log"):
    p = cap / name
    if not p.exists():
        continue
    print("---", name, "---")
    for line in p.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        low = line.lower()
        if any(k in low for k in ("high", "team", "invite", "xp", "warn", "level")):
            print(line[:300])
