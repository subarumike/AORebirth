# Map world Terminal packets to names; extract Radar from 181214 into csfrag
from __future__ import print_function
import csv, os, re, binascii, struct, collections

def parse_term(hx):
    raw = binascii.unhexlify(hx)
    for i in range(len(raw)-28):
        if raw[i:i+4] == b"\x00\x00\xc7\x3d":
            inst = struct.unpack_from(">I", raw, i+4)[0]
            o = i + 8
            if raw[o:o+4] != b"\x00\x00\x00\x00":
                return None
            o += 5  # pad + unk byte
            x,y,z = struct.unpack_from(">fff", raw, o+8)
            if not (1 < y < 20 and 0 < x < 500 and 0 < z < 500):
                return None
            # static before 02BD
            hxu = hx.upper()
            idx = hxu.find("000002BD")
            static = int(hxu[idx-8:idx], 16) if idx >= 8 else 0
            return dict(inst=inst, x=x, y=y, z=z, static=static, hx=hx)
    return None

# Build name map from events for both captures
names = {}
for cap in [
    r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-5-different-shape-fo-mish",
    r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-181214",
]:
    for line in open(os.path.join(cap, "events.log"), encoding="utf-8", errors="replace"):
        m = re.search(r"identity=\(Terminal:([0-9A-F]+)\) name=([^=]+) pos=", line)
        if m:
            names[int(m.group(1), 16)] = m.group(2).strip()

# Collect from shape capture by PF
PF_HEX = {0x15A82E: 1419310, 0x15A876: 1419382, 0x15A847: 1419335}
windows = [
    (1419310, "2026-07-19T03:33:19", "2026-07-19T03:37:12"),
    (1419382, "2026-07-19T03:37:26", "2026-07-19T03:40:38"),
    (1419335, "2026-07-19T03:40:38", "2026-07-19T03:46:46"),
]
def pf_for(utc):
    for pf,s,e in windows:
        if s<=utc<=e: return pf
    return None

by_pf = collections.defaultdict(list)
seen = collections.defaultdict(set)
CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-5-different-shape-fo-mish"
with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("Direction") or "").upper() != "IN": continue
        if (r.get("N3TypeName") or "").strip() != "SimpleItemFullUpdate": continue
        hx = (r.get("RawHex") or "").strip().upper().replace(" ", "")
        if "0000C73D" not in hx: continue
        info = parse_term(hx)
        if not info: continue
        utc = r.get("CapturedUtc") or ""
        pf = None
        for needle,pfi in PF_HEX.items():
            if ("%08X"%needle) in hx:
                pf=pfi; break
        if pf is None: pf = pf_for(utc)
        if pf is None: continue
        if info["inst"] in seen[pf]: continue
        seen[pf].add(info["inst"])
        info["name"] = names.get(info["inst"], "?")
        by_pf[pf].append(info)

print("=== shape world terminals ===")
for pf in sorted(by_pf):
    print("PF", pf)
    for info in by_pf[pf]:
        print(" ", info["name"], "inst=%X" % info["inst"], "static=%d" % info["static"],
              "pos=(%.2f,%.2f,%.2f)" % (info["x"], info["y"], info["z"]))

# 181214 radar
CAP2 = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-181214"
radar = []
seenr=set()
with open(os.path.join(CAP2, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("Direction") or "").upper() != "IN": continue
        if (r.get("N3TypeName") or "").strip() != "SimpleItemFullUpdate": continue
        hx = (r.get("RawHex") or "").strip().upper().replace(" ", "")
        info = parse_term(hx)
        if not info: continue
        if info["inst"] in seenr: continue
        seenr.add(info["inst"])
        info["name"] = names.get(info["inst"], "?")
        radar.append(info)
print("=== 181214 world terminals ===")
for info in radar:
    print(" ", info["name"], "inst=%X" % info["inst"], "static=%d" % info["static"],
          "pos=(%.2f,%.2f,%.2f)" % (info["x"], info["y"], info["z"]))
    print("  hexlen", len(info["hx"])//2)

# Write csfrag for terminals that are Radar Display OR ICC Cell Structure Scanner
want = set()
lines = []
lines.append("        // World Terminal SimpleItemFullUpdate (machines / scanners). Capture-backed.")
for pf, items in sorted(by_pf.items()):
    keep = [i for i in items if "Radar" in i["name"] or "Cell Structure" in i["name"] or "Archive" in i["name"] or i["static"] in (0x187CA, 0x18806)]
    if not keep:
        keep = items  # any world terminal
    lines.append("        public static readonly string[] Terminals_%d =" % pf)
    lines.append("        {")
    for i in keep:
        lines.append('            "%s", // %s static=%d' % (i["hx"], i["name"], i["static"]))
    lines.append("        };")
    lines.append("")

if radar:
    lines.append("        // Capture 20260724-181214 Radar Display (hologram machine look).")
    lines.append("        public static readonly string[] Terminals_RadarDisplay =")
    lines.append("        {")
    for i in radar:
        lines.append('            "%s", // %s static=%d' % (i["hx"], i["name"], i["static"]))
    lines.append("        };")

open(r"tools-temp\_tmp_mission_shapes_assets\terminals_world.csfrag", "w").write("\n".join(lines))
print("wrote terminals_world.csfrag lines", len(lines))
