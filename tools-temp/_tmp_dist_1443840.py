from __future__ import print_function
import re

path = r"AORebirth/Server/ZoneEngine/Core/Playfields/MissionInstanceShapeCatalog.cs"
text = open(path, encoding="utf-8", errors="replace").read()
m = re.search(
    r"CapturedPlayfieldId = 1443840,.*?Npcs = new\[\]\s*\{(.*?)\n        \},",
    text,
    re.S,
)
if not m:
    print("no match")
    raise SystemExit(1)

block = m.group(1)
# looser: Name then later X/Z
parts = re.split(r"new MissionNpc\s*\{", block)
sx, sz = 298.199, 225.01
for part in parts[1:]:
    nm = re.search(r'Name = "([^"]+)"', part)
    role = re.search(r"Role = MissionNpcRole\.(\w+)", part)
    xs = re.search(r"X = ([0-9.\-]+)f", part)
    zs = re.search(r"Z = ([0-9.\-]+)f", part)
    if not (nm and role and xs and zs):
        continue
    x = float(xs.group(1))
    z = float(zs.group(1))
    d = ((x - sx) ** 2 + (z - sz) ** 2) ** 0.5
    print("%6.1fm %-12s %s" % (d, role.group(1), nm.group(1)))
