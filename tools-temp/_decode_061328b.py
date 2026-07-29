import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-061328")
rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))

# Decode first InfoPacket + SCFU Level for id 90
for i in (3, 4, 11, 12):
    r = rows[i]
    h = (r.get("RawHex") or "").replace(" ", "")
    b = bytes.fromhex(h)
    n3 = r.get("N3TypeName")
    print("====", i, n3, "len", len(b), "====")
    if n3 == "InfoPacket":
        # find type after n3 header rough: look for identity then body
        # dump around known area - Level is often a byte in CharacterInfoPacket
        print(h)
    if n3 == "SimpleCharFullUpdate":
        # Level is short in SCFU - search after flags/name
        # Print first 80 bytes hex and try find level=1 or 25
        print(h[:160])
        # level often appears as 00 01 or 00 19
        for needle in ("0001", "0019"):
            pos = h.find(needle)
            print("find", needle, "at", pos)

# What is 0x69?
print("==== CharacterActionType 0x69 ====")
cat = Path(r"AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CharacterActionType.cs")
for line in cat.read_text(encoding="utf-8").splitlines():
    if "0x69" in line.lower() or "0x00000069" in line.lower() or "InfoRequest" in line:
        print(line)
