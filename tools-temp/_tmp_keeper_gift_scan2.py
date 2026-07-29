import csv, os, json, sys

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper"
out = open(r"tools-temp\_tmp_keeper_gift_out.txt", "w", encoding="utf-8")

def p(*a):
    out.write(" ".join(str(x) for x in a) + "\n")

keys = (
    "gift", "daily", "shop", "vendor", "window", "start", "icc",
    "immigration", "officer", "bill", "novak", "marco", "spida",
    "claim", "reward", "knubot", "trade", "vending", "newcomer",
    "welcome", "starter", "free"
)

def scan_text(name):
    path = os.path.join(cap, name)
    p("====", name)
    if not os.path.isfile(path):
        p("missing")
        return
    n = 0
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        for line in f:
            low = line.lower()
            if any(k in low for k in keys):
                p(line.rstrip()[:500])
                n += 1
                if n >= 80:
                    break
    p("matched", n)

for name in ("chat-dialogue.log", "npc-interactions.log", "system-messages.log", "mission-flow.log"):
    scan_text(name)

# events: KnuBot / Trade / GenericCmd / Use
p("==== events KnuBot/Trade/Use/Vendor")
path = os.path.join(cap, "events.log")
n = 0
with open(path, encoding="utf-8-sig", errors="replace") as f:
    for line in f:
        if any(k in line for k in ("KnuBot", "Trade", "Vendor", "Shop", "GenericCmd", "Use", "Vending", "Open")):
            if any(k in line for k in ("KnuBot", "Trade", "Vendor", "Shop", "Vending", "Gift", "gift")):
                p(line.rstrip()[:500])
                n += 1
                if n >= 80:
                    break
p("matched", n)

for name in ("shop-updates.csv", "vendor-full-updates.csv", "scfu-appearance.csv"):
    path = os.path.join(cap, name)
    p("====", name, "exists", os.path.isfile(path))
    if not os.path.isfile(path):
        continue
    rows = list(csv.DictReader(open(path, encoding="utf-8-sig", errors="replace")))
    p("cols", list(rows[0].keys()) if rows else None, "count", len(rows))
    for r in rows:
        p({k: v for k, v in r.items() if v and k.lower() not in ("rawpackethex", "rawhex") and len(str(v)) < 300})

# dossier / state names
for name in ("enemy-dossier.json", "enemy-state.json"):
    path = os.path.join(cap, name)
    p("====", name)
    if not os.path.isfile(path):
        continue
    d = json.load(open(path, encoding="utf-8-sig"))
    enemies = d.get("enemies") or []
    p("count", len(enemies))
    for e in enemies:
        p(e.get("name"), e.get("identity"), e.get("position"), "md", e.get("monsterData"), "pf", e.get("playfieldId"))

out.close()
print("wrote tools-temp/_tmp_keeper_gift_out.txt")
