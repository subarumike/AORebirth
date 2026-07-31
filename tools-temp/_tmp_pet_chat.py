import re
p = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260731-072612/events.log"
with open(p, encoding="utf-8", errors="ignore") as f:
    for line in f:
        if "NpcMessage" not in line or "'s pet," not in line:
            continue
        m = re.search(r'Text="([^"]+)"', line)
        if m:
            print(line[:26], m.group(1))
