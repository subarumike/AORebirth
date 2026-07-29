from pathlib import Path
import csv
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def decode_tm(hx):
    b = bytes.fromhex(hx.strip())
    i = b.find(bytes.fromhex("46312D2E"))
    body = b[i+4:]
    # Identity viewer + unk + Member + Team + u4 + level + u5 + namelen + name
    viewer = (int.from_bytes(body[0:4],"big"), int.from_bytes(body[4:8],"big"))
    unk = body[8]
    rest = body[9:]
    mem = (int.from_bytes(rest[0:4],"big"), int.from_bytes(rest[4:8],"big"))
    team = (int.from_bytes(rest[8:12],"big"), int.from_bytes(rest[12:16],"big"))
    u4 = int.from_bytes(rest[16:20],"big", signed=True)
    level = int.from_bytes(rest[20:24],"big")
    u5 = int.from_bytes(rest[24:26],"big")
    nlen = int.from_bytes(rest[26:30],"big")
    name = rest[30:30+nlen].decode("ascii", errors="replace")
    return dict(viewer=viewer, unk=unk, mem=mem, team=team, u4=u4, level=level, u5=u5, name=name)

for name, idxs in (("20260729-003944", (46,48,102,105)), ("20260729-003950", (170,173,548,550))):
    cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")/name
    rows = list(csv.DictReader((cap/"raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print("\n===", name, "===")
    for idx in idxs:
        r = rows[idx]
        d = decode_tm(r["RawHex"])
        print(f"{idx} {r['Direction']} mem={d['mem'][0]:X}:{d['mem'][1]:X} lvl={d['level']} u5={d['u5']} name={d['name']!r} team={d['team'][1]:X}")

# Compare gold 234012 TeamMember
print("\n=== gold 234012 ===")
cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-234012")
rows = list(csv.DictReader((cap/"raw-packets.csv").open(encoding="utf-8-sig", newline="")))
for idx in (124,126):
    d = decode_tm(rows[idx]["RawHex"])
    print(f"{idx} mem={d['mem'][0]:X}:{d['mem'][1]:X} lvl={d['level']} u5={d['u5']} name={d['name']!r} team={d['team'][1]:X}")

# session info
for name in ("20260729-003944","20260729-003950"):
    p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")/name/"capture_info.json"
    print("\n", name, p.read_text(encoding="utf-8")[:500])
