# -*- coding: utf-8 -*-
import pathlib, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ids = {29625,43381,30110,99212,46430,46438,223373,248257}
sql = pathlib.Path(r"AORebirth/Libraries/Source/AORebirth.Database/SqlTables/itemnames.sql")
pat = re.compile(r"\(\s*(\d+)\s*,\s*'((?:''|[^'])*)'")
names = {}
with sql.open(encoding="utf-8", errors="replace") as fh:
    for line in fh:
        for m in pat.finditer(line):
            i = int(m.group(1))
            if i in ids and i not in names:
                names[i] = m.group(2).replace("''", "'")
for i in sorted(ids):
    print(i, names.get(i, "?"))
