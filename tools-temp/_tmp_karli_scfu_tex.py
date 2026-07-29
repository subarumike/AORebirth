# Decode Karli SCFU textures/meshes/appearance
from __future__ import print_function
import struct

hexstr = (
"000F000A0001013A00000DB17996C028271B3A6B0000C350799AD394003A0A0B4ACB00001F494210964C3EDEB8514237AD58000000003F25312F000000003F4391E1"
"00000748114B61726C692043617070656C6C65726900108812010000000089000000000F0189000000660D0064001F000000001C3FBCA457BDF1EE873E7FF7650202010100010001000100000002000000009D1734000003F10000C350799AD394000000024210964C3EDEB8514237AD584224F525350E0000423B1CA5000017A6000000000000000000000000000000010004184E00000000000000020003C8B500000000000000030004185100000000000000040004185000000000000017A600000418480003C4A0020000009D170000000004010004184700000000020200041861000000000305000418460003C4A0000000000000"
)
raw = bytes.fromhex(hexstr)
# find name
idx = raw.find(b"Karli Cappelleri")
body = raw[idx+len("Karli Cappelleri")+1:]
print("flags", struct.unpack_from(">I", body, 0)[0])
print("acct", struct.unpack_from(">I", body, 4)[0])
print("exp", struct.unpack_from(">I", body, 8)[0])
# NPCInfo level etc - after expansions often side/breed etc packed
print("body[:80]", body[:80].hex())
# From known: after flags/acct/exp: Level=15 Health=393 MD=26125 Scale=100 VF=31
# 0000000F 00000189 00000066 0D006400 1F000000
off = 12
print("lvl", struct.unpack_from(">I", body, off)[0]); off+=4
print("hp", struct.unpack_from(">I", body, off)[0]); off+=4
print("md", struct.unpack_from(">I", body, off)[0]); off+=4
# scale might be ushort
print("next8", body[off:off+16].hex())
# 0D00 6400 1F00 0000 001C ...
print("npcfam?", struct.unpack_from(">H", body, off)[0])
print("scale?", struct.unpack_from(">H", body, off+2)[0])
print("vf?", struct.unpack_from(">H", body, off+4)[0])
# ScfuUnk1 28 bytes starting after title?
# From detail ScfuUnk1 at after VisibleTitle=0
# skip to textures: look for pattern place/id
# Known textures section: 0000010004184E ...
tex_at = body.find(bytes.fromhex("0000010004184E"))
print("tex_at", tex_at, "hex", body[tex_at:tex_at+80].hex() if tex_at>=0 else None)
if tex_at >= 0:
    # count was earlier - 5 textures: each is place(int) id(int)? or place ushort?
    # format from other code: Texture place + id as ints
    p = tex_at
    # maybe leading count already consumed; data starts at 0000010004184E = place1=1? 
    # Actually 00 00 01 00 04 18 4E = place=0x00000100? 
    # Leonora uses new[] { 0, 85939 } - place, textureId
    # Try: int place, int id repeated
    for i in range(5):
        place, tid = struct.unpack_from(">II", body, p)
        print("tex", i, place, tid)
        p += 8

# meshes after textures - 000017A600000418480003C4A0 then more?
mesh_marker = body.find(bytes.fromhex("000017A6"))
print("mesh_marker", mesh_marker, body[mesh_marker:mesh_marker+60].hex() if mesh_marker>=0 else None)
# From end: 00009D170000000004010004184700000000020200041861000000000305000418460003C4A0
mesh_at = body.find(bytes.fromhex("00009D1700000000"))
print("mesh_at", mesh_at)
if mesh_at >= 0:
    p = mesh_at
    for i in range(5):
        a,b,c,d = struct.unpack_from(">IIII", body, p) if False else None
        # Mesh format often: id, unknown, position, override?
        vals = struct.unpack_from(">IIII", body, p)
        print("mesh_try4", i, vals)
        p += 16

# Better from known Leonora Meshes = new[] { new[] { 0, 40228, 0, 4 }, new[] { 1, 268645, 0, 2 } };
# 4 ints: pos, meshId, unk, override?
p = mesh_at
for i in range(5):
    if p+16 > len(body):
        break
    vals = struct.unpack_from(">IIII", body, p)
    print("mesh", i, vals)
    p += 16
