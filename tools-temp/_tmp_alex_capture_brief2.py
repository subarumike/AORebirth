# -*- coding: utf-8 -*-
import csv, json, collections
from pathlib import Path

base = Path(r'C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260720-204431')

HOSTILE = ['32-V Docker','Waste Collector','Garbage Flea','Cleanmeister Intelligence Robot','Cleaning Robot','IIV-X Advanced Docker','Supreme Collector of Waste']

print('=== SCFU distinct templates per name ===')
with open(base/'scfu-appearance.csv', encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))

by = collections.defaultdict(list)
for r in rows:
    n = r.get('Name') or ''
    if n in HOSTILE:
        by[n].append(r)

for n in HOSTILE:
    rs = by.get(n) or []
    if not rs:
        print(n, 'NO SCFU')
        continue
    # unique by (md, scale, level, hp, tex, run, fam)
    keys = set()
    for r in rs:
        k = (r['MonsterData'], r['MonsterScale'], r['Level'], r['Health'], r['NpcFamily'], r['RunSpeedBase'], r['TextureOverrides'][:40] if r.get('TextureOverrides') else '', r.get('Meshes') or '', r.get('HeadMesh') or '')
        if k in keys:
            continue
        keys.add(k)
        print(n, 'md=%s scale=%s lvl=%s hp=%s fam=%s runBase=%s tex=%r meshes=%r head=%s flags=%s' % (
            r['MonsterData'], r['MonsterScale'], r['Level'], r['Health'], r['NpcFamily'], r['RunSpeedBase'],
            (r.get('TextureOverrides') or '')[:60], (r.get('Meshes') or '')[:40], r.get('HeadMesh'), r.get('FlagsNumeric')))
        # also OpaqueExtensionHex length
        ox = r.get('OpaqueExtensionHex') or ''
        print('  pos=(%s,%s,%s) opaqueLen=%d special=%s' % (r['PositionX'], r['PositionY'], r['PositionZ'], len(ox)//2, (r.get('SpecialAttacks') or '')[:80]))

print('\n=== Near-Alex spawn density (y~5, x 3485-3535, z 850-910) for slot candidates ===')
alex_box = []
with open(base/'enemy-dossier.json', encoding='utf-8-sig') as f:
    d = json.load(f)
for e in d['enemies']:
    n = e.get('name')
    if n not in ('32-V Docker','Waste Collector','Garbage Flea','Cleanmeister Intelligence Robot','IIV-X Advanced Docker'):
        continue
    p = e.get('position') or {}
    x,y,z = float(p.get('x') or 0), float(p.get('y') or 0), float(p.get('z') or 0)
    if 3485 <= x <= 3535 and 850 <= z <= 910 and y < 6.5:
        alex_box.append((n, e.get('level'), e.get('maxHealth'), e.get('monsterScale'), round(x,2), round(y,3), round(z,2), e.get('deathObserved'), e.get('identity')))

for row in sorted(alex_box, key=lambda t: (t[0], t[6], t[4])):
    print('%s lvl=%s hp=%s scale=%s (%.2f, %.3f, %.2f) death=%s %s' % row)

print('\n=== XP deltas vs nearby deaths ===')
# load deaths from enemy-combat Death actions for local player kills
deaths = []
with open(base/'enemy-combat.csv', encoding='utf-8-sig', newline='') as f:
    for r in csv.DictReader(f):
        if r.get('MessageType')=='CharacterAction' and r.get('Action')=='Death':
            deaths.append((r['CapturedUtc'], r['SourceIdentity'], r['SourceRole']))

xp = []
with open(base/'enemy-stat-updates.csv', encoding='utf-8-sig', newline='') as f:
    for r in csv.DictReader(f):
        if r.get('Stat')=='XP' and '797E3029' in (r.get('Identity') or ''):
            xp.append((r['CapturedUtc'], int(r['Value'])))

# map identity to name from dossier
idname = {e['identity']: e['name'] for e in d['enemies']}
# also from loot
loot_names = {}
with open(base/'corpse-loot-observations.csv', encoding='utf-8-sig', newline='') as f:
    for r in csv.DictReader(f):
        loot_names[r['DeadNpcIdentity'] if r['DeadNpcIdentity'].startswith('(') else '('+r['DeadNpcIdentity']+')'] = r['EnemyName']
        # normalize
        di = r['DeadNpcIdentity']
        if not di.startswith('('):
            di = '(' + di + ')'
        loot_names[di] = r['EnemyName']
        loot_names[r['DeadNpcIdentity']] = r['EnemyName']

prev = None
for t,v in xp:
    delta = None if prev is None else v - prev
    # find death within 2s before
    near = [x for x in deaths if x[0] <= t]
    killer = near[-1] if near else None
    name = ''
    if killer:
        name = idname.get(killer[1], '') or loot_names.get(killer[1],'') or loot_names.get(killer[1].replace('(','').replace(')',''),'')
    print('XP=%d delta=%s time=%s death=%s name=%s' % (v, delta, t, killer[1] if killer else None, name))
    prev = v

print('\n=== Docker SpecialAttackWeapon / AttackInfo sample (enemy) ===')
with open(base/'enemy-combat.csv', encoding='utf-8-sig', newline='') as f:
    counts = collections.Counter()
    weapon_instances = collections.Counter()
    amounts = collections.defaultdict(list)
    for r in csv.DictReader(f):
        if r.get('SourceRole')!='enemy':
            continue
        sid = r.get('SourceIdentity')
        name = idname.get(sid,'')
        if name not in HOSTILE:
            continue
        mt = r.get('MessageType')
        counts[(name, mt)] += 1
        if mt=='AttackInfo':
            # parse amount from Detail
            det = r.get('Detail') or ''
            if 'Amount=' in det:
                a = det.split('Amount=')[1].split(' ')[0]
                amounts[name].append(a)
            if 'WeaponInstance=' in det:
                w = det.split('WeaponInstance=')[1].split(' ')[0]
                weapon_instances[(name,w)] += 1
    for k,v in sorted(counts.items()):
        print(k, v)
    print('weapon instances', dict(weapon_instances))
    for n,a in amounts.items():
        print('hit amounts', n, a[:20], 'unique', sorted(set(a)))

print('\n=== Supreme Collector / IIV-X presence ===')
for e in d['enemies']:
    if e['name'] in ('Supreme Collector of Waste','IIV-X Advanced Docker','Cleanmeister Intelligence Robot'):
        p=e['position']
        print(e['name'], e['level'], e['maxHealth'], e['monsterData'], e.get('monsterScale'), (p['x'],p['y'],p['z']), 'death', e['deathObserved'])
