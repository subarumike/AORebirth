path = r'C:\Users\nermi\source\repos\CellAO-NightPredator-private prije AOrebirth\CellAO\Built\Debug\ZoneEngineLog.txt'
lines = open(path, encoding='utf-8', errors='replace').read().splitlines()

# Today's sessions around statue use
for marker in ('2026-07-16 12:52', '2026-07-16 11:11', '2026-07-16 11:36'):
    print('========', marker, '========')
    idxs = [i for i, l in enumerate(lines) if marker in l]
    if not idxs:
        print('no hits')
        continue
    start = max(0, idxs[0] - 5)
    end = min(len(lines), idxs[-1] + 80)
    for i in range(start, end):
        l = lines[i]
        if any(k in l for k in (
            'GenericCmd', 'Called', 'teleport', 'Teleport', 'Function',
            'Playfield', 'Transfer', 'Acknowledge', 'Result:', 'Values:',
            'CharInPlay', 'ClientConnected', 'SimpleItem', 'StaticDynel',
            'DoNotDoTimers', 'GameFunctions', 'expansion', 'secondary',
            'INFO|', 'ERROR'
        )):
            print(l[:450])
    print()
