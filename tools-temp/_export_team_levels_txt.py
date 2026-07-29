from pathlib import Path

src = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_team_level_ranges.csv")
out = Path(r"C:\Users\nermi\Desktop\team-levels-export.txt")

rows = [l.strip() for l in src.read_text(encoding="utf-8").splitlines() if l.strip()]
lines = [
    "# level,teamMin,teamMax",
    "# AORebirth export — same data as TeamLevelRanges / TeamXpShareWindow",
    "# source: Desktop team-levels.txt (0 diffs)",
]
lines.extend(rows)
out.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(out)
print("rows", len(rows))
