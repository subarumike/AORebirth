import struct
from pathlib import Path
for name in ['items.dat', 'nanos.dat', 'playfields.dat']:
    p = Path(r'AORebirth/Built/Debug') / name
    d = p.read_bytes()
    le = d.count((53032).to_bytes(4, 'little'))
    be = d.count((53032).to_bytes(4, 'big'))
    print(name, 'size', len(d), '53032 LE', le, 'BE', be)
# Also search string Insurance in items
raw = Path(r'AORebirth/Built/Debug/items.dat').read_bytes()
for needle in [b'Insurance', b'insurance', b'SaveChar', b'Shadowknowledge']:
    print(needle, raw.count(needle))
