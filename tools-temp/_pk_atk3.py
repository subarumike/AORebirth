# -*- coding: utf-8 -*-
from pathlib import Path
import re, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
cap = Path(r'tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-162913')
lines=(cap/'events.log').read_text(encoding='utf-8',errors='replace').splitlines()

# All packets from peacekeeper identities
pks={'7962A3F9','79978ED1','7962A325'}
for i,l in enumerate(lines):
    for pk in pks:
        if pk in l and any(t in l for t in ['SpecialAttack','AttackInfo','AttackMessage','WeaponItem','WIFU','Attack ']):
            print(f'{i}|{l[:550]}')
            break

print('\n==== WeaponItemFullUpdate for PK ====')
for i,l in enumerate(lines):
    if 'WeaponItemFullUpdate' in l and any(pk in l for pk in ['7962A3F9','79978ED1','7962A325','265090','Peacekeeper']):
        print(f'{i}|{l[:700]}')
    # also Owner= peacekeeper
    if 'WeaponItemFullUpdate' in l:
        # check nearby context - print if 265090
        if '265090' in l or '265091' in l:
            print(f'{i}|WIFU265 {l[:700]}')
