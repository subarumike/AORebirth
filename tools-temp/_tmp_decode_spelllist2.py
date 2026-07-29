b = bytes.fromhex(
    "0000cf2643bd71c70000000400000000000000010000000000000000000000000000a871"
    "00000000000000000000000000000000000000000000000000000000000000000000"
    "c3507987c60d000000000000000000"
)
print("len", len(b))
off = 0
et = int.from_bytes(b[off:off+4], "big"); off += 4
ei = int.from_bytes(b[off:off+4], "big"); off += 4
print(f"Effect {et:X}:{ei:X}")
names = [
    "U1", "Crit", "Hits", "Delay", "U2", "U3",
    "GfxV", "GfxL", "GfxS", "GfxR", "GfxG", "GfxB", "GfxF",
]
for n in names:
    v = int.from_bytes(b[off:off+4], "big"); off += 4
    print(f"{n}={v:08X}")
print("after NanoEffect off", off, "rest", b[off:].hex())
# Character?
if len(b) - off >= 8:
    t = int.from_bytes(b[off:off+4], "big"); i = int.from_bytes(b[off+4:off+8], "big")
    print(f"next id {t:X}:{i:X}")
    off += 8
    print("rest2", b[off:].hex())

a = bytes.fromhex(
    "0000cf4a00049d1d00000004000000010000008000000090000000010000000100000000"
    "0000000200000009000495cf0000c350797e30d70000c350797e30d7000013416d6269656e"
    "7420526573746f726174696f6e000000000000"
)
print("\nAMBIENT len", len(a))
off = 0
et = int.from_bytes(a[off:off+4], "big"); off += 4
ei = int.from_bytes(a[off:off+4], "big"); off += 4
print(f"Effect {et:X}:{ei:X}")
for n in names:
    v = int.from_bytes(a[off:off+4], "big"); off += 4
    print(f"{n}={v:08X}")
print("after NanoEffect off", off, "rest", a[off:].hex())
t = int.from_bytes(a[off:off+4], "big"); i = int.from_bytes(a[off+4:off+8], "big")
print(f"Char? {t:X}:{i:X} — WAIT GfxF already took C350, so Char.Inst is first of rest")
# Re-parse ambient: after GfxB, Character then string (no GfxF in wire?)
off = 8 + 12 * 4  # Effect + 12 ints (no GfxF)
print("alt off without GfxF", off, a[off:].hex())
t = int.from_bytes(a[off:off+4], "big"); i = int.from_bytes(a[off+4:off+8], "big"); off += 8
print(f"Character {t:X}:{i:X}")
slen = a[off]; off += 1
print("name", slen, a[off:off+slen])
off += slen
print("pad", a[off:].hex())
