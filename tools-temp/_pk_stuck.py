# -*- coding: utf-8 -*-
from pathlib import Path
import re, json, sys
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
cap = Path(r'tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260726-153408')
print('exists', cap.exists())
info = json.loads((cap/'capture_info.json').read_text(encoding='utf-8-sig'))
print('char', info.get('characterName'), 'pf', info.get('playfieldId'), 'dur', info.get('sessionDurationSeconds'))
# movement summary / enemy dossier for peacekeeper
for name in ['movement-summary.json','enemy-dossier.json','npc-lifecycle.csv']:
    p = cap/name
    if not p.exists(): continue
    t = p.read_text(encoding='utf-8', errors='replace')
    print('====', name, 'len', len(t))
    if 'Peacekeeper' in t or 'peace' in t.lower():
        # print matching lines/chunks
        if name.endswith('.csv') or name.endswith('.log'):
            for i,l in enumerate(t.splitlines()):
                if 'Peace' in l or '7962' in l or '7997' in l:
                    print(f'{i}|{l[:300]}')
        else:
            # json - find peacekeeper entries
            try:
                data=json.loads(t)
                print(type(data), list(data)[:20] if isinstance(data,dict) else 'list', len(data) if hasattr(data,'__len__') else '')
            except Exception as e:
                print('json err', e)
