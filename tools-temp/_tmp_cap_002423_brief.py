# Brief of L7 mission capture 20260725-002423
import os, re, json
from collections import Counter

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-002423"
OUT = r"tools-temp\_tmp_cap_002423_brief.txt"

def read(name):
    p = os.path.join(CAP, name)
    if not os.path.exists(p):
        return ""
    with open(p, "r", encoding="utf-8", errors="ignore") as f:
        return f.read()

lines = []
def w(s=""):
    lines.append(s)

# session
for n in ("capture_info.json", "capture-session.json", "capture-health.json"):
    t = read(n)
    if t:
        w("=== %s ===" % n)
        w(t[:2000])
        w()

# mission-flow
mf = read("mission-flow.log")
w("=== mission-flow (teleport/quest/key) ===")
for ln in mf.splitlines():
    if any(k in ln for k in ("TELEPORT", "PLAYFIELD", "QUEST", "MISSION-KEY", "CreateQuest", "N3-TELEPORT", "ACG")):
        w(ln)
w()

# system / chat
w("=== system-messages ===")
w(read("system-messages.log")[:8000])
w()
w("=== chat-dialogue ===")
w(read("chat-dialogue.log")[:8000])
w()

# events: doors / anarchy / info / quest
ev = read("events.log")
w("=== events door/anarchy/quest/info/token ===")
for ln in ev.splitlines():
    low = ln.lower()
    if any(k in ln for k in ("DoorFull", "PlayfieldAnarchy", "Quest", "InfoRequest", "Token", "FormatFeedback", "ChestFull", "SpecialAttack", "AttackMessage")):
        w(ln)
w()

# packets.hex door counts at enter
hexlog = read("packets.hex.log")
door_times = []
paf_times = []
scfu = []
for ln in hexlog.splitlines():
    if "DoorFullUpdate" in ln:
        door_times.append(ln[:80])
    if "PlayfieldAnarchy" in ln:
        paf_times.append(ln[:120])
    if "SimpleCharFullUpdate" in ln and len(scfu) < 5:
        scfu.append(ln[:100])

w("=== DoorFullUpdate count=%d first20 ===" % len(door_times))
for x in door_times[:20]:
    w(x)
w("... last10 ...")
for x in door_times[-10:]:
    w(x)
w()
w("=== PlayfieldAnarchy count=%d ===" % len(paf_times))
for x in paf_times[:5]:
    w(x)
w()

# enemy combat summary
ec = read("enemy-combat.csv")
w("=== enemy-combat.csv head ===")
for i, ln in enumerate(ec.splitlines()[:40]):
    w(ln)
w()

# fight events
w("=== enemy-fight-events (first 80 lines) ===")
for i, ln in enumerate(read("enemy-fight-events.log").splitlines()[:80]):
    w(ln)
w()

# npc lifecycle names/levels
w("=== npc-lifecycle sample ===")
for i, ln in enumerate(read("npc-lifecycle.csv").splitlines()[:50]):
    w(ln)
w()

# inventory for token
w("=== inventory token/key hits ===")
for ln in read("inventory-updates.csv").splitlines():
    if any(k in ln.lower() for k in ("token", "mission", "key", "reward")):
        w(ln)
w()

# dossier
ed = read("enemy-dossier.json")
if ed:
    w("=== enemy-dossier.json (trim) ===")
    w(ed[:4000])

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print("wrote", OUT, "lines", len(lines))
