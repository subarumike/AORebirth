path=r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
lines=open(path,encoding='utf-8',errors='replace').read().splitlines()
for i,l in enumerate(lines):
    if '2026-07-16 12:26:5' in l or '2026-07-16 12:27:0' in l:
        if 'ERROR' in l or 'Exception' in l or 'GenericCmd' in l or 'teleport' in l.lower() or 'statue' in l.lower() or 'Function' in l:
            print(l[:280])
