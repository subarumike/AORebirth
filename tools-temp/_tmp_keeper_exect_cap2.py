# Deeper decode: death Parameter2, player CastNanoSpell nano ids, SpecialAttackWeapon, chat
from pathlib import Path
import re, csv

cap = Path(r'tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper-exect-nano')
out = Path(r'tools-temp\_tmp_keeper_exect_cap2.txt')
lines = []

def add(s=''):
    lines.append(s)

ev = (cap/'events.log').read_text(encoding='utf-8-sig', errors='replace').splitlines()

# All Death actions with Parameter2
add('=== Death Parameter2 ===')
for line in ev:
    if 'Action=Death' in line or 'Action = Death' in line:
        add(line[:260])

# Player CastNanoSpell detail lines near sequences
add('\n=== CastNanoSpell / nano program details ===')
for i, line in enumerate(ev):
    if 'CastNanoSpell' in line or ('NanoProgram:' in line and 'SetNanoDuration' in line):
        add(line[:280])
        # nearby detail
        for j in range(i+1, min(i+4, len(ev))):
            if 'DETAIL' in ev[j] or 'Nano' in ev[j] or 'Cast' in ev[j]:
                add('  ' + ev[j][:280])

# SpecialAttackWeapon full
add('\n=== SpecialAttackWeapon ===')
for line in ev:
    if 'SpecialAttackWeapon' in line:
        add(line[:300])

# AttackInfo on Burning
add('\n=== AttackInfo involving 7987C60D ===')
for line in ev:
    if '7987C60D' in line and ('AttackInfo' in line or 'Death' in line or 'Despawn' or 'Corpse' in line or 'StopFight' in line):
        if any(x in line for x in ('AttackInfo','Death','Despawn','Corpse','StopFight','SpecialAttack')):
            add(line[:280])

# ChatText
add('\n=== ChatText ===')
for line in ev:
    if 'ChatText' in line or 'hit you' in line.lower() or 'heal' in line.lower():
        add(line[:280])

# Player cast nano ids from combat csv observations
add('\n=== enemy-combat CastNanoSpell local-player rows ===')
with (cap/'enemy-combat.csv').open(encoding='utf-8-sig', errors='replace') as f:
    for r in csv.DictReader(f):
        if r.get('MessageType') == 'CastNanoSpell' and r.get('SourceRole') == 'local-player':
            add(str(dict(r))[:400])

# hex log search for CastNanoSpell payloads near start
add('\n=== raw combat / nano mentions in system-messages for Getkeep ===')
sm = cap/'system-messages.log'
if sm.exists():
    for line in sm.read_text(encoding='utf-8-sig', errors='replace').splitlines():
        if 'CastNano' in line or 'ChatText' in line or 'Feedback' in line or 'heal' in line.lower():
            add(line[:280])

out.write_text('\n'.join(lines), encoding='utf-8')
print('wrote', out, 'lines', len(lines))
