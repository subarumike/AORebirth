# -*- coding: utf-8 -*-
"""Extract Repair Machine instance dynels + ACG from capture 20260727-mission-repair-machine-new."""
from __future__ import print_function
import re
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-mission-repair-machine-new")
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines()

# Window: instance enter 19:54:00 to exit 19:59:17
doors = []
chests = []
terminals = []
paf = []
teleports = []
scfu_names = []

for line in hexlog:
    if "19:54:" not in line and "19:55:" not in line and "19:56:" not in line and "19:57:" not in line and "19:58:" not in line and "19:59:" not in line:
        continue
    m = re.search(r"n3=(\w+).*hex=([0-9A-Fa-f]+)", line)
    if not m:
        continue
    n3, hx = m.group(1), m.group(2)
    if n3 == "DoorFullUpdate":
        doors.append(hx)
    elif n3 == "ChestFullUpdate":
        chests.append(hx)
    elif n3 == "SimpleItemFullUpdate":
        terminals.append((line[:40], hx))
    elif n3 == "PlayfieldAnarchyF":
        paf.append(hx)
    elif n3 == "N3Teleport" or n3 == "Teleport":
        teleports.append((line[:80], hx[:120]))

print("DOORS unique count", len(set(doors)), "total", len(doors))
# first-seen unique by identity bytes around C748
seen = set()
unique_doors = []
for hx in doors:
    raw = bytes.fromhex(hx)
    i = raw.find(bytes.fromhex("C748"))
    key = raw[i:i+8].hex() if i >= 0 else hx[-32:]
    if key in seen:
        continue
    seen.add(key)
    unique_doors.append(hx)
print("unique doors", len(unique_doors))
for hx in unique_doors:
    raw = bytes.fromhex(hx)
    # find float xyz - after identity often
    print(" DOOR", hx[:80], "... len", len(hx)//2)

print("\nCHESTS", len(chests), "unique", len(set(chests)))
seen = set()
unique_chests = []
for hx in chests:
    raw = bytes.fromhex(hx)
    i = raw.find(bytes.fromhex("C749"))
    key = raw[i:i+8].hex() if i >= 0 else hx[-32:]
    if key in seen:
        continue
    seen.add(key)
    unique_chests.append(hx)
print("unique chests", len(unique_chests))

print("\nSIFU/terminals", len(terminals))
for ts, hx in terminals[:20]:
    raw = bytes.fromhex(hx)
    # StaticInstance often after 00000017
    if b"\x00\x01\x87\x99" in raw or "018799" in hx.upper():  # 100249?
        pass
    # find template ids 100345 = 0x18819
    if "00018819" in hx.upper() or "000187A4" in hx.upper() or "000187A4" in hx:
        print(" KIT/DISP?", ts, hx[:100])
    if "00018819" in hx.upper():
        print(" DISPENSER", hx)

# search 100345 = 0x18819, 100292 = 0x187A4
print("\n--- search templates ---")
for name, tid in [("dispenser", 100345), ("kit", 100292), ("broken", 0x027B47)]:
    needle = "%08X" % tid
    hits = [hx for _, hx in terminals if needle in hx.upper()]
    # also in all hexlog
    allhits = []
    for line in hexlog:
        if needle in line.upper() and "hex=" in line:
            allhits.append(line.split("hex=")[-1].strip()[:20] + "...")
    print(name, tid, "sifu", len(hits), "any", len(allhits))

# PAF
print("\nPAF count", len(paf))
for hx in paf[:3]:
    print("PAF len", len(hx)//2, hx[:160])
    raw = bytes.fromhex(hx)
    # look for D7425E
    if b"\x00\xd7\x42\x5e" in raw or "D7425E" in hx.upper():
        print("  has D7425E")
    idx = hx.upper().find("D7425E")
    print("  D7425E at", idx)

# Teleport enter
print("\n--- enter teleport ---")
for line in hexlog:
    if "19:53:57" in line and "Teleport" in line:
        print(line[:220])
        if "hex=" in line:
            hx = line.split("hex=")[-1].strip()
            print("hex len", len(hx)//2)
            print(hx)

# Write unique door/chest hex out for paste
out = Path(r"tools-temp/_tmp_repair_machine_extract.txt")
with out.open("w", encoding="utf-8") as f:
    f.write("UNIQUE_DOORS=%d\n" % len(unique_doors))
    for i, hx in enumerate(unique_doors):
        f.write("DOOR_%d=%s\n" % (i, hx))
    f.write("UNIQUE_CHESTS=%d\n" % len(unique_chests))
    for i, hx in enumerate(unique_chests):
        f.write("CHEST_%d=%s\n" % (i, hx))
    # dispenser SIFU in instance
    for line in hexlog:
        if "00018819" in line.upper() and "hex=" in line and ("19:54:" in line or "19:55:" in line or "19:56:" in line or "19:57:" in line or "19:58:" in line):
            f.write("DISPENSERline=%s\n" % line[:120])
            f.write("DISPENSER=%s\n" % line.split("hex=")[-1].strip())
            break
    for hx in paf[:1]:
        f.write("PAF=%s\n" % hx)
print("wrote", out)
