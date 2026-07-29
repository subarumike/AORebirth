import re
from pathlib import Path

# Find Program Crystal (exact, not Prepared) and rock->ore recipe lines
p_names = Path(r"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql")
prog = []
with p_names.open("r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if "Program Crystal" not in line:
            continue
        for m in re.finditer(r"\(\s*(\d+)\s*,\s*'([^']*Program Crystal[^']*)'", line):
            name = m.group(2)
            if name.startswith("Prepared"):
                continue
            if "Programmed" in name:
                continue
            prog.append((m.group(1), name))
            if len(prog) >= 20:
                break
        if len(prog) >= 20:
            break
print("Program Crystal items:")
for x in prog:
    print(x)

# Find tradeskill rows involving rock high ids
p_ts = Path(r"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\tradeskill.sql")
hits = []
with p_ts.open("r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if "150274" in line or "150273" in line:
            hits.append(line.strip()[:240])
            if len(hits) >= 15:
                break
print("rock recipe hits", len(hits))
for h in hits:
    print(h)
