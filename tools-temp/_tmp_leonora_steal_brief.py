# Capture brief: credit card steal money use
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-073341")
out = Path(r"tools-temp/_tmp_leonora_steal_out.txt")
lines = []


def p(*a):
    lines.append(" ".join(str(x) for x in a))


keys = (
    "297302",
    "297315",
    "Credit",
    "credit",
    "steal",
    "Steal",
    "money",
    "GenericCmd",
    "TemplateAction",
    "DeleteItem",
    "FormatFeedback",
    "FormattedMessage",
    "Feedback",
    "Inventory",
    "5565",
    "Cash",
    "credits",
    "15000",
    "Quest",
    "CharacterAction",
    "Confirm",
)

p("=== files ===")
for f in sorted(cap.iterdir()):
    if f.is_file():
        p(f.name, f.stat().st_size)

for name in (
    "mission-flow.log",
    "system-messages.log",
    "npc-interactions.log",
    "chat-dialogue.log",
):
    path = cap / name
    if not path.exists():
        continue
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    p(f"\n=== {name} ({len(text)} chars) ===")
    # keep focused slices
    for line in text.splitlines():
        if any(k.lower() in line.lower() for k in ("297302", "credit", "steal", "FormatFeedback", "GenericCmd", "TemplateAction", "Delete", "15000", "Cash", "Quest", "5565", "money")):
            p(line[:500])

events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
p(f"\n=== events focused ({len(events)} lines) ===")
hit = 0
for i, line in enumerate(events):
    if any(k in line for k in keys):
        p(f"{i+1}:{line[:480]}")
        hit += 1
        if hit > 120:
            p("...truncated...")
            break

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines), "hits", hit)
