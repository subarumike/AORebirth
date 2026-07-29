import csv
from pathlib import Path

# Decode LFT reply playfields from live LFT capture if present
caps = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")
for name in ("20260727-104625", "20260727-lft-list-search"):
    p = caps / name
    if not p.exists():
        print(name, "missing")
        continue
    # find chat packets or events mentioning LFT
    for f in p.iterdir():
        print(name, f.name)
