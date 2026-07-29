path = r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
import os
print('exists', os.path.exists(path), 'size', os.path.getsize(path) if os.path.exists(path) else 0)
lines = open(path, encoding='utf-8', errors='replace').read().splitlines()[-1200:]
keys = (
    'Shadowlands', 'GenericCmd', 'garden', 'teleport', 'statue', 'OnUse',
    'Function ', 'Called teleport', 'not found', '14428396', 'expansion',
    'DoNotDoTimers', 'TransferToPlayfield', 'passage'
)
for line in lines:
    low = line.lower()
    if any(k.lower() in low for k in keys):
        print(line[:320])
