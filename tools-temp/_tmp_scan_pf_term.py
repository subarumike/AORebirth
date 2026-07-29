# Dump playfield 4680 terminals from PlayfieldLoader-compatible playfields.dat if possible,
# else brute-search for C0070320-like patterns and template ints near terminals.
import struct, pathlib

data = pathlib.Path(r"AORebirth\Built\Debug\playfields.dat").read_bytes()
print("playfields.dat", len(data))

# search for playfield id 4680 as int32
for endian, fmt in (("le", "<I"), ("be", ">I")):
    pat = struct.pack(fmt.replace("I","I"), 4680)
    idxs = []
    start = 0
    while len(idxs) < 20:
        i = data.find(pat, start)
        if i < 0:
            break
        idxs.append(i)
        start = i + 1
    print("4680", endian, "hits", idxs[:10])

# Search identity type Terminal 0xC73D near instance patterns with high bit set (0xC0......)
term = 0xC73D
for endian, fmt in (("le", "<II"), ("be", ">II")):
    count = 0
    # scan aligned
    step = 4
    unpack = struct.Struct(fmt)
    for i in range(0, len(data) - 8, step):
        a, b = unpack.unpack_from(data, i)
        if a == term and (b & 0xFF000000) == 0xC0000000:
            print(f"Terminal identity {endian}@{i}: instance={b:08X} signed={b if b<0x80000000 else b-0x100000000}")
            # nearby ints
            nearby = []
            for off in range(-16, 48, 4):
                if 0 <= i+off <= len(data)-4:
                    nearby.append(f"{off}:{struct.unpack_from(fmt[0]+'I', data, i+off)[0]:08X}")
            print(" ", " ".join(nearby))
            count += 1
            if count >= 30:
                break
    print("count", endian, count)

# specifically find C0070320 anywhere
for endian, fmt in (("le", "<I"), ("be", ">I")):
    pat = struct.pack(fmt, 0xC0070320)
    start = 0
    found = 0
    while found < 10:
        i = data.find(pat, start)
        if i < 0:
            break
        print(f"C0070320 {endian}@{i}")
        print(" ", data[max(0,i-32):i+64].hex())
        start = i + 4
        found += 1
