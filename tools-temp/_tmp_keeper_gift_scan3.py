import csv, os

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper"
out = open(r"tools-temp\_tmp_keeper_gift_out2.txt", "w", encoding="utf-8")

def p(*a):
    out.write(" ".join(str(x) for x in a) + "\n")

# inventory updates around gift
path = os.path.join(cap, "inventory-updates.csv")
p("==== inventory-updates.csv")
rows = list(csv.DictReader(open(path, encoding="utf-8-sig", errors="replace")))
p("cols", list(rows[0].keys()) if rows else None, "count", len(rows))
for r in rows:
    p({k: v for k, v in r.items() if v and len(str(v)) < 250})

# raw packets mentioning reward / Inventory 45/47 / Market / PrivateMsg early
path = os.path.join(cap, "raw-packets.csv")
rows = list(csv.DictReader(open(path, encoding="utf-8-sig", errors="replace")))
p("==== early raw packets first 80 interesting")
n = 0
for r in rows:
    t = r.get("N3TypeName") or ""
    utc = r.get("CapturedUtc") or ""
    if utc > "2026-07-22T04:31:00":
        break
    if t in ("PrivateMessage", "GenericCmd", "ContainerAddItem", "InventoryUpdate", "MarketSend", "ShopUpdate", "VendingMachineFullUpdate", "CharacterAction", "ChatText", "SystemMessage", "TemplateAction", "Feedback") or "Inventory" in str(r.get("IdentityType")):
        p(utc, t, "id", r.get("IdentityType"), r.get("IdentityInstance"), "len", r.get("PacketLength"))
        hx = r.get("RawHex") or ""
        if hx:
            p("  hex", hx[:240])
        n += 1
p("matched early", n)

# events.log early lines with reward/gift/inventory/market
p("==== events early reward/gift/market/inventory")
with open(os.path.join(cap, "events.log"), encoding="utf-8-sig", errors="replace") as f:
    for line in f:
        if line.startswith("2026-07-22T04:31"):
            break
        low = line.lower()
        if any(k in low for k in ("reward", "gift", "claim", "inventory", "market", "shop", "0045", "0047", "privatemsg", "template", "feedback", "container")):
            p(line.rstrip()[:450])

out.close()
print("done")
