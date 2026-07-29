# Decode CastNanoSpell from raw packets for player 797E30D7
from pathlib import Path
import re, csv

cap = Path(r'tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper-exect-nano')
out = Path(r'tools-temp\_tmp_keeper_nano_ids.txt')
lines=[]

# raw-packets.csv look for CastNanoSpell
rp = cap/'raw-packets.csv'
with rp.open(encoding='utf-8-sig', errors='replace') as f:
    rows=list(csv.DictReader(f))
lines.append(f'raw rows={len(rows)} cols={list(rows[0].keys()) if rows else []}')
for r in rows:
    blob=' '.join(str(v) for v in r.values())
    if 'CastNano' in blob or 'castnano' in blob.lower() or 'NanoSpell' in blob:
        lines.append(str({k:str(r.get(k,''))[:120] for k in list(r.keys())[:12]}))

# packets.hex.log around sequences 6,7,362
hexp = cap/'packets.hex.log'
text = hexp.read_text(encoding='utf-8-sig', errors='replace').splitlines()
lines.append('\n=== hex around CastNano / 797E30D7 ===')
for i,l in enumerate(text):
    if 'CastNano' in l or ('797E30D7' in l and i<200) or re.search(r'seq[=:]?\s*(6|7|362|363|832|833)\b', l, re.I):
        for j in range(max(0,i-1), min(len(text), i+3)):
            lines.append(text[j][:300])
        lines.append('---')

# enemy-combat Detail field for CastNanoSpell local
cc=cap/'enemy-combat.csv'
with cc.open(encoding='utf-8-sig', errors='replace') as f:
    for r in csv.DictReader(f):
        if r.get('MessageType')=='CastNanoSpell':
            lines.append('COMBAT '+str(dict(r))[:500])

out.write_text('\n'.join(lines), encoding='utf-8')
print('wrote', out)
