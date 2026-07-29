import struct, os
paths = [
    r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\items.dat',
    r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat',
]
ids = [244737, 244730, 222955, 223577, 100000, 1]
for p in paths:
    d = open(p, 'rb').read()
    print(p, 'size', len(d))
    for i in ids:
        print(' ', i, struct.pack('<I', i) in d)
