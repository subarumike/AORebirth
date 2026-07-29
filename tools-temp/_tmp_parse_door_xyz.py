# Parse door/chest XYZ from capture hex packets (shape catalog + this capture)
from __future__ import print_function
import binascii, struct, os, re

def find_floats(raw):
    # find plausible mission coords: x,z in 0..400, y ~5
    hits=[]
    for i in range(0, len(raw)-12):
        try:
            x,y,z = struct.unpack_from(">fff", raw, i)
        except:
            continue
        if 0 < x < 400 and 0 < z < 400 and 0 < y < 30:
            hits.append((i,x,y,z))
    return hits

# sample doors from shape assets
path = r"tools-temp\_tmp_mission_shapes_assets\doors_1419310.hex"
if os.path.exists(path):
    lines=open(path).read().strip().splitlines()
    print("shape doors sample", len(lines))
    for hx in lines[:3]:
        raw=binascii.unhexlify(hx.strip())
        hits=find_floats(raw)
        print(" len", len(raw), "hits", hits[:3])

# chests
path = r"tools-temp\_tmp_mission_shapes_assets\chests_1419310.hex"
if os.path.exists(path):
    lines=open(path).read().strip().splitlines()
    print("shape chests sample", len(lines))
    for hx in lines[:3]:
        raw=binascii.unhexlify(hx.strip())
        hits=find_floats(raw)
        print(" len", len(raw), "hits", hits[:3])

# radar
hx=open(r"tools-temp\_tmp_cap_181214_assets\radar_sifu.hex").read().strip().splitlines()[0]
raw=binascii.unhexlify(hx)
print("radar hits", find_floats(raw)[:5])
print("radar identity types:")
for i in range(len(raw)-3):
    if raw[i:i+2]==b"\x00\x00" and raw[i+2] in (0xC7, 0x0D):
        print(i, binascii.hexlify(raw[i:i+8]).decode())
