import struct

hx = "470B2E140000C350762ABC21000000C350762ABC21000186A0000003F1"
b = bytes.fromhex(hx)
print("len", len(b))
for i, x in enumerate(b):
    print("%02d %02X" % (i, x))

print("\nbytes 12-24:", b[12:24].hex())
# Expected second type C350 at 12 if no gap: would need bytes 00 00 C3 50
# Actual: 00 00 00 C3 50 76 2A BC 21 00 01 86

# Hypothesis: trailing F1 is NOT part of message — or Unknown byte at end
# Strip last byte and reparse as clean 28-byte body
b2 = b[:28]
print("\nstrip to 28:", b2.hex())
print([hex(struct.unpack_from(">I", b2, i)[0]) for i in range(0, 28, 4)])
# Still: 470B2E14 C350 762ABC21 000000C3 50762ABC 21000186 A0000003 — last incomplete without F1

# Full with F1 as completing last dword? A0000003 F1 → not a dword
# Unless last values are not aligned from start...

# Compare OUT2 which has clean trailing 5A as last byte of qty dword 0000005A
out2 = bytes.fromhex("470B2E140000C350762ABC21000000C350762ABC2100000000000007E2000000680000005A")
print("\nOUT2 len", len(out2))
print([hex(struct.unpack_from(">I", out2, i)[0]) for i in range(0, len(out2) - 3, 4)])
# same mis-align for second identity!

# So BOTH have 000000C3 50... meaning the pattern is real: second "type" is not C350 as u32 at +12

# Maybe only ONE identity after type, then payload ints that start with character again?
# type | C350 | char | then u32s:
vals = [struct.unpack_from(">I", out2, i)[0] for i in range(0, len(out2) - 3, 4)]
print("OUT2 single-id view from 0:")
for i, v in enumerate(vals):
    print(" ", i, hex(v), v)

# type, idType, idInst, then ??? 0xC3, 0x50762ABC, 0x21000000, 7, 0xE2000000, 0x68000000 + rem 5a
# That doesn't work.

# Identity type might be stored as 3 bytes? 
# Or: the hex in the log DOUBLE-COUNTS something...

# Look at AoSerializer for N3 - maybe Unknown (int16) after message type before identity
# type(4) + unknown(2) + identity...

body = out2
# N3Message base already consumed Unknown+Identity when logged as type MarketSend identity SimpleChar
# So raw OUT packet includes FULL stream from client wrapper, and 470B is interior.

# Client OUT format from Mail captures - compare a known Mail OUT hex shape.
print("\nOK: treat as type + two Standard identities but first idType is WRONG size in dump")
# What if id types are only written as 2 bytes:
# 470B2E14 | C350 | 762ABC21 | C350 | 762ABC21 | ...
off = 4
for n in range(2):
    t = struct.unpack_from(">H", body, off)[0]
    inst = struct.unpack_from(">I", body, off + 2)[0]
    print("id", n, "type16", hex(t), "inst", hex(inst))
    off += 6
print("payload", body[off:].hex())
print("payload u32", [hex(struct.unpack_from(">I", body, i)[0]) for i in range(off, len(body) - 3, 4)])
