# Concurrent Malfunctioning Cleaning Robot population + paths
from __future__ import print_function
import csv, json, os
from collections import defaultdict
from datetime import datetime

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260721-Rox-robots"
OUT = r"tools-temp\_tmp_rex_robots_cap_out2.txt"
lines = []
def w(s=""):
    lines.append(s)

def parse_dt(s):
    s = (s or "").strip().strip('"')
    if not s:
        return None
    s = s.replace("Z", "")
    for n in (26, 23, 19):
        try:
            frag = s[:n]
            if n == 19:
                return datetime.strptime(frag, "%Y-%m-%dT%H:%M:%S")
            return datetime.strptime(frag, "%Y-%m-%dT%H:%M:%S.%f")
        except Exception:
            continue
    return None

def hex_inst(ident):
    # (SimpleChar:79866560) -> int
    try:
        return int(ident.strip().strip("()").split(":")[-1], 16)
    except Exception:
        return 0

with open(os.path.join(CAP, "enemy-dossier.json"), encoding="utf-8-sig") as f:
    dossier = json.load(f)

# enemy-state columns
state_path = os.path.join(CAP, "enemy-state.csv")
with open(state_path, encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    cols = r.fieldnames
    w("enemy-state cols=%s" % cols)
    sample = next(r, None)
    w("sample=%s" % sample)

# lifecycle
life_path = os.path.join(CAP, "npc-lifecycle.csv")
with open(life_path, encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    w()
    w("lifecycle cols=%s" % r.fieldnames)
    malf_life = []
    for row in r:
        name = (row.get("Name") or "")
        if "Malfunctioning Cleaning Robot" not in name:
            continue
        malf_life.append(row)
    w("malf lifecycle rows=%d" % len(malf_life))
    for row in malf_life[:40]:
        w("  %s | %s | %s | %s" % (row.get("CapturedUtc"), row.get("Event") or row.get("EventType") or row.get("LifecycleEvent"), row.get("Identity"), row.get("Name")))

# Track alive set from lifecycle spawn/despawn/death
alive = set()
max_alive = 0
max_alive_at = None
max_alive_set = set()
timeline = []
for row in malf_life:
    ev = (row.get("Event") or row.get("EventType") or row.get("LifecycleEvent") or row.get("Kind") or "").lower()
    ident = row.get("Identity") or ""
    ts = row.get("CapturedUtc")
    # inspect keys
    if not timeline:
        w("life keys example=%s" % list(row.keys()))
    if "spawn" in ev or ev in ("appeared", "visible", "charinplay"):
        alive.add(ident)
    elif "despawn" in ev or "gone" in ev or "death" in ev or "died" in ev:
        alive.discard(ident)
    timeline.append((ts, ev, ident, len(alive)))
    if len(alive) > max_alive:
        max_alive = len(alive)
        max_alive_at = ts
        max_alive_set = set(alive)

w()
w("max_concurrent_malf=%d at %s" % (max_alive, max_alive_at))
w("ids=%s" % sorted(max_alive_set))

# Also: dossier firstSeen - cluster by rounded position for unique anchors
w()
w("=== POSITION CLUSTERS (Malfunctioning only, y>=50 platform) ===")
clusters = []  # list of (cx,cy,cz, ids)
for e in dossier["enemies"]:
    if e.get("name") != "Malfunctioning Cleaning Robot":
        continue
    pos = e["position"]
    p = (pos["x"], pos["y"], pos["z"])
    if p[1] < 50:
        continue
    placed = False
    for c in clusters:
        cx,cy,cz,ids = c
        if abs(cx-p[0]) < 2.5 and abs(cz-p[2]) < 2.5 and abs(cy-p[1]) < 1.5:
            ids.append((e["identity"], p, e.get("firstSeenUtc"), e.get("lastUpdateUtc")))
            placed = True
            break
    if not placed:
        clusters.append([p[0], p[1], p[2], [(e["identity"], p, e.get("firstSeenUtc"), e.get("lastUpdateUtc"))]])

w("cluster_count=%d" % len(clusters))
for i, c in enumerate(sorted(clusters, key=lambda x: (x[0], x[2]))):
    ids = c[3]
    # earliest firstSeen as spawn identity
    ids_sorted = sorted(ids, key=lambda t: t[2] or "")
    first = ids_sorted[0]
    w("  C%02d n=%d center=(%.3f,%.3f,%.3f) first=%s @ %s last_ids=%s" % (
        i, len(ids), c[0], c[1], c[2], first[0], first[2], [x[0] for x in ids_sorted]))

# enemy-state with flexible columns
w()
w("=== STATE TRACKING ===")
with open(state_path, encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    # find name/identity/pos columns
    name_c = None
    id_c = None
    x_c = y_c = z_c = None
    t_c = None
    for c in (r.fieldnames or []):
        cl = c.lower()
        if cl == "name" or cl.endswith("name") and name_c is None:
            if "name" in cl:
                name_c = c
        if "identity" in cl and id_c is None:
            id_c = c
        if cl in ("x", "posx", "positionx") or cl.endswith(".x"):
            x_c = c
        if cl in ("y", "posy", "positiony"):
            y_c = c
        if cl in ("z", "posz", "positionz"):
            z_c = c
        if "utc" in cl and t_c is None:
            t_c = c
    w("mapped name=%s id=%s xyz=%s,%s,%s t=%s" % (name_c, id_c, x_c, y_c, z_c, t_c))

    # concurrent by timestamp buckets using dossier deathObserved false unique positions?
    pass

# movement packets header + malf sources
w()
w("=== MOVEMENT for Malfunctioning source instances ===")
malf_inst = set()
for e in dossier["enemies"]:
    if e.get("name") == "Malfunctioning Cleaning Robot":
        malf_inst.add(hex_inst(e["identity"]))

move_path = os.path.join(CAP, "movement-packets.csv")
with open(move_path, encoding="utf-8-sig", newline="") as f:
    r = csv.DictReader(f)
    w("move cols=%s" % r.fieldnames)
    by = defaultdict(list)
    for row in r:
        if row.get("MessageType") != "FollowTarget":
            continue
        if row.get("FollowKind") != "NpcPath":
            continue
        try:
            si = int(row.get("SourceInstance") or "0", 0)
        except Exception:
            continue
        if si not in malf_inst:
            continue
        try:
            by[si].append((
                row["CapturedUtc"],
                float(row["CurrentX"]), float(row["CurrentY"]), float(row["CurrentZ"]),
                float(row["DestinationX"]), float(row["DestinationY"]), float(row["DestinationZ"]),
            ))
        except Exception:
            pass
    w("malf movers=%d" % len(by))
    for si in sorted(by.keys(), key=lambda k: -len(by[k])):
        path = by[si]
        w("  0x%X segs=%d" % (si, len(path)))
        # compress unique dest sequence
        uniq = []
        for _,_,_,_,dx,dy,dz in path:
            key = (round(dx, 2), round(dy, 2), round(dz, 2))
            if not uniq or uniq[-1] != key:
                uniq.append(key)
        w("    dest_seq_len=%d" % len(uniq))
        for j, (dx,dy,dz) in enumerate(uniq[:20]):
            w("      wp%02d (%.3f,%.3f,%.3f)" % (j, dx, dy, dz))
        if len(uniq) > 20:
            w("      ... +%d" % (len(uniq)-20))
        # first/last current
        f = path[0]; l = path[-1]
        w("    first_cur=(%.3f,%.3f,%.3f) last_cur=(%.3f,%.3f,%.3f)" % (f[1],f[2],f[3], l[1],l[2],l[3]))

# scfu appearance for malf
scfu = os.path.join(CAP, "scfu-appearance.csv")
if os.path.exists(scfu):
    w()
    w("=== SCFU appearance Malfunctioning ===")
    with open(scfu, encoding="utf-8-sig", newline="") as f:
        r = csv.DictReader(f)
        w("cols=%s" % r.fieldnames)
        n = 0
        for row in r:
            blob = " ".join((v or "") for v in row.values())
            if "Malfunctioning" not in blob and "798665" not in blob and "797D36A5" not in blob and "79543CB6" not in blob:
                continue
            # print compact
            keys = ["Identity", "Name", "MonsterData", "MonsterScale", "VisualFlags", "RunSpeed", "Level", "Health", "MaxHealth", "Flags", "NpcFamily", "X", "Y", "Z"]
            w("  " + str({k: row.get(k) for k in keys if k in (r.fieldnames or [])}))
            n += 1
            if n >= 30:
                break

# enemy-full-updates
efu = os.path.join(CAP, "enemy-full-updates.csv")
if os.path.exists(efu):
    w()
    w("=== enemy-full-updates Malfunctioning sample ===")
    with open(efu, encoding="utf-8-sig", newline="") as f:
        r = csv.DictReader(f)
        w("cols=%s" % r.fieldnames)
        n = 0
        for row in r:
            name = row.get("Name") or ""
            if name != "Malfunctioning Cleaning Robot":
                continue
            w("  id=%s pos=(%s,%s,%s) flags=%s waypoints=%s los=%s scale=%s fam=%s hp=%s/%s" % (
                row.get("Identity"), row.get("X") or row.get("PosX"), row.get("Y") or row.get("PosY"), row.get("Z") or row.get("PosZ"),
                row.get("Flags"), row.get("HasWaypoints") or row.get("Waypoints"), row.get("NpcLosHeight"),
                row.get("MonsterScale"), row.get("NpcFamily"), row.get("Health") or row.get("CurrentHealth"), row.get("MaxHealth")))
            n += 1
            if n >= 25:
                break

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))
print("Wrote", OUT, "lines", len(lines), "clusters", len(clusters), "max_alive", max_alive)
