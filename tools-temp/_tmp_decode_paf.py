from __future__ import print_function
import binascii, struct, os

# Full decode of first PAF for 1419310 using PlayfieldAnarchyFMessageSerializer layout
# RawHex may include packet framing - find N3MessageType

path = r"tools-temp\_tmp_mission_shapes_assets\paf_1419310.hex"
hx = open(path).read().strip().splitlines()[0].strip().replace(" ","").upper()
raw = binascii.unhexlify(hx)

# Try offset 0
def try_parse(raw, off):
    try:
        p = off
        n3 = struct.unpack_from(">I", raw, p)[0]; p += 4
        id_t = struct.unpack_from(">I", raw, p)[0]; p += 4
        id_i = struct.unpack_from(">I", raw, p)[0]; p += 4
        unk = raw[p]; p += 1
        unk1 = struct.unpack_from(">I", raw, p)[0]; p += 4
        x,y,z = struct.unpack_from(">fff", raw, p); p += 12
        unk2 = raw[p]; p += 1
        pf1t = struct.unpack_from(">I", raw, p)[0]; p += 4
        pf1i = struct.unpack_from(">I", raw, p)[0]; p += 4
        unk3 = struct.unpack_from(">I", raw, p)[0]; p += 4
        unk4 = struct.unpack_from(">I", raw, p)[0]; p += 4
        pf2t = struct.unpack_from(">I", raw, p)[0]; p += 4
        pf2i = struct.unpack_from(">I", raw, p)[0]; p += 4
        rem = raw[p:]
        return dict(off=off,n3=n3,id_t=id_t,id_i=id_i,unk=unk,unk1=unk1,xyz=(x,y,z),unk2=unk2,
                    pf1=(pf1t,pf1i),unk3=unk3,unk4=unk4,pf2=(pf2t,pf2i),remlen=len(rem),
                    remhead=binascii.hexlify(rem[:16]).decode())
    except Exception as e:
        return {"err": str(e), "off": off}

for off in range(0, 16):
    r = try_parse(raw, off)
    if r.get("err"):
        continue
    # look for sensible: pf1 type C79F, pf2 type 9C50
    if r["pf1"][0] == 0xC79F and r["pf2"][0] == 0x9C50:
        print("MATCH", r)
        print(" remlen", r["remlen"])
        break
else:
    print("no match; first tries:")
    for off in (0,4,8):
        print(try_parse(raw, off))
