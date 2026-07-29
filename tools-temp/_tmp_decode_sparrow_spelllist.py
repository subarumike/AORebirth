"""Decode Sparrow Flight SpellList + any Stat updates on caster after cast."""
import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260723-053632")

# SpellList full hex
with (cap / "raw-packets.csv").open(encoding="utf-8", newline="") as f:
    for row in csv.DictReader(f):
        if row["N3TypeName"] == "SpellList" and row["Sequence"] == "42":
            hx = row["RawHex"]
            print("SpellList len", len(hx)//2)
            # skip N3 header roughly - find body after identity
            body = bytes.fromhex(hx)
            # print dword dump from offset after common header
            # header: 2 byte seq? actually packet starts with 00E1...
            print("full hex:")
            print(hx)
            print("--- dwords after type ---")
            # find 4D450114
            idx = hx.upper().find("4D450114")
            payload = hx[idx+8:]  # after type
            data = bytes.fromhex(payload)
            for i in range(0, min(len(data), 200), 4):
                chunk = data[i:i+4]
                if len(chunk) < 4:
                    break
                val = int.from_hex if False else int.from_bytes(chunk, "big")
                print(f"  +{i:03d}: {chunk.hex()} = {val} (0x{val:X})")

print("\n=== enemy-stat-updates mentioning 139459 or interesting stats ===")
stats = cap / "enemy-stat-updates.csv"
if stats.exists():
    with stats.open(encoding="utf-8", newline="") as f:
        for i, row in enumerate(csv.DictReader(f)):
            line = ",".join(row.values())
            if "139459" in line or "Monster" in line or "Run" in line or "Vehicle" in line or "Shape" in line:
                print(line[:300])
                if i > 40:
                    break
