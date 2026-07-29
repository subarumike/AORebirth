import re
from pathlib import Path

p = Path(r"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql")
# stream scan for carbon / jensen / isotope / neutron / photon particle / symbol library / reflection
needles = [
    "carbonrich", "carbon rich", "jensen personal", "isotope separator",
    "neutron displacer", "photon particle", "symbol library",
    "crystal reflection", "program crystal", "prepared program",
    "pure carbon", "instruction disc",
]
found = {n: [] for n in needles}
# itemnames is huge; scan line by line for quoted names
with p.open("r", encoding="utf-8", errors="replace") as f:
    for line in f:
        low = line.lower()
        if not any(n in low for n in needles):
            continue
        # extract ( id , 'name' pairs roughly
        for m in re.finditer(r"\(\s*(\d+)\s*,\s*'([^']*)'", line):
            iid, name = m.group(1), m.group(2)
            nl = name.lower()
            for n in needles:
                if n in nl and len(found[n]) < 8:
                    found[n].append((iid, name))

for n, items in found.items():
    print("===", n, "count_shown", len(items), "===")
    for iid, name in items:
        print(iid, name)
