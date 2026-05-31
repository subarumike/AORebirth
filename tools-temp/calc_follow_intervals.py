import csv, collections, statistics
from pathlib import Path
p=Path(r'C:\Users\Mike\Documents\Cellao-Clean\tools-temp\live-pcaps\official-live-mob-chase\2026-05-29_20-59-19\s2c_frames.csv')
rows=list(csv.DictReader(p.open(newline='',encoding='utf-8-sig')))
by=collections.defaultdict(list)
for r in rows:
    if r.get('name')!='FollowTarget':
        continue
    hx=''.join(r['frame_hex'].split())
    b=bytes.fromhex(hx)
    if len(b)<30: continue
    ident_type=int.from_bytes(b[20:24],'big')
    ident_inst=int.from_bytes(b[24:28],'big')
    body=b[28:]
    if len(body)>=4 and body[1]==1:
        by[f'{ident_type:X}:{ident_inst:08X}'].append(float(r['relative_timestamp']))
for ident,times in sorted(by.items(), key=lambda kv:len(kv[1]), reverse=True)[:12]:
    if len(times)<2:
        print(ident, len(times))
        continue
    intervals=[b-a for a,b in zip(times,times[1:])]
    print(ident, 'n=',len(times),'span=',round(times[-1]-times[0],3),'min=',round(min(intervals),3),'p50=',round(statistics.median(intervals),3),'avg=',round(statistics.mean(intervals),3),'max=',round(max(intervals),3))
