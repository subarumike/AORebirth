path = r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
for line in lines:
    if '2026-07-16 12:3' in line and (
        'SimpleItem' in line or 'Materialize' in line or 'ResolveStatic' in line
        or 'SendStatic' in line or 'loaded=' in line or 'yielded=' in line
        or 'Acknowledge' in line or 'Shadowlands' in line or 'not in Pool' in line
        or 'GameFunctions' in line or 'Called teleport' in line or 'Function Teleport' in line
        or 'Could not create lambda' in line or 'Result:' in line
    ):
        print(line[:420])
