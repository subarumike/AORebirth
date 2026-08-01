# -*- coding: utf-8 -*-
import csv
import pathlib
import sys
import json
from collections import defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth")
CAPS = [
    ROOT / r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\arete part 1",
    ROOT / r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\arete part 2",
]

# Try resolve names from ObservedLiveLootSeed + any local name dumps
name_by_id = {}
seed = ROOT / r"docs\reference\loot\mob-loot-coverage\ObservedLiveLootSeed.csv"
if seed.exists():
    with seed.open(encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            for k in ("low_id", "high_id"):
                try:
                    i = int(row.get(k) or 0)
                except ValueError:
                    continue
                n = (row.get("item_name") or "").strip()
                if i and n:
                    name_by_id[i] = n

# Also scan inventory-updates for names if present
for cap in CAPS:
    inv = cap / "inventory-updates.csv"
    if not inv.exists():
        continue
    with inv.open(encoding="utf-8-sig", newline="") as fh:
        r = csv.DictReader(fh)
        cols = r.fieldnames or []
        # print once later
        for row in r:
            for c in cols:
                if not c:
                    continue
                cl = c.lower()
                if "name" in cl or "item" in cl:
                    pass

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
        low, high, ql, qty = int(bits[0]), int(bits[1]), int(bits[2]), int(bits[3])
        out.append((low, high, ql, qty))
    return out

def item_label(low, high, ql):
    nlow = name_by_id.get(low)
    nhigh = name_by_id.get(high)
    name = nlow or nhigh or ""
    if low == high:
        base = f"{low}" + (f" {name}" if name else "")
    else:
        base = f"{low}-{high}" + (f" {name}" if name else "")
    return f"{base} QL{ql}"

# Aggregate: only InitialSnapshot=true opens (first open of corpse)
mobs = {}  # key = (EnemyName or MonsterData, MonsterData)

for cap in CAPS:
    path = cap / "corpse-loot-observations.csv"
    with path.open(encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            if str(row.get("InitialSnapshot", "")).lower() != "true":
                continue
            name = (row.get("EnemyName") or "").strip() or "(unnamed)"
            md = (row.get("MonsterData") or "").strip()
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
                    "item_hits": defaultdict(int),  # (low,high,ql) -> count of corpses with this drop
                    "item_qty": defaultdict(int),
                    "all_item_rows": [],
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
                m["all_item_rows"].append((low, high, ql, qty))

# Output summary
out_rows = []
print("=== ARETE LOOT FROM CAPTURES (part1 + part2) ===")
print("Note: observed drops only (not full drop tables). Initial corpse opens only.\n")

for key in sorted(mobs.keys(), key=lambda k: (k[0].lower(), k[1])):
    m = mobs[key]
    levels = sorted(m["levels"])
    creds = m["credits"]
    print(f"## {m['name']}  monsterdata={m['monsterData'] or '?'}")
    print(f"   corpses={m['corpses']} empty={m['empty']} levels={levels or '?'} credits={min(creds)}-{max(creds)} (avg {sum(creds)/len(creds):.1f})")
    if not m["item_hits"]:
        print("   (no items observed)")
        print()
        continue
    for (low, high, ql), hits in sorted(m["item_hits"].items(), key=lambda x: (-x[1], x[0][0])):
        rate = 100.0 * hits / m["corpses"]
        label = item_label(low, high, ql)
        qty = m["item_qty"][(low, high, ql)]
        print(f"   {hits}/{m['corpses']} ({rate:5.1f}%)  {label}  total_qty={qty}")
        out_rows.append({
            "enemy_name": m["name"],
            "monster_data": m["monsterData"],
            "corpses": m["corpses"],
            "empty_corpses": m["empty"],
            "levels": ",".join(str(x) for x in levels),
            "credits_min": min(creds),
            "credits_max": max(creds),
            "credits_avg": round(sum(creds)/len(creds), 2),
            "low_id": low,
            "high_id": high,
            "ql": ql,
            "item_label": label,
            "item_name": name_by_id.get(low) or name_by_id.get(high) or "",
            "observed_on_corpses": hits,
            "observed_rate_pct": round(rate, 1),
            "total_qty": qty,
        })
    print()

# Unique item AOIDs across all
unique_ids = set()
for m in mobs.values():
    for low, high, ql in m["item_hits"]:
        unique_ids.add(low)
        unique_ids.add(high)
print("=== UNIQUE ITEM IDS ===")
for i in sorted(unique_ids):
    print(f"{i}\t{name_by_id.get(i, '')}")

out_path = ROOT / r"tools-temp\_arete_loot_part1_part2.json"
out_path.write_text(json.dumps({"mobs": [
    {
        "name": m["name"],
        "monsterData": m["monsterData"],
        "corpses": m["corpses"],
        "empty": m["empty"],
        "levels": sorted(m["levels"]),
        "creditsMin": min(m["credits"]),
        "creditsMax": max(m["credits"]),
        "creditsAvg": round(sum(m["credits"])/len(m["credits"]), 2),
        "drops": [
            {
                "lowId": low,
                "highId": high,
                "ql": ql,
                "name": name_by_id.get(low) or name_by_id.get(high) or "",
                "label": item_label(low, high, ql),
                "observedOnCorpses": hits,
                "ratePct": round(100.0 * hits / m["corpses"], 1),
                "totalQty": m["item_qty"][(low, high, ql)],
            }
            for (low, high, ql), hits in sorted(m["item_hits"].items(), key=lambda x: (-x[1], x[0][0]))
        ],
    }
    for key, m in sorted(mobs.items(), key=lambda kv: (kv[0][0].lower(), kv[0][1]))
]}, indent=2), encoding="utf-8")
print(f"\nWrote {out_path}")

# CSV
csv_path = ROOT / r"tools-temp\_arete_loot_part1_part2.csv"
with csv_path.open("w", encoding="utf-8", newline="") as fh:
    w = csv.DictWriter(fh, fieldnames=list(out_rows[0].keys()) if out_rows else [])
    if out_rows:
        w.writeheader()
        w.writerows(out_rows)
print(f"Wrote {csv_path}")
