# Decode captured roll/accept mission packets for icon/location offsets.
from __future__ import print_function
import os, re, struct

missions = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Missions"

def load_hex(path, const_name="CapturedPacketHex"):
    text = open(path, encoding="utf-8").read()
    # join all long hex string literals in file
    parts = re.findall(r'"([0-9A-Fa-f]{32,})"', text)
    return "".join(parts)

def find_all(hexs, needle):
    out = []
    i = 0
    while True:
        j = hexs.find(needle, i)
        if j < 0:
            break
        out.append(j // 2)
        i = j + 2
    return out

roll = load_hex(os.path.join(missions, "MissionRollCaptureTemplate.cs"))
accept = load_hex(os.path.join(missions, "MissionAcceptCaptureTemplate.cs"))
print("roll bytes", len(roll)//2, "accept bytes", len(accept)//2)

# Known icons from prior analysis
icons = {
    "00002C41": "FindItem11329",
    "00002C42": "Kill11330",
    "00002C47": "FindPerson11335",
    "00002C49": "FindItem11337",
    "00002C4E": "Repair11342",
}
print("=== ROLL icons ===")
for h, name in icons.items():
    offs = find_all(roll, h)
    print(name, offs)

print("=== ACCEPT icons ===")
for h, name in icons.items():
    offs = find_all(accept, h)
    print(name, offs)

# Quest ids in roll: type DAC3
print("=== ROLL DAC3 quest ids ===")
i = 0
while True:
    j = roll.find("0000DAC3", i)
    if j < 0:
        break
    off = j // 2
    inst = roll[j+8:j+16]
    print("off", off, "instance", inst)
    i = j + 8

# Playfield 02DF (735) coords near QuestActions - look for 009C50000002DF
print("=== ROLL playfield 735 markers ===")
needle = "009C50000002DF"
i = 0
while True:
    j = roll.find(needle, i)
    if j < 0:
        break
    off = j // 2
    # after playfield + 2 ints (8 bytes) come x,y,z floats
    # structure: Playfield Identity (8) + Unknown18(4)+Unknown19(4)+X+Y+Z
    # Identity type 009C50 instance 000002DF = 8 bytes at needle
    base = j + len(needle)
    # skip 8 hex chars? Unknown18/19 are 8 bytes = 16 hex
    rest = roll[base:base+16+24]
    u18 = struct.unpack(">i", bytes.fromhex(rest[0:8]))[0]
    u19 = struct.unpack(">i", bytes.fromhex(rest[8:16]))[0]
    x = struct.unpack(">f", bytes.fromhex(rest[16:24]))[0]
    y = struct.unpack(">f", bytes.fromhex(rest[24:32]))[0]
    z = struct.unpack(">f", bytes.fromhex(rest[32:40]))[0]
    print("off", off, "u18", u18, "u19", u19, "xyz", x, y, z)
    i = j + 8

print("=== ACCEPT MissionIconId offset candidates ===")
print(find_all(accept, "00002C42"))
