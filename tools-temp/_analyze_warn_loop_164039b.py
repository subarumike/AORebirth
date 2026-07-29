# Detail timeline around the two TeamRequestInvite outs
from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260729-164039")
rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))

# print column names
print("cols:", list(rows[0].keys()) if rows else None)

def brief(r, idx):
    name = r.get("N3TypeName") or ""
    direction = r.get("Direction") or ""
    ts = r.get("TimestampUtc") or r.get("Utc") or ""
    detail = ""
    hexstr = (r.get("RawHex") or "").strip()
    if name == "CharacterAction" and hexstr:
        b = bytes.fromhex(hexstr)
        i = b.find(bytes.fromhex("5E477770"))
        if i >= 0:
            rest = b[i+4:]
            act = int.from_bytes(rest[9:13], "big")
            p1 = int.from_bytes(rest[13:17], "big")
            tt = int.from_bytes(rest[17:21], "big")
            ti = int.from_bytes(rest[21:25], "big")
            p2 = int.from_bytes(rest[25:29], "big") if len(rest) >= 29 else 0
            detail = f" act={act:#x}({act}) p1={p1} tgt={tt:X}:{ti:X}({ti}) p2={p2}"
    elif name == "Stat" and hexstr:
        # show first few bytes after marker if any
        detail = f" len={len(hexstr)//2}"
    elif name == "InfoPacket":
        detail = f" len={len(hexstr)//2}"
    elif name == "LookAt" and hexstr:
        b = bytes.fromhex(hexstr)
        detail = f" len={len(b)} head={b[:24].hex()}"
    print(f"#{idx:03d} {direction:3} {name}{detail} ts={ts}")

print("\n=== window #0..#35 ===")
for i in range(0, min(36, len(rows))):
    brief(rows[i], i)

print("\n=== window #80..#95 ===")
for i in range(80, min(96, len(rows))):
    brief(rows[i], i)

# events detail for OUT
print("\n=== events OUT-N3-DETAIL all ===")
for line in (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "OUT-N3" in line or "TeamRequest" in line or "0xA9" in line or "TeamInvite" in line:
        print(line[:300])

# system messages
print("\n=== system-messages (team/invite/xp/high) ===")
sm = cap / "system-messages.log"
if sm.exists():
    for line in sm.read_text(encoding="utf-8", errors="replace").splitlines():
        low = line.lower()
        if any(x in low for x in ("team", "invite", "high", "level", "xp", "experience")):
            print(line[:250])
