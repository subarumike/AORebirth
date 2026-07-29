import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-062507")
rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))

print("rows", len(rows))
print("==== ALL OUT ====")
for i, r in enumerate(rows):
    if r.get("Direction") != "OUT":
        continue
    n3 = r.get("N3TypeName") or ""
    print(i, r.get("CapturedUtc"), n3, "id", r.get("IdentityInstance"))
    if n3 == "CharacterAction" and r.get("RawHex"):
        b = bytes.fromhex(r["RawHex"].replace(" ", ""))
        idx = b.find(bytes.fromhex("5E477770"))
        if idx >= 0:
            rest = b[idx + 4 :]
            if len(rest) >= 33:
                a = int.from_bytes(rest[9:13], "big")
                tt = int.from_bytes(rest[17:21], "big")
                ti = int.from_bytes(rest[21:25], "big")
                p1 = int.from_bytes(rest[25:29], "big")
                p2 = int.from_bytes(rest[29:33], "big")
                print("   CA act=0x%X tgt=%X:%X p1=%d p2=%d" % (a, tt, ti, p1, p2))

print("==== ALL team/info related ====")
for i, r in enumerate(rows):
    n3 = r.get("N3TypeName") or ""
    d = r.get("Direction") or ""
    if n3 in ("TeamInvite", "InfoPacket", "SimpleCharFullUpdate", "Stat", "Feedback", "ChatText"):
        h = (r.get("RawHex") or "").replace(" ", "")
        extra = ""
        if n3 == "InfoPacket" and h:
            b = bytes.fromhex(h)
            # after N3 type InfoPacket identity: find C350 then level bytes
            # Pattern from prior: ...C350xxxxxxxx 01 ?? 01 LEVEL TITLE...
            j = b.find(bytes.fromhex("0000C350"))
            if j >= 0 and j + 20 < len(b):
                # identity 8 bytes from j, then unk1, type?, then character info
                chunk = b[j:j+40]
                extra = " idchunk=" + chunk.hex()
        if n3 == "Stat":
            extra = " (see system log)"
        print(i, d, n3, "id", r.get("IdentityInstance"), "len", len(h)//2, extra)
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
    if a not in (0x1A, 0x1C, 0x15, 0xA9, 0x23, 0x69, 0x18, 0x20):
        continue
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big")
    p2 = int.from_bytes(rest[29:33], "big")
    print("%d %s CA act=0x%X tgt=%X:%X p1=%d p2=%d" % (i, d, a, tt, ti, p1, p2))

print("==== system/chat ====")
for name in ("system-messages.log", "chat-dialogue.log", "events.log"):
    p = cap / name
    if not p.exists():
        continue
    print("---", name, "---")
    for line in p.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        low = line.lower()
        if any(k in low for k in ("high", "team", "invite", "level", "info", "stat", "lookat", "0x69", "request")):
            print(line[:350])
