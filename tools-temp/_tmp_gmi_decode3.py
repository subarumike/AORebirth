# Proper N3 header: type(4) unknown(4) idType(4) idInst(4)
out1 = bytes.fromhex("0000000A00000000762ABC2100000002470B2E140000C350762ABC21000000C350762ABC21000186A0000003F1")
print("len", len(out1))
# Case A: identity is only Instance (no type) — 12 byte header
print("A payload", out1[12:].hex(), "len", len(out1)-12)
# Case B: standard 16-byte header — but then first payload bytes would be 00000002 after type in header wrongly
# If chars: type 0A, unk 0, THEN idType should be next - but next is 762ABC21 which is instance
# So this capture strips Identity.Type from raw? Or Type is 0 and missing?

# Case C: type(4) unk(4) idType(4)=762ABC21??? nonsense

# Case D: the hex in csv is NOT including N3 type as 0x0A - maybe 0x0A is something else
# and MarketSend real type is hash 470B2E14?

# Compare IN: framing + body without outbound envelope
inn = bytes.fromhex("01E6000A0001002D00000DAD762ABC21470B2E140000C350762ABC21000000C350762ABC2100000000000003F1")
print("IN len", len(inn))
# Common zone in: [flags?][size][msgtype]... 
# Find 470B2E14 in both
print("OUT has 470B @", out1.find(bytes.fromhex("470B2E14")))
print("IN  has 470B @", inn.find(bytes.fromhex("470B2E14")))

# Shared payload after 470B:
shared_start = out1.find(bytes.fromhex("470B2E14"))
print("OUT from 470B", out1[shared_start:].hex())
print("IN from 470B", inn[inn.find(bytes.fromhex("470B2E14")):].hex())

# Shared then before that OUT has 00000002, IN has none (action only on client?)
# OUT before 470B from start:
print("OUT before", out1[:shared_start].hex())
print("IN before", inn[:inn.find(bytes.fromhex("470B2E14"))].hex())

# Shared body interpretation from 470B:
# 470B2E14 | 0000C350 762ABC21 | 0000C350 762ABC21 | 000186A0 | 000003F1
body = out1[shared_start:]
import struct
print("body u32", [hex(struct.unpack_from('>I', body, i)[0]) for i in range(0, len(body)-3, 4)])
print("rem", body[32:].hex() if len(body)>32 else body[len(body)//4*4:].hex())
# 470B2E14 might be the real N3MessageType for MarketSend!
# Then identity Type C350 Instance char (twice?). Then amount 100000 (0x186A0). Then action 0x3F1?

# OUT2:
out2 = bytes.fromhex("0000000A00000000762ABC2100000002470B2E140000C350762ABC21000000C350762ABC2100000000000007E2000000680000005A")
b2 = out2[out2.find(bytes.fromhex("470B2E14")):]
print("\nOUT2 from 470B", b2.hex())
print("u32", [hex(struct.unpack_from('>I', b2, i)[0]) for i in range(0, len(b2)-3, 4)])
print("rem", b2[len(b2)//4*4:].hex() if len(b2)%4 else "")
# 470B2E14 C350 char C350 char 00000000 000007E2 00000068 0000005A
# credits 0, itemId 0x7E2=2018, ql=0x68=104, stacks=0x5A=90

print("\nitem", 0x7E2, "ql", 0x68, "qty", 0x5A, "credits OUT1", 0x186A0)
