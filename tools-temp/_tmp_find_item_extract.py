# Extract Find-Item mission capture evidence.
from __future__ import print_function
import csv, collections, os, re

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-mission-find-item"
OUT = r"tools-temp\_tmp_find_item_extract.txt"

def read_csv(name):
    path = os.path.join(CAP, name)
    if not os.path.exists(path):
        return []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        return list(csv.DictReader(f))

def read_lines(name, limit=None):
    path = os.path.join(CAP, name)
    if not os.path.exists(path):
        return []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()
    return lines if limit is None else lines[:limit]

out = []
def p(s=""):
    out.append(s)

# Session / playfields
p("=== SESSION ===")
for name in ("capture-session.json", "capture_info.json", "capture-health.json"):
    path = os.path.join(CAP, name)
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            data = f.read()
        p("%s (%d bytes)" % (name, len(data)))
        p(data[:1500])
        p()

# Mission flow
p("=== MISSION-FLOW (all) ===")
for line in read_lines("mission-flow.log"):
    p(line.rstrip())

# Playfield from SCFU / enemy
scfu = read_csv("scfu-appearance.csv")
enemy = read_csv("enemy-full-updates.csv")
p("\n=== PLAYFIELDS (scfu) ===")
pfs = collections.Counter()
for r in scfu:
    for k in ("playfield", "Playfield", "pf", "resource", "ResourceId", "realmId"):
        if k in r and r[k]:
            pfs[r[k]] += 1
p("scfu cols: %s" % (list(scfu[0].keys()) if scfu else []))
p("pf counts sample: %s" % pfs.most_common(20))

p("\n=== ENEMY FULL UPDATE NAMES/LEVELS ===")
p("enemy cols: %s" % (list(enemy[0].keys()) if enemy else []))
names = collections.Counter()
levels = collections.Counter()
mds = collections.Counter()
for r in enemy:
    n = r.get("name") or r.get("Name") or ""
    if n:
        names[n] += 1
    lv = r.get("level") or r.get("Level") or ""
    if lv:
        levels[lv] += 1
    md = r.get("monsterData") or r.get("MonsterData") or r.get("md") or ""
    if md:
        mds[md] += 1
p("names: %s" % names.most_common(40))
p("levels: %s" % levels.most_common(20))
p("monsterData: %s" % mds.most_common(20))

# NPC lifecycle for containers / find item
p("\n=== NPC-LIFECYCLE interesting ===")
life = read_csv("npc-lifecycle.csv")
p("lifecycle cols: %s" % (list(life[0].keys()) if life else []))
interesting = []
for r in life:
    blob = " ".join((r.get(k) or "") for k in r)
    if re.search(r"(?i)cube|item|chest|barrel|treasure|garbage|crate|bottle|android|skeleton|door|container|find|radioactive|capsule|isotope", blob):
        interesting.append(r)
p("interesting rows: %d" % len(interesting))
for r in interesting[:80]:
    p(str({k: r[k] for k in r if r[k]}))

# Inventory / find item take
p("\n=== INVENTORY (find/item hints) ===")
inv = read_csv("inventory-updates.csv")
p("inv cols: %s" % (list(inv[0].keys()) if inv else []))
for r in inv:
    blob = " ".join((r.get(k) or "") for k in r)
    if re.search(r"(?i)radioactive|capsule|isotope|encrypted|mission|11329|11337|find", blob):
        p(str({k: r[k] for k in r if r[k]}))

# Interactions / use
p("\n=== NPC-INTERACTIONS ===")
for line in read_lines("npc-interactions.log"):
    if re.search(r"(?i)use|loot|container|cube|item|chest|door|find", line):
        p(line.rstrip())

# System messages
p("\n=== SYSTEM-MESSAGES (mission/token/reward) ===")
for line in read_lines("system-messages.log"):
    if re.search(r"(?i)mission|token|reward|possibility|find|complete", line):
        p(line.rstrip())

# Corpse / chest
p("\n=== CORPSE / CHEST CSV counts ===")
for name in ("corpse-full-updates.csv", "corpse-loot-observations.csv"):
    rows = read_csv(name)
    p("%s rows=%d cols=%s" % (name, len(rows), list(rows[0].keys()) if rows else []))

# Events with Door / Chest / Container
p("\n=== EVENTS.log Door/Chest/Container/SCFU mission pf ===")
door_n = chest_n = cont_n = 0
pf_hits = collections.Counter()
name_hits = collections.Counter()
for line in read_lines("events.log"):
    if "DoorFullUpdate" in line or "Door " in line:
        door_n += 1
    if "ChestFullUpdate" in line or "Chest" in line:
        chest_n += 1
    if "Container" in line:
        cont_n += 1
    m = re.search(r"playfield[=:]?\s*(\d{5,})", line, re.I)
    if m:
        pf_hits[m.group(1)] += 1
    m2 = re.search(r"name[=:]?\s*([A-Za-z0-9][A-Za-z0-9 '\-]{1,40})", line, re.I)
    if m2 and re.search(r"(?i)cube|barrel|treasure|garbage|crate|bottle|android|skeleton|rift|chest|door|isotope|capsule", m2.group(1)):
        name_hits[m2.group(1)] += 1
p("DoorFullUpdate-ish=%d Chest=%d Container=%d" % (door_n, chest_n, cont_n))
p("pf hits: %s" % pf_hits.most_common(15))
p("name hits: %s" % name_hits.most_common(40))

# Raw packets sample for mission icon / find item
p("\n=== MISSION FLOW / CHAT related ===")
for line in read_lines("chat-dialogue.log")[:50]:
    p(line.rstrip())

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("wrote", OUT, "lines", len(out))
