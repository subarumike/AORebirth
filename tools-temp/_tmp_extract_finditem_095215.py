# Extract FindItemReturn facts from capture 20260728-095215
from pathlib import Path
import re

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215")
replay = (cap / "mission-flow.replay.log").read_text(encoding="utf-8", errors="replace")
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")

patterns = [
    r"IN-MISSION-ACCEPT[^\n]*",
    r"LOG-IN-MISSION-ACCEPT[^\n]*",
    r"IN-MISSION-ACCEPT-ITEM[^\n]*",
    r"IN-MISSION-ACCEPT-CORRELATION[^\n]*",
    r"PLAYFIELD-INIT[^\n]*",
    r"IN-N3-TELEPORT[^\n]*",
    r"IN-MISSION-KEY[^\n]*",
    r"icon=11337[^\n]*",
    r"icon=11329[^\n]*",
    r"Action=59[^\n]*",
    r"Action=47[^\n]*",
    r"QuestMessage[^\n]*Delete[^\n]*",
    r"IN-QUEST[^\n]*",
    r"OUT-TERMINAL-USE[^\n]*",
    r"IN-TERMINAL-USE[^\n]*",
    r"Find prototype[^\n]*",
    r"awarded[^\n]*",
    r"Received[^\n]*",
    r"Feedback[^\n]*",
    r"ContainerAdd[^\n]*",
    r"MISSION-CUBE|Mission Cube|Isotope|Encrypted|capsule|Radioactive[^\n]*",
    r"UseItemOnItem[^\n]*",
    r"OUT-CREATE-QUEST[^\n]*",
    r"OUT-CreateQuest[^\n]*",
]

print("=== REPLAY HITS ===")
for pat in patterns:
    hits = re.findall(pat, replay, flags=re.I)
    if hits:
        print(f"\n## {pat} ({len(hits)})")
        for h in hits[:8]:
            print(h[:500])

print("\n=== EVENTS KEY ===")
for pat in [
    r"\[PLAYFIELD-INIT\][^\n]*",
    r"\[MISSION-FLOW\][^\n]*ACCEPT[^\n]*",
    r"\[MISSION-FLOW\][^\n]*",
    r"icon=11337[^\n]*",
    r"Action=59[^\n]*",
    r"Action = 59[^\n]*",
    r"QuestMessage \{ Action=Delete[^\n]*",
    r"CharacterActionMessage \{ Action=59[^\n]*",
    r"CharacterActionMessage \{ Action=47[^\n]*",
    r"You are awarded[^\n]*",
    r"You received[^\n]*",
    r"credits[^\n]*",
    r"StaticInstance=100010[^\n]*",
    r"StaticInstance=165839[^\n]*",
    r"Mission Cube[^\n]*",
    r"Find prototype[^\n]*",
    r"OUT-TERMINAL-USE[^\n]*",
    r"GenericCmdMessage \{[^\}]*Use[^\}]*MissionTerminal[^\n]*",
]:
    hits = re.findall(pat, events, flags=re.I)
    if hits:
        print(f"\n## {pat} ({len(hits)})")
        for h in hits[:6]:
            print(h[:600])
