# Accurate aggro range + SAW special decode from packets.hex
from __future__ import print_function
import os, re, struct, math
from datetime import datetime

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-002423"
OUT = r"tools-temp\_tmp_cap_002423_combat3.txt"
PLAYER = 0x797E30D7

def parse_ts(s):
    return datetime.strptime(s[:26], "%Y-%m-%dT%H:%M:%S.%f")

buf=[]
def w(s=""): buf.append(s)

# Track player XYZ from SCFU of player in events
events = open(os.path.join(CAP,"events.log"),encoding="utf-8",errors="replace")
player_pos=[]
mob_pos={}
for line in events:
    ts=None
    m=re.match(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+)", line)
    if m: ts=parse_ts(m.group(1))
    if ts is None: continue
    if "SimpleCharFullUpdateMessage" in line and 'Name="Getkeep"' in line and "PlayfieldId=1443840" in line:
        pos=re.search(r"Position=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        if pos:
            player_pos.append((ts,float(pos.group(1)),float(pos.group(3))))
    if "[DYNEL-SPAWNED]" in line and "player=False" in line:
        mid=re.search(r"identity=\(SimpleChar:([0-9A-F]+)\)", line, re.I)
        pm=re.search(r"name=([^=]+?) player=.*?pos=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        if mid and pm and float(pm.group(3))<20:
            mob_pos[mid.group(1)]=(pm.group(1).strip(), float(pm.group(2)), float(pm.group(4)))
    # also update mob from SCFU when fighting
    if "SimpleCharFullUpdateMessage" in line and "PlayfieldId=1443840" in line and 'Name="Getkeep"' not in line:
        mid=re.search(r"\[IN-N3\].*identity=\(SimpleChar:([0-9A-F]+)\)", line)  # won't match DETAIL
        pass

w("player SCFU pos samples in instance: %d" % len(player_pos))
for p in player_pos[:5]:
    w("  %s (%.1f, %.1f)" % p)

# Also parse CHARACDCMove / CharDCMove from events
move_n=0
for line in open(os.path.join(CAP,"events.log"),encoding="utf-8",errors="replace"):
    if "CharDCMove" in line or "CHAR-MOVED" in line or "MoveMessage" in line:
        move_n+=1
        if move_n<=5:
            w("MOVE sample: %s" % line[:200])
w("move-like lines=%d" % move_n)

# From packets.hex: find mob SAW hex and decode specials
# Look for SpecialAttackWeapon n3 with identity after 00C350
hexlog=open(os.path.join(CAP,"packets.hex.log"),encoding="utf-8",errors="replace")
saw_samples=[]
for line in hexlog:
    if "n3=SpecialAttackWeapon" not in line and "SpecialAttackWeapon" not in line:
        continue
    if "799361" not in line and "7990F375" not in line:
        continue
    hm=re.search(r"hex=([0-9A-Fa-f]+)", line)
    if not hm: continue
    h=hm.group(1).upper()
    # body after transport: find C350 + identity
    b=bytes.fromhex(h)
    # find SpecialAttackWeapon type 0x365A507? actually N3 type for SAW
    # Decode Unknown1-5 from detail we already have; decode first SpecialAttackInfo
    # Structure after identity: Unknown byte, then Specials count, then infos...
    # Prefer detail from events - extract SpecialAttackInfo fields if present
    if len(saw_samples)<3:
        saw_samples.append(h[:120])
        w("SAW hex head %s" % h[:160])

# Decode first mob SAW from events DETAIL if it expands Specials
# Grep packets for AttackInfo after SAW
w("\n=== AttackInfo after mob SAW (first fights) ===")
events=open(os.path.join(CAP,"events.log"),encoding="utf-8",errors="replace").read().splitlines()
for i,line in enumerate(events):
    if "type=SpecialAttackWeapon" in line and ("799361" in line or "7990F375" in line):
        # next 15 lines
        for j in range(i, min(i+12, len(events))):
            if any(k in events[j] for k in ("AttackInfo","HealthDamage","AttackMessage","SpecialAttack")):
                w(events[j][:300])
        w("---")
        if i>2000: 
            break

# Better aggro: when mob first SAW, find latest player Getkeep SCFU OR use Coordinated from CharDCMove OUT
# Parse OUT CharDCMove positions
out_pos=[]
for line in events:
    ts=None
    m=re.match(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+)", line)
    if m: ts=parse_ts(m.group(1))
    if "OUT-N3-DETAIL" in line and "CharDCMove" in line:
        pos=re.search(r"Coordinates=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line) or re.search(r"Position=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        if pos and ts:
            out_pos.append((ts,float(pos.group(1)),float(pos.group(3))))
w("OUT CharDCMove samples=%d" % len(out_pos))
for p in out_pos[::max(1,len(out_pos)//10)][:12]:
    w("  %s (%.1f,%.1f)" % p)

# For each sight-aggro mob SAW before player attack, compute dist with out_pos
player_first_atk={}
for line in events:
    if "OUT-N3-DETAIL" in line and "AttackMessage" in line:
        tm=re.search(r"Target=\(SimpleChar:([0-9A-F]+)\)", line)
        ts=parse_ts(line[:26]) if line[:4]=="2026" else None
        m=re.match(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+)", line)
        if m and tm and tm.group(1) not in player_first_atk:
            player_first_atk[tm.group(1)]=parse_ts(m.group(1))

w("\n=== sight aggro with player move pos ===")
dists=[]
for line in events:
    if "[IN-N3]" not in line or "type=SpecialAttackWeapon" not in line:
        continue
    mid=re.search(r"identity=\(SimpleChar:([0-9A-F]+)\)", line, re.I)
    if not mid or mid.group(1)=="797E30D7":
        continue
    mob=mid.group(1)
    mts=re.match(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+)", line)
    if not mts: continue
    ts=parse_ts(mts.group(1))
    if mob in player_first_atk and player_first_atk[mob] <= ts:
        continue  # player attacked first
    if mob not in mob_pos:
        continue
    name,mx,mz=mob_pos[mob]
    px=pz=None
    for pts,x,z in out_pos:
        if pts<=ts: px,pz=x,z
    if px is None:
        for pts,x,z in player_pos:
            if pts<=ts: px,pz=x,z
    if px is None:
        continue
    d=math.sqrt((px-mx)**2+(pz-mz)**2)
    dists.append(d)
    w("SIGHT mob=%s name=%s dist=%.2f player=(%.1f,%.1f) mob=(%.1f,%.1f)" % (mob,name,d,px,pz,mx,mz))

if dists:
    dists.sort()
    w("sight n=%d min=%.1f p25=%.1f med=%.1f p75=%.1f max=%.1f" % (
        len(dists), dists[0], dists[len(dists)//4], dists[len(dists)//2], dists[3*len(dists)//4], dists[-1]))

open(OUT,"w",encoding="utf-8").write("\n".join(buf))
print("wrote", OUT, "dists", len(dists) if dists else 0)
