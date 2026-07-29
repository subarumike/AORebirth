"""Find ME rock-chain style recipes: Skill contains 125, SkillPercent 300."""
from pathlib import Path
import re

p = Path(r"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\tradeskill.sql")
me300 = []
me375 = []
fqp425 = []
with p.open("r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if not line.startswith("INSERT INTO `tradeskill`"):
            continue
        # VALUES (Id1,Id2,MinTarget,'Result',QlRange,Delete,"Skill","SkillPercent",...)
        m = re.match(
            r"INSERT INTO `tradeskill` VALUES \((\d+),(\d+),(\d+),'([^']*)',(\d+),(\d+),\"([^\"]*)\",\"([^\"]*)\",\"([^\"]*)\",(\d+),(\d+),(\d+),(\d+)\);",
            line.strip(),
        )
        if not m:
            # try single quotes for skills
            m = re.match(
                r"INSERT INTO `tradeskill` VALUES \((\d+),(\d+),(\d+),'([^']*)',(\d+),(\d+),'([^']*)','([^']*)','([^']*)',(\d+),(\d+),(\d+),(\d+)\);",
                line.strip(),
            )
        if not m:
            continue
        skill = m.group(7)
        pct = m.group(8)
        if "125" in skill.split(",") and pct.startswith("300"):
            me300.append(m.groups())
        if "125" in skill and "126" in skill and "375" in pct:
            me375.append(m.groups())
        if "125" in skill and "157" in skill and "425" in pct:
            fqp425.append(m.groups())

print("ME 300-ish count", len(me300))
for g in me300[:10]:
    print(g)
print("ME+EE 375 count", len(me375))
for g in me375[:10]:
    print(g)
print("ME+FQP 425 count", len(fqp425))
for g in fqp425[:10]:
    print(g)
