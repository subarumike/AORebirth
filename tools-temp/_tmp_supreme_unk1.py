from pathlib import Path
import re

ev = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260720-204431/events.log")
for line in ev.read_text(encoding="utf-8", errors="ignore").splitlines():
    if 'Name="Supreme Collector of Waste"' in line and "ScfuUnk1=byte[28]:" in line:
        m = re.search(r"ScfuUnk1=byte\[28\]:([0-9A-Fa-f]+)", line)
        print("ScfuUnk1", m.group(1) if m else None)
        # TextureOverride may be truncated in ToString - use hex instead
        break

# decode texture id from supreme override
print("supreme tex", 0x01768D)
print("waste tex", 0x43A6)
