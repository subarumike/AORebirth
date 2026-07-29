# -*- coding: utf-8 -*-
import re
import struct

text = open(
    r"AORebirth/Server/ZoneEngine/Core/Thrak/Quests/ThrakGardenKeyPacketSender.cs",
    encoding="utf-8",
).read()
consts = re.findall(
    r'private const string (\w+)\s*=\s*"([0-9A-Fa-f]+)"',
    text,
)

# Find 6A83xxxx and nearby A8C0 (43200) and 26ADD patterns
for name, hx in consts:
    data = bytes.fromhex(hx)
    print("===", name, "len", len(data))
    for off in range(0, len(data) - 4):
        v = struct.unpack(">I", data[off : off + 4])[0]
        if (v & 0xFFFF0000) == 0x6A830000:
            ctx = data[max(0, off - 16) : off + 20].hex()
            print("  hash@%d = 0x%08X (%d) ctx=%s" % (off, v, v, ctx))
        if v == 0xA8C0:
            print("  dur43200@%d" % off)
        if v == 0x26ADD:
            print("  icon26ADD@%d" % off)
