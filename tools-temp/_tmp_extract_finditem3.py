from pathlib import Path
import re

events = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\events.log").read_text(encoding="utf-8", errors="replace")

# Track 100361 / Encrypted / cube / loot around instance
print("=== 100361 / Encrypted / Container / corpse ===")
for pat in [
    r"[^\n]*100361[^\n]*",
    r"[^\n]*Encrypted Info[^\n]*",
    r"[^\n]*Mission Cube[^\n]*",
    r"[^\n]*165839[^\n]*",
    r"[^\n]*100010[^\n]*",
    r"\[DYNEL-SPAWNED\][^\n]*Container[^\n]*",
    r"\[DYNEL-SPAWNED\][^\n]*Terminal[^\n]*",
    r"ChestFullUpdate[^\n]*1492999[^\n]*",
    r"PlayfieldId=1492999[^\n]*Name=\"[^\"]+\"[^\n]*",
]:
    hits = list(re.finditer(pat, events))
    print(f"\n## {pat} ({len(hits)})")
    for h in hits[:12]:
        print(h.group(0)[:400])

# Player spawn on 1492999
print("\n=== Engynera on 1492999 ===")
for h in re.finditer(r"PlayfieldId=1492999[^\n]*Name=\"Engynera\"[^\n]*", events):
    print(h.group(0)[:500])
    break

# Finish sequence detail around Action 47
print("\n=== Finish sequence lines ===")
lines = events.splitlines()
for i, line in enumerate(lines):
    if "Action=59" in line and "556D76DE" in line:
        for j in range(i - 5, min(len(lines), i + 40)):
            if any(k in lines[j] for k in ["Action=", "Quest", "FormatFeedback", "TemplateAction", "Despawn", "ContainerAdd", "Cash", "100361", "57AC323C", "F6F2DD", "Received", "token", "UseItemOnItem"]):
                print(lines[j][:320])
        break

# Icon constants usage in accepted quest full
print("\n=== QuestFull icon for accepted ===")
for h in re.finditer(r"IN-QUEST-FULL[^\n]*556D76DE[^\n]*", events):
    print(h.group(0)[:800])
for h in re.finditer(r"QuestFullUpdate[^\n]*556D76DE[^\n]*", events):
    print(h.group(0)[:800])
for h in re.finditer(r"icon=11329[^\n]*556D76[^\n]*", Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\mission-flow.replay.log").read_text(encoding='utf-8', errors='replace')):
    print(h.group(0)[:500])
