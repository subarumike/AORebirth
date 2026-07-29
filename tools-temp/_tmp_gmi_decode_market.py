import struct
from pathlib import Path

msgs = [
    ("OUT1", bytes.fromhex("0000000A00000000762ABC2100000002470B2E140000C350762ABC21000000C350762ABC21000186A0000003F1")),
    ("IN1 ", bytes.fromhex("01E6000A0001002D00000DAD762ABC21470B2E140000C350762ABC21000000C350762ABC2100000000000003F1")),
    ("OUT2", bytes.fromhex("0000000A00000000762ABC2100000002470B2E140000C350762ABC21000000C350762ABC2100000000000007E2000000680000005A")),
    ("IN2 ", bytes.fromhex("01EE000A0001002D00000DAD762ABC21470B2E140000C350762ABC21000000C350762ABC2100000000000003F1")),
]

def dump(name, b):
    print("===", name, "len", len(b), "hex", b.hex())
    # OUT format: type(4 BE) unk(4) identity(4 type? + 4 id) then payload
    # IN has zone framing before N3
    # Find MarketSend type 0x0000000A
    idx = b.find(b"\x00\x00\x00\x0a")
    if idx < 0:
        idx = b.find(b"\x00\x0a")
    print(" find 0000000a @", idx)
    # try interpret as AO N3 out:
    # 00 00 00 0A | 00 00 00 00 | 76 2A BC 21 | ...
    if b[:4] == b"\x00\x00\x00\x0a":
        body = b[4:]
        unk = struct.unpack_from(">I", body, 0)[0]
        char = struct.unpack_from(">I", body, 4)[0]
        rest = body[8:]
        print(" OUT-ish unk=%08X char=%08X restlen=%d" % (unk, char, len(rest)))
        print(" rest hex", rest.hex())
        # dump u32s
        for i in range(0, len(rest) - 3, 4):
            print("  +%02d %08X (%d)" % (i, struct.unpack_from(">I", rest, i)[0], struct.unpack_from(">I", rest, i)[0]))
        if len(rest) % 4:
            print("  rem", rest[-(len(rest)%4):].hex())
    else:
        # IN: skip to after identity in framed packet
        # 01 E6 00 0A 00 01 00 2D 00 00 0D AD 76 2A BC 21 | payload
        # common pattern from other analyzes: after char id comes N3 body without type
        if len(b) >= 16 and b[2:4] == b"\x00\x0a":
            # short type at [2:4]? Actually bytes 1-3 might be size
            print(" bytes[0:16]", b[:16].hex())
            # Look for char id 762ABC21
            ci = b.find(bytes.fromhex("762ABC21"))
            print(" char@", ci)
            if ci >= 0:
                rest = b[ci+4:]
                print(" after-char hex", rest.hex())
                for i in range(0, len(rest) - 3, 4):
                    print("  +%02d %08X (%d)" % (i, struct.unpack_from(">I", rest, i)[0], struct.unpack_from(">I", rest, i)[0]))

for n, b in msgs:
    dump(n, b)

# Also read events DETAIL for MarketSend
cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260715-GMI")
for name in ("events.log", "events.txt"):
    p = cap / name
    if p.exists():
        print("\n===", name, "MarketSend DETAIL lines ===")
        for i, line in enumerate(p.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
            if "Market" in line or "market" in line.lower():
                print("L%d: %s" % (i, line[:500]))
