# -*- coding: utf-8 -*-
"""Extract Shiny Sword quest dialogue + items from capture."""
import re
from pathlib import Path

CAP = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260725-shiny-sword-nano")
OUT = Path(r"tools-temp/_tmp_shiny_sword_brief.txt")
lines = []

def decode_ao_string(hexstr, start_hint="546865205368"):
    # find ASCII strings in hex
    raw = bytes.fromhex(hexstr.replace(" ", ""))
    # extract length-prefixed strings (AO often uses 2-byte BE or 4-byte BE length)
    i = 0
    found = []
    while i < len(raw) - 4:
        # try 2-byte BE length
        for size_len in (2, 4):
            if i + size_len > len(raw):
                break
            ln = int.from_bytes(raw[i:i+size_len], "big")
            if 3 <= ln <= 400 and i + size_len + ln <= len(raw):
                chunk = raw[i+size_len:i+size_len+ln]
                if all(32 <= b < 127 or b in (10, 13) for b in chunk):
                    s = chunk.decode("ascii", errors="ignore")
                    if len(s) >= 3:
                        found.append(s)
                        i = i + size_len + ln
                        break
        else:
            i += 1
    return found

# Decode QFU hex
hexline = None
for ln in (CAP / "packets.hex.log").read_text(encoding="utf-8-sig", errors="replace").splitlines():
    if "QuestFullUpdate" in ln and "5565CD87" in ln.upper().replace("dac3", "DAC3"):
        m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
        if m:
            hexline = m.group(1)
            break
    if "QuestFullUpdate" in ln:
        m = re.search(r"hex=([0-9A-Fa-f]+)", ln)
        if m and "5368696E79" in m.group(1):  # Shiny
            hexline = m.group(1)
            break

if hexline:
    strs = decode_ao_string(hexline)
    lines.append("=== QFU strings ===")
    for s in strs:
        lines.append(repr(s))

# Scan events for KnuBot append / text
events = (CAP / "events.log").read_text(encoding="utf-8-sig", errors="replace")
for pat in ("KnuBot", "Shiny", "sword", "Greedy", "nano", "Trade", "Inventory", "Quest"):
    pass

# Extract all KnuBot detail lines
lines.append("\n=== KnuBot / Quest / Inventory events ===")
for ln in events.splitlines():
    if any(k in ln for k in (
        "KnuBot", "QuestFullUpdate", "QuestMessage", "Quest ",
        "Inventory", "Template", "Greedy", "Shiny", "Append",
        "StartTrade", "FinishTrade", "Rejected", "SystemMessage"
    )):
        # shorten huge lines
        if len(ln) > 500:
            lines.append(ln[:500] + "...")
        else:
            lines.append(ln)

# chat + interactions already separate - peek inventory csv
inv = CAP / "inventory-updates.csv"
if inv.exists():
    lines.append("\n=== inventory-updates.csv (head) ===")
    for i, row in enumerate(inv.read_text(encoding="utf-8-sig").splitlines()[:40]):
        lines.append(row)

OUT.write_text("\n".join(lines), encoding="utf-8")
print("\n".join(lines[:80]))
print("... total", len(lines), "->", OUT)
