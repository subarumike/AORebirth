path=r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
lines=open(path,encoding='utf-8',errors='replace').read().splitlines()
# after 12:22 restart
for l in lines:
    if '2026-07-16 12:2' in l or '2026-07-16 12:3' in l:
        if 'GenericCmd' in l or 'statue' in l.lower() or 'SendStaticDynels' in l or 'Shadowlands' in l or 'teleport' in l.lower():
            print(l[:260])
