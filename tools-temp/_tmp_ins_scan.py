import struct
import pathlib
data = pathlib.Path(r'AORebirth/Built/Debug/playfields.dat').read_bytes()
pat = struct.pack('<I', 53032)
idxs = []
start = 0
while True:
    i = data.find(pat, start)
    if i < 0:
        break
    idxs.append(i)
    start = i + 4
print('53032 hits', len(idxs))
# Also search C005028F
pat2 = struct.pack('<I', 0xC005028F)
print('C005028F hits', data.count(pat2))
pat3 = struct.pack('<I', 0x028F)  # pf 655
print('done')
