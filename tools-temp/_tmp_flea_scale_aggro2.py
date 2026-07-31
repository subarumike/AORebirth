# -*- coding: utf-8 -*-
import csv, pathlib, math, re
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-220951")

# Mutated flea corpse scale + living SCFU MonsterScale
print("=== mutated corpse ===")
with (p/"corpse-full-updates.csv").open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        if "Mutated" in (row.get("DeadNpcName") or "") or "Mutated" in (row.get("CorpseName") or ""):
            print(row.get("CorpseName"), "scale", row.get("MonsterScale"), "mesh", row.get("CorpseCatMesh"), "md", row.get("CorpseMonsterData"), "credits", row.get("CorpseCredits"))

# Living SCFU MonsterScale from raw or events
print("\n=== MonsterScale in events for fleas ===")
n=0
for line in (p/"events.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "Garbage Flea" in line and "MonsterScale" in line:
        m = re.search(r"MonsterScale[=:]?\s*(\d+)", line)
        print(m.group(0) if m else line[:200])
        n+=1
        if n>=15: break
if n==0:
    # try scfu-appearance
    for cand in ["scfu-appearance.csv","enemy-state.csv","enemy-dossier.json"]:
        f=p/cand
        if f.exists():
            print("file", cand, "size", f.stat().st_size)

# Aggro distance: find first CombatTarget / AttackInfo for a flea and player distance
print("\n=== aggro distances from combat csv ===")
combat = p/"enemy-combat.csv"
player = None
# Approximate from movement: parse first AttackInfo where name has Flea
with combat.open(encoding="utf-8-sig", newline="") as fh:
    rows=list(csv.DictReader(fh))
print("combat cols", rows[0].keys() if rows else None)
hits=0
for row in rows:
    blob=" ".join((row.get(c) or "") for c in row)
    if "Flea" not in blob and "17657" not in blob:
        continue
    if (row.get("MessageType") or row.get("N3TypeName") or "") not in ("AttackInfo","CombatTarget", "Attack"):
        # print first few flea rows
        if hits < 5:
            print({k:row[k] for k in row if row.get(k)})
        hits += 1
        if hits>=5: 
            pass
print("flea combat rows", sum(1 for r in rows if "Flea" in " ".join((r.get(c) or "") for c in r)))

# From enemy-state: when attacking becomes true, note position vs player
print("\n=== enemy-state flea attacking transitions ===")
es=p/"enemy-state.csv"
with es.open(encoding="utf-8-sig", newline="") as fh:
    rdr=csv.DictReader(fh)
    cols=rdr.fieldnames
    print("cols", cols)
    n=0
    for row in rdr:
        name=row.get("Name") or row.get("EnemyName") or ""
        if "Flea" not in name:
            continue
        attacking = row.get("Attacking") or row.get("IsAttacking") or row.get("AttackingState") or ""
        ft = row.get("FightingTarget") or row.get("Target") or ""
        if str(attacking).lower() in ("true","1") or (ft and ft not in ("null","None","")):
            print({k:row[k] for k in cols if row.get(k)})
            n+=1
            if n>=12: break
