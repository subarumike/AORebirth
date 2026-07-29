from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Find TeamMemberInfo magic and any packets between join stats
# TeamMemberInfo N3 type?
# Search repo for TeamMemberInfo = 
for name in ("20260729-003944", "20260729-003950", "20260728-234012"):
    cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")/name
    rows = list(csv.DictReader((cap/"raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print(f"\n=== {name} n3 type counts around team ===")
    from collections import Counter
    c = Counter((r.get("Direction"), r.get("N3TypeName")) for r in rows)
    for k,v in sorted(c.items()):
        if k[1] and ("Team" in str(k[1]) or k[1] in ("CharacterAction","Stat","Feedback")):
            print(f"  {k}: {v}")

# dump 003944 rows 40-55 full n3
print("\n=== 003944 idx 40-55 ===")
rows = list(csv.DictReader(Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-003944/raw-packets.csv").open(encoding="utf-8-sig", newline="")))
for idx in range(40, 55):
    r = rows[idx]
    print(idx, r.get("Direction"), r.get("N3TypeName"), (r.get("RawHex") or "")[:60])

print("\n=== 003950 idx 160-180 ===")
rows = list(csv.DictReader(Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-003950/raw-packets.csv").open(encoding="utf-8-sig", newline="")))
for idx in range(160, 180):
    r = rows[idx]
    print(idx, r.get("Direction"), r.get("N3TypeName"), (r.get("RawHex") or "")[:60])

# gold TeamMemberInfo
print("\n=== gold 234012 looking for TeamMemberInfo ===")
rows = list(csv.DictReader(Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012/raw-packets.csv").open(encoding="utf-8-sig", newline="")))
for idx, r in enumerate(rows):
    if "TeamMember" in (r.get("N3TypeName") or ""):
        print(idx, r.get("Direction"), r.get("N3TypeName"))
