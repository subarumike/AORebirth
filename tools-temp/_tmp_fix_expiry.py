import re, struct
h = open(r"AORebirth/Server/ZoneEngine/Core/Missions/MissionAcceptCaptureTemplate.cs", encoding="utf-8").read()
hx = "".join(re.findall(r'"([0-9A-Fa-f]+)"', h))
data = bytes.fromhex(hx)
needle = "6A5CE568"
print("6A5CE568 offset", hx.find(needle)//2)
# perk style: look at context after UnknownHash / before playfield
# print 20 ints before 009C50000002DF
pf = hx.find("009C50000002DF")
print("playfield marker offset", pf//2)
# walk back
off = pf // 2
print("bytes before pf:")
chunk = data[off-40:off+28]
for i in range(0, len(chunk)-3, 4):
    abs_off = off - 40 + i
    print(abs_off, chunk[i:i+4].hex(), struct.unpack(">I", chunk[i:i+4])[0], struct.unpack(">i", chunk[i:i+4])[0])

# Also check 679EB100 which appeared near coords in earlier read
print("679EB100", hx.find("679EB100")//2)
print("D2FC1C67", hx.find("D2FC1C67")//2)
# Live capture accept expiry from pull-mish - the constant ExpiryOffset=671 was claimed
# Writing at 671 would overwrite: data[671]=0x5C from 6A5CE568 if 6A is at 668
print("if write at 671, corrupts:", data[668:676].hex())
print("correct aligned timestamp candidates:")
for off in [660, 664, 668, 672, 676, 680, 684]:
    print(off, data[off:off+4].hex(), struct.unpack(">I", data[off:off+4])[0])
