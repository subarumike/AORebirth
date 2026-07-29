# Deep extract: Rex/Marcus, Mongo Slam AoE, targeted NPCs, spawn data
import csv, json, re, collections, math
from pathlib import Path

CAP = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260719-Rex-Markus-stone")
OUT = Path(r"tools-temp/_tmp_arete_deep.txt")
lines=[]
def p(*a):
    lines.append(" ".join(str(x) for x in a))

# scfu by name for key NPCs
with open(CAP/"scfu-appearance.csv", newline="", encoding="utf-8-sig") as f:
    rows=list(csv.DictReader(f))

def dump_row(r):
    keys=["Identity","Name","PlayfieldId","PositionX","PositionY","PositionZ",
          "HeadingX","HeadingY","HeadingZ","HeadingW","MonsterData","MonsterScale",
          "HeadMesh","Level","Health","HealthDamage","Side","Breed","Gender","Race",
          "VisualFlags","Textures","Meshes","TextureOverrides","NpcFamily",
          "CharacterFlags","Flags"]
    for k in keys:
        if k in r and r[k]:
            p(f"  {k}={r[k]}")

wanted = re.compile(r"Rex|Marcus|Markus|Stone|Larsson|Mongo|Flint|Dockworker|Protester|Bruiser|Kneebreaker|Cleaning|Garbage|Bureaucrat", re.I)
seen=set()
p("=== KEY SCFU ROWS ===")
for r in rows:
    name=r.get("Name") or ""
    ident=r.get("Identity") or ""
    if wanted.search(name) or wanted.search(ident):
        key=(name,ident)
        if key in seen: continue
        seen.add(key)
        p(f"\n## {name} {ident}")
        dump_row(r)

# InfoRequest targets mapped to names from scfu
p("\n=== TARGETED (InfoRequest) ===")
ev=(CAP/"events.log").read_text(encoding="utf-8-sig", errors="replace")
targets=re.findall(r"Action=InfoRequest.*?Target=\(SimpleChar:([0-9A-F]+)\)", ev)
# also LookAt detail
name_by={}
for r in rows:
    m=re.search(r"SimpleChar:([0-9A-F]+)", r.get("Identity") or "")
    if m:
        name_by[m.group(1)] = r.get("Name")
for t in collections.OrderedDict.fromkeys(targets):
    p(f"  {t} -> {name_by.get(t,'?')}")

# Mongo Slam sequence around CastNano 287046
p("\n=== MONGO SLAM WINDOW ===")
# find lines around Parameter2=287046
idxs=[]
all_lines=ev.splitlines()
for i,line in enumerate(all_lines):
    if "287046" in line or "Mongo" in line:
        idxs.append(i)
p("hit lines", idxs[:20])
for i in idxs[:5]:
    start=max(0,i-5); end=min(len(all_lines), i+40)
    p(f"\n--- around line {i} ---")
    for j in range(start,end):
        p(all_lines[j][:260])

# HealthDamage / Stat health around that time from enemy-combat / raw
p("\n=== enemy-combat around mongo (sample) ===")
with open(CAP/"enemy-combat.csv", newline="", encoding="utf-8-sig") as f:
    combat=list(csv.DictReader(f))
p("cols", list(combat[0].keys()) if combat else None)
# find rows near 16:52:14
for r in combat:
    utc=r.get("CapturedUtc") or r.get("Utc") or ""
    if "16:52:1" in utc or "16:52:2" in utc:
        p({k:r.get(k) for k in list(r.keys())[:12]})

# SpellList after player cast - decode from events DETAIL if any
p("\n=== SpellList DETAIL near player cast ===")
for i,line in enumerate(all_lines):
    if "16:52:14" in line or "16:52:15" in line:
        if "SpellList" in line or "HealthDamage" in line or "Stat" in line or "CastNano" in line or "287046" in line:
            p(line[:300])

# Nano 287046 from raw packets - CastNanoSpell / effects
p("\n=== raw CastNanoSpell / HealthDamage around mongo ===")
with open(CAP/"raw-packets.csv", newline="", encoding="utf-8-sig") as f:
    pkts=list(csv.DictReader(f))
# find OUT CharacterAction with 287046
for r in pkts:
    if "287046" in (r.get("RawHex") or "") or (r.get("N3TypeName")=="CharacterAction" and r.get("Direction")=="OUT" and "16:52:14" in (r.get("CapturedUtc") or "")):
        if "16:52:14" in (r.get("CapturedUtc") or "") or "287046" in (r.get("RawHex") or ""):
            p(r.get("CapturedUtc"), r.get("Direction"), r.get("N3TypeName"), r.get("IdentityInstance"), r.get("PacketLength"))

# positions of targeted cleaning robots + rex/marcus
p("\n=== positions of focused + dialog NPCs ===")
focus=["78E0FC62","78E0FC63","79543CB6","797D36A5","797DD292","797DD296","797DD29B","797DD29C","797DD2A8","797DD2AA","797DD2B3","797DD2B5","797DD2BA","797DD2BB","797DD2CD"]
for r in rows:
    m=re.search(r"SimpleChar:([0-9A-F]+)", r.get("Identity") or "")
    if not m: continue
    iid=m.group(1)
    if iid in focus or "Rex" in (r.get("Name") or "") or "Marcus" in (r.get("Name") or "") or "Markus" in (r.get("Name") or ""):
        p(f"{r.get('Name')} {iid} xyz=({r.get('PositionX')},{r.get('PositionY')},{r.get('PositionZ')}) heading=({r.get('HeadingX')},{r.get('HeadingY')},{r.get('HeadingZ')},{r.get('HeadingW')}) md={r.get('MonsterData')} head={r.get('HeadMesh')}")
        p(f"  tex={r.get('Textures')}")
        p(f"  mesh={r.get('Meshes')}")

# enemy-full-updates for rex/marcus names
p("\n=== enemy-full-updates name counts ===")
with open(CAP/"enemy-full-updates.csv", newline="", encoding="utf-8-sig") as f:
    efu=list(csv.DictReader(f))
p("cols", list(efu[0].keys())[:20] if efu else None)
names=collections.Counter((r.get("Name") or r.get("EnemyName") or "?") for r in efu)
for n,c in names.most_common(40):
    p(f"  {c} {n}")

# movement sample for one robot
p("\n=== enemy-movement sample robot 797DD292 ===")
with open(CAP/"enemy-movement.csv", newline="", encoding="utf-8-sig") as f:
    mov=list(csv.DictReader(f))
p("cols", list(mov[0].keys()) if mov else None)
count=0
for r in mov:
    if "797DD292" in str(r.values()):
        p({k:r.get(k) for k in list(r.keys())[:10]})
        count+=1
        if count>=8: break

OUT.write_text("\n".join(lines), encoding="utf-8")
print("wrote", OUT, "lines", len(lines))
