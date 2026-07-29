# Deep extract: doors/chests per mission PF, Broken Machine, loot item ids, named targets.
from __future__ import print_function
import csv
import os
import struct
import collections
import re

CAP = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-5-different-shape-fo-mish"
OUT = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_mission_shapes_deep.txt"
lines = []
def p(s=""):
    lines.append(s)

# Map utc windows per instance from mission-flow
# 1419310: 03:33:19 - 03:37:12 (first), and 03:48:41 - end
# 1419382: 03:37:26 - ~03:40:19
# 1419335: 03:40:38 - ~03:48:?

windows = [
    (1419310, "2026-07-19T03:33:19", "2026-07-19T03:37:12"),
    (1419382, "2026-07-19T03:37:26", "2026-07-19T03:40:38"),
    (1419335, "2026-07-19T03:40:38", "2026-07-19T03:48:41"),
    (1419310, "2026-07-19T03:48:41", "2026-07-19T99:99:99"),
]

def in_window(utc, start, end):
    return start <= utc <= end

def pf_for_utc(utc):
    for pf, s, e in windows:
        if in_window(utc, s, e):
            return pf
    return None

# Broken / Machine / Treasure in lifecycle
p("=== LIFECYCLE Broken/Machine/Chest/Treasure/Door ===")
with open(os.path.join(CAP, "npc-lifecycle.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        blob = ((r.get("Name") or "") + " " + (r.get("Detail") or "")).lower()
        if any(k in blob for k in ("broken", "machine", "chest", "treasure", "door", "terminal")):
            p("%s | %s | %s" % (r.get("CapturedUtc","")[:19], r.get("Name"), (r.get("Detail") or "")[:200]))

# Door/Chest packets by window — extract hex + playfield from body
# DoorFullUpdate typically embeds playfield id; look for 0015A82E etc.
PF_HEX = {
    0x15A82E: 1419310,
    0x15A876: 1419382,
    0x15A847: 1419335,
}

door_by_pf = collections.defaultdict(list)
chest_by_pf = collections.defaultdict(list)
with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("Direction") or "").upper() != "IN":
            continue
        n3 = (r.get("N3TypeName") or "").strip()
        if n3 not in ("DoorFullUpdate", "ChestFullUpdate"):
            continue
        hexraw = (r.get("RawHex") or "").strip()
        if not hexraw:
            continue
        # find which mission pf is embedded
        found = None
        for hx, pf in PF_HEX.items():
            needle = "%08X" % hx
            if needle in hexraw.upper():
                found = pf
                break
        if found is None:
            # fallback to time window
            found = pf_for_utc(r.get("CapturedUtc") or "")
        if found is None:
            continue
        entry = {
            "utc": r.get("CapturedUtc"),
            "len": int(r.get("PacketLength") or 0),
            "hex": hexraw,
            "ident": r.get("IdentityInstance"),
        }
        if n3 == "DoorFullUpdate":
            door_by_pf[found].append(entry)
        else:
            chest_by_pf[found].append(entry)

for pf in sorted(set(list(door_by_pf.keys()) + list(chest_by_pf.keys()))):
    # dedupe by hex body after header
    doors = door_by_pf[pf]
    chests = chest_by_pf[pf]
    uniq_d = []
    seen = set()
    for d in doors:
        body = d["hex"][32:] if len(d["hex"]) > 32 else d["hex"]  # rough
        key = d["hex"][-80:]  # tail uniqueness
        if key in seen:
            continue
        seen.add(key)
        uniq_d.append(d)
    uniq_c = []
    seen = set()
    for d in chests:
        key = d["hex"][-80:]
        if key in seen:
            continue
        seen.add(key)
        uniq_c.append(d)
    p("\n=== PF %d doors=%d unique~%d chests=%d unique~%d ===" % (
        pf, len(doors), len(uniq_d), len(chests), len(uniq_c)))
    p("door lens: " + str(sorted(collections.Counter(d["len"] for d in uniq_d).items())))
    p("chest lens: " + str(sorted(collections.Counter(d["len"] for d in uniq_c).items())))

# Corpse loot full rows
p("\n=== FULL CORPSE LOOT ===")
loot_path = os.path.join(CAP, "corpse-loot-observations.csv")
with open(loot_path, newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        p("%s name=%s md=%s items=%s dead=%s corpse=%s" % (
            (r.get("CapturedUtc") or "")[:19],
            r.get("EnemyName"),
            r.get("MonsterData"),
            r.get("ItemCount"),
            r.get("DeadNpcIdentity"),
            r.get("CorpseIdentity"),
        ))
        # dump all keys that look like item
        for k, v in r.items():
            if v and ("item" in k.lower() or "low" in k.lower() or "high" in k.lower() or "ql" in k.lower() or "name" in k.lower()):
                if k not in ("EnemyName",):
                    p("  %s=%s" % (k, v[:120] if isinstance(v, str) and len(v) > 120 else v))

# Named unique humans (likely Find targets) vs Carlo (kill?)
p("\n=== NAMED HUMANS (non-generic titles) ===")
generic = re.compile(r"^(Seasoned|Skilled|Tough|Hardened|Rough|Veteran|Master|Boosted|CEO|Probe|Bileswarm|Bioarranged|Hellhound|Small|Medium)")
scfu = os.path.join(CAP, "scfu-appearance.csv")
with open(scfu, newline="", encoding="utf-8-sig") as f:
    seen = set()
    for r in csv.DictReader(f):
        pf = int(r.get("PlayfieldId") or 0)
        if pf < 1000000:
            continue
        if (r.get("CharacterInfoType") or "") != "NPCInfo":
            continue
        name = (r.get("Name") or "").strip()
        ident = r.get("Identity")
        if not name or ident in seen:
            continue
        seen.add(ident)
        if not generic.match(name) or name in ("Carlo Pinnetti", "Berneice Cornelius", "Nichole Orender", "Chae Aronstein"):
            if " " in name and not generic.match(name):
                p("PF%s %s md=%s lv=%s hp=%s pos=(%s,%s,%s) tex=%s mesh=%s" % (
                    pf, name, r.get("MonsterData"), r.get("Level"), r.get("Health"),
                    r.get("PositionX"), r.get("PositionY"), r.get("PositionZ"),
                    r.get("Textures"), r.get("Meshes")))

# PlayfieldAnarchyF counts per instance window
p("\n=== PlayfieldAnarchyF / geometry-ish packets per instance ===")
geo = collections.Counter()
with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("Direction") or "").upper() != "IN":
            continue
        n3 = r.get("N3TypeName") or ""
        if n3 not in ("PlayfieldAnarchyF", "PlayfieldAnon", "TemplateAction", "WallCollision", "VendingMachineFullUpdate"):
            continue
        pf = pf_for_utc(r.get("CapturedUtc") or "")
        if pf:
            geo[(pf, n3)] += 1
for k, c in sorted(geo.items()):
    p("%s %s = %d" % (k[0], k[1], c))

# mission-flow remaining enters/deletes
p("\n=== MISSION FLOW KEY LINES ===")
for line in open(os.path.join(CAP, "mission-flow.log"), encoding="utf-8-sig", errors="ignore"):
    if any(k in line for k in ("PLAYFIELD-INIT", "Delete mission", "IN-QUEST-FULL", "likelyMissionInstance=True")):
        p(line.rstrip()[:220])

open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
print("wrote", OUT, "lines", len(lines))
