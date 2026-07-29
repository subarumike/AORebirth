#!/usr/bin/env python3
"""Decode Ambient Restoration + Burning SpellList raw hex from capture."""
import binascii

ambient = "1135000A0001007D00000DC1797E30D74D4501140000C350797E30D700000007E20000CF4A00049D1D000000040000000100000080000000900000000100000001000000000000000200000009000495CF0000C350797E30D70000C350797E30D7000013416D6269656E7420526573746F726174696F6E000000000000"
burn = "1229000A0001007600000DC1797E30D74D4501140000C3507987C60D00000007E20000CF2643BD71C70000000400000000000000010000000000000000000000000000A87100000000000000000000000000000000000000000000000000000000000000000000C3507987C60D000000000000000000"

def decode(label, hx):
    b = binascii.unhexlify(hx)
    print("====", label, "len", len(b))
    # skip packet framing to N3 payload start: find 4D450114 (ME..)
    i = b.find(bytes.fromhex("4D450114"))
    print("ME offset", i)
    # After ME: Identity(8) Unknown(4) then body
    # Standard N3: msgtype already in header
    # From Identity in payload after ME:
    # Identity type+inst at i+4?
    payload = b[i:]  # from ME
    print("from ME", payload.hex())
    # ME(2) + type(2)? Actually 4D450114 = N3MessageType SpellList?
    # 4D45 = 'ME', 0114 = ?
    # Common AO: after length header, Identity of receiver, then N3 type, then body identity
    # Raw starts: 11 35 00 0A 00 01 00 7D 00 00 0D C1 79 7E 30 D7 4D 45 01 14 00 00 C3 50 79 7E 30 D7 00 00 00 07 E2
    # Let's parse from body after Unknown=0x7E2
    body_start = b.find(bytes.fromhex("00000007E2")) + 5
    body = b[body_start:]
    print("body", body.hex())
    print("body len", len(body))
    # X3F1 array size: first 3 bits of first byte?
    # Looking at SmokeLounge ArraySizeType.X3F1
    off = 0
    # Effect identity
    et = int.from_bytes(body[off:off+4],'big'); off+=4
    ei = int.from_bytes(body[off:off+4],'big'); off+=4
    print(f"Effect type=0x{et:X} inst=0x{ei:X} ({ei})")
    for name in ("Unknown1","CriterionCount","Hits","Delay","Unknown2","Unknown3","GfxValue","GfxLife","GfxSize","GfxRed","GfxGreen","GfxBlue","GfxFade"):
        v = int.from_bytes(body[off:off+4],'big'); off+=4
        print(f"  {name}={v} (0x{v:X})")
    print("remaining", body[off:].hex())
    rem = body[off:]
    # try parse identities + string
    if len(rem) >= 8:
        t=int.from_bytes(rem[0:4],'big'); i=int.from_bytes(rem[4:8],'big')
        print(f" next Identity type=0x{t:X} inst=0x{i:X}")
    if len(rem) >= 16:
        t=int.from_bytes(rem[8:12],'big'); i=int.from_bytes(rem[12:16],'big')
        print(f" next Identity type=0x{t:X} inst=0x{i:X}")
    if len(rem) >= 17:
        sl = rem[16]
        print(f" string len byte={sl} text={rem[17:17+sl]!r}")
        print(f" after string={rem[17+sl:].hex()}")

decode("ambient", ambient)
decode("burn", burn)
