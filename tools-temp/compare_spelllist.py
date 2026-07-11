import struct
import io

heal = bytes.fromhex(
    open(
        r"c:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260710-185528\packets.hex.log",
        encoding="utf-8",
    )
    .readlines()[9]
    .split("hex=")[1]
    .strip()
)
atk = bytes.fromhex(
    open(
        r"c:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260710-185528\packets.hex.log",
        encoding="utf-8",
    )
    .readlines()[45]
    .split("hex=")[1]
    .strip()
)


def write_u16_be(s, v):
    s.write(struct.pack(">H", v))


def write_i32_be(s, v):
    s.write(struct.pack(">i", v))


def write_i32_le(s, v):
    s.write(struct.pack("<i", v))


def build_heal_body():
    s = io.BytesIO()
    s.write(bytes([0x07, 0xE2, 0, 0]))
    write_u16_be(s, 53167)
    write_u16_be(s, 0)
    write_i32_be(s, 125746)
    write_i32_le(s, 4)
    write_i32_le(s, 2)
    healing_mid = bytes(
        [
            0x00, 0x00, 0x00, 0x00, 0x02, 0xD0, 0x00, 0x00, 0x05, 0xD0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x02, 0xA0, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00,
        ]
    )
    s.write(healing_mid)
    write_i32_le(s, 9)
    s.write(b"MT09\x00\x00\x00\x00\x00")
    write_i32_le(s, -1073741824)
    healing_tail = bytes(
        [
            0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xB1, 0xAD, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00,
            0x83, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x51, 0x03, 0x00, 0x00,
            0x51, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        ]
    )
    s.write(healing_tail)
    write_u16_be(s, 0xC350)
    write_i32_le(s, 0x6828FE35)
    write_u16_be(s, 0)
    write_u16_be(s, 0xC350)
    write_i32_le(s, 0x6828FE35)
    write_u16_be(s, 0)
    name = b"Calling of Belamorte"
    write_i32_le(s, len(name))
    s.write(name)
    s.write(b"\x00" * ((4 - len(name) % 4) % 4))
    return s.getvalue()


def compare(name, cap, built):
    print(name, "cap", len(cap), "built", len(built))
    for i in range(max(len(cap), len(built))):
        cb = cap[i] if i < len(cap) else None
        bb = built[i] if i < len(built) else None
        if cb != bb:
            print(" diff", i, cb, bb)
            print("  cap", cap[max(0, i - 4) : i + 16].hex())
            print("  bld", built[max(0, i - 4) : i + 16].hex())
            return
    print(" EXACT MATCH")


compare("heal", heal[31:], build_heal_body())

# wire packet
built = build_heal_body()
prefix_len = 23
wire = 8 + prefix_len + len(built)
print("expected wire", len(heal), "calc", wire)
