# Test door XYZ parse with same logic as C# ReadFloatBe
from __future__ import print_function
import struct, binascii

hx = open(r"AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceDynelCapture.cs", encoding="utf-8", errors="replace").read()
# grab first door hex string
import re
m = re.search(r'Doors_1419310.*?\"([0-9A-F]{80,})\"', hx, re.S)
hx0 = m.group(1)
raw = binascii.unhexlify(hx0)
print("len", len(raw))

def read_float_be_csharp_style(packet, offset):
    bits = (packet[offset] << 24) | (packet[offset+1] << 16) | (packet[offset+2] << 8) | packet[offset+3]
    # BitConverter.GetBytes(bits) on LE then ToSingle
    import struct as st
    return st.unpack('<f', st.pack('<I', bits & 0xFFFFFFFF))[0]

def try_parse(raw):
    for i in range(len(raw)-28):
        if raw[i:i+3] != b'\x00\x00\xc7':
            continue
        kind = raw[i+3]
        if kind not in (0x48, 0x49, 0x3D):
            continue
        o = i + 8
        if raw[o:o+4] != b'\x00\x00\x00\x00':
            continue
        o += 5
        x = read_float_be_csharp_style(raw, o+8)
        y = read_float_be_csharp_style(raw, o+12)
        z = read_float_be_csharp_style(raw, o+16)
        if 1 < y < 30 and 0 < x < 500 and 0 < z < 500:
            return i, x, y, z
    return None

print("parse", try_parse(raw))
# correct BE float
for i in range(len(raw)-28):
    if raw[i:i+4]==b'\x00\x00\xc7\x48':
        o=i+8+4+1+8
        x,y,z=struct.unpack_from('>fff', raw, o)
        print("direct BE", x,y,z)
        break

# spawn for 1419310
print("spawn approx 298,115 - door0 should be near")
