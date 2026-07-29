# -*- coding: utf-8 -*-
"""Decode Wounded Dockworker SCFU movement/HP from capture packets.hex.log"""
from __future__ import print_function
import re, struct, os

path = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-134750\packets.hex.log"
# Focus identities
want = {"78E0FC5F", "78E0FC6E", "78E0FC6F", "78E0FC70", "78E0FC71", "78E0FC72"}
pat = re.compile(r"n3=SimpleCharFullUpdate hex=([0-9A-Fa-f]+)")
name_pat = re.compile(r"576F756E64656420446F636B776F726B6572")  # "Wounded Dockworker"

def be_u32(b, o):
    return struct.unpack_from(">I", b, o)[0]

def be_u16(b, o):
    return struct.unpack_from(">H", b, o)[0]

seen = set()
with open(path, encoding="utf-8", errors="replace") as f:
    for line in f:
        if "SimpleCharFullUpdate" not in line:
            continue
        m = pat.search(line)
        if not m:
            continue
        hx = m.group(1)
        if "576F756E64656420446F636B776F726B6572" not in hx:
            continue
        raw = bytes.fromhex(hx)
        # Find identity after flags-ish: look for 78E0FC
        idx = hx.find("78E0FC")
        if idx < 0:
            continue
        ident = hx[idx:idx+8]
        if ident not in want or ident in seen:
            continue
        seen.add(ident)
        # After name string, typical SCFU has: level? Look for pattern after name
        # Name ends at hex of Wounded Dockworker then 00
        name_hex = "576F756E64656420446F636B776F726B6572"
        ni = hx.find(name_hex)
        after = hx[ni+len(name_hex):]
        # dump next 80 bytes as words
        after_bytes = bytes.fromhex(after[:160])
        print("===", ident, "len", len(raw), "===")
        # Heuristic: search for health 32 (0x20) and current related
        # In AO SCFU, MovementMode often appears near breed/side block as single byte 08
        # Print bytes around '0801' breed block
        for i in range(len(raw)-8):
            if raw[i] == 0x08 and raw[i+1] == 0x01 and raw[i+2] == 0x00 and raw[i+3] == 0x01:
                print("  sit-block@%d:" % i, raw[i:i+20].hex())
                break
        # Stat health packets in same capture: 0000001B0000000C => stat 0x1B (27=health?) value 12
        print("  after-name words:", " ".join("%08X" % be_u32(after_bytes, o) for o in range(0, min(40, len(after_bytes)-3), 4)) if len(after_bytes)>=4 else after[:80])

print("seen", sorted(seen))
