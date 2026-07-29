"""Count nanocrystal-ish recipes in tradeskill.sql by skill percent patterns from AO-Universe."""
from pathlib import Path
import re

p = Path(r"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\tradeskill.sql")
# SkillPercent examples from guide:
# 4x = 400, 4.25x = 425, 3x = 300, 3.75x = 375, 4.5x = 450, 4.7x = 470
patterns = {
    "400,400": 0,  # NP+CL 4x
    "425": 0,
    "300": 0,
    "375,375": 0,
    "425,425": 0,
    "450,450": 0,
    "470,450": 0,
    "470": 0,
}
rows = 0
sample = []
with p.open("r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if not line.startswith("INSERT INTO `tradeskill`"):
            continue
        rows += 1
        for k in patterns:
            if k in line:
                patterns[k] += 1
        if '"160,161","400,400"' in line or "'160,161','400,400'" in line:
            if len(sample) < 5:
                sample.append(line.strip()[:180])

print("total insert rows", rows)
for k, v in patterns.items():
    print("pattern", k, "->", v)
print("sample NP+CL 4x:")
for s in sample:
    print(s)
