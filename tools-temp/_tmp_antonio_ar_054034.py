# Extract Antonio Assault Rifle tip from capture 20260727-054034.
from __future__ import print_function
import re
import struct
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260727-054034")
out = Path(r"tools-temp/_tmp_antonio_ar_054034.txt")
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace").splitlines()
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")

lines = []
lines.append("=== mission-flow / events markers ===")
for path in ("mission-flow.log", "chat-dialogue.log"):
    p = cap / path
    if not p.exists():
        continue
    for ln in p.read_text(encoding="utf-8", errors="replace").splitlines():
        if any(k in ln for k in ("BO-18", "Assault", "Quest", "Knubot", "556A8FC0", "Append")):
            lines.append(ln[:500])

lines.append("")
lines.append("=== QuestFullUpdate hex packets ===")
found = 0
for line in hexlog:
    if "n3=QuestFullUpdate" not in line:
        continue
    m = re.search(r"hex=([0-9A-Fa-f]+)", line)
    if not m:
        continue
    raw = bytes.fromhex(m.group(1))
    # skip if no BO-18 / Assemble text
    if b"BO-18" not in raw and b"Assemble" not in raw and b"Blue Offset" not in raw:
        # still dump mission id if present
        if struct.pack(">I", 0x556A8FC0) not in raw and struct.pack(">I", 0x5569CDBF) not in raw:
            continue
    found += 1
    ts = line.split(" ", 1)[0] if " " in line else "?"
    lines.append("--- packet #%d ts=%s len=%d ---" % (found, ts, len(raw)))
    # mission identity: look for type 0xDAC3 (Mission) then instance
    for i in range(len(raw) - 6):
        if raw[i : i + 2] == b"\xDA\xC3":
            mid = struct.unpack(">I", raw[i + 2 : i + 6])[0]
            lines.append("mission id @%d = 0x%08X" % (i, mid))
    # icon near short name
    runs = re.findall(rb"[\x20-\x7e]{6,}", raw)
    for r in runs:
        s = r.decode("ascii")
        if any(
            k in s
            for k in (
                "Assemble",
                "Antonio",
                "itemref",
                "BO-18",
                "Fluid",
                "Worn",
                "Assault",
                "Chemical",
                "Factory",
                "Objective",
                "Combine",
            )
        ):
            lines.append("TEXT: " + s[:1200])
    # write raw hex for tip update
    hex_out = cap.parent.parent.parent / "_tmp_antonio_ar_054034.hex"
    # actually write beside txt under tools-temp
    Path(r"tools-temp/_tmp_antonio_ar_054034.hex").write_text(m.group(1).upper(), encoding="ascii")
    lines.append("wrote tools-temp/_tmp_antonio_ar_054034.hex (%d bytes payload)" % len(raw))
    lines.append("")

# Knubot append text near assault
lines.append("=== KnubotAppend / Answer around assault ===")
for line in hexlog:
    if "KnubotAppend" in line or "n3=KnubotAppendText" in line or "n3=KnuBotAppendText" in line:
        m = re.search(r"hex=([0-9A-Fa-f]+)", line)
        if not m:
            continue
        raw = bytes.fromhex(m.group(1))
        runs = re.findall(rb"[\x20-\x7e]{20,}", raw)
        for r in runs:
            s = r.decode("ascii")
            if any(k in s.lower() for k in ("assault", "bo-18", "rifle", "adapt", "recipe", "worn", "fluid")):
                lines.append(s[:800])

# events Detail for QFU around 03:43:33
for ln in events.splitlines():
    if "03:43:3" in ln and ("Quest" in ln or "Knubot" in ln or "Append" in ln or "BO-18" in ln):
        lines.append("EVT: " + ln[:600])

lines.append("found_qfu=%d" % found)
out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "lines", len(lines), "qfu", found)
