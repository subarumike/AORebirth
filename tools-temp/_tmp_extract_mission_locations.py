# Extract playfield+xyz from live QuestAlternative rolls in capture 20260718-053650
from __future__ import print_function
import csv, re, struct, collections

raw = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260718-053650/raw-packets.csv"
# columns unknown - peek header
with open(raw, newline="", encoding="utf-8", errors="replace") as f:
    reader = csv.reader(f)
    header = next(reader)
    print("header", header)
    # find hex column
    hex_idx = None
    for i, h in enumerate(header):
        if "hex" in h.lower() or "raw" in h.lower() or "payload" in h.lower():
            hex_idx = i
            print("hex col", i, h)
    type_idx = None
    for i, h in enumerate(header):
        if "type" in h.lower() or "n3" in h.lower() or "name" in h.lower():
            type_idx = i
            print("type col", i, h)

    locs = []
    rolls = 0
    for row in reader:
        line = ",".join(row)
        if "QuestAlternative" not in line and (type_idx is None or (len(row) > type_idx and "QuestAlternative" not in row[type_idx])):
            # also check any field
            if "QuestAlternative" not in line and "5c436609" not in line.lower():
                continue
        # prefer IN direction
        if "OUT" in line and "IN" not in line[:80]:
            # might still be useful
            pass
        hx = None
        if hex_idx is not None and hex_idx < len(row):
            hx = row[hex_idx]
        if not hx:
            m = re.search(r"([0-9A-Fa-f]{200,})", line)
            hx = m.group(1) if m else None
        if not hx or len(hx) < 200:
            continue
        # server replies are longer
        if "5C436609" not in hx.upper() and "5c436609" not in hx:
            # body may start after header
            pass
        rolls += 1
        # find Playfield2 00009C50 + instance + entrance + xyz
        data = bytes.fromhex(hx if len(hx) % 2 == 0 else hx[:-1])
        # search for 00 00 9C 50
        for i in range(0, len(data) - 28):
            if data[i] == 0 and data[i+1] == 0 and data[i+2] == 0x9C and data[i+3] == 0x50:
                pf = struct.unpack(">I", data[i+4:i+8])[0]
                u18 = struct.unpack(">i", data[i+8:i+12])[0]
                u19 = struct.unpack(">i", data[i+12:i+16])[0]
                x, y, z = struct.unpack(">fff", data[i+16:i+28])
                # filter garbage
                if 1 <= pf <= 5000 and -5000 < x < 5000 and -500 < y < 2000 and -5000 < z < 5000:
                    locs.append((pf, round(x, 2), round(y, 2), round(z, 2), u18, u19))

print("rows_with_questaltish", rolls, "locs", len(locs))
uniq = collections.OrderedDict()
for L in locs:
    key = (L[0], L[1], L[3])  # pf,x,z
    uniq[key] = L
print("unique", len(uniq))
for L in list(uniq.values())[:80]:
    print("pf=%s xyz=(%s,%s,%s) ent=%s/%s" % (L[0], L[1], L[2], L[3], L[4], L[5]))
