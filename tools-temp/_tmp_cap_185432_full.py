# Full gameplay extract from 20260725-185432 (doors/mobs/combat/corpse/loot/target)
from __future__ import print_function
import csv
import collections
import json
import os
import struct

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-185432"
OUT = r"tools-temp/_tmp_cap_185432_full.txt"
lines = []


def w(s=""):
    if isinstance(s, str):
        s = s.replace("\ufeff", "")
    lines.append(s)
    try:
        print(s)
    except Exception:
        print(s.encode("ascii", "replace").decode("ascii"))


info = json.load(open(os.path.join(CAP, "capture_info.json"), encoding="utf-8-sig"))
w("=== capture_info ===")
w("pf=%s char=%s" % (info.get("playfieldId"), info.get("characterName")))
w("counts=%s" % info.get("packetCounts"))

# mission flow
w("\n=== mission-flow ===")
with open(os.path.join(CAP, "mission-flow.log"), encoding="utf-8", errors="replace") as f:
    for line in f:
        w(line.rstrip()[:240])

# doors/chests uniq
doors = []
chests = []
saws = []
attacks = []
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        nt = row.get("N3TypeName") or ""
        d = row.get("Direction") or ""
        if not d.startswith("IN"):
            continue
        hx = (row.get("RawHex") or "").replace(" ", "").upper()
        if nt == "DoorFullUpdate":
            doors.append(hx)
        if nt == "ChestFullUpdate":
            chests.append(hx)
        if nt == "SpecialAttackWeapon":
            saws.append((row.get("Timestamp"), row.get("Detail") or "", hx[:160]))
        if nt in ("Attack", "AttackInfo", "WeaponItemFullUpdate"):
            attacks.append((row.get("Timestamp"), nt, (row.get("Detail") or "")[:200], hx[:120]))

seen = set()
ud = []
for h in doors:
    k = h[40:80] if len(h) > 80 else h
    if k in seen:
        continue
    seen.add(k)
    ud.append(h)
seen = set()
uc = []
for h in chests:
    k = h[40:80] if len(h) > 80 else h
    if k in seen:
        continue
    seen.add(k)
    uc.append(h)
w("\ndoors=%d uniq=%d chests=%d uniq=%d" % (len(doors), len(ud), len(chests), len(uc)))

# write door/chest frags if any new
if ud:
    open(r"tools-temp/_tmp_doors_1419349_185432.csfrag", "w", encoding="utf-8", newline="\n").write(
        "        public static readonly string[] Doors_1419349 =\n        {\n"
        + "\n".join('            "%s",' % h for h in ud)
        + "\n        };\n"
    )
if uc:
    open(r"tools-temp/_tmp_chests_1419349_185432.csfrag", "w", encoding="utf-8", newline="\n").write(
        "        public static readonly string[] Chests_1419349 =\n        {\n"
        + "\n".join('            "%s",' % h for h in uc)
        + "\n        };\n"
    )

# SCFU npcs
w("\n=== SCFU uniq NPCs (mission-ish coords) ===")
npcs = []
path = os.path.join(CAP, "scfu-appearance.csv")
cols = None
with open(path, encoding="utf-8-sig") as f:
    r = csv.DictReader(f)
    cols = r.fieldnames
    for row in r:
        name = row.get("Name") or ""
        if not name or "Getkeep" in name:
            continue
        try:
            x = float(row.get("PositionX") or 0)
            y = float(row.get("PositionY") or 0)
            z = float(row.get("PositionZ") or 0)
        except Exception:
            continue
        npcs.append(row)

w("scfu cols=%s" % cols)
seen = set()
uniq = []
for row in npcs:
    ident = row.get("Identity") or ""
    if ident in seen:
        continue
    seen.add(ident)
    uniq.append(row)

# Prefer interior cluster near spawn (~0-120 x/z) else all with y~5
interior = []
for row in uniq:
    try:
        x = float(row.get("PositionX") or 0)
        y = float(row.get("PositionY") or 0)
        z = float(row.get("PositionZ") or 0)
    except Exception:
        continue
    if y < 20 and 0 <= x <= 150 and 0 <= z <= 200:
        interior.append(row)

w("uniq=%d interior_cluster=%d" % (len(uniq), len(interior)))
use = interior if interior else uniq
for row in use:
    w(
        "  %s xyz=(%s,%s,%s) md=%s lvl=%s head=%s meshes=%s tex=%s flags=%s"
        % (
            row.get("Name"),
            row.get("PositionX"),
            row.get("PositionY"),
            row.get("PositionZ"),
            row.get("MonsterData"),
            row.get("Level"),
            row.get("HeadMesh"),
            (row.get("Meshes") or "")[:80],
            (row.get("Textures") or "")[:80],
            (row.get("Flags") or "")[:40],
        )
    )

# enemy dossier / fight
w("\n=== enemy-dossier summary ===")
dossier = os.path.join(CAP, "enemy-dossier.json")
if os.path.exists(dossier):
    data = json.load(open(dossier, encoding="utf-8-sig"))
    if isinstance(data, dict):
        w("keys=%s" % list(data.keys())[:20])
        for k in list(data.keys())[:5]:
            v = data[k]
            w("  %s -> %s" % (k, str(v)[:200]))
    elif isinstance(data, list):
        w("list len=%d" % len(data))
        for e in data[:15]:
            w("  %s" % str(e)[:220])

w("\n=== enemy-fight-events (Death/Attack/SAW) ===")
with open(os.path.join(CAP, "enemy-fight-events.log"), encoding="utf-8", errors="replace") as f:
    for i, line in enumerate(f):
        if i > 80:
            break
        if any(k in line for k in ("Death", "Attack", "SpecialAttack", "Corpse", "Weapon", "StopFight")):
            w(line.rstrip()[:220])

w("\n=== SAW sample (%d) ===" % len(saws))
for t, det, hx in saws[:12]:
    w("  %s | %s | %s" % (t, det[:140], hx))

w("\n=== Attack/WIFU sample ===")
for t, nt, det, hx in attacks[:20]:
    w("  %s %s | %s" % (nt, t, det))

# corpses
w("\n=== corpse-full-updates ===")
cpath = os.path.join(CAP, "corpse-full-updates.csv")
if os.path.exists(cpath):
    with open(cpath, encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    w("rows=%d cols=%s" % (len(rows), rows[0].keys() if rows else None))
    for row in rows[:15]:
        w(
            "  name=%s md=%s mesh=%s xyz=(%s,%s,%s) hx=%s"
            % (
                row.get("Name") or row.get("CorpseName"),
                row.get("MonsterData"),
                row.get("HeadMesh") or row.get("Mesh"),
                row.get("PositionX") or row.get("X"),
                row.get("PositionY") or row.get("Y"),
                row.get("PositionZ") or row.get("Z"),
                (row.get("RawHex") or "")[:40],
            )
        )

w("\n=== corpse-loot-observations ===")
lpath = os.path.join(CAP, "corpse-loot-observations.csv")
if os.path.exists(lpath):
    with open(lpath, encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    w("rows=%d" % len(rows))
    if rows:
        w("cols=%s" % list(rows[0].keys()))
    for row in rows[:40]:
        w("  %s" % " | ".join("%s=%s" % (k, (row.get(k) or "")[:60]) for k in list(rows[0].keys())[:12]))

# inventory loot creates
w("\n=== inventory / TemplateAction loot hints ===")
ipath = os.path.join(CAP, "inventory-updates.csv")
if os.path.exists(ipath):
    with open(ipath, encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    w("inv rows=%d" % len(rows))
    for row in rows[:30]:
        w("  %s" % " | ".join("%s=%s" % (k, (row.get(k) or "")[:50]) for k in list(row.keys())[:10]))

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
w("\nwrote " + OUT)
