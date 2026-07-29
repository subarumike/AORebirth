# Find SIFU for Terminal:574187CF and extract template id + flags + heading
from pathlib import Path
import csv
import struct

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-sara")
out = Path(r"tools-temp/_tmp_sarah_thief_sifu.txt")
lines = []

# Terminal:574187CF = type C73D, instance 574187CF
needle = "C73D574187CF"
with (cap / "raw-packets.csv").open(newline="", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        hx = (row.get("RawHex") or "").upper()
        if needle in hx or "574187CF" in hx:
            lines.append(f"seq={row.get('Sequence')} dir={row.get('Direction')} n3={row.get('N3TypeName')} len={row.get('PacketLength')}")
            lines.append(hx[:300])
            # try decode ascii name
            try:
                data = bytes.fromhex(hx)
                # find Remains
                idx = data.find(b"Remains")
                if idx >= 0:
                    lines.append("name@" + str(idx) + ": " + data[idx:idx+40].decode("latin1", "replace"))
                # look for common template patterns near end of stats
            except Exception as e:
                lines.append("err " + str(e))

# Also search events for SimpleItemFullUpdate near Shop Thief spawn
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace").splitlines()
for i, line in enumerate(events):
    if "574187CF" in line or ("Shop Thief" in line):
        for j in range(max(0, i-5), min(len(events), i+15)):
            if "SimpleItem" in events[j] or "574187CF" in events[j] or "Shop" in events[j] or "StaticInstance" in events[j]:
                lines.append(f"{j+1}:{events[j][:500]}")

out.write_text("\n".join(lines), encoding="utf-8")
print("wrote", out, "n", len(lines))
