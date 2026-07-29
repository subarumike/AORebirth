# Extract Find-Person mission capture: PF/shape/mobs/textures/fight/layout.
from __future__ import print_function
import csv, collections, json, os, re

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-mission-find-person"
OUT = r"tools-temp\_tmp_find_person_extract.txt"

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

p("=== SESSION ===")
for name in ("capture-session.json", "capture_info.json", "capture-health.json", "enemy-dossier.json"):
    path = os.path.join(CAP, name)
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            data = f.read()
        p("%s (%d bytes)" % (name, len(data)))
        p(data[:2500])
        p()

p("=== MISSION-FLOW ===")
for line in lines("mission-flow.log"):
    p(line.rstrip())

p("\n=== SYSTEM (mission/token/%) ===")
for line in lines("system-messages.log"):
    if re.search(r"(?i)mission|token|reward|possibility|find|complete|percent|%", line):
        p(line.rstrip())

scfu = rows("scfu-appearance.csv")
pfs = collections.Counter((r.get("PlayfieldId") or "?") for r in scfu)
p("\n=== SCFU PlayfieldId counts ===")
p(str(pfs.most_common(15)))
p("scfu cols: %s" % (list(scfu[0].keys()) if scfu else []))
p("scfu rows=%d" % len(scfu))

# Mission PF = most common non-city / high instance id
mission_pfs = [pf for pf, _ in pfs.most_common() if pf.isdigit() and int(pf) > 1000000]
p("mission-ish PFs: %s" % mission_pfs)

# Unique identities on largest mission PF
PF = mission_pfs[0] if mission_pfs else None
p("PRIMARY_PF=%s" % PF)

by_id = {}
for r in scfu:
    if PF and (r.get("PlayfieldId") or "") != PF:
        continue
    ident = r.get("Identity") or ""
    if not ident:
        continue
    if ident not in by_id or (not by_id[ident].get("Name") and r.get("Name")):
        by_id[ident] = r

PROP_KEYS = ("barrel","treasure","garbage","crate","bottle","android","skeleton","rift","skull",
             "cube","door","chest","machine","bag","terminal","container")
SKIP = ("Carlo Pinnetti", "CEO Guardian", "Corporate Guardian")

names = collections.Counter()
mobs = []
props = []
playerish = []
for ident, r in by_id.items():
    name = (r.get("Name") or "").strip()
    names[name or "?"] += 1
    rec = {
        "id": ident,
        "name": name,
        "x": r.get("PositionX"), "y": r.get("PositionY"), "z": r.get("PositionZ"),
        "hx": r.get("HeadingX"), "hy": r.get("HeadingY"), "hz": r.get("HeadingZ"), "hw": r.get("HeadingW"),
        "lvl": r.get("Level"), "hp": r.get("Health"), "md": r.get("MonsterData"),
        "scale": r.get("MonsterScale"), "head": r.get("HeadMesh"),
        "tex": r.get("Textures"), "mesh": r.get("Meshes"),
        "side": r.get("Side"), "flags": r.get("Flags"), "vf": r.get("VisualFlags"),
        "breed": r.get("Breed"), "gender": r.get("Gender"), "profession": r.get("Profession"),
    }
    low = name.lower()
    if any(k in low for k in PROP_KEYS) or "Terminal:" in name:
        props.append(rec)
    elif name in SKIP or name.startswith("Cratonera"):
        playerish.append(rec)
    elif name:
        mobs.append(rec)

p("\nunique on PF=%s identities=%d names=%s" % (PF, len(by_id), names.most_common(60)))

p("\n=== PROPS/TERMINALS (%d) ===" % len(props))
for r in sorted(props, key=lambda x: (x["name"], x["z"] or "")):
    p("%(name)s id=%(id)s xyz=(%(x)s,%(y)s,%(z)s) md=%(md)s lvl=%(lvl)s side=%(side)s" % r)

p("\n=== MOBS (%d) ===" % len(mobs))
seen = set()
for r in sorted(mobs, key=lambda x: (x["name"], x["lvl"] or "", x["z"] or "")):
    key = (r["name"], r["md"], r["x"], r["z"])
    if key in seen:
        continue
    seen.add(key)
    p("%(name)s lvl=%(lvl)s hp=%(hp)s md=%(md)s scale=%(scale)s head=%(head)s side=%(side)s vf=%(vf)s xyz=(%(x)s,%(y)s,%(z)s) tex=%(tex)s mesh=%(mesh)s id=%(id)s" % r)

# Side / grey vs colored
p("\n=== SIDE DISTRIBUTION (grey=0 side?) ===")
sides = collections.Counter((r.get("side") or "?") for r in mobs)
p(str(sides))
for r in mobs:
    p("SIDE name=%s side=%s vf=%s flags=%s lvl=%s" % (r["name"], r["side"], r["vf"], r["flags"], r["lvl"]))

p("\n=== ENEMY-FULL-UPDATES name/level/md ===")
enemy = rows("enemy-full-updates.csv")
p("cols=%s rows=%d" % (list(enemy[0].keys()) if enemy else [], len(enemy)))
enames = collections.Counter()
levels = collections.Counter()
for r in enemy:
    n = r.get("Name") or r.get("name") or ""
    if n:
        enames[n] += 1
    lv = r.get("Level") or r.get("level") or ""
    if lv:
        levels[lv] += 1
p("names=%s" % enames.most_common(40))
p("levels=%s" % levels.most_common(20))

p("\n=== ENEMY-COMBAT (sample) ===")
combat = rows("enemy-combat.csv")
p("cols=%s rows=%d" % (list(combat[0].keys()) if combat else [], len(combat)))
for r in combat[:30]:
    p(str({k: r[k] for k in r if r[k]}))

p("\n=== ENEMY-FIGHT-EVENTS (all/anim hints) ===")
for line in lines("enemy-fight-events.log"):
    if re.search(r"(?i)anim|attack|hit|special|nano|cast|fight|damage|weapon", line):
        p(line.rstrip()[:400])

p("\n=== ENEMY-STATE / MOVEMENT summary ===")
for name in ("enemy-state.csv", "enemy-movement.csv", "enemy-stat-updates.csv"):
    rs = rows(name)
    p("%s rows=%d cols=%s" % (name, len(rs), list(rs[0].keys()) if rs else []))

p("\n=== NPC-INTERACTIONS ===")
for line in lines("npc-interactions.log"):
    p(line.rstrip()[:400])

p("\n=== EVENTS Door/Chest/Container/FindPerson hints ===")
counts = collections.Counter()
samples = collections.defaultdict(list)
with open(os.path.join(CAP, "events.log"), "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        for key in ("DoorFullUpdate", "ChestFullUpdate", "Container", "Treasure", "Barrel",
                    "Find", "Person", "Use", "Tag", "Terminal", "PlayfieldGenerator",
                    "PlayfieldAnonFlags", "SimpleCharFullUpdate"):
            if key in line:
                counts[key] += 1
                if len(samples[key]) < 2:
                    samples[key].append(line.strip()[:350])
p("counts %s" % counts)
for k in ("DoorFullUpdate", "ChestFullUpdate", "PlayfieldGenerator", "Terminal"):
    for s in samples.get(k, []):
        p("[%s] %s" % (k, s))

p("\n=== MOVEMENT-SUMMARY ===")
path = os.path.join(CAP, "movement-summary.json")
if os.path.exists(path):
    p(open(path, encoding="utf-8").read()[:2000])

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(out))
print("wrote", OUT, "lines", len(out), "PRIMARY_PF", PF)
