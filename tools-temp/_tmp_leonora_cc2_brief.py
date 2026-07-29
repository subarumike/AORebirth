# Capture brief: second try credit card deny
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-secon try CC")
out = Path(r"tools-temp/_tmp_leonora_cc2_out.txt")
lines = []


def p(*a):
    lines.append(" ".join(str(x) for x in a))


keys = (
    "297302",
    "297315",
    "57A421",
    "Credit",
    "credit",
    "TemplateAction",
    "GenericCmd",
    "DeleteItem",
    "FormatFeedback",
    "FormattedMessage",
    "Feedback",
    "Terminal",
    "Inventory",
    "5565",
    "Leonora",
    "pick",
    "already",
    "denied",
    "Cannot",
    "ItemLowId",
    "ItemHighId",
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
    p(f"\n=== {name} ===")
    p(text[:12000] if len(text) > 12000 else text)

events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
p(f"\n=== events focused ({len(events)} lines) ===")
for i, line in enumerate(events):
    if any(k in line for k in keys):
        p(f"{i+1}:{line[:500]}")

raw = (cap / "raw-packets.csv").read_text(encoding="utf-8-sig", errors="replace").splitlines()
p("\n=== raw focused ===")
hit = 0
for line in raw[1:]:
    if any(k in line for k in ("297302", "297315", "GenericCmd", "TemplateAction", "FormatFeedback", "Feedback", "57A421")):
        p(line[:600])
        hit += 1
        if hit > 60:
            p("...truncated...")
            break

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines))
