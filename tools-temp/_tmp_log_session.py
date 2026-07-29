import re
path=r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
# last login session around 11:55 - count MaterializeStaticDynels errors and find playfield
text=open(path,encoding='utf-8',errors='replace').read().splitlines()
start=None
for i,l in enumerate(text):
    if '11:55:09.8680' in l or '11:55:10.2438' in l:
        start=i
        break
if start is None:
    print('no start'); raise SystemExit
chunk=text[start:start+200]
mat=sum(1 for l in chunk if 'MaterializeStaticDynels' in l)
print('MaterializeStaticDynels mentions', mat)
for l in chunk:
    if 'teleport' in l.lower() or 'Playfield' in l and '4677' in l or 'StaticDynel' in l or 'Creating playfield' in l or 'playfield=' in l:
        if 'Statel collision' not in l:
            print(l[:200])
