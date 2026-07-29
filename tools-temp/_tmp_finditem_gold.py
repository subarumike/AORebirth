from pathlib import Path
import re

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215")
replay = (cap / "mission-flow.replay.log").read_text(encoding="utf-8", errors="replace")
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")

print("=== ACCEPTED FIND/RETURN ===")
for pat in [
    r"OUT-CREATE-QUEST[^\n]*556D76DD[^\n]*",
    r"IN-MISSION-OFFER[^\n]*556D76DD[^\n]*",
    r"IN-MISSION-ACCEPT[^\n]*556D76DD[^\n]*",
    r"IN-MISSION-ACCEPT-CORRELATION[^\n]*556D76DD[^\n]*",
    r"IN-MISSION-KEY[^\n]*F6F2DD[^\n]*",
    r"icon=11329[^\n]*Encrypted[^\n]*",
    r"icon=11329[^\n]*bring[^\n]*",
]:
    hits = re.findall(pat, replay, flags=re.I)
    if hits:
        print(f"\n# {pat} ({len(hits)})")
        print(hits[0][:700])

print("\n=== INSTANCE / ITEM / FINISH ===")
for line in [
    "PLAYFIELD-INIT] 1492999",
    "ACGBuildingGeneratorData:D79A95",
    "Encrypted Info Capsule",
    "StaticInstance=100361",
    "Action=146",
    "type=PickUp",
    "UseItemOnItem",
    "Action=59",
    "QuestMessage { Action=Delete",
    "Received reward",
]:
    print(line, "->", events.count(line) if line in events or True else 0)

# spawn player
m = re.search(r"PlayfieldId=1492999[^\n]*Position=\(([^)]+)\)[^\n]*Name=\"Engynera\"", events)
print("\nspawn", m.group(1) if m else None)
m = re.search(r"Identity=\(Terminal:57AC323C\)[^\n]*Position=\(([^)]+)\)", events)
print("capsule pos from detail", m.group(1) if m else None)
# better
m = re.search(r"name=Encrypted Info Capsule pos=\(([^)]+)\)", events)
print("capsule dynel", m.group(1) if m else None)

# offer icon confirm
m = re.search(r"mission=\(Mission:556D76DD\)[^\n]*icon=(\d+)[^\n]*", replay)
print("accepted offer icon", m.group(1) if m else None)
m = re.search(r"mission=\(Mission:556D76DD\)[^\n]*credits=(\d+) xp=(\d+)", replay)
print("rewards", m.groups() if m else None)
