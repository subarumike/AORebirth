import csv
import json
import os
import re
from collections import Counter, defaultdict

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-mp-pets"
OUT = r"tools-temp\_mp_pets_extract.txt"

lines = []
def w(s=""):
    lines.append(s)

info = json.load(open(os.path.join(CAP, "capture_info.json"), encoding="utf-8-sig"))
w("char=%s pf=%s duration=%s" % (info.get("characterName"), info.get("playfieldId"), info.get("sessionDurationSeconds")))
w("scfu raw=%s decoded=%s" % (info.get("packetCounts",{}).get("rawSimpleCharFullUpdatePackets"), info.get("packetCounts",{}).get("rawSimpleCharFullUpdateDecoded")))

scfu = list(csv.DictReader(open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig")))
w("\n=== SCFU %d ===" % len(scfu))
for r in scfu:
    w("%s | %s | lvl=%s hp=%s md=%s scale=%s family=%s flags=%s owner=%s textures=%s meshes=%s pos=(%s,%s,%s)" % (
        r.get("Identity"), r.get("Name"), r.get("Level"), r.get("Health"), r.get("MonsterData"),
        r.get("MonsterScale"), r.get("NpcFamily"), r.get("Flags"), r.get("Owner"),
        r.get("Textures"), r.get("Meshes"), r.get("PositionX"), r.get("PositionY"), r.get("PositionZ")))

# events: cast/finish/shell/pet
ev = open(os.path.join(CAP, "events.log"), encoding="utf-8-sig", errors="replace")
pat = re.compile(
    r"CastNano|FinishNano|CastNanoSpell|TemplateAction|ContainerAdd|GenericCmd|Shell|"
    r"AddPet|SetPet|PetCommand|SpellList|SimpleCharFullUpdate|DYNEL|CHAR-SEEN|"
    r"Name=\"|Parameter2=|pet=True|IsPet|Summon|Overflow|DeleteItem",
    re.I,
)
w("\n=== KEY EVENTS ===")
hits = 0
casts = []
shells = []
for i, line in enumerate(ev, 1):
    if "CurrentNano=" in line and not re.search(r"CastNano|FinishNano|Shell|Pet|Template|GenericCmd|AddPet", line, re.I):
        continue
    if not pat.search(line):
        continue
    hits += 1
    clip = line.rstrip()
    if len(clip) > 900:
        clip = clip[:900] + "..."
    w("%d: %s" % (i, clip))
    m = re.search(r"Action=CastNano .*?Parameter2=(\d+).*?Target=\(([^)]+)\)", clip)
    if not m:
        m = re.search(r"Action=CastNano .*?Target=\(([^)]+)\).*?Parameter2=(\d+)", clip)
        if m:
            casts.append(("cast", int(m.group(2)), m.group(1), i))
    else:
        casts.append(("cast", int(m.group(1)), m.group(2), i))
    m = re.search(r"FinishNanoCasting .*?Parameter2=(\d+)", clip)
    if m:
        casts.append(("finish", int(m.group(1)), "", i))
    m = re.search(r"TemplateActionMessage \{ ItemLowId=(\d+) ItemHighId=(\d+) Quality=(\d+).*?Placement=\(([^)]+)\)", clip)
    if m:
        shells.append((int(m.group(1)), int(m.group(2)), int(m.group(3)), m.group(4), i, clip[:200]))

w("\n=== CAST/FINISH SUMMARY ===")
for c in casts:
    w("%s nano=%s target=%s line=%s" % c)
w("\n=== TEMPLATE ACTIONS ===")
for s in shells:
    w("low=%s high=%s ql=%s place=%s line=%s" % (s[0], s[1], s[2], s[3], s[4]))

# chat
chat = open(os.path.join(CAP, "chat-dialogue.log"), encoding="utf-8-sig", errors="replace").read()
w("\n=== CHAT ===")
w(chat[:4000])

open(OUT, "w", encoding="utf-8").write("\n".join(lines))
print("wrote", OUT, "events", hits, "scfu", len(scfu), "casts", len(casts))
print("\n".join(lines[:80]))
print("...")
print("\n".join([l for l in lines if l.startswith("cast") or l.startswith("finish") or l.startswith("low=") or "SCFU" in l or l.startswith("char=")][:60]))
