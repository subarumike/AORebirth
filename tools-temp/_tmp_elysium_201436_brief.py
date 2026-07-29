# Brief Elysium capture 20260727-201436
import csv
import os
from collections import Counter, OrderedDict

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-201436"
print("files", sorted(os.listdir(cap)))

by_id = OrderedDict()
with open(os.path.join(cap, "scfu-appearance.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        if row.get("CharacterInfoType") != "NPCInfo":
            continue
        owner = (row.get("Owner") or "").strip()
        if owner and owner not in ("0", "(None)", "None"):
            continue
        pf = str(row.get("PlayfieldId") or "")
        if pf not in ("4540", "4543"):
            continue
        name = (row.get("Name") or "").strip()
        if not name:
            continue
        by_id[row["Identity"]] = row

print("unique", len(by_id))
print("pf", Counter(r.get("PlayfieldId") for r in by_id.values()))
print("side", Counter(r.get("Side") for r in by_id.values()))
print("names:")
for n, c in Counter(r.get("Name") for r in by_id.values()).most_common(50):
    print(" ", c, n)

# Slayerdroid / pet-like
print("\nslayer/pet-ish:")
for r in by_id.values():
    name = r.get("Name") or ""
    if "Slayer" in name or "Slayd" in name or "IsPet" in (r.get("Flags") or ""):
        if "Heckler" in name or "Mortiig" in name or "Kolaana" in name:
            continue
        print(name, "pf", r.get("PlayfieldId"), "side", r.get("Side"), "owner", r.get("Owner"), "flags", (r.get("Flags") or "")[:100])
