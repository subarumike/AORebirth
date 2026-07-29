# Map Side strings and unique identity counts for Elysium merge planning
import csv
from collections import Counter, OrderedDict

caps = [
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-182451",
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-190145",
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-193914",
]

def load(cap):
    by = OrderedDict()
    with open(cap + r"\scfu-appearance.csv", encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            if row.get("CharacterInfoType") != "NPCInfo":
                continue
            name = (row.get("Name") or "").strip()
            if not name:
                continue
            by[row["Identity"]] = row
    return by

merged = OrderedDict()
for cap in caps:
    part = load(cap)
    print(cap.split("\\")[-1], "unique", len(part))
    for k, v in part.items():
        merged[k] = v

print("merged unique", len(merged))
sides = Counter(r.get("Side") for r in merged.values())
print("sides", sides)
names = Counter(r.get("Name") for r in merged.values())
print("names", len(names))
for n, c in names.most_common(40):
    print(c, n)

# Omni/Clan names
for n, c in sorted(names.items()):
    sides_n = Counter(
        r.get("Side") for r in merged.values() if r.get("Name") == n
    )
    if any(s in ("OmniTek", "Clan", "Omni") for s in sides_n):
        print("FACTION", n, dict(sides_n))
