import csv
import struct
from collections import OrderedDict
from datetime import datetime

path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-093557\raw-packets.csv"
DOOR = bytes.fromhex("365A5071")
CHEST = bytes.fromhex("465A5D73")
PF = 1441792
pf_be = PF.to_bytes(4, "big")

doors = []
chests = []
with open(path, newline="", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        if not str(row.get("Direction", "")).upper().startswith("IN"):
            continue
        hx = (row.get("RawHex") or "").strip()
        if not hx:
            continue
        try:
            data = bytes.fromhex(hx)
        except ValueError:
            continue
        if pf_be not in data:
            continue
        for marker, bucket in ((DOOR, doors), (CHEST, chests)):
            i = data.find(marker)
            if i < 0 or len(data) < i + 40:
                continue
            inst = int.from_bytes(data[i + 8 : i + 12], "big")
            utc = row.get("CapturedUtc") or ""
            x, y, z = struct.unpack_from("<fff", data, i + 25)
            bucket.append((inst, hx, utc, x, y, z))


def uniq(items):
    ordered = OrderedDict()
    for inst, hx, utc, x, y, z in items:
        if inst not in ordered:
            ordered[inst] = (hx, utc, x, y, z)
    return ordered


ud = uniq(doors)
uc = uniq(chests)
print("unique doors", len(ud), "chests", len(uc))
for inst, (hx, utc, x, y, z) in list(ud.items())[:20]:
    print("door", inst, "xyz", round(x, 3), round(y, 3), round(z, 3), utc)

first_utc = min((v[1] for v in ud.values() if v[1]), default="")
print("first door utc", first_utc)
if first_utc:
    t0 = datetime.fromisoformat(first_utc.replace("Z", "+00:00"))
    early = [
        (inst, v[2], v[3], v[4])
        for inst, v in ud.items()
        if v[1]
        and (datetime.fromisoformat(v[1].replace("Z", "+00:00")) - t0).total_seconds()
        <= 1.0
    ]
    print("doors in first 1s", len(early))
    for e in early:
        print(" early", e)

out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_doors_1441792.csfrag"
with open(out, "w", encoding="utf-8") as w:
    w.write("// Shape 1441792 capture 20260728-093557 unique doors=%d\n" % len(ud))
    w.write("public static readonly string[] Doors_1441792 =\n{\n")
    for inst, (hx, utc, x, y, z) in ud.items():
        w.write('    "%s",\n' % hx)
    w.write("};\n")
print("wrote", out)

outc = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_chests_1441792.csfrag"
with open(outc, "w", encoding="utf-8") as w:
    w.write("// Shape 1441792 capture 20260728-093557 unique chests=%d\n" % len(uc))
    w.write("public static readonly string[] Chests_1441792 =\n{\n")
    for inst, (hx, utc, x, y, z) in uc.items():
        w.write('    "%s",\n' % hx)
    w.write("};\n")
print("wrote", outc, "count", len(uc))
