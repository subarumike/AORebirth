import csv
import struct

path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-mission-find-person\raw-packets.csv"
out_cs = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_doors_1419349.csfrag"


def parse_xyz(hexv):
    b = bytes.fromhex(hexv)
    for i in range(0, len(b) - 28):
        if b[i] == 0 and b[i + 1] == 0 and b[i + 2] == 0xC7 and b[i + 3] in (0x48, 0x49):
            o = i + 8
            if b[o : o + 4] != bytes(4):
                continue
            o2 = o + 5
            x = struct.unpack(">f", b[o2 + 8 : o2 + 12])[0]
            y = struct.unpack(">f", b[o2 + 12 : o2 + 16])[0]
            z = struct.unpack(">f", b[o2 + 16 : o2 + 20])[0]
            if 1 < y < 30 and -1 < x < 500 and 0 < z < 500:
                return x, y, z
    return None


doors = {}
chests = {}
with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        if row["Direction"] != "IN":
            continue
        name = row["N3TypeName"]
        h = (row.get("RawHex") or "").replace(" ", "").upper()
        if not h:
            continue
        inst = row.get("IdentityInstance") or ""
        if name == "DoorFullUpdate" and inst not in doors:
            doors[inst] = h
        elif name == "ChestFullUpdate" and inst not in chests:
            chests[inst] = h

print("doors", len(doors), "chests", len(chests))
near = []
for inst, h in doors.items():
    p = parse_xyz(h)
    if p:
        near.append((p[0], p[1], p[2], inst))
near.sort(key=lambda t: (t[0] - 298) ** 2 + (t[2] - 85) ** 2)
print("nearest to (298,85):")
for t in near[:15]:
    print("  (%.2f,%.2f,%.2f) %s" % (t[0], t[1], t[2], t[3]))
batch = [t for t in near if (t[0] - 298) ** 2 + (t[2] - 85) ** 2 <= 60 * 60]
print("doors within 60m of start", len(batch))


def sort_key(kv):
    try:
        return int(kv[0])
    except Exception:
        return 0


with open(out_cs, "w", encoding="utf-8") as w:
    w.write("public static readonly string[] Doors_1419349 =\n{\n")
    for inst, h in sorted(doors.items(), key=sort_key):
        w.write('            "%s",\n' % h)
    w.write("};\n\npublic static readonly string[] Chests_1419349 =\n{\n")
    for inst, h in sorted(chests.items(), key=sort_key):
        w.write('            "%s",\n' % h)
    w.write("};\n")
print("wrote", out_cs)
