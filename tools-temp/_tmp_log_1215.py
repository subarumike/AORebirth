path=r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
lines=open(path,encoding='utf-8',errors='replace').read().splitlines()
# from 12:15 startup
start=None
for i,l in enumerate(lines):
    if '2026-07-16 12:15:42' in l:
        start=i
        break
print('start', start)
chunk=lines[start:]
print('chunk lines', len(chunk))
for l in chunk:
    if any(k in l for k in ['Connected','CharInPlay','StaticDynel','ResolveStatic','Materialize','CreatePlayfield','playfield','ZoneLogin','FullCharacter','Info|','ERROR|']):
        if 'CharDCMove' not in l and 'Zone receive' not in l and 'Zone message decoded' not in l:
            print(l[:250])
