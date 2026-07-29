path = r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
# find context around 12:36:42 uses
for i, line in enumerate(lines):
    if '12:36:42.2144' in line and 'GenericCmd' in line:
        start = max(0, i - 40)
        end = min(len(lines), i + 5)
        for j in range(start, end):
            print(lines[j][:350])
        break

print('--- later around first use session playfield ---')
for i, line in enumerate(lines):
    if '12:36:' in line and ('playfield' in line.lower() or 'Playfield' in line or '4677' in line or 'CharInPlay' in line or 'ClientConnected' in line or 'MaterializeStatic' in line or 'SendStaticDynels' in line):
        print(lines[i][:350])
