import csv, os, re, json

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper"

def scan_text(name, keys):
    p = os.path.join(cap, name)
    print("====", name)
    if not os.path.isfile(p):
        print("missing")
        return
    n = 0
    with open(p, encoding="utf-8", errors="replace") as f:
        for line in f:
            low = line.lower()
            if any(k in low for k in keys):
                print(line.rstrip()[:400])
                n += 1
                if n >= 60:
                    break
    print("matched", n)

keys = (
    "gift", "daily", "shop", "vendor", "window", "start", "icc",
    "immigration", "officer", "bill", "novak", "marco", "spida",
    "claim", "reward", "knubot", "trade", "vending"
)
for name in ("chat-dialogue.log", "npc-interactions.log", "system-messages.log", "mission-flow.log", "events.log"):
    scan_text(name, keys)

# shop / vendor csv
for name in ("shop-updates.csv", "vendor-full-updates.csv", "npc-lifecycle.csv", "scfu-appearance.csv", "enemy-dossier.json"):
    p = os.path.join(cap, name)
    print("====", name, "exists", os.path.isfile(p))
    if not os.path.isfile(p):
        continue
    if name.endswith(".json"):
        d = json.load(open(p, encoding="utf-8-sig"))
        enemies = d.get("enemies") or d.get("npcs") or []
        if isinstance(d, list):
            enemies = d
        for e in enemies[:50]:
            if isinstance(e, dict):
                n = e.get("name") or e.get("Name") or ""
                print(n, e.get("identity") or e.get("Identity"), e.get("position") or e.get("Position"))
        continue
    rows = list(csv.DictReader(open(p, encoding="utf-8-sig", errors="replace")))
    print("cols", list(rows[0].keys()) if rows else None, "count", len(rows))
    for r in rows[:30]:
        blob = " ".join(str(v) for v in r.values() if v)
        if any(k in blob.lower() for k in keys) or True:
            # print compact interesting fields
            interesting = {}
            for k, v in r.items():
                if not v:
                    continue
                kl = k.lower()
                if any(x in kl for x in ("name", "ident", "pos", "x", "y", "z", "playfield", "type", "shop", "vendor", "action")) or len(rows) < 40:
                    if len(str(v)) < 200:
                        interesting[k] = v
            if interesting:
                print(interesting)
