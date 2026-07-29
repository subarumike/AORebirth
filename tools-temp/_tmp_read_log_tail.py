import os
path=r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\ZoneEngineLog.txt'
print('log size', os.path.getsize(path), 'mtime', os.path.getmtime(path))
lines=open(path,encoding='utf-8',errors='replace').read().splitlines()
print('total lines', len(lines))
# search last 3000 lines for key markers
tail=lines[-3000:]
for key in ['ResolveStaticDynels','StaticDynelSnapshot','MaterializeStaticDynels pf=','loaded=','An item with the same key','Starting the network','4677','teleport']:
    hits=[l for l in tail if key in l]
    print(key, 'hits', len(hits))
    for l in hits[-5:]:
        print(' ', l[:220])
print('--- last 40 ---')
for l in lines[-40:]:
    print(l[:220])
