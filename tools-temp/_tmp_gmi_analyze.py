#!/usr/bin/env python3
from __future__ import annotations

import csv
import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

CAP = pathlib.Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture"
    r"\bin\Debug\captures\20260715-GMI"
)
OUT = CAP / "_gmi_analysis.txt"
keys = (
    "Market", "GMI", "trade", "vgtp", "uwg", "Web", "Browser", "AOBrowser",
    "GenericCmd", "Terminal", "index.app", "400", "nginx"
)
lines: list[str] = []

info = (CAP / "capture_info.json").read_text(encoding="utf-8-sig", errors="replace")
lines.append("=== capture_info ===")
lines.append(info[:1500])

# events hits
lines.append("\n=== events hits ===")
ev = (CAP / "events.log").read_text(encoding="utf-8", errors="replace")
for i, line in enumerate(ev.splitlines(), 1):
    low = line.lower()
    if any(k.lower() in low for k in keys) or "Market" in line or "trade" in low:
        lines.append(f"L{i}: {line[:450]}")

# system messages
lines.append("\n=== system-messages hits ===")
sysp = CAP / "system-messages.log"
if sysp.exists():
    for i, line in enumerate(sysp.open(encoding="utf-8", errors="replace"), 1):
        low = line.lower()
        if any(k.lower() in low for k in keys):
            lines.append(f"L{i}: {line[:450]}")

# npc interactions
lines.append("\n=== npc-interactions hits ===")
npc = CAP / "npc-interactions.log"
if npc.exists():
    for i, line in enumerate(npc.open(encoding="utf-8", errors="replace"), 1):
        low = line.lower()
        if any(k.lower() in low for k in keys) or "Use" in line:
            lines.append(f"L{i}: {line[:450]}")

# chat
chat = CAP / "chat-dialogue.log"
if chat.exists():
    lines.append("\n=== chat ===")
    lines.append(chat.read_text(encoding="utf-8", errors="replace")[:2000])

# raw packet type histogram + marketish
lines.append("\n=== raw-packets type histogram ===")
types = {}
interesting = []
with (CAP / "raw-packets.csv").open(newline="", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        t = row.get("N3TypeName") or ""
        types[t] = types.get(t, 0) + 1
        blob = (row.get("RawHex") or "") + t + (row.get("Direction") or "")
        if any(x in blob for x in ("Market", "trade", "vgtp", "uwg", "Browser", "Web")):
            interesting.append(row)
for t, c in sorted(types.items(), key=lambda x: -x[1])[:40]:
    lines.append(f"  {c:5d} {t}")
lines.append(f"interesting rows: {len(interesting)}")
for row in interesting[:40]:
    lines.append(
        f"  {row.get('Direction')} {row.get('CapturedUtc')} type={row.get('N3TypeName')} "
        f"len={row.get('PacketLength')} hex={(row.get('RawHex') or '')[:120]}"
    )

# hex log search for ascii strings
hexlog = (CAP / "packets.hex.log").read_text(encoding="utf-8", errors="replace")
lines.append("\n=== packets.hex.log string search ===")
for needle in (b"vgtp", b"uwg.trade", b"index.app", b"Market", b"omni-rk", b"trade", b"GMI", b"nginx"):
    # search in decoded ascii from hex lines is hard; search text as-is
    count = hexlog.lower().count(needle.decode().lower())
    lines.append(f"  text count {needle!r}={count}")

# also decode all IN GenericCmd / any unknown around use
lines.append("\n=== GenericCmd + nearby from events (Use) ===")
for i, line in enumerate(ev.splitlines(), 1):
    if "GenericCmd" in line and ("Use" in line or "Market" in line or "Terminal" in line):
        lines.append(f"L{i}: {line[:500]}")

OUT.write_text("\n".join(lines), encoding="utf-8")
print("wrote", OUT, "lines", len(lines))
