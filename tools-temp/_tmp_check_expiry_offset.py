import re, struct
h = open(r"AORebirth/Server/ZoneEngine/Core/Missions/MissionAcceptCaptureTemplate.cs", encoding="utf-8").read()
hx = "".join(re.findall(r'"([0-9A-Fa-f]+)"', h))
data = bytes.fromhex(hx)
print("len", len(data), "ExpiryOffset const 671")
# show ints around 650-700
for off in range(640, 700, 4):
    v = struct.unpack(">I", data[off:off+4])[0]
    print(off, hex(v), v)
# also find plausible unix-ish timestamps in packet
for off in range(0, len(data)-4, 1):
    v = struct.unpack(">I", data[off:off+4])[0]
    if 1_700_000_000 < v < 2_000_000_000 or 1_200_000_000 < v < 1_300_000_000:
        print("ts candidate", off, v)
