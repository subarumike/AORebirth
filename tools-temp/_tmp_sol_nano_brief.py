# Capture brief: soldier nano pack open
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-sol-nano")
out = Path(r"tools-temp/_tmp_sol_nano_out.txt")
lines = []


def p(*a):
    lines.append(" ".join(str(x) for x in a))


keys = (
    "TemplateAction",
    "GenericCmd",
    "DeleteItem",
    "ContainerAdd",
    "FormatFeedback",
    "FormattedMessage",
    "QuestFullUpdate",
    "Inventory",
    "Overflow",
    "ItemLowId",
    "ItemHighId",
    "nano",
    "Nano",
    "Soldier",
    "Crystal",
    "Package",
    "297",
    "433",
    "498",
    "557",
)

p("=== files ===")
for f in sorted(cap.iterdir()):
    if f.is_file():
        p(f.name, f.stat().st_size)

for name in ("mission-flow.log", "system-messages.log", "npc-interactions.log"):
    path = cap / name
    if not path.exists():
        continue
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    p(f"\n=== {name} ===")
    for line in text.splitlines():
        if any(k.lower() in line.lower() for k in ("TemplateAction", "GenericCmd", "Delete", "FormatFeedback", "Overflow", "Quest", "297", "Crystal", "Package", "nano", "ItemLowId")):
            p(line[:500])

events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
p(f"\n=== events focused ({len(events)} lines) ===")
hit = 0
for i, line in enumerate(events):
    if any(k in line for k in ("TemplateAction", "GenericCmd", "DeleteItem", "ContainerAdd", "FormatFeedback", "ItemLowId", "Overflow", "QuestFullUpdate")):
        if "FollowTarget" in line or "SetNanoDuration" in line:
            continue
        p(f"{i+1}:{line[:480]}")
        hit += 1
        if hit > 100:
            p("...truncated...")
            break

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "hits", hit)
