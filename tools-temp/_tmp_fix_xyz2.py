# Fix terminal XYZ parse; list world terminals only
from __future__ import print_function
import binascii, struct, os

def world_xyz(hx):
    raw = binascii.unhexlify(hx)
    for i in range(len(raw)-28):
        if raw[i]==0 and raw[i+1]==0 and raw[i+2]==0xC7 and raw[i+3] in (0x3D, 0x48, 0x49):
            # Identity(8) + pad(4) + unk(1) + owner(8) + xyz(12)
            o = i + 8
            if o+4 <= len(raw) and raw[o:o+4] != b"\x00\x00\x00\x00":
                continue
            o += 4
            if o >= len(raw):
                continue
            unk = raw[o]; o += 1
            if o+20 > len(raw):
                continue
            # owner may be nonzero for some; still try floats after 8 bytes
            x,y,z = struct.unpack_from(">fff", raw, o+8)
            if 1 < y < 20 and 0 < x < 500 and 0 < z < 500:
                return (x,y,z, unk, "id=%02X%02X%02X%02X" % (raw[i+4],raw[i+5],raw[i+6],raw[i+7]))
    return None

base = r"tools-temp\_tmp_mission_shapes_assets"
for name in sorted(os.listdir(base)):
    if not name.startswith("terms_"):
        continue
    print("===", name)
    for hx in open(os.path.join(base,name)).read().strip().splitlines():
        print(" ", world_xyz(hx), "static@", hx.find("000002BD"), hx[hx.find("000002BD")-8:hx.find("000002BD")] if hx.find("000002BD")>=8 else None)

# doors first packet
hx=open(os.path.join(base,"doors_1419310.hex")).read().strip().splitlines()[0]
print("door0", world_xyz(hx))
