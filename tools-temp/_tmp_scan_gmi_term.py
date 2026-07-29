# Extract OnUse / template for Terminal C0070320 from playfield data
import struct, pathlib, sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TARGET = 0xC0070320
NEEDLES = [b"uwg.trade", b"aomarket", b"omni-rk", b"http://", b"https://", b"vgtp"]

roots = [
    pathlib.Path(r"AORebirth\Built\Debug"),
    pathlib.Path(r"AORebirth\Server\ZoneEngine"),
    pathlib.Path(r"AORebirth"),
]

# Find likely playfield/statel/item blobs
cands = []
for root in roots:
    if not root.exists():
        continue
    for p in root.rglob("*"):
        if not p.is_file():
            continue
        if p.suffix.lower() not in {".dat", ".bin", ".json", ".xml", ".csv", ".txt"}:
            continue
        name = p.name.lower()
        if any(k in name for k in ("playfield", "statel", "items", "itemlist", "pfdata", "4680")):
            if p.stat().st_size < 200_000_000:
                cands.append(p)

print("candidates", len(cands))
for p in cands[:40]:
    print(" ", p, p.stat().st_size)

# Search binary for instance dword (both endians) and nearby ascii urls
pat_le = struct.pack("<I", TARGET)
pat_be = struct.pack(">I", TARGET)
hits = []
for p in cands:
    try:
        data = p.read_bytes()
    except Exception as e:
        continue
    for pat, endian in ((pat_le, "le"), (pat_be, "be")):
        start = 0
        while True:
            i = data.find(pat, start)
            if i < 0:
                break
            window = data[max(0, i - 64) : i + 256]
            ascii_bits = "".join(chr(b) if 32 <= b < 127 else "." for b in window)
            urlish = [n.decode() for n in NEEDLES if n in window.lower() or n in data[max(0,i-200):i+400].lower()]
            hits.append((str(p), i, endian, ascii_bits[:120], urlish))
            start = i + 4
            if len(hits) > 30:
                break
        if len(hits) > 30:
            break
    if len(hits) > 30:
        break

print("instance hits", len(hits))
for h in hits[:20]:
    print(h)

# Global url search in Built Debug medium files
print("\nURL scans:")
for p in pathlib.Path(r"AORebirth\Built\Debug").rglob("*"):
    if not p.is_file() or p.stat().st_size > 80_000_000:
        continue
    if p.suffix.lower() not in {".dat", ".bin", ".txt", ".xml", ".json"}:
        continue
    try:
        data = p.read_bytes()
    except Exception:
        continue
    for n in NEEDLES:
        if n in data.lower():
            idx = data.lower().find(n)
            print(p, n, "at", idx, data[max(0,idx-20):idx+80])
