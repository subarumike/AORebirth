import re
from pathlib import Path

data = Path(r"C:\Funcom\Anarchy Online\GUI.dll").read_bytes()
# wider window around BrowserWindowConfig / aoshop / dailyrewards
for key in (b"BrowserWindowConfig", b"aoshop", b"dailyrewards", b"DailyLoginWindow", b"ShopWindow", b"vgtp://"):
    i = data.find(key)
    while i >= 0:
        start = max(0, i - 40)
        end = min(len(data), i + 500)
        chunk = data[start:end]
        printable = "".join(chr(b) if 32 <= b < 127 else "|" for b in chunk)
        print(f"\n===== {key.decode()} @{i} =====")
        print(printable)
        i = data.find(key, i + 1)
        if key == b"vgtp://" and i > 1844000:
            break
