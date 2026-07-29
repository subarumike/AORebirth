# Extract Antonio recipe combine evidence from Antonio-1 and antonio-2 captures.
from __future__ import print_function
import re
from pathlib import Path

caps = [
    Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-Antonio-1"),
    Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-antonio-2"),
]
out = Path(r"tools-temp/_tmp_antonio_recipe_combines.txt")
lines = []


def p(*a):
    lines.append(" ".join(str(x) for x in a))


keys = (
    "UseItemOnItem",
    "TemplateAction",
    "FormatFeedback",
    "TradeSkill",
    "combined",
    "Combine",
    "Overflow",
    "ContainerAddItem",
    "248306",
    "248315",
    "248316",
    "248347",
    "Adaptation",
    "Chemical",
    "BO-18",
    "Fluid",
    "QuestFullUpdate",
    "CharacterAction",
    "Delete",
)

for cap in caps:
    p("=" * 60)
    p("CAP", cap.name)
    events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
    focused = []
    for i, line in enumerate(events):
        if any(k in line for k in keys):
            focused.append("%d:%s" % (i + 1, line[:700]))
    p("focused", len(focused))
    for line in focused:
        p(line)

    # inventory updates
    inv = cap / "inventory-updates.csv"
    if inv.exists():
        p("\n--- inventory-updates (first 80 non-header) ---")
        for j, row in enumerate(inv.read_text(encoding="utf-8", errors="replace").splitlines()[1:81]):
            p(row[:300])

    # system messages
    sysf = cap / "system-messages.log"
    if sysf.exists():
        p("\n--- system-messages ---")
        for line in sysf.read_text(encoding="utf-8", errors="replace").splitlines():
            if any(k.lower() in line.lower() for k in ("combin", "trade", "skill", "recipe", "assemble", "adapt", "factory", "fluid", "BO-18", "quality")):
                p(line[:500])

    # mission flow
    mf = cap / "mission-flow.log"
    if mf.exists():
        p("\n--- mission-flow ---")
        p(mf.read_text(encoding="utf-8", errors="replace")[:4000])

    # chat dialogue options selected
    chat = cap / "chat-dialogue.log"
    if chat.exists():
        p("\n--- chat knubot answers/options ---")
        for line in chat.read_text(encoding="utf-8", errors="replace").splitlines():
            if "AnswerList" in line or "Answer=" in line or "AppendText" in line:
                # shorten
                m = re.search(r"text=([^ ]+|.*?) detail=", line)
                if "AnswerList" in line and "text=" in line:
                    t = line.split("text=", 1)[1].split(" detail=", 1)[0]
                    p("OPTS", t[:400])
                elif "Answer=" in line and "OUT" in line:
                    p("ANS", line[line.find("Answer="):line.find("Answer=")+20])

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines))
