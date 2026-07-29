"""Decode Sparrow Flight named SpellList NanoEffects (56 bytes each)."""
hx = "00E1000A000101A800000DB9001394594D4501140000C3500013945900000023790000CFB7000143930000000400000000000000010000000000000001000000090000009C000004B00000CFB7000143930000000400000000000000010000000000000001000000090000009A000000B60000CFB70001439300000004000000000000000100000000000000010000000900000099000000B60000CFB7000143930000000400000000000000010000000000000001000000090000009B000000B60000CF44000143930000000400000000000000010000000000000001000000090000769D000000000000CF4C0001439300000004000000000000000100000000000000010000000900000002000000000000CF92000143930000000400000003000002130000000000000000000000000000000000000070000000000000000000000004000000010000000000000001000000090000CF3B0001439300000004000000000000000100000000000000020000000900042B8C0000C350001394590000C3500013945900000E53706172726F7720466C69676874010000CF1B000143930000000000"
# After type 4D450114 and identity 8 bytes and count dword:
payload = bytes.fromhex(hx)
# find after 4D450114
i = hx.upper().find("4D450114") // 2
# skip type(4) + identity(8) + unknown byte? Looking at serializer: N3Type, Identity, Unknown byte, then count
# Packet structure from AO: after N3 header with identity in header AND body?
# From serializer Deserialize: ReadInt32 type, ReadIdentity, ReadByte unknown, ReadInt32 count
# But raw starts with zone header then N3...
# Body after zone: looking at 4D450114 then 0000C35000139459 then 00 then 00002379
body_start = hx.upper().find("4D450114")
rest = hx[body_start+8:]  # after type
# identity
print("identity", rest[:16])
rest = rest[16:]
print("unknown byte", rest[:2])
rest = rest[2:]
count_raw = int(rest[:8], 16)
print("count field", count_raw, "effects", (count_raw // 0x3F1) - 1)
rest = rest[8:]
n = (count_raw // 0x3F1) - 1
data = bytes.fromhex(rest)
for e in range(n):
    chunk = data[e*56:(e+1)*56]
    ints = [int.from_bytes(chunk[i:i+4], "big") for i in range(0, 56, 4)]
    print(f"effect{e}: type=0x{ints[0]:X} inst={ints[1]} u1={ints[2]} crit={ints[3]} hits={ints[4]} delay={ints[5]} u2={ints[6]} u3={ints[7]} gfxV={ints[8]} gfxL={ints[9]} gfxS={ints[10]} r={ints[11]} g={ints[12]} b={ints[13]}")
