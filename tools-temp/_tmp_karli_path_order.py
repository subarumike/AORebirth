# Extract unique Karli NpcPath destinations in order
from __future__ import print_function
import re
import struct
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-055715")
KARLI = "799AD394"
pts = []
for ln in (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "FollowTarget" not in ln or KARLI.upper() not in ln.upper():
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    if len(raw) != 56:
        continue
    # After identity C350799AD394: 00011802 then curX curY curZ destX unk destZ?
    # hex after identity (12 bytes header before floats?): 
    # 00011802 421F747F 3EDEB851 423A2F40 42455D0D 350E0000 42464D5F
    body = raw[raw.find(bytes.fromhex("799AD394"))+4:]
    # 00011802 + 6*4 floats/ints
    if len(body) < 28:
        continue
    # skip 00011802 (4 bytes)
    off = 4
    curx, cury, curz, destx, unk, destz = struct.unpack_from(">ffffff", body, off)
    # unk might be move marker 350E0000 -> interpret as int
    unk_i = struct.unpack_from(">I", body, off+16)[0]
    pts.append((round(curx,3), round(cury,3), round(curz,3), round(destx,3), unk_i, round(destz,3)))

print("count", len(pts))
for i,p in enumerate(pts):
    print("%02d cur=(%.3f, %.3f, %.3f) destXZ=(%.3f, %.3f) unk=%08X" % (i, p[0],p[1],p[2],p[3],p[5],p[4]))

# unique dests in first loop
dests = []
seen=set()
for p in pts:
    d=(p[3], p[1], p[5])  # destx, y from cur, destz
    if d in seen:
        # detect loop restart
        if dests and d == dests[0]:
            print("LOOP_RESTART at", len(dests))
            break
        continue
    seen.add(d)
    dests.append(d)
print("unique_dests_in_order:")
for d in dests:
    print("  (%.3f, %.3f, %.3f)" % d)
