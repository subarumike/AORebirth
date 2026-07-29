from pathlib import Path
import re
hexlog = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\packets.hex.log").read_text(encoding="utf-8", errors="replace")
# Find OUT PickUp packets around 08:10:12
for line in hexlog.splitlines():
    if "08:10:12" in line and ("OUT" in line or "PickUp" in line):
        print(line[:300])
for line in hexlog.splitlines():
    if "n3=PickUp" in line or "PickUp" in line:
        print(line[:350])
        if "08:10" in line:
            break
