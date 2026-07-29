# Deep extract: all SCFU + SAW by identity + corpse MD + loot items from 185432
from __future__ import print_function
import csv
import collections
import json
import os
import struct
import re

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-185432"
OUT = r"tools-temp/_tmp_cap_185432_deep.txt"
lines = []


def w(s=""):
    if isinstance(s, str):
        s = s.replace("\ufeff", "")
    lines.append(s)


# All SCFU rows unique by identity (include Levi etc regardless of cluster)
seen = set()
npcs = []
with open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        name = row.get("Name") or ""
        if not name or name == "Getkeep":
            continue
        ident = row.get("Identity") or ""
        if ident in seen:
            continue
        seen.add(ident)
        npcs.append(row)

w("=== ALL uniq SCFU (%d) ===" % len(npcs))
for row in npcs:
    w(
        "%s | id=%s | xyz=(%s,%s,%s) | md=%s lvl=%s hp=%s scale=%s head=%s | meshes=%s | tex=%s | specials=%s"
        % (
            row.get("Name"),
            row.get("Identity"),
            row.get("PositionX"),
            row.get("PositionY"),
            row.get("PositionZ"),
            row.get("MonsterData"),
            row.get("Level"),
            row.get("Health"),
            row.get("MonsterScale"),
            row.get("HeadMesh"),
            row.get("Meshes"),
            row.get("Textures"),
            (row.get("SpecialAttacks") or "")[:120],
        )
    )

# Map identity short hex -> name
id_name = {}
for row in npcs:
    ident = row.get("Identity") or ""
    m = re.search(r"([0-9A-Fa-f]{8})", ident)
    if m:
        id_name[m.group(1).upper()] = row.get("Name")

# SAW decode from raw
w("\n=== Enemy SAW decoded ===")
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") != "SpecialAttackWeapon":
            continue
        if not (row.get("Direction") or "").startswith("IN"):
            continue
        hx = (row.get("RawHex") or "").replace(" ", "").upper()
        # find C350 + identity
        b = bytes.fromhex(hx)
        # after header: look for 0000C350
        idx = b.find(bytes.fromhex("0000C350"))
        if idx < 0:
            continue
        inst = struct.unpack_from(">I", b, idx + 4)[0]
        key = "%08X" % inst
        name = id_name.get(key, "?")
        # skip player
        if key == "797E30D7":
            continue
        # specials count after identity+unk
        # layout rough: C350 + inst(4) + unk(4?) + count?
        rest = b[idx + 8 :]
        # dump first ints
        ints = []
        for i in range(0, min(len(rest) - 3, 40), 4):
            ints.append("%08X" % struct.unpack_from(">I", rest, i)[0])
        w("  %s %s ints=%s" % (key, name, " ".join(ints[:10])))

# WIFU identities
w("\n=== WIFU ===")
with open(os.path.join(CAP, "raw-packets.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") != "WeaponItemFullUpdate":
            continue
        if not (row.get("Direction") or "").startswith("IN"):
            continue
        det = row.get("Detail") or ""
        hx = (row.get("RawHex") or "").replace(" ", "").upper()[:100]
        w("  det=%s hx=%s" % (det[:180], hx))

# Corpse MD/CATMesh from raw hex
w("\n=== Corpse decoded (first unique names) ===")


def be_i(b, o):
    return struct.unpack_from(">i", b, o)[0]


seen_c = set()
with open(os.path.join(CAP, "corpse-full-updates.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        name = row.get("CorpseName") or ""
        if name in seen_c:
            continue
        seen_c.add(name)
        hx = (row.get("RawHex") or "").replace(" ", "").upper()
        b = bytes.fromhex(hx)
        # find CATMesh / MD by scanning known patterns - print MD from csv cols if any
        w(
            "  %s cat=%s md=%s credits=%s dead=%s len=%s"
            % (
                name,
                row.get("CorpseCatMesh"),
                row.get("CorpseMonsterData"),
                row.get("CorpseCredits"),
                row.get("DeadNpcName"),
                row.get("PacketLength"),
            )
        )
        # search for monster data ints near end
        # print last 20 ints
        if len(b) >= 80:
            tail = []
            for i in range(len(b) - 80, len(b) - 3, 4):
                tail.append("%d" % be_i(b, i))
            w("    tail_ints=%s" % " ".join(tail[-16:]))

# Loot items detail
w("\n=== corpse loot Items field ===")
with open(os.path.join(CAP, "corpse-loot-observations.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        w(
            "  %s count=%s items=%s md=%s credits=%s"
            % (
                row.get("EnemyName"),
                row.get("ItemCount"),
                row.get("Items"),
                row.get("MonsterData"),
                row.get("CorpseCredits"),
            )
        )

# Death Parameter2
w("\n=== Death CharacterAction ===")
with open(os.path.join(CAP, "enemy-fight-events.log"), encoding="utf-8", errors="replace") as f:
    for line in f:
        if "Death" in line or "Parameter2" in line:
            w(line.rstrip()[:240])

# enemy-state for weapons
w("\n=== enemy-state.csv sample ===")
ep = os.path.join(CAP, "enemy-state.csv")
if os.path.exists(ep):
    with open(ep, encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    w("rows=%d cols=%s" % (len(rows), list(rows[0].keys())[:15] if rows else None))
    for row in rows[:20]:
        w("  %s" % " | ".join("%s=%s" % (k, (row.get(k) or "")[:40]) for k in list(rows[0].keys())[:10]))

open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", OUT, "lines", len(lines))
