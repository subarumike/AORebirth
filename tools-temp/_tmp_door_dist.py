import struct
import math
from pathlib import Path

text = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_doors_1441792.csfrag").read_text()
hexes = []
for line in text.splitlines():
    s = line.strip().rstrip(",")
    if s.startswith('"') and s.endswith('"'):
        hexes.append(s[1:-1])

sx, sz = 298.199, 255.01


def pos(h):
    b = bytes.fromhex(h)
    for i in range(len(b) - 28):
        if b[i : i + 3] == b"\x00\x00\xc7" and b[i + 3] in (0x48, 0x49, 0x3D):
            o = i + 8
            if b[o : o + 4] != b"\x00\x00\x00\x00":
                continue
            o += 5
            x, y, z = struct.unpack(">fff", b[o + 8 : o + 20])
            if 1 < y < 30 and -50 < x < 800 and -50 < z < 800:
                return x, y, z
    return None


near = []
for h in hexes:
    p = pos(h)
    if not p:
        continue
    d = math.hypot(p[0] - sx, p[2] - sz)
    near.append((d, p))
near.sort()
print("doors", len(hexes))
for r in (40, 50, 55, 60, 70, 100, 500):
    print("near", r, sum(1 for d, _ in near if d <= r))
for d, p in near[:12]:
    print(f"{d:6.1f} ({p[0]:.1f},{p[1]:.1f},{p[2]:.1f})")
