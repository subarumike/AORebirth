# Extract repair-mission capture evidence: loot dynels, doors, mobs, token %.
from __future__ import print_function
import csv, json, os, re, collections

CAP = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-repaair-machine-mish"
OUT = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_repair_mish_extract.txt"
PF = "1419360"
NAMES = [
    "Barrel", "Crashed Android", "Garbage", "Treasure", "Small Crate",
    "Bottles and Garbage", "blasted Skeleton", "Shadow Rift", "Broken Machine",
    "Phoenix",
]

def read_lines(name):
    path = os.path.join(CAP, name)
    if not os.path.exists(path):
        return []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        return f.readlines()

lines_out = []
def w(s=""):
    lines_out.append(s)

# --- events / npc names of interest ---
w("=== LOOT/OBJECT NAME HITS (events.log) ===")
pat = re.compile("|".join(re.escape(n) for n in NAMES), re.I)
counts = collections.Counter()
samples = collections.defaultdict(list)
for line in read_lines("events.log"):
    m = pat.search(line)
    if not m:
        continue
    key = m.group(0)
    counts[key] += 1
    if len(samples[key]) < 3:
        samples[key].append(line.strip()[:300])
for k, c in counts.most_common():
    w("%s count=%d" % (k, c))
    for s in samples[k]:
        w("  " + s)

w()
w("=== CHEST/DOOR/CONTAINER PACKET TYPES (events.log) ===")
for needle in ("ChestFullUpdate", "DoorFullUpdate", "Container", "Barrel", "Crate", "Treasure", "Skeleton", "Android", "Rift"):
    n = 0
    for line in read_lines("events.log"):
        if needle in line:
            n += 1
    w("%s hits=%d" % (needle, n))

w()
w("=== FORMAT FEEDBACK / TOKEN (system-messages + events) ===")
for fname in ("system-messages.log", "events.log", "chat-dialogue.log"):
    for line in read_lines(fname):
        low = line.lower()
        if "token" in low or "possibility" in low or "repair" in low or "mission" in low and "formatfeedback" in low:
            if "FormatFeedback" in line or "token" in low or "possibility" in low or "Received reward" in line:
                w(line.strip()[:350])

w()
w("=== ENEMY DOSSIER INSIDE PF %s ===" % PF)
with open(os.path.join(CAP, "enemy-dossier.json"), encoding="utf-8-sig") as f:
    dossier = json.load(f)
mobs = []
for e in dossier.get("enemies", []):
    pf = str(e.get("runtimePlayfieldId") or e.get("capturePlayfieldObjectId") or "")
    # mission interior hex 15A860 or decimal 1419360
    if PF in pf or "15A860" in str(e.get("capturePlayfieldIdentity", "")) or pf.upper() == "15A860":
        mobs.append(e)
    elif e.get("capturePlayfieldIdentity") and "15A860" in e.get("capturePlayfieldIdentity"):
        mobs.append(e)

# Also filter by firstSeen after mission enter ~08:39:31
enter = "2026-07-24T08:39:31"
for e in dossier.get("enemies", []):
    fs = e.get("firstSeenUtc") or ""
    if fs >= enter and e not in mobs:
        # exclude outdoor Unicorn / Omni-AF from earlier PFs if identity already outdoor
        name = e.get("name") or ""
        if name and "Unicorn" not in name and "Omni-AF" not in name:
            mobs.append(e)

w("mission-ish mobs=%d" % len(mobs))
by_name = collections.Counter((e.get("name") or "?") for e in mobs)
w("names: %s" % by_name)
levels = [e.get("level") for e in mobs if e.get("level")]
w("levels min=%s max=%s sample=%s" % (min(levels) if levels else None, max(levels) if levels else None, levels[:20]))
md = collections.Counter(str(e.get("monsterData")) for e in mobs)
w("monsterData top: %s" % md.most_common(15))

w()
w("=== FIRST 25 MISSION MOBS DETAIL ===")
for e in mobs[:25]:
    w("name=%s lvl=%s md=%s scale=%s hp=%s/%s side_pf=%s pos=(%s,%s,%s) death=%s"
      % (e.get("name"), e.get("level"), e.get("monsterData"), e.get("monsterScale"),
         e.get("currentHealth"), e.get("maxHealth"), e.get("capturePlayfieldIdentity"),
         (e.get("position") or {}).get("x"), (e.get("position") or {}).get("y"), (e.get("position") or {}).get("z"),
         e.get("deathObserved")))

w()
w("=== COMBAT CSV HEAD (mission window) ===")
path = os.path.join(CAP, "enemy-combat.csv")
if os.path.exists(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        reader = csv.DictReader(f)
        cols = reader.fieldnames
        w("cols=%s" % cols)
        n = 0
        for row in reader:
            utc = row.get("utc") or row.get("Utc") or row.get("timestamp") or ""
            if utc and utc < enter:
                continue
            w(str({k: row.get(k) for k in (cols or [])[:12]})[:300])
            n += 1
            if n >= 30:
                break

w()
w("=== SCFU APPEARANCE MISSION WINDOW NAME HITS ===")
path = os.path.join(CAP, "scfu-appearance.csv")
if os.path.exists(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        reader = csv.DictReader(f)
        cols = reader.fieldnames or []
        w("cols=%s" % cols[:20])
        name_col = None
        for c in cols:
            if "name" in c.lower():
                name_col = c
                break
        hits = collections.Counter()
        for row in reader:
            name = (row.get(name_col) or "") if name_col else ""
            blob = " ".join(row.values())
            if "15A860" not in blob and PF not in blob and "1419360" not in blob:
                # still match by name list
                pass
            for nme in NAMES:
                if nme.lower() in blob.lower() or nme.lower() in name.lower():
                    hits[nme] += 1
                    break
        w("name hits: %s" % hits)

w()
w("=== NPC-LIFECYCLE NAME HITS ===")
path = os.path.join(CAP, "npc-lifecycle.csv")
if os.path.exists(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        reader = csv.DictReader(f)
        cols = reader.fieldnames or []
        w("cols=%s" % cols[:15])
        for row in reader:
            blob = " ".join((v or "") for v in row.values())
            if pat.search(blob):
                w(blob[:280])

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines_out))
print("wrote", OUT, "lines", len(lines_out))
