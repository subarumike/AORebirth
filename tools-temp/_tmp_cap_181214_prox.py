# Extract Radar Display SIFU + PAF + door proximity timeline from 20260724-181214
from __future__ import print_function
import csv, os, binascii, collections, re

CAP = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-181214"
OUT = r"tools-temp\_tmp_cap_181214_assets"

os.makedirs(OUT, exist_ok=True)

# From events: first spawn times of doors
first_door = {}
player_moves = []
radar_hex = []
paf_hex = []
chest_hex = []
door_hex_by_inst = {}

for line in open(os.path.join(CAP, "events.log"), encoding="utf-8", errors="replace"):
    m = re.search(r"(\d{4}-\d{2}-\d{2}T[\d:.]+Z).*\[DYNEL-SPAWNED\] identity=\(Door:([0-9A-F]+)\) name=Door pos=\(([^)]+)\)", line)
    if m:
        utc, inst, pos = m.group(1), m.group(2), m.group(3)
        if inst not in first_door:
            first_door[inst] = (utc, pos)
    m = re.search(r"(\d{4}-\d{2}-\d{2}T[\d:.]+Z).*\[DYNEL-SPAWNED\] identity=\(Terminal:([0-9A-F]+)\) name=([^=]+) pos=\(([^)]+)\)", line)
    if m and "Radar" in m.group(3):
        print("Radar first", m.group(1), m.group(2), m.group(3).strip(), m.group(4))

with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        if (r.get("Direction") or "").upper() != "IN":
            continue
        n3 = (r.get("N3TypeName") or "").strip()
        hx = (r.get("RawHex") or "").strip().upper().replace(" ", "")
        if n3 == "PlayfieldAnarchyF":
            paf_hex.append(hx)
        elif n3 == "SimpleItemFullUpdate":
            # Radar StaticInstance 100358 = 0x18806
            if "00018806" in hx or "5796D655" in hx:
                radar_hex.append(hx)
        elif n3 == "ChestFullUpdate":
            chest_hex.append(hx)
        elif n3 == "DoorFullUpdate":
            # extract door identity instance near start
            door_hex_by_inst.setdefault(hx[40:56] if len(hx) > 56 else hx[:32], hx)

print("PAF", len(paf_hex), "RadarSIFU", len(radar_hex), "Chest", len(chest_hex), "uniqueDoorKeys", len(door_hex_by_inst))
print("first doors by time:")
for inst, (utc, pos) in sorted(first_door.items(), key=lambda x: x[1][0]):
    print(" ", utc, inst, pos)

# Write files
open(os.path.join(OUT, "paf.hex"), "w").write("\n".join(paf_hex))
open(os.path.join(OUT, "radar_sifu.hex"), "w").write("\n".join(radar_hex))
# unique chests
seen=set(); uniq=[]
for hx in chest_hex:
    k=hx[-80:]
    if k in seen: continue
    seen.add(k); uniq.append(hx)
open(os.path.join(OUT, "chests.hex"), "w").write("\n".join(uniq))
print("unique chests", len(uniq))

if radar_hex:
    hx = radar_hex[0]
    print("radar len", len(hx)//2)
    print("radar head", hx[:160])
    # find ACG template after 18806
    idx = hx.find("00018806")
    print("static@", idx, "ctx", hx[idx:idx+80] if idx>=0 else None)

if paf_hex:
    hx=paf_hex[0]
    raw=binascii.unhexlify(hx)
    print("PAF0 len", len(raw))
    for j in range(len(raw)-3):
        if raw[j:j+4]==b"\x00\x00\xc7\x9f":
            print("C79F@", j, binascii.hexlify(raw[j:j+16]).decode())
