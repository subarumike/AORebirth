path = r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
keys = (
    'MaterializeStatic', 'ResolveStaticDynels', 'SendStaticDynels', 'StaticDynel',
    '1245', '4677', 'pool', 'RegisterDynel', 'not in Pool', 'Shadowlands'
)
for i, line in enumerate(lines):
    if '2026-07-16 12:3' in line:
        low = line.lower()
        if any(k.lower() in low for k in keys):
            print(line[:400])
