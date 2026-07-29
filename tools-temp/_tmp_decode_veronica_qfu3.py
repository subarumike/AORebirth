import struct
from pathlib import Path

hex_payload = Path(r"tools-temp\_tmp_veronica_qfu.hex").read_text(encoding="ascii").strip()
data = bytes.fromhex(hex_payload)
needle = bytes.fromhex("0000DAC35556893A")
idx = data.find(needle)
pos = idx + 8
u1, u2, u3, u4 = struct.unpack_from(">IIII", data, pos)
pos += 16
print("u", u1, u2, u3, u4)

end = data.index(b"\x00", pos)
short = data[pos:end].decode("latin1")
pos = end + 1
print("SHORT:", short)

long_len = struct.unpack_from(">I", data, pos)[0]
pos += 4
long = data[pos : pos + long_len].decode("latin1")
pos += long_len
print("LONG_LEN", long_len)
print("LONG:", long)

# UnknownId1
t, i = struct.unpack_from(">II", data, pos)
print("UnknownId1", hex(t), hex(i))
pos += 8
ints = struct.unpack_from(">IIIIII", data, pos)
print("u5-10", ints)
pos += 24
# MissionItemData X3F1
# typically 000003F1 then count then items, or empty marker
print("at MissionItemData", data[pos : pos + 48].hex())

# Find MissionIconId by searching for known patterns - dump rest as structured via trial
# Look for character identity 765A690A (captured player)
char = bytes.fromhex("765A690A")
print("char occurrences", [i for i in range(len(data)) if data.startswith(char, i)])
print("veronica 787B54B2", data.find(bytes.fromhex("787B54B2")))
print("full hex saved already")

# Also extract all later QuestFullUpdates for chain stages
import csv
from pathlib import Path as P

missions = {
    "5556893A": "veronica",
    "55563C16": "insignia",
    "55563C17": "unknown_middle",
    "55563C18": "garden",
    "5556591A": "souls",
    "5556893B": "souls1",
    "5556893C": "souls2",
    "5556893D": "return",
}

csv_path = P(r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-185306\raw-packets.csv")
seen = set()
with csv_path.open(newline="", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") != "QuestFullUpdate":
            continue
        hx = row["RawHex"]
        for mid, name in missions.items():
            if mid.lower() in hx.lower() and mid not in seen:
                seen.add(mid)
                out = P(f"tools-temp/_tmp_thrak_qfu_{name}_{mid}.hex")
                out.write_text(hx, encoding="ascii")
                print("saved", out.name, "len", len(hx) // 2, "time", row.get("CapturedUtc"))

print("done", seen)
