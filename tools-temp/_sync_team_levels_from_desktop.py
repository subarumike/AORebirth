# Parse Desktop team-levels.txt → CSV + diff vs embedded TeamLevelRanges
from pathlib import Path
import re
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

src = Path(r"c:\Users\nermi\Desktop\team-levels.txt")
text = src.read_text(encoding="utf-8", errors="replace")

# Patterns seen:
#   "lvl 1 Team 1-7"
#   "lvl 2" then later "Team 1-8"
#   "eam 9-24" typo
#   "216Team 156-220"
#   " 216Team 156-220 "

ranges = {}  # level -> (min, max)
pending_lvl = None

# First pass: combined lines
for raw in text.splitlines():
    line = raw.strip()
    if not line:
        continue
    m = re.search(r"(?:^|\s)lvl\s*(\d+)\s+T?eam\s*(\d+)\s*-\s*(\d+)", line, re.I)
    if m:
        lvl, mn, mx = int(m.group(1)), int(m.group(2)), int(m.group(3))
        ranges[lvl] = (mn, mx)
        pending_lvl = None
        continue
    m = re.search(r"^(\d+)\s*T?eam\s*(\d+)\s*-\s*(\d+)\s*$", line, re.I)
    if m:
        lvl, mn, mx = int(m.group(1)), int(m.group(2)), int(m.group(3))
        ranges[lvl] = (mn, mx)
        pending_lvl = None
        continue
    m = re.search(r"^lvl\s*(\d+)\s*$", line, re.I)
    if m:
        pending_lvl = int(m.group(1))
        continue
    m = re.search(r"^(\d+)\s*$", line)
    if m and pending_lvl is None:
        # bare number may be level header (216)
        pending_lvl = int(m.group(1))
        continue
    m = re.search(r"T?eam\s*(\d+)\s*-\s*(\d+)", line, re.I)
    if m and pending_lvl is not None:
        ranges[pending_lvl] = (int(m.group(1)), int(m.group(2)))
        pending_lvl = None
        continue

print(f"parsed levels: {len(ranges)} min={min(ranges)} max={max(ranges)}")
missing = [i for i in range(1, 221) if i not in ranges]
dup_check_18 = ranges.get(18)
print(f"missing 1..220: {missing[:20]}{'...' if len(missing)>20 else ''} count={len(missing)}")
print(f"sample 1={ranges.get(1)} 15={ranges.get(15)} 60={ranges.get(60)} 175={ranges.get(175)} 200={ranges.get(200)} 220={ranges.get(220)}")

# Load current embedded from TeamLevelRanges.cs
cs = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ChatEngine\Lists\TeamLevelRanges.cs")
cs_text = cs.read_text(encoding="utf-8")
# extract EmbeddedCsv raw block between @" and ";
m = re.search(r'EmbeddedCsv\s*=\s*@"(.*?)"\s*;', cs_text, re.S)
old = {}
if m:
    for line in m.group(1).splitlines():
        line = line.strip()
        if not line or line.startswith("//"):
            continue
        parts = line.split(",")
        if len(parts) == 3 and parts[0].isdigit():
            old[int(parts[0])] = (int(parts[1]), int(parts[2]))

diffs = []
for lvl in range(1, 221):
    a = old.get(lvl)
    b = ranges.get(lvl)
    if a != b:
        diffs.append((lvl, a, b))
print(f"old embedded count={len(old)} diffs vs new={len(diffs)}")
for d in diffs[:30]:
    print(f"  lvl {d[0]}: old={d[1]} new={d[2]}")
if len(diffs) > 30:
    print(f"  ... {len(diffs)-30} more")

out_csv = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_team_level_ranges.csv")
lines = [f"{lvl},{ranges[lvl][0]},{ranges[lvl][1]}" for lvl in sorted(ranges)]
out_csv.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"wrote {out_csv} rows={len(lines)}")
