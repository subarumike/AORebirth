# Extract aggro range, fight packets, death/corpse from L7 gold 20260725-002423
from __future__ import print_function
import os, re, math
from collections import defaultdict

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-002423"
OUT = r"tools-temp\_tmp_cap_002423_combat.txt"
PLAYER = "797E30D7"
PF = 1443840

def parse_ts(line):
    m = re.match(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+)", line)
    if not m:
        return None
    from datetime import datetime
    return datetime.strptime(m.group(1)[:26], "%Y-%m-%dT%H:%M:%S.%f")

lines_out = []
def w(s=""):
    lines_out.append(s)

events = open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace").read().splitlines()

# Player positions over time from CHAR-MOVED or SCFU self
player_pos = []  # (ts, x, z)
mob_spawn = {}   # id -> (name, x, z, first_ts)

for line in events:
    ts = parse_ts(line)
    if ts is None:
        continue
    if "[CHAR-MOVED]" in line and PLAYER in line:
        m = re.search(r"pos=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        if m:
            player_pos.append((ts, float(m.group(1)), float(m.group(3))))
    if "[DYNEL-SPAWNED]" in line and "player=False" in line:
        mid = re.search(r"identity=\(SimpleChar:([0-9A-F]+)\)", line, re.I)
        m = re.search(r"name=([^=]+?) player=.*?pos=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        if mid and m:
            mob_spawn[mid.group(1)] = (m.group(1), float(m.group(2)), float(m.group(4)), ts)

w("=== first fights: FollowTarget Target then Attack/SpecialAttack ===")
# Find sequences where mob switches to FollowTarget Type=Target toward player, then SAW/Attack
fight_events = []
for i, line in enumerate(events):
    if "FollowTargetMessage" in line and "Type=Target" in line:
        mid = re.search(r"Identity=\(SimpleChar:([0-9A-F]+)\)", line)
        if mid and mid.group(1) != PLAYER:
            fight_events.append((parse_ts(line), "FOLLOW_TARGET", mid.group(1), line[:200]))
    if "SpecialAttackWeaponMessage" in line or "type=SpecialAttackWeapon" in line:
        mid = re.search(r"Identity=\(SimpleChar:([0-9A-F]+)\)", line, re.I)
        if mid:
            fight_events.append((parse_ts(line), "SAW", mid.group(1), line[:220]))
    if "AttackMessage" in line or ("type=Attack " in line and "IN-N3" in line):
        mid = re.search(r"Identity=\(SimpleChar:([0-9A-F]+)\)", line, re.I)
        if mid:
            fight_events.append((parse_ts(line), "ATTACK", mid.group(1), line[:220]))
    if "CharacterActionMessage" in line and ("Death" in line or "Die" in line or "Action=Death" in line or "Action=Die" in line):
        fight_events.append((parse_ts(line), "DEATH_ACT", "?", line[:260]))
    if "CorpseFullUpdate" in line or "type=Corpse" in line:
        fight_events.append((parse_ts(line), "CORPSE", "?", line[:260]))

# Print first 80 combat-ish
for e in fight_events[:80]:
    w("%s %s %s %s" % (e[0], e[1], e[2], e[3]))

w("\n=== Aggro distance: first FOLLOW_TARGET Type=Target per mob vs player pos ===")
aggro_dists = []
seen_mob = set()
for ts, kind, mid, _ in fight_events:
    if kind != "FOLLOW_TARGET" or mid in seen_mob:
        continue
    seen_mob.add(mid)
    if mid not in mob_spawn or not player_pos:
        continue
    # nearest player pos at or before ts
    px = pz = None
    for pts, x, z in player_pos:
        if pts <= ts:
            px, pz = x, z
        else:
            break
    if px is None:
        px, pz = player_pos[0][1], player_pos[0][2]
    name, mx, mz, _ = mob_spawn[mid]
    dist = math.sqrt((px - mx) ** 2 + (pz - mz) ** 2)
    aggro_dists.append((dist, mid, name, px, pz, mx, mz))
    w("aggro dist=%.2f mob=%s name=%s player=(%.1f,%.1f) mob=(%.1f,%.1f)" % (dist, mid, name, px, pz, mx, mz))

if aggro_dists:
    dists = [d[0] for d in aggro_dists]
    w("aggro_count=%d min=%.2f median=%.2f max=%.2f mean=%.2f" % (
        len(dists), min(dists), sorted(dists)[len(dists)//2], max(dists), sum(dists)/len(dists)))

w("\n=== SAW / Attack detail samples ===")
saw_n = atk_n = 0
for line in events:
    if "SpecialAttackWeaponMessage" in line and saw_n < 8:
        w(line[:400])
        saw_n += 1
    if "AttackMessage {" in line and atk_n < 8:
        w(line[:400])
        atk_n += 1

w("\n=== Death / corpse / die anim samples ===")
for line in events:
    if any(k in line for k in ("Action=Death", "Action=Die", "dieAnim", "CorpseFullUpdate", "Health=0", "alive=False")):
        if "Stat " in line and "Health=0" in line:
            w(line[:300])
        elif "Corpse" in line or "Death" in line or "Die" in line or "dieAnim" in line or "alive=False" in line:
            w(line[:350])

# Timing: player Attack then mob SAW delay for first fight
w("\n=== First fight timing (OUT Attack vs IN SAW/Attack for mob 799361E7) ===")
# From earlier brief Carrol Welding was first fight target 799361E7 around 22:26:17
for line in events:
    ts = parse_ts(line)
    if ts is None:
        continue
    if ts.hour == 22 and ts.minute == 26 and 15 <= ts.second <= 40:
        if any(k in line for k in ("Attack", "SpecialAttack", "FollowTarget", "NumFighting", "Health=")):
            if "DETAIL" in line or "OUT-N3" in line or "IN-N3]" in line:
                w(line[:280])

open(OUT, "w", encoding="utf-8").write("\n".join(lines_out))
print("wrote", OUT, "lines", len(lines_out), "aggros", len(aggro_dists))
