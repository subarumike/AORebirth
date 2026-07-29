from __future__ import print_function
import re, binascii, struct

path = r"AORebirth/Server/ZoneEngine/Core/Missions/MissionInstanceDynelCapture.cs"
text = open(path, encoding="utf-8", errors="replace").read()
m = re.search(r"Doors_1443840 =\s*\{(.*?)\n        \};", text, re.S)
hexes = re.findall(r'"([0-9A-Fa-f]+)"', m.group(1))
sx, sz = 298.199, 225.01


def parse_pos(hx):
    b = binascii.unhexlify(hx)
    for i in range(0, len(b) - 28):
        if b[i] != 0 or b[i + 1] != 0 or b[i + 2] != 0xC7:
            continue
        kind = b[i + 3]
        if kind not in (0x48, 0x49, 0x3D):
            continue
        o = i + 8
        if b[o : o + 4] != b"\x00\x00\x00\x00":
            continue
        o += 5
        x = struct.unpack_from(">f", b, o + 8)[0]
        y = struct.unpack_from(">f", b, o + 12)[0]
        z = struct.unpack_from(">f", b, o + 16)[0]
        if 1 < y < 30 and -1 < x < 500 and 0 < z < 500:
            return x, y, z
    return None


dists = []
for i, hx in enumerate(hexes):
    p = parse_pos(hx)
    if not p:
        print("fail", i)
        continue
    d = ((p[0] - sx) ** 2 + (p[2] - sz) ** 2) ** 0.5
    dists.append((d, i, p[0], p[2]))
dists.sort()
for d, i, x, z in dists:
    print("%5.1fm door %2d xz=(%.1f,%.1f)" % (d, i, x, z))
for r in (12, 18, 25, 40, 55):
    print("within", r, sum(1 for d, _, _, _ in dists if d <= r))
