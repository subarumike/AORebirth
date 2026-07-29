# Analyze 20260724-181214: mission enter fog + machine/chest dynels
from __future__ import print_function
import csv, os, collections, re, binascii, struct

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-181214"
OUT = r"tools-temp\_tmp_cap_181214_brief.txt"

def w(f, *a):
    line = " ".join(str(x) for x in a)
    f.write(line + "\n")
    print(line)

counts = collections.Counter()
mission_pf = None
paf_hex = []
chest_hex = []
door_hex = []
teleports = []
dynel_names = []
n3_by_pf = collections.defaultdict(collections.Counter)

# events / mission flow
with open(OUT, "w") as out:
    w(out, "=== capture_info ===")
    try:
        w(out, open(os.path.join(CAP, "capture_info.json")).read()[:800])
    except Exception as e:
        w(out, e)

    w(out, "\n=== mission-flow.log (key) ===")
    mf = os.path.join(CAP, "mission-flow.log")
    if os.path.exists(mf):
        for line in open(mf, encoding="utf-8", errors="replace"):
            if any(k in line for k in ("PLAYFIELD", "TELEPORT", "QUEST", "DOOR", "CHEST", "DYNEL", "Machine", "Repair", "ACG", "likelyMission")):
                w(out, line.rstrip()[:240])

    w(out, "\n=== events.log mission/machine ===")
    el = os.path.join(CAP, "events.log")
    if os.path.exists(el):
        for line in open(el, encoding="utf-8", errors="replace"):
            low = line.lower()
            if any(k in low for k in ("machine", "broken", "chest", "container", "playfield-init", "n3-teleport", "dynel", "acg", "generator")):
                w(out, line.rstrip()[:260])

    w(out, "\n=== raw-packets IN summary ===")
    with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            if (r.get("Direction") or "").upper() != "IN":
                continue
            n3 = (r.get("N3TypeName") or "").strip()
            counts[n3] += 1
            hx = (r.get("RawHex") or "").strip().upper().replace(" ", "")
            if n3 == "PlayfieldAnarchyF":
                paf_hex.append(hx)
            elif n3 == "ChestFullUpdate":
                chest_hex.append(hx)
            elif n3 == "DoorFullUpdate":
                door_hex.append(hx)
            elif n3 == "N3Teleport" or n3 == "Teleport":
                teleports.append((r.get("CapturedUtc"), hx[:120], (r.get("Summary") or "")[:160]))

    for k, v in counts.most_common(40):
        w(out, "%5d %s" % (v, k))

    w(out, "\n=== Teleports ===")
    # from events
    for line in open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace"):
        if "N3-TELEPORT" in line or "PLAYFIELD-INIT" in line:
            w(out, line.rstrip()[:280])

    w(out, "\n=== PAF count %d ===" % len(paf_hex))
    for i, hx in enumerate(paf_hex[:4]):
        raw = binascii.unhexlify(hx)
        # find C79F
        pos = []
        for j in range(len(raw)-3):
            if raw[j:j+4] == b"\x00\x00\xc7\x9f":
                pos.append(j)
        w(out, "PAF[%d] len=%d C79F@%s" % (i, len(raw), pos))
        if pos:
            p = pos[-1]
            pl = raw[p:]
            bi = (pl[4]<<24)|(pl[5]<<16)|(pl[6]<<8)|pl[7]
            w(out, "  payloadLen=%d building=0x%X head=%s" % (len(pl), bi, binascii.hexlify(pl[:40]).decode()))
            w(out, "  tail=%s" % binascii.hexlify(pl[-16:]).decode())
        # Identity: look for Playfield2 9C50
        for j in range(len(raw)-7):
            if raw[j:j+4] == b"\x00\x00\x9c\x50":
                pf = struct.unpack_from(">I", raw, j+4)[0]
                w(out, "  Playfield2@%d pf=%d (0x%X)" % (j, pf, pf))

    w(out, "\n=== ChestFullUpdate count %d ===" % len(chest_hex))
    # extract template ids near ACG patterns 027B / common
    templates = collections.Counter()
    for hx in chest_hex:
        raw = binascii.unhexlify(hx)
        # scan for IdentityType Container / Chest patterns and template ints
        # ACGItemTemplateID often appears as int in packet; look for known 0x027B47 etc
        for j in range(0, len(raw)-3, 1):
            # big-endian template candidates in range
            v = struct.unpack_from(">I", raw, j)[0]
            if 0x020000 <= v <= 0x030000:
                templates[v] += 1
            if 100000 <= v <= 400000 and v not in (0,):
                # also common decimal templates
                pass
    w(out, "template-like 0x02xxxx hits:")
    for t, c in templates.most_common(20):
        w(out, "  0x%X (%d) x%d" % (t, t, c))

    # Save unique chest packets (by static instance tail)
    uniq = []
    seen = set()
    for hx in chest_hex:
        key = hx[-96:]
        if key in seen:
            continue
        seen.add(key)
        uniq.append(hx)
    w(out, "unique chests ~%d" % len(uniq))
    os.makedirs(r"tools-temp\_tmp_cap_181214_assets", exist_ok=True)
    open(r"tools-temp\_tmp_cap_181214_assets\chests.hex", "w").write("\n".join(uniq))
    open(r"tools-temp\_tmp_cap_181214_assets\paf.hex", "w").write("\n".join(paf_hex))
    open(r"tools-temp\_tmp_cap_181214_assets\doors.hex", "w").write("\n".join(door_hex[:80]))

    w(out, "\n=== scfu / dynel names mentioning machine/rift/crate ===")
    scfu = os.path.join(CAP, "scfu-appearance.csv")
    if os.path.exists(scfu):
        with open(scfu, newline="", encoding="utf-8-sig") as f:
            for r in csv.DictReader(f):
                name = (r.get("Name") or "")
                if any(k in name.lower() for k in ("machine", "rift", "crate", "barrel", "treasure", "broken", "shadow")):
                    w(out, name, "pf=", r.get("PlayfieldId"), "md=", r.get("MonsterData"), "type=", r.get("CharacterInfoType"),
                      "pos=", r.get("PositionX"), r.get("PositionY"), r.get("PositionZ"))

    w(out, "\n=== npc-lifecycle Container/Chest ===")
    nl = os.path.join(CAP, "npc-lifecycle.csv")
    if os.path.exists(nl):
        with open(nl, newline="", encoding="utf-8-sig") as f:
            for i, r in enumerate(csv.DictReader(f)):
                line = ",".join((r.get(k) or "") for k in r)
                if any(k in line.lower() for k in ("chest", "container", "machine", "door", "vending")):
                    w(out, line[:300])
                    if i > 200:
                        break

    w(out, "\n=== DYNEL-SPAWNED from events ===")
    for line in open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace"):
        if "DYNEL" in line or "ChestFull" in line or "Broken" in line or "Machine" in line:
            w(out, line.rstrip()[:300])

print("wrote", OUT)
