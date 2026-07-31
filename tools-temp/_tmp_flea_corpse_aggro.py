# -*- coding: utf-8 -*-
import csv, pathlib, math, re, collections
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-220951")

print("=== corpse-full-updates flea ===")
f = p / "corpse-full-updates.csv"
if f.exists():
    with f.open(encoding="utf-8-sig", newline="") as fh:
        rows = list(csv.DictReader(fh))
    print("cols", rows[0].keys() if rows else None)
    for row in rows:
        blob = " ".join((row.get(c) or "") for c in row)
        if "Flea" in blob or "17657" in blob or "flea" in blob.lower():
            interesting = {k: (v[:100] if isinstance(v,str) and len(v)>100 else v)
                           for k,v in row.items() if v}
            print(interesting)
            print("---")
else:
    print("missing")

print("\n=== enemy-dossier flea snippets ===")
dossier = p / "enemy-dossier.json"
if dossier.exists():
    text = dossier.read_text(encoding="utf-8", errors="replace")
    for m in re.finditer(r".{0,80}[Ff]lea.{0,200}", text):
        print(m.group(0).replace("\n"," ")[:280])
        print("---")

print("\n=== fight events flea first engage ===")
f = p / "enemy-fight-events.log"
if f.exists():
    n=0
    for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
        if "Flea" in line or "flea" in line:
            print(line[:350])
            n+=1
            if n>=40: break

print("\n=== scfu / events MonsterScale for flea ===")
for fname in ["events.log", "system-messages.log"]:
    fp = p/fname
    if not fp.exists(): continue
    n=0
    for line in fp.read_text(encoding="utf-8", errors="replace").splitlines():
        if "Flea" in line and ("Scale" in line or "scale" in line or "Corpse" in line or "corpse" in line or "Aggro" in line):
            print(fname, line[:300])
            n+=1
            if n>=30: break
