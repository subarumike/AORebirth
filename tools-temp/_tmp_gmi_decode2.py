import struct

def dump_u32(label, hx):
    b = bytes.fromhex(hx)
    print("===", label)
    print([hex(struct.unpack_from(">I", b, i)[0]) for i in range(0, len(b) - 3, 4)])
    if len(b) % 4:
        print("rem", b[-(len(b) % 4) :].hex())

# rest after N3 header type+unk+char for OUT
dump_u32("OUT1 rest", "00000002470b2e140000c350762abc21000000c350762abc21000186a0000003f1")
dump_u32("OUT2 rest", "00000002470b2e140000c350762abc21000000c350762abc2100000000000007e2000000680000005a")
dump_u32("IN1 after char", "470b2e140000c350762abc21000000c350762abc2100000000000003f1")
dump_u32("IN2 after char", "470b2e140000c350762abc21000000c350762abc2100000000000003f1")

# reinterpret starting at different offsets for OUT1
rest = bytes.fromhex("00000002470b2e140000c350762abc21000000c350762abc21000186a0000003f1")
print("\nOUT1 tentative:")
print("  field0", struct.unpack_from(">I", rest, 0)[0])  # 2
# maybe next is not identity but 470B2E14 as single dword stamp/crc
print("  field1", hex(struct.unpack_from(">I", rest, 4)[0]))
# then Identity type 0x0000C350 instance char?
print("  idA type", hex(struct.unpack_from(">I", rest, 8)[0]), "inst", hex(struct.unpack_from(">I", rest, 12)[0]))
print("  idB type", hex(struct.unpack_from(">I", rest, 16)[0]), "inst", hex(struct.unpack_from(">I", rest, 20)[0]))
print("  last pair?", hex(struct.unpack_from(">I", rest, 24)[0]), hex(struct.unpack_from(">I", rest, 28)[0]))
# rem 1 byte 0xf1 at end of OUT1 - so last is not clean u32 pairs from 24
print("  bytes 24-end", rest[24:].hex())

# Better: after two identities (8+8), credits and action:
# offset 8: type C350 inst char
# offset 16: type C350 inst char  
# offset 24: 000186A0 = 100000 credits?
# offset 28: 000003F1 = action/result?
print("\ncredits?", struct.unpack_from(">I", rest, 24)[0])
print("tail32?", struct.unpack_from(">I", rest, 28)[0] if len(rest)>=32 else None)

rest2 = bytes.fromhex("00000002470b2e140000c350762abc21000000c350762abc2100000000000007e2000000680000005a")
print("\nOUT2:")
print("  field0", struct.unpack_from(">I", rest2, 0)[0])
print("  field1", hex(struct.unpack_from(">I", rest2, 4)[0]))
print("  idA", hex(struct.unpack_from(">I", rest2, 8)[0]), hex(struct.unpack_from(">I", rest2, 12)[0]))
print("  idB", hex(struct.unpack_from(">I", rest2, 16)[0]), hex(struct.unpack_from(">I", rest2, 20)[0]))
print("  from24", rest2[24:].hex())
print("  u32 from24", [hex(struct.unpack_from(">I", rest2, i)[0]) for i in range(24, len(rest2)-3, 4)])
# 00000000 000007E2 00000068 0000005A
# credits 0, item lowId 2018?, ql 104?, count 90?
print("  maybe credit", struct.unpack_from(">I", rest2, 24)[0])
print("  maybe item", struct.unpack_from(">I", rest2, 28)[0])
print("  maybe ql", struct.unpack_from(">I", rest2, 32)[0])
print("  maybe qty", struct.unpack_from(">I", rest2, 36)[0])

# Alignment issue: OUT1 has rem f1 — so structure might include a trailing byte or the last value is 3-byte?
# OUT1 length 33 = 1 + 32? or 4+4+8+8+4+4 +1?
# 4 (action2) + 4 (stamp) + 8 + 8 + 4 (credits 100000) + 4 (3F1) = 32, rem 1 byte F1
# OR credits is 5 bytes? Unlikely.
# OR field1 is Identity Type 0x470B Instance 0x2E14...? type 18187?
print("\nAlt identity for field1:")
print(" type", hex(struct.unpack_from(">H", rest, 4)[0]), "pad?", rest[6:8].hex(), "inst", hex(struct.unpack_from(">I", rest, 8)[0]))
# That would scramble following.

# Look at AO identity types - C350 = 50000 decimal. In AO, identity types:
# 50000 might be Market slot?
