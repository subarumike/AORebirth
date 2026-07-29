# Capture 20260722-keeper-exect-nano — robots + keeper heal
from pathlib import Path
from collections import Counter
import csv, re, json

cap = Path(r'tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper-exect-nano')
out = Path(r'tools-temp\_tmp_keeper_exect_cap_out.txt')
lines = []

def add(s=''):
    lines.append(s)

info = json.loads((cap/'capture_info.json').read_text(encoding='utf-8-sig', errors='replace'))
add('=== capture_info ===')
for k in sorted(info.keys()):
    add(f'  {k}={info[k]}')

# Respawn CSV
add('\n=== enemy-respawns.csv ===')
rp = cap/'enemy-respawns.csv'
if rp.exists():
    with rp.open(encoding='utf-8', errors='replace') as f:
        rows = list(csv.DictReader(f))
    add(f'  rows={len(rows)}')
    for r in rows[:40]:
        add('  ' + ' | '.join(f'{k}={r.get(k)}' for k in r.keys()))

# Combat CSV sample for Cleaning Robot / Death / SpecialAttack / ChatText / CastNano
add('\n=== enemy-combat.csv robot/death/special ===')
cc = cap/'enemy-combat.csv'
robot_rows = []
death_rows = []
if cc.exists():
    with cc.open(encoding='utf-8', errors='replace') as f:
        for r in csv.DictReader(f):
            blob = ' '.join(str(v) for v in r.values())
            if re.search(r'Cleaning Robot|297023|Death|SpecialAttack|Parameter2|AttackInfo|ChatText|CastNano|Adaptive|Composite|heal|Hit you', blob, re.I):
                robot_rows.append(r)
    add(f'  matched_rows={len(robot_rows)}')
    for r in robot_rows[:80]:
        add('  ' + ' | '.join(f'{k}={r.get(k,"")[:120]}' for k in list(r.keys())[:8]))

# events.log focused
add('\n=== events.log focused ===')
ev = cap/'events.log'
patterns = [
    r'Cleaning Robot', r'Burning', r'CharacterAction.*Death', r'Parameter2',
    r'SpecialAttackWeapon', r'AttackInfo', r'CorpseFullUpdate', r'Despawn',
    r'ChatText', r'hit you', r'CastNano', r'Adaptive', r'Ambient', r'Mongo',
    r'AreaCastNano', r'TauntNpc', r'SimpleCharFullUpdate.*Robot'
]
counts = Counter()
hits = []
if ev.exists():
    with ev.open(encoding='utf-8', errors='replace') as f:
        for i, line in enumerate(f, 1):
            for p in patterns:
                if re.search(p, line, re.I):
                    counts[p] += 1
                    if len(hits) < 120 and re.search(r'Cleaning Robot|Death|SpecialAttack|Parameter2=|hit you|Adaptive|Ambient|CastNano|CorpseFull|Despawn.*Robot|SimpleCharFullUpdate.*Robot', line, re.I):
                        hits.append(f'L{i}: {line.rstrip()[:220]}')
                    break
add('  pattern_counts:')
for p,c in counts.most_common():
    add(f'    {c:5d}  {p}')
add('\n  sample hits:')
for h in hits[:100]:
    add('  ' + h)

# system messages / chat
add('\n=== system-messages / chat hit+heal ===')
for name in ('system-messages.log', 'chat-dialogue.log'):
    p = cap/name
    if not p.exists():
        continue
    add(f'--- {name} ---')
    with p.open(encoding='utf-8', errors='replace') as f:
        for line in f:
            if re.search(r'hit you|heal|Adaptive|Ambient|Robot|Combat|damage', line, re.I):
                add('  ' + line.rstrip()[:240])

# corpse observations
add('\n=== corpse-loot-observations ===')
cl = cap/'corpse-loot-observations.csv'
if cl.exists():
    with cl.open(encoding='utf-8', errors='replace') as f:
        rows = list(csv.DictReader(f))
    add(f'  rows={len(rows)}')
    for r in rows[:30]:
        add('  ' + str(dict(r))[:240])

# npc-lifecycle death/respawn
add('\n=== npc-lifecycle death/respawn/robot ===')
nl = cap/'npc-lifecycle.csv'
if nl.exists():
    with nl.open(encoding='utf-8', errors='replace') as f:
        for r in csv.DictReader(f):
            blob = ' '.join(str(v) for v in r.values())
            if re.search(r'Cleaning Robot|death|respawn|Despawn|Corpse', blob, re.I):
                add('  ' + ' | '.join(f'{k}={str(r.get(k,""))[:80]}' for k in list(r.keys())[:6]))

out.write_text('\n'.join(lines), encoding='utf-8')
print(f'wrote {out} lines={len(lines)}')
