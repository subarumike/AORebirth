import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-063307")
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
        if idx >= 0 and len(b) > idx + 37:
            rest = b[idx + 4 :]
            a = int.from_bytes(rest[9:13], "big")
            tt = int.from_bytes(rest[17:21], "big")
            ti = int.from_bytes(rest[21:25], "big")
            p1 = int.from_bytes(rest[25:29], "big")
            p2 = int.from_bytes(rest[29:33], "big")
            print("   CA act=0x%X tgt=%X:%X p1=%d p2=%d" % (a, tt, ti, p1, p2))

print("==== ALL relevant IN/OUT ====")
for i, r in enumerate(rows):
    n3 = r.get("N3TypeName") or ""
    d = r.get("Direction") or ""
    if n3 in ("TeamInvite", "InfoPacket", "SimpleCharFullUpdate", "Stat", "Feedback", "ChatText", "LookAt"):
        print(i, d, n3, "id", r.get("IdentityInstance"), "len", len((r.get("RawHex") or "").replace(" ", "")) // 2)
        continue
    if n3 != "CharacterAction" or not r.get("RawHex"):
        continue
    b = bytes.fromhex(r["RawHex"].replace(" ", ""))
    idx = b.find(bytes.fromhex("5E477770"))
    if idx < 0 or len(b) < idx + 37:
        continue
    rest = b[idx + 4 :]
    a = int.from_bytes(rest[9:13], "big")
    tt = int.from_bytes(rest[17:21], "big")
    ti = int.from_bytes(rest[21:25], "big")
    p1 = int.from_bytes(rest[25:29], "big")
    p2 = int.from_bytes(rest[29:33], "big")
    print("%d %s CA act=0x%X tgt=%X:%X p1=%d p2=%d" % (i, d, a, tt, ti, p1, p2))

print("==== events/system/chat ====")
for name in ("events.log", "system-messages.log", "chat-dialogue.log", "capture_info.json", "capture-session.json"):
    p = cap / name
    if not p.exists():
        continue
    print("---", name, "---")
    text = p.read_text(encoding="utf-8-sig", errors="replace")
    if name.endswith(".json"):
        print(text[:600])
        continue
    for line in text.splitlines():
        low = line.lower()
        if any(k in low for k in ("high", "team", "invite", "level", "stat", "info", "lookat", "snapshot", "char-seen", "command", "decline", "no")):
            print(line[:400])
