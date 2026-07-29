# Pull QuestFullUpdate Find Person packet from gold capture and compute patch offsets.
import re

path = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-224228\packets.hex.log"
out_hex = r"tools-temp\_tmp_findperson_accept.hex"
out_meta = r"tools-temp\_tmp_findperson_accept_meta.txt"

want = []
with open(path, "r", encoding="utf-8", errors="ignore") as f:
    for line in f:
        if "n3=QuestFullUpdate" not in line:
            continue
        m = re.search(r"hex=([0-9A-Fa-f]+)", line)
        if not m:
            continue
        h = m.group(1).upper()
        if "00002C47" not in h:
            continue
        want.append(h)

print("qfu findperson count", len(want))
if not want:
    raise SystemExit(1)

h = want[0]
open(out_hex, "w").write(h)
b = bytes.fromhex(h)

# find character instance after C350
idx = h.find("0000C350")
print("first C350 at nibble", idx)

# find name Jeanne Messamore
name = b"Jeanne Messamore"
pos = b.find(name)
print("name offset", pos, "len", len(name))

# find icon 00002C47
ipos = b.find(bytes.fromhex("00002C47"))
print("icon offset", ipos)

# find Playfield2 9C50 (type) for marker - Kill template used 9C50
ppos = b.find(bytes.fromhex("009C50"))
print("9C50 offsets", [i for i in range(len(b)-3) if b[i:i+3]==bytes.fromhex("009C50")][:5])
# actually 00 9C 50
marks = []
for i in range(len(b)-4):
    if b[i:i+2] == bytes.fromhex("9C50"):
        marks.append(i)
print("9C50 at", marks[:8])

# expiry: Kill used offset 671. Search for large ints near quest ids.
# dump ascii strings
runs = []
cur = []
start = 0
for i, x in enumerate(b):
    if 32 <= x < 127:
        if not cur:
            start = i
        cur.append(chr(x))
    else:
        if len(cur) >= 8:
            runs.append((start, "".join(cur)))
        cur = []
if len(cur) >= 8:
    runs.append((start, "".join(cur)))

with open(out_meta, "w", encoding="utf-8") as w:
    w.write("packet_len=%d\n" % len(b))
    w.write("name_offset=%d name=%s\n" % (pos, name.decode()))
    w.write("icon_offset=%d\n" % ipos)
    w.write("9C50=%s\n" % marks[:10])
    for off, s in runs[:20]:
        w.write("TXT@%d: %s\n" % (off, s[:160]))
    # compare with kill template key offsets
    w.write("\nKill template offsets (known):\n")
    w.write("ExpiryOffset=671 MissionIconIdOffset=563 CharInst=0x13945A\n")
    # find 762ABC21 char
    cpos = b.find(bytes.fromhex("762ABC21"))
    w.write("char 762ABC21 first=%d all=%s\n" % (cpos, [i for i in range(len(b)-4) if b[i:i+4]==bytes.fromhex("762ABC21")][:8]))

print("wrote", out_hex, out_meta)
