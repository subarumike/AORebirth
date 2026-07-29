# Map-at-start gold: 20260725-080425
from __future__ import print_function
import csv, os, json, collections, binascii, struct

CAP = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-080425"
OUT = r"tools-temp/_tmp_cap_080425_map.txt"

def w(f, *a):
    line = " ".join(str(x) for x in a)
    f.write(line + "\n")
    print(line)

def parse_door_xyz(hx):
    b = binascii.unhexlify(hx.replace(" ", ""))
    for i in range(0, len(b) - 28):
        if b[i] != 0 or b[i + 1] != 0 or b[i + 2] != 0xC7:
            continue
        if b[i + 3] not in (0x48, 0x49, 0x3D):
            continue
        o = i + 8
        if b[o : o + 4] != b"\x00\x00\x00\x00":
            continue
        o += 5
        x = struct.unpack_from(">f", b, o + 8)[0]
        y = struct.unpack_from(">f", b, o + 12)[0]
        z = struct.unpack_from(">f", b, o + 16)[0]
        if 1 < y < 30 and -1 < x < 500 and 0 < z < 500:
            return x, y, z
    return None

with open(OUT, "w", encoding="utf-8") as out:
    w(out, "=== capture_info ===")
    info = json.load(open(os.path.join(CAP, "capture_info.json"), encoding="utf-8-sig"))
    w(out, "pf", info.get("playfieldId"), "char", info.get("characterName"))
    w(out, "counts", info.get("packetCounts", {}))

    # N3 timeline: PAF then Door/Chest/SCFU first seconds
    path = os.path.join(CAP, "raw-packets.csv")
    utc_name = None
    rows = []
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        r = csv.DictReader(f)
        utc_name = r.fieldnames[0]
        for row in r:
            nt = row.get("N3TypeName") or ""
            if nt in (
                "PlayfieldAnarchyF",
                "DoorFullUpdate",
                "ChestFullUpdate",
                "SimpleItemFullUpdate",
                "SimpleCharFullUpdate",
                "Teleport",
                "Warp",
            ):
                rows.append(row)

    paf = None
    doors = []
    chests = []
    scfu = []
    sifus = []
    for row in rows:
        nt = row.get("N3TypeName") or ""
        direction = row.get("Direction") or ""
        if not direction.startswith("IN"):
            continue
        if nt == "PlayfieldAnarchyF" and paf is None:
            paf = row
        if paf is None:
            continue
        t0 = paf[utc_name]
        t = row[utc_name]
        # rough: same second or within first 3s string compare works for ISO
        if nt == "DoorFullUpdate":
            doors.append(row)
        elif nt == "ChestFullUpdate":
            chests.append(row)
        elif nt == "SimpleCharFullUpdate":
            scfu.append(row)
        elif nt == "SimpleItemFullUpdate":
            sifus.append(row)

    w(out, "=== PAF ===")
    if paf:
        hx = (paf.get("RawHex") or "").replace(" ", "")
        w(out, "utc", paf[utc_name], "len", len(hx) // 2)
        w(out, "head", hx[:160])
        # PlayfieldId2
        b = binascii.unhexlify(hx)
        idx = hx.find("00009C50")
        w(out, "9C50 hex offs", [i for i in range(0, len(hx) - 8, 2) if hx[i : i + 8] == "00009C50"])
        # find last 00009C50 + next 4 bytes = pf
        last = hx.rfind("00009C50")
        if last >= 0:
            pfhex = hx[last + 8 : last + 16]
            w(out, "PlayfieldId2", int(pfhex, 16), hex(int(pfhex, 16)))
        # generator after last 9C50+pf
        gen_at = last // 2 + 8
        gen = b[gen_at:]
        w(out, "genLen", len(gen), "genHead", binascii.hexlify(gen[:32]).decode())
        if len(gen) >= 8:
            bi = (gen[4] << 24) | (gen[5] << 16) | (gen[6] << 8) | gen[7]
            w(out, "buildingInst", hex(bi))

    w(out, "=== DoorFullUpdate count", len(doors))
    # first batch: doors within 2s of PAF (string prefix)
    if paf and doors:
        t0 = paf[utc_name]
        first = []
        later = []
        for d in doors:
            # compare timestamps loosely
            if d[utc_name][:22] <= t0[:19] + "99" and d[utc_name][:19] == t0[:19]:
                first.append(d)
            elif d[utc_name][:19] == t0[:19] or (
                d[utc_name] > t0 and d[utc_name][:18] == t0[:18]
            ):
                # same minute bucket first wave if within ~1s
                first.append(d)
            else:
                later.append(d)
        # Better: parse first 15 doors by order
        w(out, "first 20 door utcs:")
        for d in doors[:20]:
            hx = (d.get("RawHex") or "").replace(" ", "")
            pos = parse_door_xyz(hx) if len(hx) > 80 else None
            w(out, " ", d[utc_name][11:23], "len", len(hx) // 2, "xyz", pos)

        # spawn from PAF floats
        hx = (paf.get("RawHex") or "").replace(" ", "")
        b = binascii.unhexlify(hx)
        # find coords  near 4395...
        spawn = None
        for i in range(0, min(80, len(b) - 12)):
            x = struct.unpack_from(">f", b, i)[0]
            y = struct.unpack_from(">f", b, i + 4)[0]
            z = struct.unpack_from(">f", b, i + 8)[0]
            if 200 < x < 400 and 1 < y < 20 and 150 < z < 350:
                spawn = (x, y, z, i)
                break
        w(out, "spawnGuess", spawn)
        if spawn:
            sx, sy, sz, _ = spawn
            w(out, "=== door distances from spawn (all doors in order) ===")
            for i, d in enumerate(doors):
                hx = (d.get("RawHex") or "").replace(" ", "")
                pos = parse_door_xyz(hx) if len(hx) > 80 else None
                if not pos:
                    w(out, i, "NO_POS", d[utc_name][11:23])
                    continue
                dist = ((pos[0] - sx) ** 2 + (pos[2] - sz) ** 2) ** 0.5
                w(out, "%3d %s dist=%5.1fm xyz=(%.1f,%.2f,%.1f)" % (i, d[utc_name][11:23], dist, pos[0], pos[1], pos[2]))

    w(out, "=== Chest count", len(chests), "first5 utcs", [c[utc_name][11:23] for c in chests[:5]])
    w(out, "=== SCFU count after PAF", len(scfu))
    w(out, "=== SIFU count after PAF", len(sifus))

    # mission-flow / system
    for name in ("mission-flow.log", "system-messages.log", "events.log"):
        p = os.path.join(CAP, name)
        if not os.path.exists(p):
            continue
        w(out, "===", name, "head ===")
        with open(p, encoding="utf-8", errors="replace") as f:
            for i, line in enumerate(f):
                if i > 40:
                    break
                if any(k in line.lower() for k in ("door", "playfield", "teleport", "mission", "map", "fog", "enter")):
                    w(out, line[:220].rstrip())

print("wrote", OUT)
