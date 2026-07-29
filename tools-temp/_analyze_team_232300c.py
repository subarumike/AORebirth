from pathlib import Path
import csv
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-232300/raw-packets.csv")
rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))

# Decode TeamMember #91 precisely from N3 type onward
hx = rows[91]["RawHex"].strip()
b = bytes.fromhex(hx)
# find F6312D2E or 46312D2E
for mag in ("F6312D2E", "46312D2E"):
    i = b.find(bytes.fromhex(mag))
    print("magic", mag, "at", i)

i = b.find(bytes.fromhex("46312D2E"))
body = b[i + 4 :]
print("body after type:", body.hex())
print("len", len(body))

# Identity 8 + unk 1 + Member 8 + Team 8 + unk4 4 + level 4 + unk5 2 + namelen 4 + name
off = 0
print("viewer", body[off : off + 8].hex())
off = 8
print("unkbyte", body[off])
off = 9
print("member", body[off : off + 8].hex())
off = 17
print("team", body[off : off + 8].hex())
off = 25
print("unk4", int.from_bytes(body[off : off + 4], "big", signed=True))
off = 29
print("level", int.from_bytes(body[off : off + 4], "big", signed=True))
off = 33
print("unk5", int.from_bytes(body[off : off + 2], "big", signed=True))
off = 35
nlen = int.from_bytes(body[off : off + 4], "big", signed=True)
print("nlen", nlen)
off = 39
print("name", body[off : off + nlen])

print("\n--- TeamMemberInfo 95 ---")
hx = rows[95]["RawHex"].strip()
b = bytes.fromhex(hx)
print(hx)
# find type 28784248
i = b.find(bytes.fromhex("28784248"))
body = b[i + 4 :]
print("body", body.hex())
off = 0
print("viewer", body[off : off + 8].hex())
print("unk", body[8])
print("member", body[9:17].hex())
print("u3", int.from_bytes(body[17:21], "big"))
print("u4", int.from_bytes(body[21:25], "big"))
print("u5", int.from_bytes(body[25:29], "big"))
print("u6", int.from_bytes(body[29:33], "big"))

print("\n--- Stat packets 87-96 detail ---")
for idx in range(87, 97):
    r = rows[idx]
    name = r.get("N3TypeName") or ""
    hx = (r.get("RawHex") or "").strip()
    b = bytes.fromhex(hx)
    if name == "Stat":
        # find 0x24 something Stat type - try common
        print(f"{idx} Stat raw={hx}")
    else:
        print(f"{idx} {name}")

# OUT only packets - what did capturer send after invite?
print("\n=== ALL OUT ===")
for idx, r in enumerate(rows):
    if r.get("Direction") != "OUT":
        continue
    print(idx, r.get("N3TypeName"), (r.get("RawHex") or "")[:80])
