# Deep combat: SAW specials decode, aggro on sight distance, death Parameter2
from __future__ import print_function
import os, re, math, csv
from datetime import datetime

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-002423"
OUT = r"tools-temp\_tmp_cap_002423_combat2.txt"
PLAYER = "797E30D7"

def parse_ts(line):
    m = re.match(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+)", line)
    if not m:
        return None
    return datetime.strptime(m.group(1)[:26], "%Y-%m-%dT%H:%M:%S.%f")

buf = []
def w(s=""):
    buf.append(s)

events = open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace").read().splitlines()

# player positions from SCFU or CHAR-MOVED
player_pos = []
mob_spawn = {}
for line in events:
    ts = parse_ts(line)
    if ts is None:
        continue
    if "PlayfieldId=1443840" in line and 'Name="Getkeep"' in line or (
        "PlayfieldId=1443840" in line and PLAYER in line and "SimpleCharFullUpdateMessage" in line and 'Name="Getkeep"' in line
    ):
        pos = re.search(r"Position=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        if pos:
            player_pos.append((ts, float(pos.group(1)), float(pos.group(3))))
    if "[CHAR-MOVED]" in line and PLAYER in line:
        m = re.search(r"pos=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        if m:
            player_pos.append((ts, float(m.group(1)), float(m.group(3))))
    if "[DYNEL-SPAWNED]" in line and "player=False" in line:
        mid = re.search(r"identity=\(SimpleChar:([0-9A-F]+)\)", line, re.I)
        m = re.search(r"name=([^=]+?) player=.*?pos=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        if mid and m and float(m.group(3)) < 20:
            mob_spawn[mid.group(1)] = (m.group(1).strip(), float(m.group(2)), float(m.group(4)))

w("player_pos samples=%d mobs=%d" % (len(player_pos), len(mob_spawn)))

# Mob-initiated SAW (SAW identity is mob, and no player OUT Attack on that mob in prior 2s)
pending_id = None
mob_saws = []
for i, line in enumerate(events):
    if "[IN-N3]" in line and "type=SpecialAttackWeapon" in line:
        mid = re.search(r"identity=\(SimpleChar:([0-9A-F]+)\)", line, re.I)
        pending_id = mid.group(1) if mid else None
        continue
    if "SpecialAttackWeaponMessage" in line and pending_id and pending_id != PLAYER:
        ts = parse_ts(line)
        unk = re.search(r"Unknown1=(\d+) Unknown2=(\d+) Unknown3=(\d+) Unknown4=(\d+) Unknown5=(\d+)", line)
        specials = re.search(r"Specials=count=(\d+)", line)
        # look ahead for Attack target
        tgt = None
        for j in range(i, min(i+5, len(events))):
            if "AttackMessage" in events[j] and pending_id in events[j]:
                tm = re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", events[j])
                if tm:
                    tgt = tm.group(1)
                break
        mob_saws.append({
            "ts": ts, "mob": pending_id, "unk": unk.groups() if unk else None,
            "specials": int(specials.group(1)) if specials else None, "tgt": tgt, "line": line[:350]
        })
        pending_id = None

w("\n=== Mob SAW packets (%d) ===" % len(mob_saws))
unk_counts = {}
for s in mob_saws:
    key = s["unk"]
    unk_counts[key] = unk_counts.get(key, 0) + 1
    w("mob=%s specials=%s unk=%s tgt=%s" % (s["mob"], s["specials"], s["unk"], s["tgt"]))

w("\nunk histogram: %s" % unk_counts)

# Aggro on sight: mob SAW where player had NOT Out-Attacked that mob yet
w("\n=== Mob-first aggro (SAW before player Attack on that mob) ===")
player_attacked = set()
for line in events:
    if "OUT-N3-DETAIL" in line and "AttackMessage" in line:
        tm = re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", line)
        if tm:
            player_attacked.add(("atk", tm.group(1), parse_ts(line)))

# rebuild chronologically
player_first_atk = {}
for line in events:
    ts = parse_ts(line)
    if "OUT-N3-DETAIL" in line and "AttackMessage" in line:
        tm = re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", line)
        if tm and tm.group(1) not in player_first_atk:
            player_first_atk[tm.group(1)] = ts

for s in mob_saws:
    mob = s["mob"]
    first_player = player_first_atk.get(mob)
    if first_player is None or s["ts"] < first_player:
        # true aggro on sight
        dist = None
        if mob in mob_spawn and player_pos:
            px = pz = None
            for pts, x, z in player_pos:
                if pts <= s["ts"]:
                    px, pz = x, z
            if px is None and player_pos:
                px, pz = player_pos[0][1], player_pos[0][2]
            name, mx, mz = mob_spawn[mob]
            if px is not None:
                dist = math.sqrt((px-mx)**2 + (pz-mz)**2)
                w("SIGHT-AGGRO mob=%s name=%s dist=%.2f player=(%.1f,%.1f) mob=(%.1f,%.1f) ts=%s" % (
                    mob, name, dist, px, pz, mx, mz, s["ts"]))
            else:
                w("SIGHT-AGGRO mob=%s no player pos" % mob)
        else:
            w("SIGHT-AGGRO mob=%s spawn_missing=%s" % (mob, mob not in mob_spawn))

# Death Parameter2 + corpse delay
w("\n=== Death Action Parameter2 + corpse delay ===")
deaths = []
for i, line in enumerate(events):
    if "Action=Death" in line and "CharacterActionMessage" in line:
        ts = parse_ts(line)
        mid = re.search(r"Identity=\(SimpleChar:([0-9A-F]+)\)", line)
        p2 = re.search(r"Parameter2=(\d+)", line)
        corpse_ts = None
        for j in range(i, min(i+20, len(events))):
            if "CorpseFullUpdateMessage" in events[j]:
                corpse_ts = parse_ts(events[j])
                w("DEATH mob=%s p2=%s delay_ms=%s detail=%s" % (
                    mid.group(1) if mid else "?",
                    p2.group(1) if p2 else "?",
                    int((corpse_ts-ts).total_seconds()*1000) if corpse_ts and ts else "?",
                    line[line.find("CharacterAction"):line.find("CharacterAction")+180] if "CharacterAction" in line else line[:180]
                ))
                break
        deaths.append(1)

w("deaths=%d" % len(deaths))

# Decode SpecialAttackInfo from hex for one mob SAW
w("\n=== enemy-combat.csv head ===")
path = os.path.join(CAP, "enemy-combat.csv")
if os.path.exists(path):
    with open(path, encoding="utf-8-sig") as f:
        r = csv.DictReader(f)
        cols = r.fieldnames
        w("cols=%s" % cols)
        n = 0
        for row in r:
            if n < 15:
                w(str({k: row.get(k) for k in cols[:12]}))
            n += 1
        w("rows=%d" % n)

# FollowTarget Unknown1 values
w("\n=== FollowTarget Type=Target Unknown1 ===")
u1 = {}
for line in events:
    if "FollowTargetMessage" in line and "Type=Target" in line:
        m = re.search(r"Unknown1=(\d+)", line)
        if m:
            u1[m.group(1)] = u1.get(m.group(1), 0) + 1
w(str(u1))

open(OUT, "w", encoding="utf-8").write("\n".join(buf))
print("wrote", OUT)
