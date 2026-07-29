# Brief for 20260724-224228 — two Find Person enters.
from __future__ import print_function
import csv, collections, os, re, struct

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-224228"
OUT = r"tools-temp\_tmp_cap_224228_brief.txt"
PFS = ("1460226", "1456133")  # from mission-flow

def rows(name):
    path = os.path.join(CAP, name)
    if not os.path.exists(path):
        return []
    with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
        return list(csv.DictReader(f))

def lines(name):
    path = os.path.join(CAP, name)
    if not os.path.exists(path):
        return []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        return f.readlines()

out = []
def p(s=""):
    out.append(s)

p("=== MISSION-FLOW (teleport/quest/key) ===")
for line in lines("mission-flow.log"):
    p(line.rstrip())

p("\n=== SYSTEM (find/complete/token) ===")
for line in lines("system-messages.log"):
    if re.search(r"(?i)find|contact|informant|mission|token|complete|assignment|reward|percent", line):
        p(line.rstrip()[:240])

# Quest / CharacterAction / InfoRequest / Door from events.log
p("\n=== EVENTS quest/info/door/charaction/anarchy ===")
for line in lines("events.log"):
    if re.search(r"(?i)Quest|InfoRequest|DoorFull|ChestFull|PlayfieldAnarchy|CharacterAction|GenericCmd|CreateQuest", line):
        p(line.rstrip()[:300])

scfu = rows("scfu-appearance.csv")
pfs = collections.Counter((r.get("PlayfieldId") or "?") for r in scfu)
p("\n=== SCFU PF counts ===")
p(str(pfs.most_common(20)))

for PF in PFS:
    by_id = {}
    for r in scfu:
        if (r.get("PlayfieldId") or "") != PF:
            continue
        ident = r.get("Identity") or ""
        if ident and ident not in by_id:
            by_id[ident] = r
    p("\n=== PF %s unique SCFU n=%d ===" % (PF, len(by_id)))
    # levels + names summary
    levels = []
    names = collections.Counter()
    findish = []
    for ident, r in sorted(by_id.items(), key=lambda kv: (kv[1].get("Name") or "", kv[0])):
        name = r.get("Name") or "?"
        lvl = r.get("Level") or ""
        md = r.get("MonsterData") or ""
        x, y, z = r.get("PositionX"), r.get("PositionY"), r.get("PositionZ")
        side = r.get("Side") or ""
        tex = r.get("Textures") or ""
        meshes = r.get("Meshes") or ""
        names[name] += 1
        try:
            levels.append(int(float(lvl)))
        except Exception:
            pass
        # likely contacts: low side / non-monster names / Find-ish
        if re.search(r"(?i)informant|contact|agent|officer|civilian|technician|scientist|engineer|broker|courier|guard|captain|lieutenant|kade|arnall|person", name) \
           or (side and side not in ("0", "1", "Monster", "4")):
            findish.append((name, ident, lvl, md, x, y, z, side, tex[:60], (meshes or "")[:60]))
        p("  %s id=%s lvl=%s md=%s xyz=(%s,%s,%s) side=%s" % (name, ident, lvl, md, x, y, z, side))
    if levels:
        p("  LEVEL min=%d max=%d median=%d n=%d" % (
            min(levels), max(levels), sorted(levels)[len(levels)//2], len(levels)))
    p("  NAME TOP: %s" % names.most_common(12))
    if findish:
        p("  FIND-ISH:")
        for row in findish:
            p("    %s" % (row,))

# Enemy combat: first attack distances
p("\n=== COMBAT sample (enemy-combat first 40) ===")
combat = rows("enemy-combat.csv")
p("cols=%s rows=%d" % (list(combat[0].keys()) if combat else [], len(combat)))
for r in combat[:40]:
    p(str({k: r.get(k) for k in list(combat[0].keys())[:12]}))

# Movement first positions per mission PF
p("\n=== MOVEMENT first positions by PF ===")
mov = rows("movement-packets.csv")
seen_pf = set()
for r in mov:
    pf = r.get("PlayfieldId") or ""
    if pf in PFS and pf not in seen_pf:
        seen_pf.add(pf)
        p("first %s utc=%s xyz=(%s,%s,%s) type=%s" % (
            pf, r.get("CapturedUtc"), r.get("PositionX"), r.get("PositionY"), r.get("PositionZ"),
            r.get("MessageType")))
# last per pf
last = {}
for r in mov:
    pf = r.get("PlayfieldId") or ""
    if pf in PFS:
        last[pf] = r
for pf, r in last.items():
    p("last %s utc=%s xyz=(%s,%s,%s)" % (
        pf, r.get("CapturedUtc"), r.get("PositionX"), r.get("PositionY"), r.get("PositionZ")))

# Door / Chest from raw-packets message names if present
p("\n=== RAW door/chest/anarchy counts ===")
raw = rows("raw-packets.csv")
if raw:
    p("raw cols=%s" % list(raw[0].keys())[:20])
    types = collections.Counter()
    for r in raw:
        mt = r.get("MessageType") or r.get("N3MessageType") or r.get("PacketType") or ""
        if re.search(r"(?i)door|chest|anarchy|quest|info|characteraction|teleport", mt):
            types[mt] += 1
    p(str(types.most_common(40)))

# Hex scan for DoorFullUpdate / PlayfieldAnarchyF markers around enter times
p("\n=== HEX scan PlayfieldAnarchy / Door / InfoRequest around enters ===")
# N3 type ids commonly used — just grep text markers in hex log headers if any
hex_path = os.path.join(CAP, "packets.hex.log")
anarchy = 0
door = 0
info = 0
charact = 0
quest_del = 0
with open(hex_path, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if "PlayfieldAnarchy" in line or "AnarchyF" in line:
            anarchy += 1
            if anarchy <= 8:
                p("ANARCHY " + line.rstrip()[:220])
        if "DoorFullUpdate" in line:
            door += 1
            if door <= 5:
                p("DOOR " + line.rstrip()[:220])
        if "InfoRequest" in line:
            info += 1
            if info <= 8:
                p("INFO " + line.rstrip()[:220])
        if "CharacterAction" in line and ("0x2F" in line or "Action=47" in line or "Action=0x2F" in line or "Parameter1=113" in line):
            charact += 1
            if charact <= 10:
                p("CA " + line.rstrip()[:220])
        if "QuestMessage" in line and "Delete" in line:
            quest_del += 1
            if quest_del <= 10:
                p("QDEL " + line.rstrip()[:220])
p("counts anarchy_lines=%d door_lines=%d info_lines=%d ca_keyish=%d qdel=%d" % (
    anarchy, door, info, charact, quest_del))

# npc-lifecycle Find / deaths
p("\n=== NPC-LIFECYCLE sample names on mission PFs ===")
life = rows("npc-lifecycle.csv")
if life:
    p("cols=%s" % list(life[0].keys())[:15])
    for PF in PFS:
        names = collections.Counter()
        for r in life:
            if (r.get("PlayfieldId") or "") != PF:
                continue
            names[r.get("Name") or "?"] += 1
        p("PF %s name events top: %s" % (PF, names.most_common(15)))

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("Wrote", OUT, "lines", len(out))
