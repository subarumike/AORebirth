from __future__ import print_function
import re, struct

path = r"AORebirth/Server/ZoneEngine/Core/Missions/MissionRollCaptureTemplate.cs"
text = open(path, encoding="utf-8").read()
h = "".join(re.findall(r'"([0-9A-Fa-f]{32,})"', text))
data = bytes.fromhex(h)
idxs = []
i = 0
while True:
    j = h.find("009C50", i)
    if j < 0:
        break
    idxs.append(j // 2)
    i = j + 2
print("009C50 offs", idxs)
for off in idxs:
    end = min(len(data), off + 40)
    b = data[off:end]
    if len(b) < 28:
        print("off", off, "short", len(b))
        continue
    typ, inst = struct.unpack(">II", b[0:8])
    u18, u19 = struct.unpack(">ii", b[8:16])
    x, y, z = struct.unpack(">fff", b[16:28])
    print("off", off, "pf", inst, "entrance", u18, u19, "xyz", round(x, 2), round(y, 2), round(z, 2))

# Find 32-char shortinfo strings: look for patterns after Unknown4 (often 0) - Thank
for needle in [b"Thank you", b"Great!", b"Kill", b"Find", b"Repair", b"track", b"stolen", b"radar", b"Art "]:
    start = 0
    while True:
        j = data.find(needle, start)
        if j < 0:
            break
        # dump up to null or 48 bytes
        chunk = data[j:j+48]
        print("text@%d %r" % (j, chunk.split(b"\0")[0][:48]))
        start = j + 1

# Item reward low/high near X3F1 marker 000003F1 followed by item ids
print("=== sample item reward ids near 0000018D ===")
i = 0
while True:
    j = h.find("00018D", i)
    if j < 0 or i > 20:
        break
    print("off", j//2, h[j:j+24])
    i = j + 2
    if len(idxs) > 0 and i > 100:
        break
