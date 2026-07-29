from pathlib import Path
import re
events = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-211947\events.log").read_text(encoding="utf-8", errors="replace")
replay = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-211947\mission-flow.replay.log").read_text(encoding="utf-8", errors="replace")

# second accepted offer full text
m = re.search(r"mission=\(Mission:556E7E44\)[^\n]*", replay)
print("OFFER44:", (m.group(0)[:800] if m else None))
m = re.search(r"quest=\(Mission:556E7E47\)[^\n]*icon=11330[^\n]*", replay)
print("QUEST47:", (m.group(0)[:800] if m else None))

# extract kill target name from longinfo
m = re.search(r"556E7E47[^\n]*LongInfo=\"([^\"]+)\"", replay)
if not m:
    m = re.search(r"acceptedQuest=\(Mission:556E7E47\)[^\n]*description=\"([^\"]+)\"", replay)
# from QUEST line
for line in replay.splitlines():
    if "556E7E47" in line and "icon=11330" in line and "description=" in line:
        mm = re.search(r'description="([^"]+)"', line)
        if mm:
            print("DESC47:", mm.group(1)[:400])
        break

# NPCs in instance around 1433600 / 1437698 - look enemy-state or dynel with level matching
# Find death then reward timing
idx = events.find("Received reward: 0 XP, 4386")
print("reward idx", idx)
chunk = events[max(0,idx-5000):idx]
# last deaths / corpse before reward
for line in chunk.splitlines()[-80:]:
    if any(x in line for x in ["Death", "alive=False", "Corpse", "DYNEL-SPAWNED", "AttackInfo", "FormatFeedback", "Action=59", "QuestMessage"]):
        if "CurrentNano" in line or "StatMessage" in line:
            continue
        print(line[:250])

# search names that died - enemy-state.csv
enemy = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-211947\enemy-state.csv")
if enemy.exists():
    text = enemy.read_text(encoding="utf-8", errors="replace")
    print("enemy lines", text.count("\n"))
    for line in text.splitlines():
        if "dead" in line.lower() or "Death" in line or "killed" in line.lower():
            print(line[:300])
