# -*- coding: utf-8 -*-
import pathlib, struct, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
hx = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-212713\_qfu_hex.txt").read_text().strip()
data = bytes.fromhex(hx)
print("len", len(data))

# Find short info ascii
needle = b"Buy some Nano Programs"
idx = data.find(needle)
print("short at", idx)

# After long string ends, parse UnknownId1 etc.
# Simpler: search for ints 1160, 2581, 223373
for val in (1160, 2581, 223373, 1240, 2569, 244818):
    b = struct.pack(">I", val)
    b2 = struct.pack("<I", val)
    print(val, "BE", data.find(b), "LE", data.find(b2))

# Dump trailing portion after longinfo
# Find end of longinfo - look for tip NPC 78E0FC65
tip = struct.pack(">I", 0x78E0FC65)
print("tip BE", data.find(tip), "LE", data.find(struct.pack("<I", 0x78E0FC65)))
# CanbeAffected type 0xC350
print("C350 count", data.count(bytes.fromhex("0000C350")))

# Print from tip onwards as hex+ints
pos = data.find(struct.pack(">I", 0x78E0FC65))
if pos < 0:
    pos = data.find(struct.pack("<I", 0x78E0FC65))
print("tip pos", pos)
if pos >= 0:
    chunk = data[pos-8:]
    print(chunk.hex())
    # walk as big-endian ints
    for i in range(0, len(chunk)-3, 4):
        v = struct.unpack_from(">I", chunk, i)[0]
        if v < 0x1000000 or v in (0x78E0FC65, 0xC350, 223373, 1160, 2581):
            print(f"  +{i:3d} {v} (0x{v:X})")
