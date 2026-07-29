from __future__ import print_function
import csv, json, os
from collections import defaultdict
from datetime import datetime

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-Rox-robots"
OUT = r"tools-temp\_tmp_rex_robots_cap_out3.txt"
lines=[]
def w(s=""):
    lines.append(s)

def parse_dt(s):
    s=(s or "").replace("Z","").strip().strip('"')
    for n in (26,23,19):
        try:
            return datetime.strptime(s[:n], "%Y-%m-%dT%H:%M:%S.%f" if n>19 else "%Y-%m-%dT%H:%M:%S")
        except Exception:
            continue
    return None

# Concurrent from enemy-state: entities with positions that match malf dossier ids
with open(os.path.join(CAP,"enemy-dossier.json"),encoding="utf-8-sig") as f:
    dossier=json.load(f)

malf=set()
malf_meta={}
for e in dossier["enemies"]:
    if e.get("name")=="Malfunctioning Cleaning Robot":
        malf.add(e["identity"])
        malf_meta[e["identity"]]=e

# state events with xy
by_t = defaultdict(set)  # rounded second -> alive with known pos
last_pos={}
alive_health={}
with open(os.path.join(CAP,"enemy-state.csv"),encoding="utf-8-sig",newline="") as f:
    for row in csv.DictReader(f):
        eid=row["entityId"]
        if eid not in malf:
            continue
        et=row.get("eventType") or ""
        ts=parse_dt(row["timestamp"])
        if row.get("x"):
            last_pos[eid]=(float(row["x"]),float(row["y"]),float(row["z"]))
        if row.get("currentHealth"):
            try:
                alive_health[eid]=int(float(row["currentHealth"]))
            except Exception:
                pass
        if et=="spawn":
            alive_health.setdefault(eid, 12)
        if et in ("despawn","death"):
            alive_health.pop(eid, None)
            last_pos.pop(eid, None)
        if ts:
            key=ts.replace(microsecond=0)
            # count entities with last known pos y>=50
            living=[i for i,p in last_pos.items() if p[1]>=50]
            by_t[key]=set(living)

max_n=0
max_t=None
max_set=set()
for t,s in sorted(by_t.items()):
    if len(s)>max_n:
        max_n=len(s); max_t=t; max_set=set(s)
w("max concurrent malf with pos y>=50 from state: %d at %s" % (max_n, max_t))
for i in sorted(max_set):
    p=last_pos.get(i) or malf_meta[i]["position"]
    if isinstance(p, dict):
        p=(p["x"],p["y"],p["z"])
    w("  %s (%.2f,%.2f,%.2f)" % (i,p[0],p[1],p[2]))

# Initial wave: firstSeen within 1s of capture start
w()
w("=== INITIAL WAVE firstSeen < start+2s y>=50 ===")
start=parse_dt("2026-07-21T19:55:03.2812444")
initial=[]
for e in dossier["enemies"]:
    if e.get("name")!="Malfunctioning Cleaning Robot":
        continue
    fs=parse_dt(e.get("firstSeenUtc"))
    pos=e["position"]
    if pos["y"]<50: continue
    if fs and (fs-start).total_seconds()<=2.0:
        initial.append((e["identity"], pos, e.get("runSpeed"), e.get("maxHealth"), e.get("level")))
w("initial_count=%d" % len(initial))
for ident,pos,rs,hp,lv in sorted(initial, key=lambda x:(x[1]["x"],x[1]["z"])):
    w("  %s L%s HP%s RS=%s (%.6f,%.6f,%.6f)" % (ident,lv,hp,rs,pos["x"],pos["y"],pos["z"]))

# Movement by SourceName
w()
w("=== MOVEMENT SourceName Malfunctioning ===")
by=defaultdict(list)
anims=defaultdict(set)
with open(os.path.join(CAP,"movement-packets.csv"),encoding="utf-8-sig",newline="") as f:
    for row in csv.DictReader(f):
        if row.get("SourceName")!="Malfunctioning Cleaning Robot":
            continue
        if row.get("MessageType")!="FollowTarget":
            continue
        sid=row.get("SourceIdentity") or row.get("SourceInstance")
        anims[sid].add(row.get("Animation") or "")
        if row.get("FollowKind")!="NpcPath":
            continue
        try:
            by[sid].append((
                row["CapturedUtc"],
                float(row["CurrentX"]),float(row["CurrentY"]),float(row["CurrentZ"]),
                float(row["DestinationX"]),float(row["DestinationY"]),float(row["DestinationZ"]),
                row.get("Speed"), row.get("Animation"), row.get("Flags"), row.get("PathCount")
            ))
        except Exception:
            pass
w("movers=%d" % len(by))
for sid in sorted(by.keys(), key=lambda k:-len(by[k])):
    path=by[sid]
    w("%s segs=%d anims=%s" % (sid, len(path), sorted(anims[sid])))
    uniq=[]
    for rec in path:
        key=(round(rec[4],2),round(rec[5],2),round(rec[6],2))
        if not uniq or uniq[-1]!=key:
            uniq.append(key)
    w("  dest_unique_seq=%d speed_sample=%s flags=%s pathCount=%s" % (
        len(uniq), path[0][7], path[0][9], path[0][10]))
    for j,(dx,dy,dz) in enumerate(uniq[:25]):
        w("    wp%02d (%.3f,%.3f,%.3f)" % (j,dx,dy,dz))
    if len(uniq)>25:
        w("    ... +%d" % (len(uniq)-25))

# FollowKind distribution for malf
w()
w("=== FollowKind / Animation for Malfunctioning ===")
fk=defaultdict(int)
an=defaultdict(int)
with open(os.path.join(CAP,"movement-packets.csv"),encoding="utf-8-sig",newline="") as f:
    for row in csv.DictReader(f):
        if row.get("SourceName")!="Malfunctioning Cleaning Robot":
            continue
        fk[row.get("FollowKind") or "?"]+=1
        an[row.get("Animation") or "?"]+=1
w("FollowKind=%s" % dict(fk))
w("Animation=%s" % dict(an))

# lifecycle dump sample full rows
w()
w("=== lifecycle Phase/MessageType for malf ===")
with open(os.path.join(CAP,"npc-lifecycle.csv"),encoding="utf-8-sig",newline="") as f:
    for i,row in enumerate(csv.DictReader(f)):
        if row.get("Name")!="Malfunctioning Cleaning Robot":
            continue
        w("  %s | Phase=%s | Msg=%s | Prim=%s | Rel=%s | Detail=%s" % (
            row.get("CapturedUtc"), row.get("Phase"), row.get("MessageType"),
            row.get("PrimaryIdentity"), row.get("RelatedIdentity"), (row.get("Detail") or "")[:120]))

# SCFU: count IsPet vs not for Malfunctioning; Waypoints field
w()
w("=== SCFU Malfunctioning IsPet / Waypoints ===")
with open(os.path.join(CAP,"scfu-appearance.csv"),encoding="utf-8-sig",newline="") as f:
    for row in csv.DictReader(f):
        if row.get("Name")!="Malfunctioning Cleaning Robot":
            continue
        w("  %s flags=%s waypoints=%s wpOwner=%s fam=%s scale=%s run=%s pos=(%s,%s,%s) isPet=%s" % (
            row.get("Identity"), row.get("Flags"), row.get("Waypoints"), row.get("WaypointOwner"),
            row.get("NpcFamily"), row.get("MonsterScale"), row.get("RunSpeedBase"),
            row.get("PositionX"), row.get("PositionY"), row.get("PositionZ"),
            "IsPet" in (row.get("Flags") or "")))

# enemy-full-updates waypoints content
w()
w("=== EFU waypoints for Malfunctioning ===")
with open(os.path.join(CAP,"enemy-full-updates.csv"),encoding="utf-8-sig",newline="") as f:
    for row in csv.DictReader(f):
        if row.get("Name")!="Malfunctioning Cleaning Robot":
            continue
        w("  %s wpCount=%s wp=%s pos=(%s,%s,%s) flags=%s run=%s fam=%s" % (
            row.get("Identity"), row.get("WaypointCount"), row.get("Waypoints"),
            row.get("PositionX"), row.get("PositionY"), row.get("PositionZ"),
            row.get("Flags"), row.get("RunSpeedBase"), row.get("NPCFamily")))

with open(OUT,"w",encoding="utf-8") as f:
    f.write("\n".join(lines))
print("Wrote", OUT, "initial", len(initial), "max", max_n, "movers", len(by))
