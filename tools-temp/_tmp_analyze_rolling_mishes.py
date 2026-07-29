# Analyze 20260719 Rolling different mishes capture for mission roll variety.
from __future__ import print_function
import csv
import os
import struct
import re
from collections import Counter

CAP = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260719-Rolling different mishes"

ICONS = {
    11329: "FindItemA",
    11330: "KillPerson",
    11335: "FindPerson",
    11337: "FindItemB",
    11342: "RepairMachine",
}

def u32be(b, off):
    return struct.unpack_from(">I", b, off)[0]

def i32be(b, off):
    return struct.unpack_from(">i", b, off)[0]

def f32be(b, off):
    return struct.unpack_from(">f", b, off)[0]

def find_icons(body):
    hits = []
    for icon, name in ICONS.items():
        needle = struct.pack(">I", icon)
        start = 0
        while True:
            j = body.find(needle, start)
            if j < 0:
                break
            hits.append((j, icon, name))
            start = j + 1
    hits.sort()
    return hits

def find_dac3(body):
    needle = struct.pack(">I", 0x0000DAC3)
    out = []
    start = 0
    while True:
        j = body.find(needle, start)
        if j < 0:
            break
        inst = u32be(body, j + 4)
        out.append((j, inst))
        start = j + 1
    return out

def find_playfields(body):
    # Identity type Playfield2 = 0x00009C50
    needle = struct.pack(">I", 0x00009C50)
    out = []
    start = 0
    while True:
        j = body.find(needle, start)
        if j < 0:
            break
        pf = u32be(body, j + 4)
        # try floats after identity + 8 bytes unknowns
        if j + 24 <= len(body):
            x = f32be(body, j + 16)
            y = f32be(body, j + 20)
            z = f32be(body, j + 24)
            out.append((j, pf, x, y, z))
        else:
            out.append((j, pf, None, None, None))
        start = j + 1
    return out

def extract_ascii_snippets(body, min_len=20):
    # Pull printable ASCII runs that look like mission text
    texts = []
    i = 0
    while i < len(body):
        if 32 <= body[i] < 127:
            j = i
            while j < len(body) and 32 <= body[j] < 127:
                j += 1
            if j - i >= min_len:
                s = body[i:j].decode("ascii", "ignore")
                if any(k in s for k in ("mission", "Mission", "Radar", "Broken", "stolen", "mutant", "Please", "Thank", "Help", "find", "kill", "Repair", "install")):
                    texts.append(s[:120])
            i = j + 1
        else:
            i += 1
    return texts

# Collect QuestAlternative IN packets
rolls = []
with open(os.path.join(CAP, "raw-packets.csv"), newline="", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        n3 = (row.get("N3TypeName") or "").strip()
        direction = (row.get("Direction") or "").strip().upper()
        hexbody = (row.get("RawHex") or "").strip()
        if n3 != "QuestAlternative" or not hexbody:
            continue
        raw = bytes.fromhex(hexbody)
        body = raw
        if len(raw) > 16 and raw[2:4] == b"\x00\x0A":
            body = raw[16:]
        entry = {
            "dir": direction,
            "utc": row.get("CapturedUtc") or "",
            "len": len(body),
            "icons": find_icons(body),
            "quests": find_dac3(body),
            "pfs": find_playfields(body),
            "texts": extract_ascii_snippets(body, 24),
            "hex": hexbody,
        }
        if direction.startswith("IN"):
            rolls.append(entry)

print("=== IN QuestAlternative rolls:", len(rolls))
type_counter = Counter()
for i, r in enumerate(rolls):
    icons = [name for _, _, name in r["icons"]]
    # dedupe consecutive same icon offsets carefully - keep unique by position order
    # icons may appear once per offer
    print("--- roll", i, "utc", r["utc"][:19], "body", r["len"], "icons", icons)
    type_counter.update(icons)
    # unique playfields
    pfs = sorted(set(pf for _, pf, _, _, _ in r["pfs"]))
    print("  playfields", pfs)
    # first few texts
    for t in r["texts"][:3]:
        print("  text:", t[:100])

print("\n=== icon frequency across rolls ===")
for k, v in type_counter.most_common():
    print(k, v)

# QuestFullUpdate accepts from mission-flow already known; also scan system messages
print("\n=== system-messages (mission-ish) ===")
syspath = os.path.join(CAP, "system-messages.log")
if os.path.exists(syspath):
    for line in open(syspath, encoding="utf-8-sig", errors="ignore"):
        low = line.lower()
        if any(k in low for k in ("mission", "broken", "radar", "key", "assignment", "quest")):
            print(line.rstrip()[:200].encode("ascii", "replace").decode("ascii"))

# Also dump first repair icon roll hex length for template candidate
print("\n=== rolls containing RepairMachine ===")
for i, r in enumerate(rolls):
    if any(name == "RepairMachine" for _, _, name in r["icons"]):
        print("roll", i, "icons", [n for _,_,n in r["icons"]], "quests", [hex(q) for _,q in r["quests"]])
