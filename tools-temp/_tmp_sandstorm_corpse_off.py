# -*- coding: utf-8 -*-
"""Compute offsets for SANDSTORM Marauder corpse capture packet."""
h = open("tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-204902/packets.hex.log", encoding="utf-8").read()
import re
m = re.search(r"Corpse:F5F804.*?hex=([0-9A-Fa-f]+)", h)
# better search
m = re.search(r"len=425 n3=CorpseFullUpdate hex=([0-9A-Fa-f]+)", h)
hexs = m.group(1)
b = bytes.fromhex(hexs)
print("len", len(b))
name = b"Remains of SANDSTORM Marauder"
idx = b.find(name)
print("name at", idx, "encodedLen field", int.from_bytes(b[idx-4:idx], "big"))
# OriginalEncodedNameLength includes null
print("name+null", len(name)+1)
# Find common offsets from AreteWaste pattern - server id at 8, etc.
# Compare with known NameOffset=231
print("byte 227-231", b[227:231].hex(), "len field", int.from_bytes(b[227:231],"big"))
print("at 231 starts", b[231:231+30])

# monster data in living was 265822 = 0x40E5E
md = (265822).to_bytes(4,"big")
print("MD 265822 at", b.find(md))
# alternate MD 287217
md2 = (287217).to_bytes(4,"big")
print("MD 287217 at", b.find(md2))

# dead npc 799F05FE
dead = bytes.fromhex("799F05FE")
i = 0
while True:
    j = b.find(dead, i)
    if j < 0: break
    print("dead at", j)
    i = j+1

# corpse F5F804
print("corpse at", b.find(bytes.fromhex("00F5F804")))
print("receiver 7996C028 at", [j for j in range(len(b)-3) if b[j:j+4]==bytes.fromhex("7996C028")])

# CATMesh - look near name for 0x40E5B etc from hex dump 00040E5B
print("40E5B at", b.find(bytes.fromhex("00040E5B")))
print("40E30 at", b.find(bytes.fromhex("00040E30")))  # cash/stat?

# Write stripped body without packet number for template (keep full as other builders do)
open("tools-temp/_tmp_sandstorm_corpse.hex","w").write(hexs)
print("suffix after name", idx+len(name)+1)
