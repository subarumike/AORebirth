path = r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
# find Materialize / Resolve around login 12:36:14
for i, line in enumerate(lines):
    if '2026-07-16 12:36:1' in line or '2026-07-16 12:36:0' in line or '2026-07-16 12:35:' in line:
        if any(k in line for k in ('Materialize', 'ResolveStatic', 'SendStatic', 'loaded=', 'playfield=4677', 'pf=4677', 'CreatePlayfield', '4677')):
            print(line[:400])
