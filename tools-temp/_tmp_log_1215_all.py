path=r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
lines=open(path,encoding='utf-8',errors='replace').read().splitlines()
start=None
for i,l in enumerate(lines):
    if '2026-07-16 12:15:42' in l:
        start=i
        break
chunk=lines[start:]
for l in chunk:
    if 'CharDCMove' in l or 'Zone receive' in l or 'Zone message decoded' in l:
        continue
    print(l[:300])
