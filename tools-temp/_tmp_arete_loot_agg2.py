# -*- coding: utf-8 -*-
import csv
import json
import pathlib
import re
import sys
from collections import defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth")
CAPS = [
    ROOT / r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\arete part 1",
    ROOT / r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\arete part 2",
]

# Collect all AOIDs first pass
ids = set()
rows_raw = []
for cap in CAPS:
    with (cap / "corpse-loot-observations.csv").open(encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            if str(row.get("InitialSnapshot", "")).lower() != "true":
                continue
            items = row.get("Items") or ""
            for part in items.split(";"):
                part = part.strip()
                if not part:
                    continue
                bits = part.split(":")
                if len(bits) >= 4:
                    ids.add(int(bits[0]))
                    ids.add(int(bits[1]))
            rows_raw.append(row)

# Resolve names from itemnames.sql INSERT lines
name_by_id = {}
sql = ROOT / r"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\itemnames.sql"
# Format often: INSERT INTO `itemnames` VALUES (id,'name',...);
# itemnames.sql: ( 248427 , 'Name' , 'Item', '33152' ) — spaces + '' escapes
pat = re.compile(r"\(\s*(\d+)\s*,\s*'((?:''|[^'])*)'")
if sql.exists():
    needed = set(ids)
    with sql.open(encoding="utf-8", errors="replace") as fh:
        for line in fh:
            for m in pat.finditer(line):
                i = int(m.group(1))
                if i in needed and i not in name_by_id:
                    name_by_id[i] = m.group(2).replace("''", "'")

# enemy identity -> name from enemy-state / dossier
id_to_name = {}
for cap in CAPS:
    for fname in ("enemy-state.csv", "enemy-full-updates.csv"):
        f = cap / fname
        if not f.exists():
            continue
        with f.open(encoding="utf-8-sig", newline="") as fh:
            for row in csv.DictReader(fh):
                ident = (row.get("Identity") or row.get("SourceIdentity") or row.get("EnemyIdentity") or "").strip()
                name = (row.get("Name") or row.get("EnemyName") or "").strip()
                if ident and name:
                    # normalize SimpleChar:XXXXXXXX
                    id_to_name[ident.replace("(", "").replace(")", "")] = name
                    if ":" in ident:
                        id_to_name[ident.split(":")[-1].strip(")")] = name

# monsterdata fallback names from observed named corpses
md_names = defaultdict(set)

def parse_items(items_field):
    out = []
    if not items_field:
        return out
    for part in items_field.split(";"):
        part = part.strip()
        if not part:
            continue
        bits = part.split(":")
        if len(bits) < 4:
            continue
        out.append((int(bits[0]), int(bits[1]), int(bits[2]), int(bits[3])))
    return out

def resolve_name(row):
    name = (row.get("EnemyName") or "").strip()
    if name:
        return name
    dead = (row.get("DeadNpcIdentity") or "").replace("(", "").replace(")", "")
    if dead in id_to_name:
        return id_to_name[dead]
    if ":" in dead:
        short = dead.split(":")[-1]
        if short in id_to_name:
            return id_to_name[short]
    return "(unnamed)"

mobs = {}
for row in rows_raw:
    name = resolve_name(row)
    md = (row.get("MonsterData") or "").strip()
    if name != "(unnamed)" and md:
        md_names[md].add(name)
    lvl = row.get("EnemyLevel") or ""
    credits = int(row.get("CorpseCredits") or 0)
    items = parse_items(row.get("Items") or "")
    key = (name, md)
    if key not in mobs:
        mobs[key] = {
            "name": name,
            "monsterData": md,
            "corpses": 0,
            "empty": 0,
            "levels": set(),
            "credits": [],
            "item_hits": defaultdict(int),
            "item_qty": defaultdict(int),
        }
    m = mobs[key]
    m["corpses"] += 1
    if lvl:
        try:
            m["levels"].add(int(lvl))
        except ValueError:
            pass
    m["credits"].append(credits)
    if not items:
        m["empty"] += 1
    seen = set()
    for low, high, ql, qty in items:
        ik = (low, high, ql)
        if ik not in seen:
            m["item_hits"][ik] += 1
            seen.add(ik)
        m["item_qty"][ik] += qty

# Merge unnamed into likely names by monsterdata when unique mapping exists
# (report unnamed separately still if identity unresolved)

def item_name(low, high):
    return name_by_id.get(low) or name_by_id.get(high) or ""

def item_label(low, high, ql):
    n = item_name(low, high)
    if low == high:
        base = f"{low}" + (f" {n}" if n else "")
    else:
        base = f"{low}-{high}" + (f" {n}" if n else "")
    return f"{base} QL{ql}"

payload = []
print(f"Resolved {len(name_by_id)}/{len(ids)} item names from itemnames.sql\n")
print("=== ARETE LOOT (arete part 1 + part 2) ===")
print("Observed initial corpse opens only — not complete drop tables.\n")

for key in sorted(mobs.keys(), key=lambda k: (k[0].lower(), k[1])):
    m = mobs[key]
    levels = sorted(m["levels"])
    creds = m["credits"]
    print(f"## {m['name']}  md={m['monsterData'] or '?'}")
    print(f"   corpses={m['corpses']} empty={m['empty']} levels={levels or ['?']} credits={min(creds)}-{max(creds)} avg={sum(creds)/len(creds):.1f}")
    drops = []
    for (low, high, ql), hits in sorted(m["item_hits"].items(), key=lambda x: (-x[1], x[0][0])):
        rate = round(100.0 * hits / m["corpses"], 1)
        lab = item_label(low, high, ql)
        qty = m["item_qty"][(low, high, ql)]
        print(f"   {hits}/{m['corpses']} ({rate:5.1f}%)  {lab}  qty={qty}")
        drops.append({
            "lowId": low,
            "highId": high,
            "ql": ql,
            "name": item_name(low, high),
            "label": lab,
            "observedOnCorpses": hits,
            "ratePct": rate,
            "totalQty": qty,
        })
    if not drops:
        print("   (no items)")
    print()
    payload.append({
        "name": m["name"],
        "monsterData": m["monsterData"],
        "corpses": m["corpses"],
        "empty": m["empty"],
        "levels": levels,
        "creditsMin": min(creds),
        "creditsMax": max(creds),
        "creditsAvg": round(sum(creds) / len(creds), 2),
        "drops": drops,
    })

out = ROOT / r"tools-temp\_arete_loot_part1_part2.json"
out.write_text(json.dumps({"source": ["arete part 1", "arete part 2"], "playfieldHint": 1044525, "mobs": payload}, indent=2), encoding="utf-8")

csv_path = ROOT / r"tools-temp\_arete_loot_part1_part2.csv"
flat = []
for m in payload:
    if not m["drops"]:
        flat.append({
            "enemy_name": m["name"], "monster_data": m["monsterData"], "corpses": m["corpses"],
            "empty_corpses": m["empty"], "levels": ",".join(map(str, m["levels"])),
            "credits_min": m["creditsMin"], "credits_max": m["creditsMax"], "credits_avg": m["creditsAvg"],
            "low_id": "", "high_id": "", "ql": "", "item_name": "", "item_label": "(empty corpse)",
            "observed_on_corpses": "", "observed_rate_pct": "", "total_qty": "",
        })
        continue
    for d in m["drops"]:
        flat.append({
            "enemy_name": m["name"], "monster_data": m["monsterData"], "corpses": m["corpses"],
            "empty_corpses": m["empty"], "levels": ",".join(map(str, m["levels"])),
            "credits_min": m["creditsMin"], "credits_max": m["creditsMax"], "credits_avg": m["creditsAvg"],
            "low_id": d["lowId"], "high_id": d["highId"], "ql": d["ql"],
            "item_name": d["name"], "item_label": d["label"],
            "observed_on_corpses": d["observedOnCorpses"], "observed_rate_pct": d["ratePct"],
            "total_qty": d["totalQty"],
        })
with csv_path.open("w", encoding="utf-8", newline="") as fh:
    w = csv.DictWriter(fh, fieldnames=list(flat[0].keys()))
    w.writeheader()
    w.writerows(flat)
print(f"Wrote {out}")
print(f"Wrote {csv_path}")
print(f"Unresolved item ids: {sorted(ids - set(name_by_id))[:40]} ... total unresolved {len(ids - set(name_by_id))}")
