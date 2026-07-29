# -*- coding: utf-8 -*-
import re
import struct
import datetime

text = open(
    r"AORebirth/Server/ZoneEngine/Core/Thrak/Quests/ThrakGardenKeyPacketSender.cs",
    encoding="utf-8",
).read()
consts = re.findall(
    r'private const string (\w+)\s*=\s*"([0-9A-Fa-f]+)"',
    text,
)
print("found", len(consts))
for name, hx in consts:
    data = bytes.fromhex(hx)
    print("===", name, "len", len(data))
    for off in range(0, len(data) - 4):
        v = struct.unpack(">I", data[off : off + 4])[0]
        if 1_700_000_000 < v < 1_900_000_000:
            dt = datetime.datetime.utcfromtimestamp(v)
            print(" unix", off, v, dt.isoformat() + "Z")
        # AO AbsoluteTime often: seconds since 2000-01-01 UTC? Or 1970?
        # Also look for 0x6A83xxxx pattern (common in captures)
        if (v & 0xFFFF0000) == 0x6A830000:
            print(" 6A83*", off, hex(v), v)
            try:
                dt = datetime.datetime.utcfromtimestamp(v)
                print("   as unix", dt.isoformat() + "Z")
            except Exception:
                pass
