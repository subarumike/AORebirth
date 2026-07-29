# Capture brief: Leonora finish + open Vacuum Packed Omni-Med Suit
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-finish leonora and open vacuumpack")
out = Path(r"tools-temp/_tmp_leonora_vac_out.txt")
lines = []


def p(*a):
    lines.append(" ".join(str(x) for x in a))


keys = (
    "297054",
    "297055",
    "297056",
    "297057",
    "297058",
    "297059",
    "297060",
    "297061",
    "297062",
    "297063",
    "297064",
    "297065",
    "297066",
    "297067",
    "297068",
    "297069",
    "297070",
    "297071",
    "297072",
    "297073",
    "297074",
    "297075",
    "297076",
    "297077",
    "297078",
    "297079",
    "297080",
    "297302",
    "297315",
    "Vacuum",
    "Omni-Med",
    "Omni Med",
    "TemplateAction",
    "GenericCmd",
    "DeleteItem",
    "CharacterAction",
    "ContainerAdd",
    "FormatFeedback",
    "FormattedMessage",
    "KnuBot",
    "5565CD8",
    "FollowTarget",
    "78E0FC74",
    "Leonora",
    "Inventory:",
    "ItemLowId",
    "ItemHighId",
    "LowId",
    "HighId",
    "Action=Use",
    "UseItem",
)

p("=== file sizes ===")
for f in sorted(cap.iterdir()):
    if f.is_file():
        p(f.name, f.stat().st_size)

for name in (
    "mission-flow.log",
    "system-messages.log",
    "npc-interactions.log",
    "chat-dialogue.log",
):
    text = (cap / name).read_text(encoding="utf-8-sig", errors="replace")
    p(f"\n=== {name} ({len(text)} chars) ===")
    p(text[:8000] if len(text) > 8000 else text)

events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
p(f"\n=== events focused ({len(events)} lines) ===")
for i, line in enumerate(events):
    if any(k in line for k in keys):
        p(f"{i+1}:{line[:450]}")

inv = (cap / "inventory-updates.csv").read_text(encoding="utf-8-sig", errors="replace").splitlines()
p(f"\n=== inventory-updates ({len(inv)} lines) ===")
for line in inv[:5]:
    p(line[:300])
for line in inv[1:]:
    if any(k in line for k in ("2970", "Vacuum", "Omni", "Template", "Delete", "Add")):
        p(line[:400])

# raw packet message types around item ids
raw = (cap / "raw-packets.csv").read_text(encoding="utf-8-sig", errors="replace").splitlines()
p(f"\n=== raw-packets header ===")
p(raw[0][:400] if raw else "empty")
hit = 0
for line in raw[1:]:
    if any(k in line for k in ("297054", "297302", "TemplateAction", "GenericCmd", "Vacuum", "Omni")):
        p(line[:500])
        hit += 1
        if hit > 80:
            p("...truncated...")
            break
p(f"raw focused hits shown up to {hit}")

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines))
