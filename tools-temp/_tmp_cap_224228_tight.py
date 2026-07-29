# Tight brief: doors/chests/complete/key around two Find Person finishes.
from __future__ import print_function
import collections, os, re

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-224228"
OUT = r"tools-temp\_tmp_cap_224228_tight.txt"
EV = os.path.join(CAP, "events.log")
HEX = os.path.join(CAP, "packets.hex.log")
NPC = os.path.join(CAP, "npc-interactions.log")

out = []
def p(s=""):
    out.append(s)

# Count Door/Chest per playfield identity in events
door_by_pf = collections.Counter()
chest_by_pf = collections.Counter()
door_times = []
anarchy = []
with open(EV, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if "DoorFullUpdate" in line:
            door_times.append(line[:32])
            m = re.search(r"Playfield2:([0-9A-Fa-f]+)", line)
            # often not on same line — count total
            door_by_pf["all"] += 1
        if "ChestFullUpdate" in line:
            chest_by_pf["all"] += 1
        if "PlayfieldAnarchyF" in line:
            anarchy.append(line.rstrip()[:260])
        if "InfoRequest" in line or "Jeanne" in line or "Lanny" in line or "Messamore" in line or "Marsalis" in line:
            p("EV " + line.rstrip()[:280])
        if "Action=47" in line or "Action = 47" in line or "Parameter1=113" in line or "Parameter1=0x71" in line:
            p("CA47 " + line.rstrip()[:280])
        if "QuestMessage" in line and "Delete" in line:
            p("QDEL " + line.rstrip()[:280])
        if "Received reward" in line or "FormatFeedback" in line and "reward" in line.lower():
            p("REW " + line.rstrip()[:280])

p("\nDoorFullUpdate total lines=%d Chest=%d" % (door_by_pf["all"], chest_by_pf["all"]))
p("Anarchy event lines:")
for a in anarchy:
    p("  " + a)

# Hex door batches: timestamps + unique door ids around enters
p("\n=== HEX DoorFullUpdate first 30 of each enter window ===")
enter_windows = [
    ("enter1", "2026-07-24T20:42:46", "2026-07-24T20:43:30"),
    ("enter2", "2026-07-24T20:48:35", "2026-07-24T20:49:20"),
    ("finish1", "2026-07-24T20:44:00", "2026-07-24T20:44:20"),
    ("finish2", "2026-07-24T20:51:45", "2026-07-24T20:52:05"),
]
doors = collections.defaultdict(set)
chests = collections.defaultdict(set)
infos = collections.defaultdict(list)
cas = collections.defaultdict(list)
with open(HEX, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        ts = line[:26] if len(line) > 26 else ""
        for name, a, b in enter_windows:
            if ts >= a and ts <= b:
                if "DoorFullUpdate" in line:
                    m = re.search(r"Door:([0-9A-Fa-f]+)", line)
                    if not m:
                        m = re.search(r"C748([0-9A-Fa-f]{8})", line)  # type Door + inst in hex often
                    # extract identity from hex after C748
                    hm = re.search(r"0000C748([0-9A-Fa-f]{8})", line)
                    if hm:
                        doors[name].add(hm.group(1))
                    elif "DoorFullUpdate" in line:
                        doors[name].add(line[40:80])
                if "ChestFullUpdate" in line:
                    hm = re.search(r"0000C74[89A-Fa-f]([0-9A-Fa-f]{8})", line)
                    chests[name].add(hm.group(0) if hm else line[40:80])
                if "InfoRequest" in line:
                    infos[name].append(line.rstrip()[:240])
                if "CharacterAction" in line and ("0000002F" in line.upper() or "Action=47" in line):
                    cas[name].append(line.rstrip()[:240])
                if name.startswith("finish") and ("Quest" in line or "InfoRequest" in line or "CharacterAction" in line):
                    if any(x in line for x in ("InfoRequest", "QuestMessage", "Quest ", "CharacterAction", "FormatFeedback")):
                        if len(infos.get(name+"_extra", [])) < 40:
                            infos.setdefault(name+"_extra", []).append(line.rstrip()[:260])

for name, a, b in enter_windows:
    p("%s doors_unique~=%d chests~=%d infos=%d" % (name, len(doors[name]), len(chests[name]), len(infos[name])))
    for x in infos[name][:8]:
        p("  INFO " + x)
    for x in infos.get(name+"_extra", [])[:25]:
        p("  EXTRA " + x)
    for x in cas[name][:8]:
        p("  CA " + x)

# npc interactions around finishes
p("\n=== NPC interactions around finishes ===")
with open(NPC, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        ts = line[:26] if len(line) > 26 else ""
        if (("20:44:0" in ts or "20:51:5" in ts or "20:43:5" in ts or "20:51:4" in ts)
            and re.search(r"(?i)info|use|look|interact|Jeanne|Lanny|Messamore|Marsalis|79925C97|79925C9E", line)):
            p(line.rstrip()[:300])

# SCFU for Jeanne / Lanny full row
p("\n=== scfu-appearance Jeanne/Lanny ===")
import csv
with open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig", errors="replace") as f:
    for r in csv.DictReader(f):
        name = r.get("Name") or ""
        if name in ("Jeanne Messamore", "Lanny Marsalis"):
            p("%s pf=%s id=%s lvl=%s md=%s side=%s xyz=(%s,%s,%s)" % (
                name, r.get("PlayfieldId"), r.get("Identity"), r.get("Level"), r.get("MonsterData"),
                r.get("Side"), r.get("PositionX"), r.get("PositionY"), r.get("PositionZ")))
            p("  tex=%s" % (r.get("Textures") or "")[:160])
            p("  mesh=%s" % (r.get("Meshes") or "")[:160])
            p("  head=%s flags=%s" % (r.get("HeadMesh"), r.get("Flags") or r.get("VisualFlags")))

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("Wrote", OUT, "n=", len(out))
