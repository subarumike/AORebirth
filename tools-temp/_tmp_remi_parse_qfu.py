# -*- coding: utf-8 -*-
"""Simulate QuestFullUpdateMessageSerializer.ReadQuest on Remi tip body."""
from pathlib import Path
import re
import struct

src = Path(r"AORebirth/Server/ZoneEngine/Core/Arete/Quests/RemiGalloisTipSender.cs").read_text(encoding="utf-8")
b = bytearray(bytes.fromhex(re.search(r'QuellTipHex =\s*"([0-9A-Fa-f]+)"', src).group(1)))

# Skip packet header: 2 byte pn + 000A0001? Looking at SendCompressed overwrites [0],[1]
# Capture: 01DB 000A 0001 02E1 0000 0DC1 IDENTITY...
# N3 messages: after pn(2), often size-ish. Look at ZoneClient - full buffer includes pn.
# Serializer starts at N3MessageType which is typically after: packetNumber(2) + ??? 
# Compare with how other code strips header.

# From capture payload used as Enqueue buffer - full 737 bytes including 01DB.
# MessageSerializer for outbound probably adds header differently.

# Looking at packets: 01DB000A000102E100000DC1...
# Common AO: [pktNum u16][flags?][size?][N3Type][Identity][Unknown]
# 01DB = pkt
# 000A = ?
# 000102E1 = size 737?
# 00000DC1 = N3 type QuestFullUpdate?

def be32(data, off):
    return struct.unpack_from(">I", data, off)[0]

def be16(data, off):
    return struct.unpack_from(">H", data, off)[0]

# Find N3 type QuestFullUpdate = 1180319841 = 0x465A4061
n3 = b.find(bytes.fromhex("465A4061"))
print("N3 QuestFullUpdate marker at", n3, "before", b[n3-8:n3].hex())

# Actually in hex: 00000DC17996C028465A4061
# 0DC1 = N3MessageType enum value?
# Identity C350? No: 0000 0DC1 7996C028 then 465A4061?

print("bytes 0-40", b[:40].hex())

# Standard N3 header in these captures:
# [0..1] packet number
# [2..3] 000A ?
# [4..7] length including something
# [8..11] 00000DC1 or similar header
# Identity at ...
# Looking: 01DB 000A 000102E1 00000DC1 7996C028 465A4061 0000C350 7996C028 01 ...
# Wait 465A4061 is ASCII "FZ@a" = N3MessageType.QuestFullUpdate!

# So structure:
# 0-1 pn
# 2-3 000A
# 4-7 size 000102E1
# 8-11 00000DC1 ???
# 12-15 7996C028 - but identity needs type...

# Alternate: 00000DC1 is part of something else
# 01DB000A000102E100000DC17996C028465A40610000C3507996C02801

# Parse as MessageSerializer expects for body starting at offset after hop?
# ZoneEngine SendCompressed writes entire buffer with pn at 0.

# From AOtomation N3Message: Type, Identity, Unknown
# Type QuestFullUpdate = 0x465A4061
off = b.find(bytes.fromhex("465A4061"))
print("type at", off)
# Identity follows type in serializer Deserialize - but wait Deserialize reads Type first from stream start

# For Enqueue, the buffer IS what SendCompressed writes after assigning pn.
# So deserializer for IN packets might start at 0 differently.

# Look at how FlintKneecapping hex starts: 02DE000A000102DE00000DC1...
# Same pattern. Serializer for outbound MessageBody.SendCompressed builds from message.

# When EnqueueOutboundCompressedBuffer is used, the hex is the COMPLETE wire packet
# matching what client receives (with capture's packet number overwritten).

# Client receives and parses starting how?
# Likely: skip 2 byte pn, then rest is zlib-decompressed content already... 
# Actually SendCompressed zlib-compresses the buffer INCLUDING pn bytes.

# The IN capture payload is the DECOMPRESSED n3 packet as client sees it.
# Structure matches what server SendCompressed writes before zlib.

# MessageSerializer.Serialize for N3Message typically produces from byte 0:
# Looking at similar tip senders - they enqueue the FULL capture hex including pn and 000A and size.

# For Deserialize of QuestFullUpdate body, serializer reads:
# N3MessageType, Identity, Unknown byte, quests...

# So where does that start in the buffer?
# Search for pattern: type 465A4061, but serializer reads type as FIRST int32.
# In buffer type is NOT at offset 0.

# Offset of type=465A4061 is after header. Header length?
print("header before type", off, b[:off].hex())

# 01DB000A000102E100000DC17996C028 = 16 bytes before type?
# 01DB 000A 000102E1 00000DC1 7996C028 = 2+2+4+4+4 = 16 yes
# So N3 payload starts at offset 16 with Type?

# But Identity would be AFTER type: 0000C350 7996C028, Unknown=01
# Bytes at 16: 465A4061 0000C350 7996C028 01 000007E2 ...

off = 16
print("start parse at 16")
n3type = be32(b, off); off += 4
print("type", hex(n3type))
id_type = be32(b, off); off += 4
id_inst = be32(b, off); off += 4
print("identity", hex(id_type), hex(id_inst))
unknown = b[off]; off += 1
print("unknown", unknown)

# X3F1 quest count
enc = be32(b, off); off += 4
count = (enc // 0x3F1) - 1
print("questCount enc", hex(enc), "count", count)

def read_identity(buf, o):
    t = be32(buf, o); o += 4
    i = be32(buf, o); o += 4
    return (t, i), o

def read_nt_string(buf, o):
    start = o
    while buf[o] != 0:
        o += 1
    s = bytes(buf[start:o]).decode("ascii", "replace")
    o += 1  # null
    return s, o

def read_lp_string(buf, o):
    ln = be32(buf, o); o += 4
    s = bytes(buf[o:o+ln]).decode("ascii", "replace")
    o += ln
    return s, o

# Read quest
qid, off = read_identity(b, off)
print("QuestId", hex(qid[0]), hex(qid[1]))
for name in ["U1","U2","U3","U4"]:
    v = be32(b, off); off += 4
    print(name, v)
short, off = read_nt_string(b, off)
print("Short", repr(short))
long, off = read_lp_string(b, off)
print("Long len", len(long), long[:60])
uid1, off = read_identity(b, off)
print("UnknownId1", hex(uid1[0]), hex(uid1[1]))
for name in ["U5","U6","U7","U8","U9","U10"]:
    v = be32(b, off); off += 4
    print(name, v, hex(v))
# MissionItemData X3F1
enc = be32(b, off); off += 4
micount = (enc // 0x3F1) - 1
print("MissionItemData count", micount, "enc", hex(enc), "off after", off)
u11 = be32(b, off); off += 4
u12 = be32(b, off); off += 4
u13 = be32(b, off); off += 4
hash1 = bytes(b[off:off+4]); off += 4
print("Unknown11 AbsoluteTime", u11, hex(u11))
print("Unknown12", u12, hex(u12))
print("Unknown13", u13, hex(u13))
print("Hash1", hash1)
print("offset now", off)
