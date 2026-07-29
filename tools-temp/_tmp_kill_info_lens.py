import re
# Extract Kill ShortInfo / Info ASCII from accept template for Find Person length-fit patching.
hexs = open(r"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcceptCaptureTemplate.cs", encoding="utf-8").read()
m = re.search(r'CapturedPacketHex =\s*"([^"]+)"', hexs)
# it's multi-line concatenated - read differently
parts = re.findall(r'"([0-9A-Fa-f]+)"', hexs)
h = "".join(parts)
# only first big hex
h = max(parts, key=len) if parts else ""
# file has one long string
m = re.search(r'CapturedPacketHex =\s*\n\s*"([0-9A-Fa-f]+)"', hexs)
if not m:
    m = re.search(r'CapturedPacketHex =\s*"([0-9A-Fa-f]+)"', hexs)
h = m.group(1)
b = bytes.fromhex(h)
short = b"Great! Come back for another..."
info_start = b"Great! Come back for another mission, will you?"
print("short", b.find(short), len(short))
print("info", b.find(info_start))
# find end of info - null after
i = b.find(info_start)
j = b.find(b"\x00", i)
print("info len", j-i)
print(repr(b[i:j].decode("ascii")))
print("short repr", repr(b[b.find(short):b.find(short)+len(short)].decode()))
