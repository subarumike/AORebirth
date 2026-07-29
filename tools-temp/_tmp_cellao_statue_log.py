path = r'C:\Users\nermi\source\repos\CellAO-NightPredator-private prije AOrebirth\CellAO\Built\Debug\ZoneEngineLog.txt'
import os
print('size', os.path.getsize(path))
lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
print('lines', len(lines))
# last 2000 lines interesting keys
keys = (
    'GenericCmd', 'Teleport', 'teleport', 'OnUse', 'StaticDynel', 'Terminal',
    'statue', 'passage', 'UseItemOn', 'secondary', 'Playfield', '4677', '4310',
    'Called teleport', 'Function', 'Transfer', 'DoNotDoTimers', 'Acknowledge'
)
tail = lines[-2500:]
hits = []
for i, line in enumerate(tail):
    low = line.lower()
    if any(k.lower() in low for k in keys):
        hits.append(line)
print('hits', len(hits))
for line in hits[-120:]:
    print(line[:400])
