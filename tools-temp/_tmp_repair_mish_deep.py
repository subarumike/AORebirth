# Deep extract: containers (locked?), doors/chests hex, Broken Machine, combat damage, roll QL.
from __future__ import print_function
import os, re, collections, csv, json

REPAIR = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-repaair-machine-mish"
ROLL = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-roll-mission-nova"
OUT = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_repair_mish_deep.txt"

def lines(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.readlines()

out = []
def w(s=""):
    out.append(s)

w("=== UNIQUE CONTAINER SPAWNS (first sight) ===")
seen = {}
for line in lines(os.path.join(REPAIR, "events.log")):
    m = re.search(r"\[DYNEL-SPAWNED\] identity=\(Container:([0-9A-Fa-f]+)\) name=(.+?) pos=\(([^)]+)\)", line)
    if not m:
        continue
    inst, name, pos = m.group(1), m.group(2).strip(), m.group(3)
    if inst not in seen:
        seen[inst] = (name, pos, line.strip()[:220])

for inst, (name, pos, sample) in sorted(seen.items(), key=lambda x: x[1][0]):
    w("Container:%s name=%s pos=%s" % (inst, name, pos))

w()
w("=== LOCK / USE / OPEN CONTAINER ===")
for line in lines(os.path.join(REPAIR, "events.log")):
    low = line.lower()
    if "container" in low and any(k in low for k in ("lock", "open", "use", "chest", "treasure", "barrel", "crate", "garbage", "skeleton", "android", "rift")):
        if "DYNEL-SPAWNED" in line:
            continue
        w(line.strip()[:320])

w()
w("=== BROKEN MACHINE / REPAIR ===")
for fname in ("events.log", "npc-interactions.log", "inventory-updates.csv", "mission-flow.log"):
    path = os.path.join(REPAIR, fname)
    if not os.path.exists(path):
        continue
    for line in lines(path):
        if re.search(r"Broken|Repair|Hacker|Machine|UseItemOnItem", line, re.I):
            w("[%s] %s" % (fname, line.strip()[:300]))

w()
w("=== DOOR/CHEST FULLUPDATE COUNTS AFTER ENTER ===")
enter = "2026-07-24T08:39:31"
door = chest = 0
for line in lines(os.path.join(REPAIR, "events.log")):
    if line[:26] < enter:
        continue
    if "DoorFullUpdate" in line:
        door += 1
    if "ChestFullUpdate" in line:
        chest += 1
w("DoorFullUpdate after enter=%d ChestFullUpdate=%d" % (door, chest))

w()
w("=== SAMPLE DOOR/CHEST EVENT LINES ===")
n = 0
for line in lines(os.path.join(REPAIR, "events.log")):
    if line[:26] < enter:
        continue
    if "DoorFullUpdate" in line or "ChestFullUpdate" in line:
        w(line.strip()[:350])
        n += 1
        if n >= 20:
            break

w()
w("=== COMBAT Attack/Health AFTER ENTER ===")
path = os.path.join(REPAIR, "enemy-combat.csv")
with open(path, encoding="utf-8-sig", errors="replace") as f:
    reader = csv.DictReader(f)
    n = 0
    dmg = []
    for row in reader:
        utc = row.get("CapturedUtc") or ""
        if utc < enter:
            continue
        action = row.get("Action") or ""
        amt = row.get("Amount") or ""
        mt = row.get("MessageType") or ""
        if action or "Attack" in mt or "Health" in mt or "Fight" in mt:
            w("%s %s action=%s amt=%s src=%s tgt=%s" % (utc[11:19], mt, action, amt, row.get("SourceIdentity"), row.get("TargetIdentity")))
            if amt.isdigit():
                dmg.append(int(amt))
            n += 1
            if n >= 40:
                break
w("damage samples count=%d min=%s max=%s" % (len(dmg), min(dmg) if dmg else None, max(dmg) if dmg else None))

w()
w("=== ROLL CAPTURE QUEST ALTERNATIVE / QL ===")
for fname in ("mission-flow.log", "events.log"):
    path = os.path.join(ROLL, fname)
    if not os.path.exists(path):
        continue
    for line in lines(path):
        if any(k in line for k in ("QuestAlternative", "ROLL", "CreateQuest", "MissionIcon", "Quality", "11342", "Repair")):
            w("[%s] %s" % (fname, line.strip()[:320]))

w()
w("=== SCFU MISSION MOBS TEXTURE (sample Aquaan/Hellhound/Demon) ===")
path = os.path.join(REPAIR, "scfu-appearance.csv")
with open(path, encoding="utf-8-sig", errors="replace") as f:
    reader = csv.DictReader(f)
    cols = reader.fieldnames or []
    w("has Texture=%s Mesh=%s" % (any("Texture" in c for c in cols), any("Mesh" in c for c in cols)))
    n = 0
    for row in reader:
        name = row.get("Name") or ""
        pf = row.get("PlayfieldId") or ""
        if pf not in ("1419360", "15A860", "0x15A860") and "15A860" not in (row.get("RawBodyHex") or ""):
            if name not in ("Aquaan Vicar", "Aquaan Grunt", "Hellhound", "Demon", "Bileswarm Dominator", "Bileswarm Colossus", "Bileswarm Breeder", "Garbage Flea"):
                continue
        # print compact appearance fields
        keys = [c for c in cols if any(x in c for x in ("Name", "Level", "Monster", "Texture", "Mesh", "Head", "Scale", "Side", "Playfield", "Position", "Health", "Flags", "Visual"))]
        compact = {k: row.get(k) for k in keys[:18]}
        w(str(compact)[:400])
        n += 1
        if n >= 12:
            break

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("wrote", OUT, "lines", len(out))
