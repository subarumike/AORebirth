from pathlib import Path
import re

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-211947")
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")
replay = (cap / "mission-flow.replay.log").read_text(encoding="utf-8", errors="replace")
out = []

# accepted offer for second kill (556E7E44 -> 556E7E47)
for mid in ["556E7E3E", "556E7E41", "556E7E44", "556E7E47"]:
    out.append(f"\n==== {mid} ====")
    for line in replay.splitlines():
        if mid in line and any(x in line for x in ["OFFER", "QUEST", "ACCEPT", "KEY", "ACTION", "CREATE"]):
            out.append(line[:500])

out.append("\n==== INOCENCIA / TARGET DYNELS ====")
for m in re.finditer(r"DYNEL-SPAWNED\][^\n]*Inocencia[^\n]*", events):
    out.append(m.group(0)[:400])
for m in re.finditer(r"DYNEL-SPAWNED\][^\n]*Western[^\n]*", events):
    out.append(m.group(0)[:400])
# names near instance PF
for m in re.finditer(r"DYNEL-SPAWNED\] identity=\(SimpleChar:[0-9A-F]+\) name=([^=\n]+) player=False npc=True[^\n]*pos=\(([^\)]+)\)[^\n]*monsterData=(\d+)", events):
    name, pos, md = m.group(1).strip(), m.group(2), m.group(3)
    if "Inocencia" in name or "Western" in name or "Cordiero" in name or "thief" in name.lower():
        out.append(f"NPC {name} md={md} pos={pos}")

# all npc names in PF 1437698 / 1433600 context - look for death of named
out.append("\n==== DEATHS ====")
for m in re.finditer(r"\[ENEMY[^\]]*\][^\n]*Death[^\n]*|[^\n]*name=[^\n]*alive=False[^\n]*", events):
    s = m.group(0)
    if "Inocencia" in s or "Western" in s or "556E7E" in s:
        out.append(s[:400])

# search Inocencia anywhere
out.append(f"\nInocencia count={events.count('Inocencia')}")
out.append(f"Western count={events.count('Western')}")
idx = events.find("Inocencia")
if idx >= 0:
    out.append(events[max(0,idx-200):idx+400])

# finish sequence around Received reward
idx = events.find("Received reward")
if idx >= 0:
    out.append("\n==== FINISH WINDOW ====")
    out.append(events[max(0,idx-3000):idx+800])

# PLAYFIELD-INIT with ACG
for m in re.finditer(r"PLAYFIELD-INIT\] (\d+)[^\n]*", events):
    out.append("PF " + m.group(0)[:200])

# mission action for kill mission target identity
for line in replay.splitlines():
    if "556E7E47" in line and "ACTION" in line:
        out.append("ACT " + line[:600])

Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_kill_gold2.txt").write_text("\n".join(out), encoding="utf-8")
print("done", len(out))
