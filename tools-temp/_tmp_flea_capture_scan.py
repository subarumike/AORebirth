# -*- coding: utf-8 -*-
import pathlib, sys, csv, re
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-220951")
print("exists", p.exists())
if not p.exists():
    raise SystemExit(1)
for name in sorted(x.name for x in p.iterdir()):
    print(" ", name)

keys = ["Garbage", "Flea", "Mutated", "297289", "Shiny", "Sword", "under", "floor", "aggro", "Attack", "Follow", "path", "mesh", "CAT", "MonsterData", "17657"]
for fname in ["enemy-dossier.json", "enemy-state.json", "npc-lifecycle.csv", "enemy-full-updates.csv", "enemy-movement.csv", "events.log", "system-messages.log", "corpse-loot-observations.csv"]:
    f = p / fname
    if not f.exists():
        continue
    print("=" * 70, fname)
    n = 0
    text = f.read_text(encoding="utf-8", errors="replace")
    if fname.endswith(".csv"):
        for line in text.splitlines():
            low = line.lower()
            if any(k.lower() in low for k in keys):
                print(line[:400])
                n += 1
                if n >= 50:
                    print("...trunc")
                    break
    else:
        for line in text.splitlines():
            low = line.lower()
            if any(k.lower() in low for k in keys):
                print(line[:450])
                n += 1
                if n >= 60:
                    print("...trunc")
                    break
    if n == 0:
        print("(no keyword hits)")
