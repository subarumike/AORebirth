import struct
from pathlib import Path

hex_payload = Path(r"tools-temp\_tmp_veronica_qfu.hex").read_text(encoding="ascii").strip()
data = bytes.fromhex(hex_payload)
print("len", len(data))

# Find mission id DAC3 5556893A
needle = bytes.fromhex("0000DAC35556893A")
idx = data.find(needle)
print("mission@", idx)

pos = idx + 8
u1, u2, u3, u4 = struct.unpack_from(">IIII", data, pos)
pos += 16
print("u1-4", u1, u2, u3, u4)
short = data[pos : pos + 32]
pos += 32
print("short", short.rstrip(b"\x00").decode("latin1"))
# Unknown4=2 in Windcaller means something before short? Actually looking at hex:
# 0000000F000000000000000000000002 then short
# Wait - after Unknown4 there's another field? Looking at hex in grep:
# DAC35556893A0000000F000000000000000000000002596F75...
# So after mission: Unknown1=0F, Unknown2=0, Unknown3=0, Unknown4=0, then 00000002?
# Actually: 0000000F 00000000 00000000 00000000 00000002 = Unknown1.. and then ShortInfo starts with length?
# Short "You agreed..." - in AO ShortInfo is often null-terminated in fixed 32 OR length-prefixed.

# Re-parse: after mission id
pos = idx + 8
vals = [struct.unpack_from(">I", data, pos + i * 4)[0] for i in range(5)]
print("five ints", vals)
pos = idx + 8 + 20
# if vals[4]==2, short is fixed? But next bytes are 59 6F 75 = 'You' - no length before short!
# So ShortInfo is NOT length prefixed - fixed? But 'You agreed to find informati...' is longer than 32?
short_candidate = data[pos:]
# Windcaller uses FixedSizeLength=32 for ShortInfo in QuestInfo but Quest.cs might differ

from pathlib import Path as P
# dump around short
print(data[pos : pos + 80])

# Try: ShortInfo is AoString with size type Int32? vals[4]=2 might be size of something else
# Looking again at SafeQuest: Unknown4=2, ShortInfo string, LongInfo with Int32 size
# If ShortInfo has NoSerialization FixedSize 32:
short32 = data[pos : pos + 32].split(b"\x00")[0].decode("latin1")
print("short32", short32)
pos2 = pos + 32
long_len = struct.unpack_from(">I", data, pos2)[0]
print("long_len", long_len)
long = data[pos2 + 4 : pos2 + 4 + long_len].decode("latin1")
print("long", long)
print("after long pos", pos2 + 4 + long_len)
after = pos2 + 4 + long_len
# UnknownId1 = identity 8 bytes
print("next16", data[after : after + 32].hex())
