from __future__ import print_function
import re, struct, os

path = r"AORebirth/Server/ZoneEngine/Core/Missions/MissionRollCaptureTemplate.cs"
text = open(path, encoding="utf-8").read()
h = "".join(re.findall(r'"([0-9A-Fa-f]{32,})"', text))
print("len", len(h) // 2)
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
    b = bytes.fromhex(h[off * 2 : off * 2 + 48])
    typ, inst = struct.unpack(">II", b[0:8])
    u18, u19 = struct.unpack(">ii", b[8:16])
    floats = [struct.unpack(">f", b[k : k + 4])[0] for k in range(16, 28, 4)]
    print("off", off, "pf", inst, "u18/19", u18, u19, "xyz", floats)

# Also dump short infos (32-byte fixed after unknowns) via looking for ASCII Thank
for needle in [b"Thank you", b"Find ", b"Kill ", b"Repair", b"track", b"stolen", b"radar"]:
    start = 0
    data = bytes.fromhex(h)
    while True:
        j = data.find(needle, start)
        if j < 0:
            break
        snippet = data[j : j + 40]
        print("text@%d %r" % (j, snippet))
        start = j + 1
