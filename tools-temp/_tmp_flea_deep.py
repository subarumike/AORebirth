# -*- coding: utf-8 -*-
import pathlib, csv, math, sys, json, collections
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-220951")

# SCFU appearance for fleas
print("=== scfu-appearance fleas ===")
f = p / "scfu-appearance.csv"
if f.exists():
    with f.open(encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            name = (row.get("Name") or row.get("EnemyName") or "").lower()
            blob = " ".join("%s=%s" % (k, row[k]) for k in row if row[k])
            if "flea" in name or "flea" in blob.lower() or "17657" in blob:
                print({k: row[k] for k in row if row[k] and k.lower() in (
                    "name","enemyname","identity","monsterdata","scale","monsterscale","catmesh","headmesh",
                    "texture","textures","extended","flags","level","health","x","y","z","position") or "tex" in k.lower() or "mesh" in k.lower() or "unknown" in k.lower()})
                print(" FULL keys", list(row.keys())[:30])
                print(" ROW", {k: (row[k][:80] if isinstance(row[k], str) and len(row[k])>80 else row[k]) for k in row if row[k]})
                print("---")

# All flea positions from lifecycle / enemy-state
print("=== flea spawn positions ===")
fleas = {}
with (p/"npc-lifecycle.csv").open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        detail = row.get("Detail") or row.get("Notes") or ""
        name = row.get("Name") or ""
        if "Garbage Flea" not in detail and "Garbage Flea" not in name and "Mutated" not in detail:
            continue
        # parse pos=(x, y, z)
        import re
        m = re.search(r"name=([^ ]+.*?)\s+player=", detail)
        nm = m.group(1) if m else name
        if "Flea" not in nm and "Flea" not in detail:
            continue
        pm = re.search(r"pos=\(([^)]+)\)", detail)
        lm = re.search(r"level=(\d+)", detail)
        hm = re.search(r"hp=(\d+)/(\d+)", detail)
        mm = re.search(r"monsterData=(\d+)", detail)
        ident = row.get("Identity") or row.get("SourceIdentity") or ""
        if pm:
            xyz = [float(x.strip()) for x in pm.group(1).split(",")]
            key = ident or nm
            fleas[key] = {
                "name": nm, "pos": xyz, "level": lm.group(1) if lm else "?",
                "hp": hm.group(0) if hm else "?", "md": mm.group(1) if mm else "?"
            }

for k,v in sorted(fleas.items(), key=lambda x: x[1]["pos"][2] if x[1]["pos"] else 0):
    print(v)

# Aggro: when flea starts attacking, distance to player Catcraty / local
print("=== flea movement sample ===")
with (p/"enemy-movement.csv").open(encoding="utf-8-sig", newline="") as fh:
    rdr = csv.DictReader(fh)
    cols = rdr.fieldnames
    print("cols", cols)
    n=0
    for row in rdr:
        blob = " ".join((row.get(c) or "") for c in cols)
        if "Flea" in blob or "17657" in blob or "79ABE9" in blob or "79ABEA" in blob:
            print({c: row[c] for c in cols if row.get(c)})
            n+=1
            if n>=25:
                break

# Compute aggro distances from combat events + player moves
print("=== aggro distance estimate ===")
# Parse movement-packets for player + flea around first CombatTarget
player_pos = {}
flea_first_attack = {}
with (p/"enemy-state.csv").open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        name = (row.get("Name") or row.get("EnemyName") or "")
        if "Flea" not in name and "Mutated" not in name:
            continue
        # try coords
        try:
            x=float(row.get("X") or row.get("PosX") or "nan")
            y=float(row.get("Y") or row.get("PosY") or "nan")
            z=float(row.get("Z") or row.get("PosZ") or "nan")
        except:
            continue
        ident = row.get("Identity") or row.get("EnemyIdentity") or name
        if ident not in flea_first_attack and math.isfinite(x):
            flea_first_attack[ident] = (x,y,z,name)

print("first flea coords", len(flea_first_attack))
for k,v in list(flea_first_attack.items())[:15]:
    print(k, v)
