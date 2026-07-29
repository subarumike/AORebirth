import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-104625")
print("exists", cap.exists())
for f in sorted(cap.iterdir()):
    print(f.name, f.stat().st_size)

# chat + events + raw for LFT
for name in ("chat-dialogue.log", "events.log", "system-messages.log", "capture_info.json"):
    p = cap / name
    if not p.exists():
        continue
    print("\n====", name, "====")
    text = p.read_text(encoding="utf-8", errors="replace")
    print(text[:4000])

rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
print("\nraw rows", len(rows))
# find 05DC/05DD/05DE and team actions
hits = []
for idx, r in enumerate(rows):
    hx = (r.get("RawHex") or "").replace(" ", "")
    if not hx:
        continue
    b = bytes.fromhex(hx)
    for tag, needle in (("05DC", b"\x05\xdc"), ("05DD", b"\x05\xdd"), ("05DE", b"\x05\xde")):
        if needle in b.lower() if False else needle in b or bytes([needle[0], needle[1]]) in b:
            hits.append((idx, r.get("Direction"), tag, len(b), b[:64].hex()))
    n3 = r.get("N3TypeName") or ""
    if n3 in ("CharacterAction", "TeamInvite", "TeamMember", "TeamMemberInfo", "InfoPacket"):
        hits.append((idx, r.get("Direction"), n3, r.get("IdentityInstance"), ""))

print("hits", len(hits))
for h in hits[:80]:
    print(h)
