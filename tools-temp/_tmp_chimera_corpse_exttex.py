# -*- coding: utf-8 -*-
import csv
from pathlib import Path

p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260723-225021/corpse-full-updates.csv")
live = bytes([
    0x00, 0x00, 0x07, 0xE2, 0x6C, 0x6F, 0x77, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x30, 0x49, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
])

with p.open(encoding="utf-8", newline="") as f:
    r = next(csv.DictReader(f))

raw = r["RawHex"]
# AOSharp seq-stripped frames start 000A; templates pad leading 0000
if raw[4:8].upper() == "000A":
    body = "0000" + raw[4:]
else:
    body = raw

body_bytes = bytes.fromhex(body)
print("pktlen_csv", r["PacketLength"])
print("raw_bytes", len(raw) // 2)
print("body_bytes", len(body_bytes))
print("live_exttex_in_body", live.hex().upper() in body.upper())
idx = body.upper().find("01000007E26C6F7732")
print("material_tail_offset", idx // 2 if idx >= 0 else None)
print("tail_hex", body.upper()[idx:] if idx >= 0 else "none")
print("catmesh", r["CorpseCatMesh"], "md", r["CorpseMonsterData"], "credits", r["CorpseCredits"])

# Offsets matching CorpseFullUpdate constants (padded template)
def be32(buf, off):
    return int.from_bytes(buf[off:off+4], "big")

print("off199 catmesh", be32(body_bytes, 199))
print("off207 cash", be32(body_bytes, 207))
print("off227 namelen", be32(body_bytes, 227))
# name starts 231; "Remains of Barking Chimera\0" = 27
name_len = be32(body_bytes, 227)
print("name", body_bytes[231:231 + name_len - 1])
# After name, monsterdata / tail like generic template with afterNameDelta=0
# Generic: CorpseMonsterDataOffset=330, TailDeadNpc=342
# But FilthFlea uses different offsets. Find 209173 / 208966
md = (209173).to_bytes(4, "big").hex().upper()
mesh = (208966).to_bytes(4, "big").hex().upper()
print("md hex", md, "at", body.upper().find(md) // 2)
print("mesh hex", mesh, "at", [i // 2 for i in range(0, len(body), 2) if body.upper()[i:i+8] == mesh])

# Write template for C# (full body hex)
out = Path(r"tools-temp/_tmp_chimera_corpse_template.hex")
out.write_text(body.upper(), encoding="ascii")
print("wrote", out, "len", len(body_bytes))
