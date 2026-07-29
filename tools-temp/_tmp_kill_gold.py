from pathlib import Path
import re

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-211947")
replay_path = cap / "mission-flow.replay.log"
events_path = cap / "events.log"
out = []

def section(title):
    out.append("\n=== " + title + " ===")

if not replay_path.exists():
    out.append("NO REPLAY YET")
else:
    replay = replay_path.read_text(encoding="utf-8", errors="replace")
    section("ACCEPT / OFFERS icon=11330 Kill")
    for m in re.finditer(r"\[IN-MISSION-OFFER\][^\n]*icon=11330[^\n]*", replay):
        out.append(m.group(0)[:900])
        break
    for m in re.finditer(r"\[OUT-CREATE-QUEST\][^\n]*", replay):
        out.append("CREATE: " + m.group(0)[:400])
    for m in re.finditer(r"\[IN-MISSION-ACCEPT[^\]]*\][^\n]*", replay):
        out.append("ACCEPT: " + m.group(0)[:500])
    for m in re.finditer(r"\[IN-MISSION-QUEST\][^\n]*icon=11330[^\n]*", replay):
        out.append("QUEST: " + m.group(0)[:700])
        break
    for m in re.finditer(r"\[IN-MISSION-KEY\][^\n]*", replay):
        out.append("KEY: " + m.group(0)[:400])
        if len([x for x in out if x.startswith("KEY:")]) >= 2:
            break

events = events_path.read_text(encoding="utf-8", errors="replace") if events_path.exists() else ""
section("INSTANCE / TARGET / FINISH COUNTS")
for s in [
    "PLAYFIELD-INIT]", "ACGBuildingGeneratorData", "icon=11330", "Suzie", "Kill",
    "Action=59", "QuestMessage { Action=Delete", "Received reward", "MissionTarget",
    "type=Attack", "Death", "Corpse",
]:
    out.append(f"{s}: {events.count(s)}")

section("SPAWN / KILL TARGET SNIPPETS")
# find playfield init
for m in re.finditer(r"\[PLAYFIELD-INIT\][^\n]*", events):
    out.append(m.group(0)[:300])
for m in re.finditer(r"ACGBuildingGeneratorData:[A-F0-9]+", events):
    out.append(m.group(0))
    break
# dynel names that look like kill targets near mission
for pat in [r"name=Suzie[^\n]*", r"DYNEL-SPAWNED\][^\n]*SimpleChar[^\n]*", r"Received reward[^\n]*", r"Action=59[^\n]*", r"QuestMessage \{ Action=Delete[^\n]*"]:
    hits = list(re.finditer(pat, events))
    out.append(f"{pat} hits={len(hits)}")
    for h in hits[:3]:
        out.append(h.group(0)[:350])

# accepted mission target name from offer text
m = re.search(r"icon=11330[^\n]*description=\"([^\"]+)\"", replay if replay_path.exists() else "")
if m:
    out.append("DESC: " + m.group(1)[:500])

Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_kill_gold.txt").write_text("\n".join(out), encoding="utf-8")
print("wrote", len(out), "lines")
