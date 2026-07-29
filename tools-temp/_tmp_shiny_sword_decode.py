# -*- coding: utf-8 -*-
from pathlib import Path
import re

CAP = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-shiny-sword-nano")
OUT = Path(r"tools-temp/_tmp_shiny_sword_decode.txt")
lines = []

def ascii_from_hex(h):
    raw = bytes.fromhex(h)
    # 4-byte BE length strings after target identity pattern
    out = []
    i = 0
    while i < len(raw) - 5:
        ln = int.from_bytes(raw[i:i+4], "big")
        if 1 <= ln <= 300 and i + 4 + ln <= len(raw):
            chunk = raw[i+4:i+4+ln]
            if all(32 <= b < 127 for b in chunk):
                out.append(chunk.decode("ascii"))
                i = i + 4 + ln
                continue
        i += 1
    return out

# All knubot append texts
for ln in (CAP / "packets.hex.log").read_text(encoding="utf-8-sig").splitlines():
    m = re.search(r"n3=(\w+).*hex=([0-9A-Fa-f]+)", ln)
    if not m:
        continue
    n3, hx = m.group(1), m.group(2)
    if n3 in ("KnubotAppendText", "KnubotAnswerList", "QuestFullUpdate", "TemplateAction", "CharacterAction"):
        strs = ascii_from_hex(hx)
        ts = ln.split(" ", 1)[0]
        lines.append("%s %s strings=%s" % (ts, n3, strs))
        if n3 == "QuestFullUpdate":
            # also dump interesting markers
            raw = bytes.fromhex(hx)
            # find SHSW GRDS tags
            if b"SHSW" in raw:
                lines.append("  has SHSW marker")
            if b"GRDS" in raw:
                lines.append("  has GRDS marker")
            # icon etc
            idx = hx.upper().find("5565CD87")
            lines.append("  mission hex idx=%s" % idx)

# Events between trade and close for TemplateAction / Inventory / Stat / Nano
events = (CAP / "events.log").read_text(encoding="utf-8-sig", errors="replace").splitlines()
lines.append("\n=== events 20:16:05 - 20:16:10 ===")
for ln in events:
    if "20:16:0" in ln or "20:15:49" in ln:
        if any(k in ln for k in ("Template", "Inventory", "Stat", "Nano", "Quest", "CharacterAction", "Container", "Add", "Remove", "System")):
            lines.append(ln[:400])

OUT.write_text("\n".join(lines), encoding="utf-8")
print("\n".join(lines))
