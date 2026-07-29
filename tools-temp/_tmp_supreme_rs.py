from pathlib import Path
import re
t = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260720-204431/events.log").read_text(encoding="utf-8", errors="ignore")
for line in t.splitlines():
    if 'Name="Supreme Collector of Waste"' in line and "RunSpeedBase=" in line:
        for k in ("RunSpeedBase=", "MonsterScale=", "Level=", "Health="):
            m = re.search(k + r"[0-9]+", line)
            print(m.group(0) if m else k + "?")
        break
