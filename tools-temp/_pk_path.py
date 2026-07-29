# -*- coding: utf-8 -*-
from pathlib import Path
import csv, re, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
cap = Path(r'tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-153408')

# enemy-movement for 7962A325 and 7962A3F9
mov = cap/'enemy-movement.csv'
rows = list(csv.DictReader(mov.open(encoding='utf-8-sig')))
print('movement cols', rows[0].keys() if rows else None)
for inst in ['7962A325','7962A3F9','79978ED1']:
    pts=[]
    for r in rows:
        blob=' '.join(r.values())
        if inst.lower() in blob.lower() or inst in blob:
            # try common fields
            pts.append(r)
    print(inst, 'rows', len(pts))
    if pts:
        # print first/last few
        keys=list(pts[0].keys())
        print('  keys', keys)
        for r in pts[:3]+pts[-3:]:
            print(' ', {k:r[k] for k in keys if k.lower() in ('x','y','z','posx','posy','posz','time','timestamp','identity','name') or 'coord' in k.lower() or 'pos' in k.lower()})

# movement-packets FollowTarget path for market east
mp = cap/'movement-packets.csv'
if mp.exists():
    mrows=list(csv.DictReader(mp.open(encoding='utf-8-sig')))
    print('mpackets', len(mrows), 'cols', mrows[0].keys() if mrows else None)
    for r in mrows:
        blob=' '.join(r.values())
        if '7962A325' in blob or 'A325' in blob:
            print(dict(r))
