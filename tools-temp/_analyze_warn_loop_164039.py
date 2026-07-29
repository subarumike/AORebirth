# Analyze 20260729-164039: warn Yes loop on live (175->60?)
from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260729-164039")
rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))

print(f"total rows={len(rows)}")
outs = [r for r in rows if (r.get("Direction") or "") == "OUT"]
print(f"OUT count={len(outs)}")
print("\n=== ALL OUT ===")
for r in outs:
    print(f"#{r.get('Index', r.get('PacketIndex', '?'))} t={r.get('TimestampUtc') or r.get('Time') or ''} type={r.get('N3TypeName')} hexlen={len((r.get('RawHex') or '').strip())//2}")

# CharacterAction decode helper
def decode_ca(hexstr):
    b = bytes.fromhex(hexstr.strip())
    # find N3 header-ish: look for common CA marker patterns
    # AOSharp live capture often has full packet; find Action after identity
    # Try find 5E477770 (CharacterAction msg type marker used before) or scan
    i = b.find(bytes.fromhex("5E477770"))
    if i < 0:
        # try without - look for N3 type CharacterAction = 0x35? 
        return {"raw_len": len(b), "note": "no 5E477770", "head": b[:40].hex()}
    rest = b[i+4:]
    if len(rest) < 25:
        return {"note": "short", "rest": rest.hex()}
    # identity type/instance often first
    id_type = int.from_bytes(rest[0:4], "big") if len(rest) >= 4 else 0
    # flexible: previous scripts used rest[9:13] for action
    act = int.from_bytes(rest[9:13], "big")
    p1 = int.from_bytes(rest[13:17], "big") if len(rest) >= 17 else 0
    tt = int.from_bytes(rest[17:21], "big") if len(rest) >= 21 else 0
    ti = int.from_bytes(rest[21:25], "big") if len(rest) >= 25 else 0
    p2 = int.from_bytes(rest[25:29], "big") if len(rest) >= 29 else None
    return {
        "act": act, "act_hex": hex(act),
        "p1": p1, "tgtType": tt, "tgtInst": ti, "p2": p2,
        "id_type_guess": id_type,
    }

print("\n=== CharacterAction all dirs ===")
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "CharacterAction":
        continue
    d = decode_ca(r["RawHex"])
    print(f"#{idx} {(r.get('Direction') or ''):3} {d}")

print("\n=== Team* / Feedback / Stat / Info interesting ===")
keys = ("Team", "Feedback", "Stat", "Info", "Social", "Invite", "LookAt")
for idx, r in enumerate(rows):
    name = r.get("N3TypeName") or ""
    if any(k.lower() in name.lower() for k in keys) or (r.get("Direction") == "OUT"):
        print(f"#{idx} {(r.get('Direction') or ''):3} {name}")

print("\n=== events.log team/invite lines ===")
ev = cap / "events.log"
if ev.exists():
    for line in ev.read_text(encoding="utf-8", errors="replace").splitlines():
        low = line.lower()
        if any(x in low for x in ("team", "invite", "0x1a", "action=26", "action=0x1a", "too high", "feedback", "out ")):
            print(line[:220])
