# Find OpenSystemDialog (53168) usages and nearby strings in items.dat / playfields.dat
import struct, pathlib

FN = 53168
pat_le = struct.pack("<I", FN)
pat_be = struct.pack(">I", FN)

files = [
    pathlib.Path(r"AORebirth\Built\Debug\items.dat"),
    pathlib.Path(r"AORebirth\Built\Debug\playfields.dat"),
    pathlib.Path(r"AORebirth\Datafiles\items.dat"),
    pathlib.Path(r"AORebirth\Datafiles\playfields.dat"),
]

for p in files:
    if not p.exists():
        continue
    data = p.read_bytes()
    print("===", p, "size", len(data))
    count = 0
    for pat, endian in ((pat_le, "le"), (pat_be, "be")):
        start = 0
        while count < 40:
            i = data.find(pat, start)
            if i < 0:
                break
            window = data[max(0, i - 32) : i + 200]
            ascii_bits = "".join(chr(b) if 32 <= b < 127 else "." for b in window)
            if any(s in ascii_bits.lower() for s in ("http", "uwg", "market", "trade", "gmi", "omni", "funcom", "vgtp")) or True:
                # print first few and any with url-ish
                interesting = any(s in window.lower() for s in (b"http", b"uwg", b"market", b"trade", b"omni", b"funcom", b"vgtp", b"aomarket"))
                if interesting or count < 5:
                    print(f"  {endian}@{i} interesting={interesting} {ascii_bits[:160]}")
            start = i + 4
            count += 1
    print("  scanned hits (capped)", count)

# also search raw url strings in items/playfields
for p in files:
    if not p.exists():
        continue
    data = p.read_bytes().lower()
    for needle in (b"uwg.trade", b"aomarket", b"omni-rk", b"vgtp://", b"http://uwg", b"https://uwg", b"http://ao"):
        idx = data.find(needle)
        print(p.name, needle, "->", idx)
        if idx >= 0:
            print(" ", p.read_bytes()[idx:idx+80])
