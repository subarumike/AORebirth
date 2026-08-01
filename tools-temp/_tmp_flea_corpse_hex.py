# -*- coding: utf-8 -*-
import csv, pathlib
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-220951/corpse-full-updates.csv")
with p.open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        if row.get("DeadNpcName") == "Garbage Flea" and int(row.get("CorpseCredits") or 0) > 0:
            hx = (row.get("RawHex") or "").replace(" ", "")
            print("len", len(hx)//2, "scale", row.get("MonsterScale"), "mesh", row.get("CorpseCatMesh"))
            print(hx)
            break
