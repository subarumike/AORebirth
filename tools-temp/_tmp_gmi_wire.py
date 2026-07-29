"""Decode MarketSend payloads with standard N3 header: type Unknown Identity then body."""
import struct
from pathlib import Path

def decode_out(hexstr, label):
    b = bytes.fromhex(hexstr)
    print("\n===", label, "len", len(b))
    # Live capture OUT strips zone framing differently than IN.
    # csv type hash column = 1191915028 = 0x470B2E14
    i = b.find(bytes.fromhex("470B2E14"))
    print("type@", i, "preamble", b[:i].hex() if i>=0 else b.hex())
    if i < 0:
        return
    # From type onward as if full N3 body including type
    body = b[i:]
    # type(4) + unknown(4)? + identity(8) ...
    # Observed: type immediately followed by identity type C350 (no unknown dword)
    # Compare Mail OUT style - check a simple mapped message.

    # Parse assuming: Type(4) Identity.Type(4) Identity.Instance(4) then payload
    t = struct.unpack_from(">I", body, 0)[0]
    idt = struct.unpack_from(">I", body, 4)[0]
    idi = struct.unpack_from(">I", body, 8)[0]
    print("type %#x idType %#x idInst %#x" % (t, idt, idi))
    pay = body[12:]
    print("payload hex", pay.hex())
    # If double identity: next also C350+char
    if len(pay) >= 8:
        idt2 = struct.unpack_from(">I", pay, 0)[0]
        idi2 = struct.unpack_from(">I", pay, 4)[0]
        print("maybe 2nd id %#x %#x" % (idt2, idi2))
        rest = pay[8:]
        print("rest hex", rest.hex())
        # OUT1: after first id in payload we'd have second id then credits
        # Actually payload after FIRST identity in body[12:] for OUT1:
        # from earlier OUT1 from470B: 470B2E14 0000C350 762ABC21 000000C350762ABC21...
        # WAIT byte-accurate earlier showed EXTRA 00 at second identity.

    # Byte dump of body
    for idx, x in enumerate(body):
        if idx % 16 == 0:
            print("%02d:" % idx, end=" ")
        print("%02X" % x, end=" ")
        if idx % 16 == 15:
            print()
    print()

# Also parse with Unknown=0 Int32 between type and identity (standard N3Message)
def decode_std(hexstr, label):
    b = bytes.fromhex(hexstr)
    i = b.find(bytes.fromhex("470B2E14"))
    body = b[i:]
    print("\n--- std N3 for", label)
    if len(body) < 16:
        print("too short")
        return
    t, unk, idt, idi = struct.unpack_from(">IIII", body, 0)
    print("type %#x unk %#x id %#x/%#x" % (t, unk, idt, idi))
    print("rest", body[16:].hex())
    rest = body[16:]
    vals = [struct.unpack_from(">I", rest, o)[0] for o in range(0, len(rest)-3, 4)]
    print("u32", ["%#x(%d)" % (v,v) for v in vals], "rem", rest[len(vals)*4:].hex())

msgs = [
("OUT1 credit", "0000000A00000000762ABC2100000002470B2E140000C350762ABC21000000C350762ABC21000186A0000003F1"),
("IN1 ack", "01E6000A0001002D00000DAD762ABC21470B2E140000C350762ABC21000000C350762ABC2100000000000003F1"),
("OUT2 item", "0000000A00000000762ABC2100000002470B2E140000C350762ABC21000000C350762ABC2100000000000007E2000000680000005A"),
("IN2 ack", "01EE000A0001002D00000DAD762ABC21470B2E140000C350762ABC21000000C350762ABC2100000000000003F1"),
]
for n,h in msgs:
    decode_out(h, n)
    decode_std(h, n)

# Compare preamble OUT: 0000000A 00000000 762ABC21 00000002
# 0x0A packet type? character without type? action 2?
print("\nOUT preamble interpretation:")
print("  dword0 0xA = ?")
print("  dword1 0 = ?")
print("  dword2 char instance only")
print("  dword3 2 = action Deposit?")
