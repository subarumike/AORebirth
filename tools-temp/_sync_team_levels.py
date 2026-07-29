from pathlib import Path
import re

t = Path(r"C:/Users/nermi/Desktop/mission level.txt").read_text(encoding="utf-8", errors="replace")
rows = []
for m in re.finditer(r"L\s+(\d+):\s+Team\s+(\d+)-(\d+)", t):
    rows.append((int(m.group(1)), int(m.group(2)), int(m.group(3))))
d = {}
for a, b, c in rows:
    d[a] = (b, c)
print("count", len(d), "min", min(d), "max", max(d))
print("15", d.get(15))
print("18", d.get(18))
print("60", d.get(60))
print("220", d.get(220))

src = Path(r"AORebirth/Server/ChatEngine/Lists/TeamLevelRanges.cs").read_text(encoding="utf-8")
miss = []
for lvl, (mn, mx) in sorted(d.items()):
    needle = f"{lvl},{mn},{mx}"
    if needle not in src:
        miss.append(needle)
print("missing_or_diff", len(miss))
print("first_miss", miss[:15])

# emit csv
lines = [f"{lvl},{d[lvl][0]},{d[lvl][1]}" for lvl in sorted(d)]
Path(r"tools-temp/_team_level_ranges.csv").write_text("\n".join(lines) + "\n", encoding="utf-8")
print("wrote", len(lines), "csv lines")
