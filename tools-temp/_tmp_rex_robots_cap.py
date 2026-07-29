# Analyze 20260721-Rox-robots for cleaning robots near Rex only.
from __future__ import print_function
import csv, json, os
from collections import defaultdict, Counter
from datetime import datetime

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-Rox-robots"
OUT = r"tools-temp\_tmp_rex_robots_cap_out.txt"

REX = (3624.06128, 51.745, 787.764648)  # from dossier

def dist(a, b):
    return ((a[0]-b[0])**2 + (a[1]-b[1])**2 + (a[2]-b[2])**2) ** 0.5

def parse_dt(s):
    if not s:
        return None
    s = s.strip().strip('"')
    for fmt in ("%Y-%m-%dT%H:%M:%S.%fZ", "%Y-%m-%dT%H:%M:%SZ"):
        try:
            return datetime.strptime(s[:26].rstrip("Z") + ("000000"[:max(0,26-len(s.rstrip("Z")))])[:], fmt.replace("Z","")) if False else datetime.strptime(s.replace("Z",""), fmt.replace("Z",""))
        except Exception:
            continue
    try:
        return datetime.fromisoformat(s.replace("Z", "+00:00")).replace(tzinfo=None)
    except Exception:
        return None

lines = []
def w(s=""):
    lines.append(s)

with open(os.path.join(CAP, "enemy-dossier.json"), encoding="utf-8-sig") as f:
    dossier = json.load(f)

robots = []
other_near = []
for e in dossier.get("enemies", []):
    name = e.get("name") or ""
    pos = e.get("position") or {}
    p = (float(pos.get("x") or 0), float(pos.get("y") or 0), float(pos.get("z") or 0))
    d = dist(p, REX)
    md = str(e.get("monsterData") or "")
    low = name.lower()
    is_robot = ("cleaning robot" in low) or md == "297023"
    if is_robot:
        robots.append((d, e, p))
    elif d < 80:
        other_near.append((d, name, e.get("identity"), p, md))

robots.sort(key=lambda x: x[0])
w("=== ROBOTS IN DOSSIER (Cleaning / monsterData 297023) ===")
w("count=%d" % len(robots))
name_counts = Counter()
for d, e, p in robots:
    name_counts[e.get("name")] += 1
    w("  %s | %s | md=%s | fam=%s | L%d HP%d/%d RS=%s | pos=(%.3f,%.3f,%.3f) dRex=%.1f death=%s" % (
        e.get("identity"), e.get("name"), e.get("monsterData"), e.get("npcFamily"),
        e.get("level") or 0, e.get("currentHealth") or 0, e.get("maxHealth") or 0,
        e.get("runSpeed"), p[0], p[1], p[2], d, e.get("deathObserved")))
w("name_counts=%s" % dict(name_counts))
w()
w("=== OTHER ENTITIES WITHIN 80u OF REX (not robots) ===")
for d, name, ident, p, md in sorted(other_near)[:40]:
    w("  %.1f %s %s md=%s pos=(%.1f,%.1f,%.1f)" % (d, ident, name, md, p[0], p[1], p[2]))

# lifecycle / scfu appearance if present
for fname in ("npc-lifecycle.csv", "scfu-appearance.csv", "enemy-full-updates.csv"):
    path = os.path.join(CAP, fname)
    w()
    w("=== %s exists=%s ===" % (fname, os.path.exists(path)))

# enemy-state: unique robot identities with positions
state_path = os.path.join(CAP, "enemy-state.csv")
robot_ids = set()
robot_first_pos = {}
robot_last_pos = {}
robot_names = {}
with open(state_path, encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    for row in r:
        name = (row.get("Name") or "").strip()
        md = (row.get("MonsterData") or "").strip()
        if "cleaning robot" not in name.lower() and md != "297023":
            continue
        ident = (row.get("Identity") or "").strip()
        robot_ids.add(ident)
        robot_names[ident] = name
        try:
            p = (float(row["X"]), float(row["Y"]), float(row["Z"]))
        except Exception:
            continue
        if ident not in robot_first_pos:
            robot_first_pos[ident] = p
        robot_last_pos[ident] = p

w()
w("=== ENEMY-STATE UNIQUE ROBOTS ===")
w("unique=%d" % len(robot_ids))
# cluster near Rex (y~40-52, x~3590-3640, z~770-840)
near_rex = []
far = []
for ident in sorted(robot_ids):
    p = robot_first_pos.get(ident) or (0,0,0)
    d = dist(p, REX)
    entry = (d, ident, robot_names.get(ident), p, robot_last_pos.get(ident))
    if d < 100 and 35 <= p[1] <= 55:
        near_rex.append(entry)
    else:
        far.append(entry)
w("near_rex_y35-55_d100=%d" % len(near_rex))
for d, ident, name, p, lp in sorted(near_rex):
    w("  %.1f %s %s first=(%.3f,%.3f,%.3f) last=(%.3f,%.3f,%.3f)" % (
        d, ident, name, p[0], p[1], p[2], lp[0], lp[1], lp[2]))
w("far=%d (sample)" % len(far))
for d, ident, name, p, lp in sorted(far)[:15]:
    w("  %.1f %s %s first=(%.3f,%.3f,%.3f)" % (d, ident, name, p[0], p[1], p[2]))

# respawns
w()
w("=== RESPAWNS ===")
with open(os.path.join(CAP, "enemy-respawns.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        w("  %s | %s | delay=%s | death=(%s,%s,%s) respawn=(%s,%s,%s) | %s" % (
            row.get("Name"), row.get("Status"), row.get("RespawnDelaySeconds"),
            row.get("DeathX"), row.get("DeathY"), row.get("DeathZ"),
            row.get("RespawnX"), row.get("RespawnY"), row.get("RespawnZ"),
            row.get("Detail")))

# movement: NpcPath FollowTarget by identity near rex robots
w()
w("=== MOVEMENT NpcPath by near-rex robot ===")
near_ids = set(x[1] for x in near_rex)
# identity formats differ: SimpleChar:79866518 vs (SimpleChar:79866518)
def norm_id(s):
    s = (s or "").strip()
    if s.startswith("(") and s.endswith(")"):
        s = s[1:-1]
    return s

near_norm = set(norm_id(i) for i in near_ids)
move_path = os.path.join(CAP, "movement-packets.csv")
segs = defaultdict(list)
with open(move_path, encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    for row in r:
        if (row.get("MessageType") or "") != "FollowTarget":
            continue
        if (row.get("FollowKind") or "") != "NpcPath":
            continue
        src = norm_id(row.get("SourceIdentity") or ("SimpleChar:" + (row.get("SourceInstance") or "")))
        # SourceInstance is often hex/int
        si = row.get("SourceInstance") or ""
        try:
            si_int = int(si, 0) if si else 0
            src2 = "SimpleChar:%X" % (si_int & 0xFFFFFFFF)
            src3 = "SimpleChar:%08X" % (si_int & 0xFFFFFFFF)
        except Exception:
            src2 = src3 = ""
        matched = None
        for cand in (src, "SimpleChar:" + si, src2, src3):
            if norm_id(cand) in near_norm or cand in near_ids:
                matched = cand
                break
        # also match by instance hex in near_ids
        for nid in near_ids:
            if si and si.upper() in nid.upper().replace("0X",""):
                matched = nid
                break
            try:
                if si_int and int(nid.split(":")[-1], 16) == (si_int & 0xFFFFFFFF):
                    matched = nid
                    break
            except Exception:
                pass
        if not matched:
            continue
        try:
            segs[matched].append((
                row.get("CapturedUtc"),
                float(row["CurrentX"]), float(row["CurrentY"]), float(row["CurrentZ"]),
                float(row["DestinationX"]), float(row["DestinationY"]), float(row["DestinationZ"]),
            ))
        except Exception:
            pass

for ident in sorted(segs.keys(), key=lambda i: -len(segs[i])):
    path = segs[ident]
    w("%s segments=%d name=%s" % (ident, len(path), robot_names.get(ident)))
    # unique destinations
    dests = []
    for _, cx,cy,cz, dx,dy,dz in path[:8]:
        dests.append((dx,dy,dz))
    for i, (ts, cx,cy,cz, dx,dy,dz) in enumerate(path[:12]):
        w("  [%d] %s cur=(%.2f,%.2f,%.2f) dst=(%.2f,%.2f,%.2f)" % (i, ts, cx,cy,cz, dx,dy,dz))
    if len(path) > 12:
        w("  ... +%d more" % (len(path)-12))
    # route length / unique waypoints
    uniq = []
    for _,_,_,_,dx,dy,dz in path:
        key = (round(dx,1), round(dy,1), round(dz,1))
        if not uniq or uniq[-1] != key:
            uniq.append(key)
    w("  unique_dest_seq=%d first=%s last=%s" % (len(uniq), uniq[0] if uniq else None, uniq[-1] if uniq else None))

# combat / animation hints
w()
w("=== COMBAT sample (robots) ===")
combat_path = os.path.join(CAP, "enemy-combat.csv")
with open(combat_path, encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    cols = r.fieldnames
    w("cols=%s" % cols)
    n = 0
    for row in r:
        name = (row.get("Name") or row.get("AttackerName") or row.get("SourceName") or "")
        blob = " ".join((v or "") for v in row.values())
        if "cleaning" not in blob.lower() and "297023" not in blob:
            continue
        w("  " + str({k: row.get(k) for k in (cols or [])[:12]}))
        n += 1
        if n >= 20:
            break
    w("combat_robot_rows_shown=%d" % n)

# fight events
w()
w("=== FIGHT EVENTS (robots) ===")
fp = os.path.join(CAP, "enemy-fight-events.log")
if os.path.exists(fp):
    for i, line in enumerate(open(fp, encoding="utf-8", errors="replace")):
        if "cleaning" in line.lower() or "297023" in line:
            w(line.rstrip()[:300])

# npc family / names summary for spawn population
w()
w("=== POPULATION: unique near-rex robots as spawn set ===")
# Prefer identities that were alive in dossier with cleaning names near Rex
dossier_robots_near = []
for d, e, p in robots:
    if d < 100 and 35 <= p[1] <= 55:
        dossier_robots_near.append((d, e, p))
w("dossier_near=%d" % len(dossier_robots_near))
for d, e, p in sorted(dossier_robots_near):
    w("  SPAWN %s | %s | md=%s | L%s HP%s RS=%s | (%.6f,%.6f,%.6f)" % (
        e["identity"], e["name"], e.get("monsterData"), e.get("level"), e.get("maxHealth"),
        e.get("runSpeed"), p[0], p[1], p[2]))

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print("Wrote", OUT, "lines", len(lines))
