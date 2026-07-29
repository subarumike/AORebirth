from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Check 234012 join burst for team/level related stats and TeamMember levels
cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012")
rows = list(csv.DictReader((cap/"raw-packets.csv").open(encoding="utf-8-sig", newline="")))
print("=== Stat ids around first join (120-130) ===")
for idx in range(115, 135):
    r = rows[idx]
    if (r.get("N3TypeName") or "") != "Stat":
        if (r.get("N3TypeName") or "") in ("TeamMember","CharacterAction","TeamMemberInfo"):
            print(idx, r.get("Direction"), r.get("N3TypeName"))
        continue
    b = bytes.fromhex(r["RawHex"].strip())
    i = b.find(bytes.fromhex("2B333D6E"))
    body = b[i+4:]
    sid = int.from_bytes(body[13:17], "big")
    val = int.from_bytes(body[17:21], "big", signed=True)
    print(f"{idx} Stat {sid}={val}")
