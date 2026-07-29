import json, os
from collections import defaultdict

base = r'C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260716-071407'
dossier = os.path.join(base, 'enemy-dossier.json')
data = json.load(open(dossier, encoding='utf-8-sig'))
# structure unknown - print type and heckler entries
if isinstance(data, list):
    rows = data
elif isinstance(data, dict):
    rows = data.get('enemies') or data.get('Entries') or data.get('items') or list(data.values())
    if rows and isinstance(rows, dict):
        rows = list(rows.values())
else:
    rows = []

print('top type', type(data).__name__, 'rows', len(rows) if hasattr(rows,'__len__') else '?')
hecklers = []
for r in rows:
    if not isinstance(r, dict):
        continue
    name = r.get('name') or r.get('Name') or ''
    if 'heckler' in name.lower():
        hecklers.append(r)

print('heckler entries', len(hecklers))
# unique by identity
by_id = {}
for r in hecklers:
    ident = r.get('identity') or r.get('Identity') or r.get('sourceIdentity')
    pos = r.get('position') or r.get('Position') or {}
    by_id[str(ident)] = {
        'name': r.get('name') or r.get('Name'),
        'monsterData': r.get('monsterData') or r.get('MonsterData'),
        'level': r.get('level') or r.get('Level'),
        'maxHealth': r.get('maxHealth') or r.get('MaxHealth') or r.get('health'),
        'runSpeed': r.get('runSpeed') or r.get('RunSpeed'),
        'npcFamily': r.get('npcFamily'),
        'monsterScale': r.get('monsterScale'),
        'x': pos.get('x') if isinstance(pos, dict) else None,
        'y': pos.get('y') if isinstance(pos, dict) else None,
        'z': pos.get('z') if isinstance(pos, dict) else None,
        'keys': sorted(r.keys())[:20],
    }

print('unique identities', len(by_id))
for k,v in sorted(by_id.items(), key=lambda kv: (kv[1]['name'] or '', kv[0]))[:50]:
    print(k, v['name'], 'md', v['monsterData'], 'lvl', v['level'], 'hp', v['maxHealth'], 'pos', v['x'], v['y'], v['z'])

# also try enemy-full-updates.csv
csv_path = os.path.join(base, 'enemy-full-updates.csv')
if os.path.exists(csv_path):
    import csv
    with open(csv_path, encoding='utf-8', errors='replace') as f:
        reader = csv.DictReader(f)
        cols = reader.fieldnames
        print('csv cols', cols)
        seen = {}
        for row in reader:
            name = row.get('name') or row.get('Name') or ''
            if 'heckler' not in name.lower():
                continue
            ident = row.get('identity') or row.get('Identity') or row.get('instance')
            seen[ident] = row
        print('csv heckler unique', len(seen))
        for ident, row in list(seen.items())[:45]:
            print(ident, row.get('name'), row.get('x') or row.get('X'), row.get('y') or row.get('Y'), row.get('z') or row.get('Z'), row.get('monsterData'), row.get('level'), row.get('health') or row.get('maxHealth'))
