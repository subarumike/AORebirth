from pathlib import Path
import csv
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260728-232300/raw-packets.csv")
rows = list(csv.DictReader(p.open(encoding="utf-8-sig", newline="")))

# Decode all Stat in capture
print("=== ALL Stat ===")
for idx, r in enumerate(rows):
    if (r.get("N3TypeName") or "") != "Stat":
        continue
    b = bytes.fromhex((r.get("RawHex") or "").strip())
    i = b.find(bytes.fromhex("2B333D6E"))
    if i < 0:
        print(idx, "no stat magic", b[:20].hex())
        continue
    body = b[i + 4 :]
    # identity 8 + unk 1 + ...
    unk = body[8]
    rest = body[9:]
    # try: count(int) then pairs of (statId int, value int)
    if len(rest) >= 4:
        maybe_count = int.from_bytes(rest[0:4], "big")
        print(f"{idx:3d} {r.get('Direction')} unk={unk} count?={maybe_count} rest={rest.hex()}")
        off = 4
        if maybe_count in (1, 2, 3, 4, 5) and len(rest) >= 4 + maybe_count * 8:
            for n in range(maybe_count):
                sid = int.from_bytes(rest[off : off + 4], "big")
                val = int.from_bytes(rest[off + 4 : off + 8], "big", signed=True)
                print(f"      stat {sid}={val}")
                off += 8

# Action 0xA9 packet
print("\n=== CA 0xA9 ===")
r = rows[72]
b = bytes.fromhex(r["RawHex"].strip())
i = b.find(bytes.fromhex("5E477770"))
body = b[i + 4 :]
print(body.hex())
print("full", r["RawHex"])

# Feedback 70
print("\n=== Feedback 70 ===")
r = rows[70]
print(r["RawHex"])
