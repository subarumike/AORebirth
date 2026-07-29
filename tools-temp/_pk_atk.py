# -*- coding: utf-8 -*-
from pathlib import Path
import re, json, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
cap = Path(r'tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-162913')
print('exists', cap.exists())
info = json.loads((cap/'capture_info.json').read_text(encoding='utf-8-sig'))
print('char', info.get('characterName'), 'pf', info.get('playfieldId'), 'dur', info.get('sessionDurationSeconds'))
print('counts', {k:info.get('captureCounts',{}).get(k) for k in ['enemyCombatRows','enemyFightCaptureStarted','npcInteractions']})

lines = (cap/'events.log').read_text(encoding='utf-8', errors='replace').splitlines()
keys = (
    'Peacekeeper','SpecialAttack','AttackInfo','AttackMessage','Attack ',
    'CastNano','WeaponItem','WIFU','GenericCmd','Fight','StatMessage',
    'SimpleCharFullUpdate','Damage','Health'
)
for i,l in enumerate(lines):
    hit=False
    for k in keys:
        if k in l:
            hit=True
            break
    if 'Peacekeeper' in l or 'SpecialAttack' in l or 'AttackInfo' in l or ('Attack' in l and ('IN-N3' in l or 'OUT-N3' or 'SMOKE' in l)):
        if 'StatMessage' in l and 'Health' in l and 'Peacekeeper' not in l:
            # skip spam health unless peacekeeper identity nearby context
            continue
        if 'SimpleCharFullUpdate' in l:
            name=re.search(r'Name="([^"]+)"', l)
            md=re.search(r'MonsterData=(\d+)', l)
            ident=re.search(r'Identity=\(([^)]+)\)', l)
            print(f'{i}|SCFU name={name.group(1) if name else "?"} id={ident.group(1) if ident else "?"} md={md.group(1) if md else "?"}')
        else:
            print(f'{i}|{l[:450]}')
