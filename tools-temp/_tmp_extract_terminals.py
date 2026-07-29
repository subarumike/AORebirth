# Extract Terminal SimpleItemFullUpdate hex per shape PF from 20260719 capture
from __future__ import print_function
import csv, os, collections, binascii, struct

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-5-different-shape-fo-mish"
OUT = r"tools-temp\_tmp_mission_shapes_assets"
PF_HEX = {0x15A82E: 1419310, 0x15A876: 1419382, 0x15A847: 1419335}
windows = [
    (1419310, "2026-07-19T03:33:19", "2026-07-19T03:37:12"),
    (1419382, "2026-07-19T03:37:26", "2026-07-19T03:40:38"),
    (1419335, "2026-07-19T03:40:38", "2026-07-19T03:46:46"),
]

def pf_for_utc(utc):
    for pf, s, e in windows:
        if s <= utc <= e:
            return pf
    return None

terms = collections.defaultdict(list)
with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("Direction") or "").upper() != "IN":
            continue
        if (r.get("N3TypeName") or "").strip() != "SimpleItemFullUpdate":
            continue
        hx = (r.get("RawHex") or "").strip().upper().replace(" ", "")
        if "0000C73D" not in hx:
            continue
        utc = r.get("CapturedUtc") or ""
        pf = None
        for needle, pfi in PF_HEX.items():
            if ("%08X" % needle) in hx:
                pf = pfi
                break
        if pf is None:
            pf = pf_for_utc(utc)
        if pf is None:
            continue
        # unique by identity instance bytes after C73D
        idx = hx.find("0000C73D")
        key = hx[idx:idx+16] if idx >= 0 else hx[-40:]
        if any(x.find(key) >= 0 for x in terms[pf]):
            continue
        terms[pf].append(hx)

# Also add Radar from 181214 as reference (pf 1413191) - optional
radar_path = r"tools-temp\_tmp_cap_181214_assets\radar_sifu.hex"
radar = open(radar_path).read().strip().splitlines()[0] if os.path.exists(radar_path) else None

def xyz(hx):
    raw = binascii.unhexlify(hx)
    # find C73D then skip 8+4+1+8
    for i in range(len(raw)-20):
        if raw[i]==0 and raw[i+1]==0 and raw[i+2]==0xC7 and raw[i+3]==0x3D:
            o = i + 8 + 4 + 1 + 8
            if o+12 <= len(raw):
                x,y,z = struct.unpack_from(">fff", raw, o)
                return x,y,z
    return None

csfrag = []
for pf in sorted(terms.keys()):
    open(os.path.join(OUT, "terms_%d.hex" % pf), "w").write("\n".join(terms[pf]))
    print("PF", pf, "terminals", len(terms[pf]))
    for hx in terms[pf]:
        pos = xyz(hx)
        # static before 02BD
        idx = hx.find("000002BD")
        static = hx[idx-8:idx] if idx>=8 else "?"
        print(" ", pos, "static", static, "len", len(hx)//2)
    csfrag.append("        public static readonly string[] Terminals_%d =" % pf)
    csfrag.append("        {")
    for hx in terms[pf]:
        csfrag.append('            "%s",' % hx)
    csfrag.append("        };")
    csfrag.append("")

open(os.path.join(OUT, "terminals.csfrag"), "w").write("\n".join(csfrag))
print("wrote terminals.csfrag")
if radar:
    print("radar xyz", xyz(radar))
