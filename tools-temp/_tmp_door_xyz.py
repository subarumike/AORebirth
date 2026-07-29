import struct

text = open(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_doors_1441792.csfrag",
    encoding="utf-8",
).read()
parts = text.split('"')
hx = parts[1]
data = bytes.fromhex(hx)
print("len", len(data))
print(hx[:160])
m = data.find(bytes.fromhex("365A5071"))
print("marker", m)
for off in range(m, min(m + 100, len(data) - 12)):
    x, y, z = struct.unpack_from("<fff", data, off)
    if 1 < y < 20 and 50 < x < 500 and 50 < z < 500:
        print("rel", off - m, "xyz", x, y, z)
