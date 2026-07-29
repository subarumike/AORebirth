# Generate AreteLandingSpawn.cs fragment from capture 20260719-Rex-Markus-stone
import csv, json, re
from pathlib import Path
from collections import OrderedDict

CAP = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260719-Rex-Markus-stone")
OUT = Path(r"tools-temp/_tmp_arete_spawn_npcs.csfrag")

with open(CAP/"scfu-appearance.csv", newline="", encoding="utf-8-sig") as f:
    scfu = list(csv.DictReader(f))
with open(CAP/"enemy-full-updates.csv", newline="", encoding="utf-8-sig") as f:
    efu = list(csv.DictReader(f))

# Prefer SCFU rows with position+texture for unique names (first occurrence with coords)
# Skip cleaning robots (handled separately)
SKIP = re.compile(r"Cleaning Robot|Burning Cleaning|Garbage Flea", re.I)  # include flea actually
SKIP = re.compile(r"^(Malfunctioning |Burning )?Cleaning Robot$", re.I)

def parse_tex(s):
    # 0:295555:0|1:295553:0 -> [[0,295555],[1,295553],...]
    if not s: return None
    out=[]
    for part in s.split("|"):
        bits=part.split(":")
        if len(bits)>=2:
            out.append([int(bits[0]), int(bits[1])])
    return out or None

def parse_mesh(s):
    # 0:205110:0:2|0:40127:0:4 -> [[0,205110,0,2],...]
    if not s: return None
    out=[]
    for part in s.split("|"):
        bits=part.split(":")
        if len(bits)>=4:
            out.append([int(bits[0]), int(bits[1]), int(bits[2]), int(bits[3])])
        elif len(bits)==3:
            out.append([int(bits[0]), int(bits[1]), int(bits[2]), 0])
    return out or None

def i(v, d=0):
    try: return int(float(v)) if v not in (None,"") else d
    except: return d

def f(v, d=0.0):
    try: return float(v) if v not in (None,"") else d
    except: return d

by_name = OrderedDict()
for r in scfu:
    name = (r.get("Name") or "").strip()
    if not name or SKIP.match(name): continue
    x,y,z = f(r.get("PositionX")), f(r.get("PositionY")), f(r.get("PositionZ"))
    if x==0 and y==0 and z==0: continue
    if name in by_name: continue
    by_name[name] = r

# Also add from efu if missing and has coords
for r in efu:
    name = (r.get("Name") or "").strip()
    if not name or SKIP.match(name): continue
    if name in by_name: continue
    x,y,z = f(r.get("PositionX")), f(r.get("PositionY")), f(r.get("PositionZ"))
    if x==0 and y==0 and z==0: continue
    by_name[name] = r

# Multiple instances for same name (dockworkers etc) - use identity-keyed unique for non-unique names
instances = []
seen_ids=set()
for src in (scfu, efu):
    for r in src:
        name=(r.get("Name") or "").strip()
        if not name or SKIP.match(name): continue
        m=re.search(r"SimpleChar:([0-9A-F]+)", r.get("Identity") or "")
        if not m: continue
        iid=m.group(1)
        if iid in seen_ids: continue
        x,y,z = f(r.get("PositionX")), f(r.get("PositionY")), f(r.get("PositionZ"))
        if abs(x)<0.01 and abs(z)<0.01: continue
        seen_ids.add(iid)
        instances.append((name, iid, r))

# Cap: unique named dialogue NPCs + all textured instances Mike targeted
# Prefer instances with textures or headmesh
prio_names = {"Flint Novak","Protester","Wounded Dockworker","Dockworker","Bruiser",
              "Kneebreaker Alfonzo Rizzolo","Bureaucrat Worker","Garbage Flea"}
selected=[]
for name,iid,r in instances:
    if name not in prio_names: continue
    tex=parse_tex(r.get("Textures") or "")
    mesh=parse_mesh(r.get("Meshes") or "")
    head=i(r.get("HeadMesh"))
    # keep if has visual OR is named unique
    if tex or mesh or head or name in ("Flint Novak","Kneebreaker Alfonzo Rizzolo","Bruiser"):
        selected.append((name,iid,r))

# Deduplicate by approximate position for same name (0.5m)
def near(a,b):
    return abs(a[0]-b[0])<0.5 and abs(a[2]-b[2])<0.5
final=[]
for name,iid,r in selected:
    pos=(f(r.get("PositionX")),f(r.get("PositionY")),f(r.get("PositionZ")))
    if any(n==name and near(pos,p) for n,_,rr,p in final):
        continue
    final.append((name,iid,r,pos))

print(f"selected {len(final)} npcs")
for n,iid,r,p in final:
    print(n, iid, p, "head", r.get("HeadMesh"), "md", r.get("MonsterData"))

# Rex / Marcus from evidence + this dossier
rex = dict(Name="Rex Larsson", Level=15, Health=5000, MonsterData=26074, Scale=100, VisualFlags=31,
           HeadMesh=0, RunSpeed=53, NpcFamily=103, LosHeight=0, CharacterFlags=268964353, AppearanceValue=1576,
           Side=0, Breed=1, Gender=2, Race=1, Fatness=1, MovementMode=3,
           X=3624.599, Y=51.745, Z=787.7465, Hx=0, Hy=-0.5, Hz=0, Hw=0.866,
           Textures=[[0,295555],[1,295553],[2,295554],[3,295552],[4,295556]],
           Meshes=[[0,205120,0,2],[0,40691,0,4]], FixedInstance=0x782DE568)
marcus = dict(Name="Marcus Stone", Level=15, Health=117800, MonsterData=258744, Scale=105, VisualFlags=31,
           HeadMesh=40667, RunSpeed=53, NpcFamily=137, LosHeight=0, CharacterFlags=268964353, AppearanceValue=1576,
           Side=0, Breed=1, Gender=2, Race=1, Fatness=1, MovementMode=3,
           X=3630.962, Y=40.985, Z=823.1738, Hx=0, Hy=-0.2588223, Hz=0, Hw=-0.965926,
           Textures=[[0,295555],[1,295553],[2,295554],[3,295552],[4,295556]],
           Meshes=[[0,40667,0,4]], FixedInstance=0x782DE567)

def emit_npc(d, comment=""):
    tex = d.get("Textures")
    mesh = d.get("Meshes")
    lines=[]
    lines.append("            new AreteNpc")
    lines.append("            {")
    if comment: lines.append(f"                // {comment}")
    if d.get("FixedInstance"):
        lines.append(f"                FixedIdentityInstance = unchecked((int)0x{d['FixedInstance']:08X}),")
    lines.append(f"                Name = \"{d['Name']}\",")
    lines.append(f"                Level = {d['Level']}, Health = {d['Health']}, MonsterData = {d['MonsterData']}, Scale = {d['Scale']}, VisualFlags = {d['VisualFlags']}, HeadMesh = {d['HeadMesh']}, RunSpeed = {d['RunSpeed']},")
    lines.append(f"                NpcFamily = {d['NpcFamily']}, LosHeight = {d['LosHeight']}, CharacterFlags = {d['CharacterFlags']}, AppearanceValue = {d['AppearanceValue']},")
    lines.append(f"                Side = {d['Side']}, Breed = {d['Breed']}, Gender = {d['Gender']}, Race = {d['Race']}, Fatness = {d['Fatness']}, MovementMode = {d['MovementMode']},")
    lines.append(f"                X = {d['X']}f, Y = {d['Y']}f, Z = {d['Z']}f,")
    lines.append(f"                Hx = {d['Hx']}f, Hy = {d['Hy']}f, Hz = {d['Hz']}f, Hw = {d['Hw']}f,")
    if tex:
        t=", ".join(f"new[] {{ {a}, {b} }}" for a,b in tex)
        lines.append(f"                Textures = new[] {{ {t} }},")
    else:
        lines.append("                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },")
    if mesh:
        m=", ".join(f"new[] {{ {a}, {b}, {c}, {d_} }}" for a,b,c,d_ in mesh)
        lines.append(f"                Meshes = new[] {{ {m} }},")
    else:
        lines.append("                Meshes = null,")
    lines.append("            },")
    return "\n".join(lines)

# breed/gender mapping from strings
BREED={"Solitus":1,"Opifex":2,"Nanomage":3,"Atrox":4,"Human":1,"Monster":6,"Robot":7}
GENDER={"Male":2,"Female":3,"Neuter":1,"None":1,"Unknown":1}
SIDE={"Neutral":0,"Clan":1,"Omni":2,"None":0}

frags=[emit_npc(rex,"Capture dossier + arete-analysis pos; dialogue id 782DE568"),
       emit_npc(marcus,"Capture dossier + marcus_stone_evidence; dialogue id 782DE567")]

for name,iid,r,pos in final:
    tex=parse_tex(r.get("Textures") or "")
    mesh=parse_mesh(r.get("Meshes") or "")
    d=dict(
        Name=name,
        Level=i(r.get("Level"),1),
        Health=i(r.get("Health"),100),
        MonsterData=i(r.get("MonsterData")),
        Scale=i(r.get("MonsterScale"),100) or 100,
        VisualFlags=i(r.get("VisualFlags"),31) or 31,
        HeadMesh=i(r.get("HeadMesh")),
        RunSpeed=i(r.get("RunSpeedBase") or r.get("RunSpeed"),20),
        NpcFamily=i(r.get("NpcFamily"),0),
        LosHeight=i(r.get("NpcLosHeight") or r.get("LosHeight"),0),
        CharacterFlags=i(r.get("CharacterFlags"),268964353) or 268964353,
        AppearanceValue=i(r.get("AppearanceValue"),1576),
        Side=SIDE.get(r.get("Side") or "", i(r.get("Side"),0)),
        Breed=BREED.get(r.get("Breed") or "", i(r.get("Breed"),1)),
        Gender=GENDER.get(r.get("Gender") or "", i(r.get("Gender"),2)),
        Race=i(r.get("Race"),1) or 1,
        Fatness=i(r.get("Fatness"),1) or 1,
        MovementMode=3,
        X=pos[0], Y=pos[1], Z=pos[2],
        Hx=f(r.get("HeadingX")), Hy=f(r.get("HeadingY")), Hz=f(r.get("HeadingZ")), Hw=f(r.get("HeadingW"),1),
        Textures=tex, Meshes=mesh
    )
    frags.append(emit_npc(d, f"Capture 20260719-Rex-Markus-stone {iid}"))

# Robot position updates
robots=[]
for r in scfu:
    name=(r.get("Name") or "").strip()
    if not re.search(r"Malfunctioning Cleaning Robot", name): continue
    x,y,z=f(r.get("PositionX")),f(r.get("PositionY")),f(r.get("PositionZ"))
    if abs(x)<1: continue
    robots.append((x,y,z,f(r.get("HeadingY")),f(r.get("HeadingW"),1), r.get("Identity")))

OUT.write_text("\n".join(frags), encoding="utf-8")
print("wrote", OUT)
print("robots", len(robots))
for r in robots[:10]:
    print(r)
