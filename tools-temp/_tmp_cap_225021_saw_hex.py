# -*- coding: utf-8 -*-
import csv
import struct

p = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260723-225021\raw-packets.csv"
out = r"tools-temp\_tmp_cap_225021_saw_hex.txt"
rows = list(csv.DictReader(open(p, encoding="utf-8-sig")))
target = 0x798C1F4F
lines = []

def rows_of(name, ident=None):
    res = []
    for r in rows:
        if r.get("N3TypeName") != name:
            continue
        if ident is not None and int(r.get("IdentityInstance") or 0) != ident:
            continue
        res.append(r)
    return res

saws = rows_of("SpecialAttackWeapon", target)
ats = rows_of("Attack", target)
ais = rows_of("AttackInfo", target)
lines.append("SAW=%d Attack=%d AI=%d for %X" % (len(saws), len(ats), len(ais), target))
if saws:
    hexdata = saws[0]["RawHex"]
    lines.append("SAW hex=%s" % hexdata)
    b = bytes.fromhex(hexdata)
    # find identity C350 + target LE
    # N3 body often starts after transport; search for C350
    idx = b.find(b"\x00\xC3\x50")
    lines.append("C350 idx=%s len=%d" % (idx, len(b)))
    # dump ints after identity
    # From audit SAW body: 1D3C0F1C 0000C350 <id> 00000003 F1 00000020 x4 00000000
    # Search for F1 marker (specials type?)
    for i in range(len(b) - 4):
        if b[i:i+2] == b"\xC3\x50" or b[i:i+4] == struct.pack("<I", target):
            lines.append("hit at %d: %s" % (i, b[i:i+80].hex()))

    # Parse: after identity 4 bytes, typically count then specials
    # Find target identity then parse
    id_bytes = struct.pack("<I", target)
    pos = b.find(id_bytes)
    lines.append("id pos=%d trailing=%s" % (pos, b[pos:].hex() if pos>=0 else ""))
    if pos >= 0:
        body = b[pos+4:]
        lines.append("after id: %s" % body.hex())
        # try: Unknown(1) + specials count + each special (low,high,tag,?) 
        # AOSharp SpecialAttackInfo typically: Unknown1 (int), Specials list
        # Message fields: Specials, Unknown1-5
        # Wire from audit empty specials: 00000003F100000020 *4 + 00000000
        # With 5 specials, each SpecialAttackInfo has template ids + tag + name?

if ais:
    lines.append("\nFirst 3 AttackInfo hex:")
    for r in ais[:3]:
        lines.append(r["RawHex"])
        bb = bytes.fromhex(r["RawHex"])
        idpos = bb.find(id_bytes)
        if idpos >= 0:
            lines.append("  after id: %s" % bb[idpos+4:].hex())

# Decode all unique weapon instances as tags - get specials from ANY chimera SAW
# Use AOSharp analyzer decode if available - brute parse known weapon instances in SAW hex
weapon_tags = [1497452619, 1111971416, 1280787787, 1414222417, 1297632336]
for tag in weapon_tags:
    tb = struct.pack("<I", tag)
    found = False
    for r in rows_of("SpecialAttackWeapon"):
        if tb.hex() in r["RawHex"].lower():
            found = True
            break
    lines.append("tag %d (0x%X) in some SAW: %s" % (tag, tag & 0xFFFFFFFF, found))

# Dump one full SAW after id with struct speculation
if saws:
    b = bytes.fromhex(saws[0]["RawHex"])
    pos = b.find(id_bytes)
    body = b[pos+4:]
    # skip Unknown byte? Attack SAW: often 00 then ...
    lines.append("\nParse attempt body ints:")
    # align to 4
    for off in range(0, min(len(body), 8)):
        chunk = body[off:]
        if len(chunk) < 8:
            continue
        ints = []
        for i in range(0, len(chunk) - 3, 4):
            ints.append(struct.unpack_from("<I", chunk, i)[0])
        lines.append("off=%d first20=%s" % (off, ints[:20]))

open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print("wrote", out)
print("\n".join(lines[:60]))
