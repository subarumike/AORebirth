# Deep Find-Item capture: SCFU mobs + containers + inventory around complete.
from __future__ import print_function
import csv, collections, os, re

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-mission-find-item"
OUT = r"tools-temp\_tmp_find_item_deep.txt"
PF = "1441804"

def rows(name):
    path = os.path.join(CAP, name)
    with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
        return list(csv.DictReader(f))

out = []
def p(s=""):
    out.append(s)

scfu = rows("scfu-appearance.csv")
p("SCFU rows=%d" % len(scfu))
# unique by identity for PF
by_id = {}
for r in scfu:
    if (r.get("PlayfieldId") or "") not in (PF, "0x16000C", "1441804"):
        # also accept empty if position present during mission
        pass
    ident = r.get("Identity") or ""
    name = r.get("Name") or ""
    if not ident:
        continue
    # keep first spawn with coords
    if ident not in by_id:
        by_id[ident] = r
    elif not by_id[ident].get("Name") and name:
        by_id[ident] = r

# filter to likely interior: levels or container-ish names
names = collections.Counter()
mobs = []
props = []
for ident, r in by_id.items():
    name = (r.get("Name") or "").strip()
    names[name or "?"] += 1
    pf = r.get("PlayfieldId") or ""
    rec = {
        "id": ident,
        "name": name,
        "pf": pf,
        "x": r.get("PositionX"),
        "y": r.get("PositionY"),
        "z": r.get("PositionZ"),
        "lvl": r.get("Level"),
        "hp": r.get("Health"),
        "md": r.get("MonsterData"),
        "scale": r.get("MonsterScale"),
        "head": r.get("HeadMesh"),
        "tex": r.get("Textures"),
        "mesh": r.get("Meshes"),
        "side": r.get("Side"),
        "vf": r.get("VisualFlags"),
    }
    low = name.lower()
    if any(k in low for k in ("barrel","treasure","garbage","crate","bottle","android","skeleton","rift","skull","cube","door","chest","isotope","capsule","radioactive","encrypted")):
        props.append(rec)
    elif name and name not in ("Cratonera",) and not name.startswith("Cratonera"):
        # skip pets known
        if name not in ("Carlo Pinnetti", "CEO Guardian", "Corporate Guardian"):
            mobs.append(rec)

p("unique identities=%d names=%s" % (len(by_id), names.most_common(50)))
p("\n=== PROPS (%d) ===" % len(props))
for r in sorted(props, key=lambda x: x["name"]):
    p("%(name)s id=%(id)s pf=%(pf)s xyz=(%(x)s,%(y)s,%(z)s) md=%(md)s lvl=%(lvl)s" % r)

p("\n=== MOBS unique (%d) ===" % len(mobs))
# dedupe by name+md
seen = set()
for r in sorted(mobs, key=lambda x: (x["name"], x["lvl"] or "")):
    key = (r["name"], r["md"], r["x"], r["z"])
    if key in seen:
        continue
    seen.add(key)
    p("%(name)s lvl=%(lvl)s hp=%(hp)s md=%(md)s scale=%(scale)s xyz=(%(x)s,%(y)s,%(z)s) id=%(id)s" % r)

# inventory around complete
p("\n=== INVENTORY ALL (last 40) ===")
inv = rows("inventory-updates.csv")
for r in inv[-40:]:
    p("low=%s high=%s ql=%s slot=%s item=%s" % (r.get("LowId"), r.get("HighId"), r.get("Quality"), r.get("Slot"), r.get("ItemIdentity")))

p("\n=== INVENTORY with LowId around complete window ===")
for r in inv:
    # any non-empty
    if r.get("LowId") and r.get("LowId") not in ("0",""):
        p("%s low=%s high=%s ql=%s item=%s" % (r.get("CapturedUtc"), r.get("LowId"), r.get("HighId"), r.get("Quality"), r.get("ItemIdentity")))

# corpse loot items
p("\n=== CORPSE LOOT ===")
for r in rows("corpse-loot-observations.csv"):
    p("enemy=%s md=%s lvl=%s credits=%s items=%s" % (r.get("EnemyName"), r.get("MonsterData"), r.get("EnemyLevel"), r.get("CorpseCredits"), r.get("Items")))

# events for ChestFullUpdate / SimpleItem / find item names
p("\n=== EVENTS Container/Chest/Door name lines (sample) ===")
path = os.path.join(CAP, "events.log")
counts = collections.Counter()
samples = collections.defaultdict(list)
with open(path, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        for key in ("Treasure", "Barrel", "Small Crate", "Bottles", "Skeleton", "Android", "Garbage", "Door", "Chest", "Radioactive", "Encrypted", "Isotope", "Capsule", "Cube"):
            if key in line:
                counts[key] += 1
                if len(samples[key]) < 3:
                    samples[key].append(line.strip()[:300])
p("counts %s" % counts)
for k, ss in samples.items():
    p("-- %s --" % k)
    for s in ss:
        p(s)

# PlayfieldId distribution in scfu
pfs = collections.Counter((r.get("PlayfieldId") or "?") for r in scfu)
p("\nSCFU PlayfieldId counts: %s" % pfs.most_common(10))

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("wrote", OUT, "lines", len(out))
