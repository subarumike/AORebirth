# -*- coding: utf-8 -*-
import csv, collections, pathlib, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
caps = [
    pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/arete part 1"),
    pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/arete part 2"),
]
by = collections.defaultdict(list)
cols = None
for cap in caps:
    path = cap / "corpse-loot-observations.csv"
    with path.open(encoding="utf-8-sig", newline="") as fh:
        rdr = csv.DictReader(fh)
        if cols is None:
            cols = rdr.fieldnames
            print("COLS", cols)
        for r in rdr:
            if str(r.get("InitialSnapshot", "")).lower() != "true":
                continue
            name = (r.get("EnemyName") or "").strip() or "(unnamed)"
            md = r.get("MonsterData") or r.get("monsterData") or ""
            items = r.get("Items") or ""
            cred = r.get("Credits") or r.get("Cash") or r.get("CreditAmount") or "?"
            # try common credit column names
            for k in r:
                if "cred" in k.lower() or k.lower() == "cash":
                    if r[k]:
                        cred = r[k]
                        break
            nitems = len([p for p in items.split(";") if p.strip()])
            empty = 1 if nitems == 0 else 0
            by[(name, md)].append((str(cred), empty, nitems, items[:160]))

print("mobs", len(by))
for k, v in sorted(by.items(), key=lambda x: (-len(x[1]), x[0][0])):
    empties = sum(1 for x in v if x[1] == 1)
    creds = sorted({x[0] for x in v})
    avg_items = sum(x[2] for x in v) / float(len(v))
    print("%s | md=%s n=%d empty=%d creds=%s avgItems=%.1f" % (k[0], k[1], len(v), empties, creds, avg_items))
    for s in v[:2]:
        print("  ", s)
