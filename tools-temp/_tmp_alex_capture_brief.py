# -*- coding: utf-8 -*-
import json, csv, collections
from pathlib import Path

base = Path(r'C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260720-204431')
base2 = Path(r'C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260720-190432')

HOSTILE = {
    '32-V Docker', 'Waste Collector', 'Garbage Flea',
    'Cleanmeister Intelligence Robot', 'Cleaning Robot',
    'IIV-X Advanced Docker'
}
EXCLUDE = {
    'Alex Gibbs', 'ICC Immigration Officer Bill', 'Stanley Goodman', 'Sarah Greene',
    'Leonora Marty', 'Shipping Manifest Terminal', 'Wounded Dockworker', 'Patrick Sun',
    'Dockworker', 'Bruiser', 'Mrhella', 'Hankchill', 'Alienlooter', 'Rhamori',
    'Ahrinviter'
}

def load_dossier(p):
    with open(p, encoding='utf-8-sig') as f:
        return json.load(f)

def near_alex(x, z, radius=80):
    # Alex ~ (3520.8, 856.7)
    return ((x - 3520.8)**2 + (z - 856.7)**2) ** 0.5 <= radius

print('========== CAPTURE 204431 HOSTILES NEAR ALEX ==========')
d = load_dossier(base / 'enemy-dossier.json')
by_type = collections.defaultdict(list)
all_names = collections.Counter()
for e in d['enemies']:
    n = e.get('name') or ''
    all_names[n] += 1
    p = e.get('position') or {}
    x, z = float(p.get('x') or 0), float(p.get('z') or 0)
    if n in HOSTILE or (n not in EXCLUDE and e.get('deathObserved') and near_alex(x, z, 100)):
        by_type[n].append(e)

print('All names count:')
for n, c in all_names.most_common():
    print('  %3d %s' % (c, n))

for n in sorted(by_type.keys()):
    ents = by_type[n]
    levels = sorted({e.get('level') for e in ents})
    hps = sorted({e.get('maxHealth') for e in ents})
    scales = sorted({e.get('monsterScale') for e in ents})
    mds = sorted({e.get('monsterData') for e in ents})
    fams = sorted({e.get('npcFamily') for e in ents})
    runs = sorted({e.get('runSpeed') for e in ents})
    print('\n--- %s n=%d md=%s lvl=%s hp=%s scale=%s fam=%s run=%s' % (
        n, len(ents), mds, levels, hps, scales, fams, runs))
    # unique spawn positions from firstSeen / non-death population
    seen = []
    for e in ents:
        p = e.get('position') or {}
        pos = (round(float(p.get('x') or 0), 2), round(float(p.get('y') or 0), 3), round(float(p.get('z') or 0), 2))
        if pos not in seen:
            seen.append(pos)
            print('  sample id=%s lvl=%s hp=%s scale=%s fam=%s run=%s head=%s pos=%s death=%s' % (
                e.get('identity'), e.get('level'), e.get('maxHealth'), e.get('monsterScale'),
                e.get('npcFamily'), e.get('runSpeed'), e.get('headMesh'), pos, e.get('deathObserved')))

print('\n========== SCFU APPEARANCE (hostile names) ==========')
scfu = base / 'scfu-appearance.csv'
with open(scfu, encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))
print('cols:', list(rows[0].keys()) if rows else None)
# filter by name if present
name_cols = [c for c in (rows[0].keys() if rows else []) if 'name' in c.lower() or 'Name' in c]
print('name-ish cols', name_cols)
# print unique by identity for hostiles - need join via identity from dossier
id_to_name = {e['identity']: e['name'] for e in d['enemies']}
# also try entity id forms
for e in d['enemies']:
    id_to_name[e['identity'].replace('(', '').replace(')', '')] = e['name']

shown = collections.defaultdict(list)
for r in rows:
    # try match identity fields
    ident = r.get('Identity') or r.get('identity') or r.get('EntityIdentity') or r.get('SimpleCharIdentity') or ''
    name = r.get('Name') or r.get('name') or id_to_name.get(ident) or ''
    if not name:
        # try any key containing Identity
        for k, v in r.items():
            if v in id_to_name:
                name = id_to_name[v]
                ident = v
                break
    if name in HOSTILE:
        shown[name].append(r)

if not shown:
    print('No name match; dumping first 3 rows keys/sample')
    for r in rows[:3]:
        print({k: r[k] for k in list(r)[:20]})
    # dump all unique values of possible name fields and identity
    print('--- searching identities in scfu ---')
    id_fields = [c for c in rows[0].keys() if 'dent' in c.lower() or 'entity' in c.lower() or 'char' in c.lower()]
    print('id fields', id_fields)
    matched = 0
    for r in rows:
        for c in id_fields or rows[0].keys():
            v = r.get(c, '')
            if v in id_to_name and id_to_name[v] in HOSTILE:
                shown[id_to_name[v]].append(r)
                matched += 1
                break
    print('matched rows', matched)

for n, rs in sorted(shown.items()):
    print('\nSCFU', n, 'rows', len(rs))
    r = rs[0]
    # print non-empty interesting fields
    keys_of_interest = [k for k in r.keys() if any(x in k.lower() for x in
        ('texture', 'mesh', 'scale', 'monster', 'level', 'health', 'life', 'family', 'flag', 'head', 'breed', 'side', 'fat', 'visual', 'run', 'name', 'ident', 'ext'))]
    for rr in rs[:2]:
        print({k: rr[k] for k in keys_of_interest if rr.get(k)})

print('\n========== LOCAL PLAYER XP SEQUENCE ==========')
with open(base / 'enemy-stat-updates.csv', encoding='utf-8-sig', newline='') as f:
    for r in csv.DictReader(f):
        if r.get('StatName') == 'XP' and (r.get('SourceRole') == 'local-player' or '797E3029' in (r.get('SourceIdentity') or '') or r.get('EntityRole') == 'local-player'):
            print(r.get('CapturedUtc'), r.get('StatName'), r.get('NewValue') or r.get('Value'), r.get('SourceIdentity') or r.get('EntityIdentity'), r.get('Detail','')[:80])
        # flexible column names
        detail = r.get('Detail') or ''
        if 'XP=' in detail and ('797E3029' in detail or 'local-player' in (r.get('EntityRole') or r.get('SourceRole') or '')):
            print('XPROW', r.get('CapturedUtc'), detail[:120])

# print header of enemy-stat-updates
with open(base / 'enemy-stat-updates.csv', encoding='utf-8-sig', newline='') as f:
    rr = csv.DictReader(f)
    print('stat cols:', rr.fieldnames)
    xp_rows = []
    for r in rr:
        if (r.get('StatName') or r.get('statName') or '') == 'XP':
            role = r.get('EntityRole') or r.get('SourceRole') or r.get('Role') or ''
            if role == 'local-player' or '797E3029' in str(r.values()):
                xp_rows.append(r)
    print('xp local rows', len(xp_rows))
    for r in xp_rows:
        print({k: r[k] for k in r if r[k]})

print('\n========== RESPAWN COMPLETE ==========')
with open(base / 'enemy-respawns.csv', encoding='utf-8-sig', newline='') as f:
    for r in csv.DictReader(f):
        if r.get('Status') == 'complete' and r.get('Name') in HOSTILE:
            print(r['Name'], 'delay=', r['RespawnDelaySeconds'], 'death=', (r['DeathX'], r['DeathY'], r['DeathZ']),
                  'respawn=', (r['RespawnX'], r['RespawnY'], r['RespawnZ']), 'delta=', r['PositionDelta'])

print('\n========== LOOT WITH ITEM IDS ==========')
with open(base / 'corpse-loot-observations.csv', encoding='utf-8-sig', newline='') as f:
    for r in csv.DictReader(f):
        print(r['EnemyName'], 'credits=', r['CorpseCredits'], 'items=', r['Items'], 'lvl=', r['EnemyLevel'])

print('\n========== CAPTURE 190432 NEAR ALEX HOSTILES ==========')
d2 = load_dossier(base2 / 'enemy-dossier.json')
by2 = collections.defaultdict(list)
for e in d2['enemies']:
    n = e.get('name') or ''
    p = e.get('position') or {}
    x, z = float(p.get('x') or 0), float(p.get('z') or 0)
    if n in HOSTILE and near_alex(x, z, 90):
        by2[n].append(e)
for n in sorted(by2.keys()):
    print(n, 'n=', len(by2[n]))
    for e in by2[n][:6]:
        p = e.get('position') or {}
        print('  lvl=%s hp=%s md=%s scale=%s pos=(%.2f,%.3f,%.2f)' % (
            e.get('level'), e.get('maxHealth'), e.get('monsterData'), e.get('monsterScale'),
            float(p.get('x') or 0), float(p.get('y') or 0), float(p.get('z') or 0)))
