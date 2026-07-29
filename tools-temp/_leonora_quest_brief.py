# -*- coding: utf-8 -*-
import re, sys, csv, struct
sys.stdout.reconfigure(encoding='utf-8')

path = r'AORebirth/Libraries/Source/AORebirth.Database/SqlTables/itemnames.sql'
with open(path, encoding='utf-8', errors='replace') as f:
    data = f.read()

for i in (297054, 297302, 297315, 297243, 291043):
    m = re.search(r'\(\s*%d\s*,\s*\'([^\']*)\'' % i, data)
    print(i, '->', m.group(1) if m else 'NOT FOUND')

print('--- vacuum/omni-med hits ---')
for m in re.finditer(r"\(\s*(\d+)\s*,\s*'([^']*(?:Vacuum|Omni-Med|Omni Med|Vacuumpack)[^']*)'", data, re.I):
    print(m.group(1), m.group(2))

base = r'tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-credit-card'
# decode SIFU 57A4218D fully from events detail already had stats
print('--- SIFU 57A4218D from events ---')
with open(base + '/events.log', encoding='utf-8', errors='replace') as f:
    for line in f:
        if '57A4218D' in line and 'SimpleItemFullUpdate' in line:
            print(line[:500])
            break

# heading from hex: after identity
raw = None
with open(base + '/raw-packets.csv', encoding='utf-8-sig', newline='') as f:
    for row in csv.DictReader(f):
        if '57A4218D' in row['RawHex'].upper() and row['N3TypeName']=='SimpleItemFullUpdate':
            raw = bytes.fromhex(row['RawHex'])
            print('SIFU hex', row['RawHex'])
            # floats for pos - from events: 3449.283, 0.01, 889.0574
            # rotation Y=0.9859381 W=0.1671108
            # template 297315
            break
