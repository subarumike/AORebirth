# -*- coding: utf-8 -*-
from pathlib import Path
import csv, re, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
cap = Path(r'tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-162913')

# combat csv
for name in ['enemy-combat.csv','enemy-fight-events.log','events.log']:
    p=cap/name
    if not p.exists():
        continue
    print('====', name)
    if name.endswith('.csv'):
        rows=list(csv.DictReader(p.open(encoding='utf-8-sig')))
        print('rows', len(rows), 'cols', list(rows[0].keys()) if rows else None)
        for r in rows[:40]:
            print({k:r[k] for k in r if r[k]})
    elif name.endswith('.log') and 'fight' in name:
        for i,l in enumerate(p.read_text(encoding='utf-8',errors='replace').splitlines()[:80]):
            print(f'{i}|{l[:350]}')

# Focus: SpecialAttackWeapon / Attack / AttackInfo detail from events
lines=(cap/'events.log').read_text(encoding='utf-8',errors='replace').splitlines()
print('\n==== combat packet details ====')
for i,l in enumerate(lines):
    if any(k in l for k in ['SpecialAttackWeapon','AttackInfoMessage','AttackMessage','WeaponItemFullUpdate','WIFU']):
        print(f'{i}|{l[:600]}')
    if 'IN-N3-DETAIL] Attack' in l or 'SpecialAttack' in l:
        print(f'{i}|{l[:600]}')
