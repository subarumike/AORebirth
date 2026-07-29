import struct
from pathlib import Path

data = Path(r'AORebirth/Built/Debug/playfields.dat').read_bytes()
print('size', len(data))

# Search instance values that look like terminal instances: top byte 0xC0
# and low 16 bits interesting for insurance captures
targets = [0xC005028F, 0xC0070320, 0xC008028F, 0xC0040320]
for t in targets:
    for endian, fmt in (('LE', '<I'), ('BE', '>I')):
        pat = struct.pack(fmt, t)
        c = data.count(pat)
        print(f'{t:08X} {endian} count={c}')

# Search playfield 655 (0x28F) headers and dump nearby terminal-like ints
pf = 655
pat = struct.pack('<I', pf)
start = 0
hits = 0
while hits < 5:
    i = data.find(pat, start)
    if i < 0:
        break
    # dump 256 bytes around as uint32 LE
    chunk = data[i:i+256]
    vals = [struct.unpack_from('<I', chunk, o)[0] for o in range(0, len(chunk)-3, 4)]
    termish = [f'{v:08X}' for v in vals if (v & 0xFF000000) == 0xC0000000 or v in (0xC73D, 51005)]
    print(f'pf655@{i} termish={termish[:20]}')
    start = i + 4
    hits += 1
