# Analyze 20260719-5-different-shape-fo-mish for mission instance content.
from __future__ import print_function
import csv
import json
import os
import collections
import re

CAP = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-5-different-shape-fo-mish"
OUT = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_mission_shapes_analysis.txt"

lines = []
def p(s=""):
    lines.append(s)

# --- mission-flow: instance enters ---
p("=== MISSION INSTANCE ENTERS ===")
instances = []
flow = open(os.path.join(CAP, "mission-flow.log"), encoding="utf-8-sig", errors="ignore").read().splitlines()
for line in flow:
    if "PLAYFIELD-INIT" in line and "likelyMissionInstance=True" in line:
        m = re.search(r"pf=(\d+) hex=(0x[0-9A-Fa-f]+)", line)
        if m:
            instances.append({"pf": int(m.group(1)), "hex": m.group(2), "line": line[:160]})
            p(line[:200])
    elif "PLAYFIELD-INIT" in line and "0x15" in line:
        m = re.search(r"pf=(\d+) hex=(0x[0-9A-Fa-f]+)", line)
        if m and int(m.group(1)) > 1000000:
            instances.append({"pf": int(m.group(1)), "hex": m.group(2), "line": line[:160]})
            p("HIGH-BAND " + line[:200])

p("\nunique instance pfs: " + str(sorted(set(i["pf"] for i in instances))))

# --- scfu by playfield ---
p("\n=== SCFU NPCInfo by PlayfieldId (mission band >1e6) ===")
scfu_path = os.path.join(CAP, "scfu-appearance.csv")
by_pf = collections.defaultdict(list)
players = set()
with open(scfu_path, newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        pf = int(r.get("PlayfieldId") or "0")
        ct = (r.get("CharacterInfoType") or "").strip()
        name = (r.get("Name") or "").strip()
        if ct == "PlayerInfo":
            players.add(name)
            continue
        if ct != "NPCInfo" or not name:
            continue
        if pf < 1000000:
            continue
        md = int(r.get("MonsterData") or "0")
        vf = int(r.get("VisualFlags") or "0")
        if md <= 0 or vf == 127:
            continue
        ident = r.get("Identity") or ""
        by_pf[pf].append(r)

for pf in sorted(by_pf.keys()):
    rows = by_pf[pf]
    # unique by identity
    seen = {}
    for r in rows:
        ident = r.get("Identity") or ""
        if ident not in seen:
            seen[ident] = r
    p("\n--- PF %d (%d unique NPCs) ---" % (pf, len(seen)))
    names = collections.Counter((r.get("Name") or "").strip() for r in seen.values())
    for n, c in sorted(names.items(), key=lambda x: (-x[1], x[0])):
        p("  %3d %s" % (c, n))
    # detail each unique
    for ident, r in sorted(seen.items(), key=lambda kv: (kv[1].get("Name") or "", kv[0])):
        p("  %s md=%s lv=%s hp=%s scale=%s vf=%s head=%s pos=(%s,%s,%s) tex=%s mesh=%s" % (
            r.get("Name"),
            r.get("MonsterData"),
            r.get("Level"),
            r.get("Health"),
            r.get("MonsterScale"),
            r.get("VisualFlags"),
            r.get("HeadMesh"),
            r.get("PositionX"),
            r.get("PositionY"),
            r.get("PositionZ"),
            (r.get("Textures") or "")[:80],
            (r.get("Meshes") or "")[:80],
        ))

# --- corpse loot ---
p("\n=== CORPSE LOOT OBSERVATIONS ===")
loot_path = os.path.join(CAP, "corpse-loot-observations.csv")
if os.path.exists(loot_path):
    with open(loot_path, newline="", encoding="utf-8-sig") as f:
        for i, r in enumerate(csv.DictReader(f)):
            if i > 40:
                p("... truncated")
                break
            p(str(dict(r))[:300])

# --- doors / chests from raw packets N3 types ---
p("\n=== RAW PACKET N3 TYPE COUNTS (IN) ===")
type_counts = collections.Counter()
doorish = []
chestish = []
with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("Direction") or "").upper() != "IN":
            continue
        n3 = (r.get("N3TypeName") or "").strip()
        type_counts[n3] += 1
        low = n3.lower()
        if "door" in low or "chest" in low or "container" in low or "statel" in low or "playfield" in low:
            if len(doorish) < 5 or "Door" in n3 or "Chest" in n3 or "Treasure" in n3:
                pass
        if any(k in n3 for k in ("Door", "Chest", "Treasure", "Container", "Statel", "PlayfieldAnon", "PlayfieldAnarchy", "TemplateAction", "Despawn")):
            if len(doorish) < 200:
                doorish.append((n3, r.get("CapturedUtc"), r.get("PacketLength")))

for n3, c in type_counts.most_common(40):
    p("%5d %s" % (c, n3))

p("\n=== DOOR/CHEST/CONTAINER-ISH PACKET SAMPLES ===")
interesting = [x for x in doorish if any(k in x[0] for k in ("Door", "Chest", "Treasure", "Container", "Statel"))]
for x in interesting[:50]:
    p("%s utc=%s len=%s" % x)

# --- enemy-dossier top names ---
p("\n=== ENEMY DOSSIER (if present) ===")
dossier = os.path.join(CAP, "enemy-dossier.json")
if os.path.exists(dossier):
    data = json.load(open(dossier, encoding="utf-8-sig"))
    # structure unknown — print keys
    if isinstance(data, dict):
        p("keys: " + str(list(data.keys())[:30]))
        ents = data.get("entities") or data.get("enemies") or data.get("dossier") or data
        if isinstance(ents, dict):
            p("entity_count " + str(len(ents)))
            for k, v in list(ents.items())[:20]:
                name = v.get("name") if isinstance(v, dict) else None
                p("  %s name=%s" % (k, name))
        elif isinstance(ents, list):
            p("list_count " + str(len(ents)))

# --- npc-lifecycle names with Broken/Boss/Target/Chest ---
p("\n=== NPC LIFECYCLE notable names ===")
notable_re = re.compile(r"Broken|Boss|Target|Chest|Treasure|Door|Machine|Kill|Find|Mission|Elite|Captain|Leader", re.I)
seen_names = collections.Counter()
with open(os.path.join(CAP, "npc-lifecycle.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        name = (r.get("Name") or "").strip()
        detail = r.get("Detail") or ""
        if name:
            seen_names[name] += 1
        if notable_re.search(name) or notable_re.search(detail):
            p("%s | %s" % (name, (detail or "")[:180]))

p("\n=== TOP NPC NAMES (lifecycle) ===")
for n, c in seen_names.most_common(60):
    p("%4d %s" % (c, n))

open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
print("wrote", OUT, "lines", len(lines))
