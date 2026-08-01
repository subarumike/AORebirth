# -*- coding: utf-8 -*-
"""Extract SystemMessage-related strings/metadata from AOSharp.Common.dll"""
from pathlib import Path
p = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/AOSharp.Common.dll")
data = p.read_bytes()
# find UTF-8 / UTF-16 strings containing SystemMessage
needles = [b"SystemMessage", b"SimpleSystemMessage", b"NpcMessage", b"Unk1", b"Unk2"]
for n in needles:
    idx = 0
    while True:
        i = data.find(n, idx)
        if i < 0:
            break
        # dump surrounding printable
        start = max(0, i - 40)
        end = min(len(data), i + 80)
        chunk = data[start:end]
        ascii_s = "".join(chr(b) if 32 <= b < 127 else "." for b in chunk)
        print(n.decode(), "at", i, ascii_s)
        idx = i + 1
        if idx > i + 500000:
            break

# Also UTF-16LE
for n8 in [b"SystemMessage", b"SimpleSystemMessage"]:
    n16 = n8.decode().encode("utf-16le")
    idx = 0
    c = 0
    while c < 20:
        i = data.find(n16, idx)
        if i < 0:
            break
        start = max(0, i - 20)
        end = min(len(data), i + len(n16) + 60)
        try:
            s = data[start:end].decode("utf-16le", errors="ignore")
        except Exception:
            s = repr(data[start:end])
        print("u16", n8.decode(), "at", i, repr(s)[:200])
        idx = i + 2
        c += 1
