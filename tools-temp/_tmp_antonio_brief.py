# Decode Antonio Stacklund capture: dialogue text, vendor, recipe tips.
from __future__ import print_function
import csv
import re
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-Antonio-Stacklund")
out = Path(r"tools-temp/_tmp_antonio_brief.txt")
lines = []


def p(*a):
    lines.append(" ".join(str(x) for x in a))


# Shop stock
with (cap / "shop-updates.csv").open(newline="", encoding="utf-8", errors="replace") as f:
    shop = list(csv.DictReader(f))
slots = {}
for r in shop:
    if "12E7720D" not in r.get("TerminalIdentity", ""):
        continue
    slots[int(r["Slot"])] = (int(r["LowId"]), int(r["HighId"]), int(r["Quality"]))
p("=== shop slots ===")
for s in sorted(slots):
    p(s, slots[s])

# vendor full
p("\n=== vendor-full-updates.csv ===")
p((cap / "vendor-full-updates.csv").read_text(encoding="utf-8", errors="replace")[:3000])

# events focused
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
keys = (
    "78E0FC7C",
    "12E7720D",
    "KnuBot",
    "AppendText",
    "ShopUpdate",
    "Trade",
    "Vending",
    "GenericCmd",
    "FormatFeedback",
    "ItemInfo",
    "StaticInstance",
)
p("\n=== focused events (count) ===")
focused = []
for i, line in enumerate(events):
    if any(k in line for k in keys):
        focused.append("%d:%s" % (i + 1, line[:600]))
p("focused", len(focused))
for line in focused[:250]:
    p(line)

# hexlog: extract AppendText-ish ASCII runs near Antonio times / recipe keywords
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace")
needles = (
    "Welcome",
    "Stacklund",
    "Antonio",
    "upgrade",
    "Assault",
    "bracer",
    "leather",
    "hud",
    "Shopping Cart",
    "shopping cart",
    "weapon",
    "General Store",
    "Adaptation",
    "combine",
    "recipe",
    "Teach",
)
p("\n=== hex needle counts ===")
for n in needles:
    p(repr(n), hexlog.count(n))

# Extract printable strings of length >= 20 from hexlog payload sections
# Also scan raw-packets for AppendText / Shop / Vending
with (cap / "raw-packets.csv").open(newline="", encoding="utf-8", errors="replace") as f:
    rows = list(csv.DictReader(f))
p("\n=== raw-packets cols ===", list(rows[0].keys()) if rows else None, "nrows", len(rows))
append_rows = []
shop_rows = []
for row in rows:
    blob = " ".join(str(v) for v in row.values())
    if "AppendText" in blob or "KnubotAppend" in blob or "KnuBotAppend" in blob:
        append_rows.append(row)
    if "12E7720D" in blob or "ShopUpdate" in blob or "VendingMachine" in blob:
        shop_rows.append(row)
p("append_rows", len(append_rows), "shopish", len(shop_rows))
for row in append_rows[:40]:
    keep = {}
    for k, v in row.items():
        vs = str(v)
        if len(vs) > 200:
            vs = vs[:200] + "..."
        keep[k] = vs
    p(keep)

# Decode ASCII from hex payloads in packets.hex.log for knubot texts
# Look for lines containing 78E0FC7C near Append
ascii_hits = []
for m in re.finditer(r"[\x20-\x7e]{30,}", hexlog):
    s = m.group(0)
    low = s.lower()
    if any(
        x in low
        for x in (
            "upgrade",
            "weapon",
            "bracer",
            "leather",
            "hud",
            "antonio",
            "stacklund",
            "shopping",
            "assault",
            "rifle",
            "bat",
            "blade",
            "combine",
            "sell",
            "general store",
            "adaptation",
            "naja",
            "oak bo",
        )
    ):
        ascii_hits.append(s)
# unique preserve order
seen = set()
uniq = []
for s in ascii_hits:
    if s not in seen:
        seen.add(s)
        uniq.append(s)
p("\n=== ascii recipe/dialog hits ===", len(uniq))
for s in uniq:
    p(s[:500])

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines))
