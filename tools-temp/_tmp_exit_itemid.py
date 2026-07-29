# -*- coding: utf-8 -*-
from pathlib import Path
import re

# itemnames: Exit Arete
p = Path(r"AORebirth/Libraries/Source/AORebirth.Database/SqlTables/itemnames.sql")
text = p.read_text(encoding="utf-8", errors="ignore")
for m in re.finditer(r"\(\s*(\d+)\s*,\s*'([^']*Exit[^']*Arete[^']*)'", text):
    print("item", m.group(1), m.group(2))
idx = text.find("Exit Arete")
print("idx", idx)
if idx >= 0:
    print(text[max(0, idx - 60): idx + 100])

# staticdynels dumps
for path in Path("tools-temp").glob("*staticdynel*"):
    try:
        t = path.read_text(encoding="utf-8", errors="ignore")
    except Exception:
        continue
    if "574187C3" in t or "1464338371" in t:  # 0x574187C3 decimal
        print("found in", path)
print("decimal", int("574187C3", 16))
