# -*- coding: utf-8 -*-
from pathlib import Path
import csv, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
cap = Path(r'tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-153408')
mp = list(csv.DictReader((cap/'movement-packets.csv').open(encoding='utf-8-sig')))

for inst in ['7962A325','7962A3F9','79978ED1']:
    print('====', inst)
    rows=[r for r in mp if r['SourceInstance']==inst and r['FollowKind']=='NpcPath']
    print('count', len(rows))
    prev=None
    for r in rows:
        cx,cy,cz=float(r['CurrentX']),float(r['CurrentY']),float(r['CurrentZ'])
        dx,dy,dz=float(r['DestinationX']),float(r['DestinationY']),float(r['DestinationZ'])
        jump=''
        if prev is not None:
            # distance from prev dest to this current
            px,py,pz=prev
            d=((cx-px)**2+(cz-pz)**2)**0.5
            dyv=abs(cy-py)
            if d>8 or dyv>2:
                jump=f'  << GAP from prevDest d2d={d:.1f} dy={dyv:.1f}'
        print(f"  cur=({cx:.2f},{cy:.2f},{cz:.2f}) dest=({dx:.2f},{dy:.2f},{dz:.2f}){jump}")
        prev=(dx,dy,dz)
