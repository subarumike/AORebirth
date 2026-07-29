# Decode Sarah Greene capture: QFU tips, loot item IDs, terminal use.
import csv
import re
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-sara")
out = Path(r"tools-temp/_tmp_sarah_cap_out.txt")

lines = []
def p(*a):
    lines.append(" ".join(str(x) for x in a))

p("=== mission-flow ===")
p((cap / "mission-flow.log").read_text(encoding="utf-8-sig", errors="replace"))

events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
keys = (
    "574187CF",
    "Shop Thief",
    "ContainerAddItem",
    "QuestFullUpdate",
    "Inventory:",
    "TemplateAction",
    "ItemInfo",
    "KnuBotNpc",
    "NpcChat",
    "DNA",
    "thief",
    "Vernon",
    "FormatFeedback",
    "FormattedMessage",
    "Template",
    "LowId",
    "HighId",
)
p("\n=== focused events ===")
for i, line in enumerate(events):
    if any(k in line for k in keys):
        p(f"{i+1}:{line[:500]}")

with (cap / "raw-packets.csv").open(newline="", encoding="utf-8", errors="replace") as f:
    r = csv.DictReader(f)
    rows = list(r)
p("\n=== raw-packets ===")
p("cols:", list(rows[0].keys()) if rows else None)
p("nrows:", len(rows))

# dump rows around sequences for tip/loot
for row in rows:
    seq = row.get("Sequence") or row.get("sequence") or ""
    typ = row.get("N3MessageType") or row.get("Type") or row.get("DecodedType") or ""
    blob = " ".join(str(v) for v in row.values())
    if any(k in blob for k in ("QuestFullUpdate", "ContainerAddItem", "TemplateAction", "FormatFeedback", "574187CF", "ItemInfo")):
        # keep short
        keep = {}
        for k, v in row.items():
            if k.lower() in ("capturedutc", "direction", "sequence", "n3messagetype", "type", "decodedtype", "identity", "name") or "hex" in k.lower():
                keep[k] = (v[:120] + "...") if isinstance(v, str) and len(v) > 120 else v
        p(keep)

# ascii search in hex log for tip strings
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace")
for needle in ("Find the thief", "DNA-Locked", "Speak to Vernon", "Shop Thief", "stolen", "DNA Locked", "armorsmith", "Sarah"):
    p(f"text '{needle}' count:", hexlog.count(needle))

# Extract readable strings near QuestFullUpdate by scanning hex payload lines for ASCII
# Also decode raw hex for QFU packets: look for known mission IDs as big-endian
mission_ids = {
    "555CF538": "talk_sarah_live",
    "555CF53C": "find_thief",
    "555CF53F": "deliver_armor",
    "555CF540": "vernon",
    "555BE9F3": "talk_sarah_stable",
}
p("\n=== mission id in hexlog ===")
hu = hexlog.upper().replace(" ", "")
for mid, label in mission_ids.items():
    p(label, mid, "count", hu.count(mid))

# Find TemplateAction / item ids around terminal use time by scanning events for numbers near ContainerAddItem
p("\n=== window around terminal use (events 1930-2000) ===")
for i in range(1920, min(2050, len(events))):
    p(f"{i+1}:{events[i][:450]}")

p("\n=== window around turn-in (events 3860-3950) ===")
for i in range(3850, min(4000, len(events))):
    p(f"{i+1}:{events[i][:450]}")

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines))
