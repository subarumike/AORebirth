import struct

def parse_market_body(hexstr, label):
    b = bytes.fromhex(hexstr)
    i = b.find(bytes.fromhex("470B2E14"))
    body = b[i:]
    # type(4) idType(4) idInst(4) idType(4) idInst(4) then fields
    fields = []
    for off in range(20, len(body) - 3, 4):
        fields.append(struct.unpack_from(">I", body, off)[0])
    rem = body[20 + 4 * len(fields) :]
    print(label)
    print("  ids", hex(struct.unpack_from(">I", body, 4)[0]), hex(struct.unpack_from(">I", body, 8)[0]),
          hex(struct.unpack_from(">I", body, 12)[0]), hex(struct.unpack_from(">I", body, 16)[0]))
    print("  fields", fields, "rem", rem.hex() if rem else "")
    # OUT preamble before type
    print("  preamble", b[:i].hex())

parse_market_body("0000000A00000000762ABC2100000002470B2E140000C350762ABC21000000C350762ABC21000186A0000003F1", "OUT1 credits")
parse_market_body("01E6000A0001002D00000DAD762ABC21470B2E140000C350762ABC21000000C350762ABC2100000000000003F1", "IN1 ack")
parse_market_body("0000000A00000000762ABC2100000002470B2E140000C350762ABC21000000C350762ABC2100000000000007E2000000680000005A", "OUT2 item")
parse_market_body("01EE000A0001002D00000DAD762ABC21470B2E140000C350762ABC21000000C350762ABC2100000000000003F1", "IN2 ack")

# OUT preamble: 0000000A 00000000 762ABC21 00000002
# action=2 on both OUTs before type hash
print("action before type on OUT:", 2)
print("OUT1: credits=100000 (0x186A0), trailer=0x3F1")
print("OUT2: credits=0, item=2018, ql=104, qty=90")
print("IN ack: credits=0, trailer=0x3F1 (same both)")
print("MarketSend N3 type hash = 0x470B2E14 =", 0x470B2E14)
