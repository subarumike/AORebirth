from pathlib import Path
import re
events = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\events.log").read_text(encoding="utf-8", errors="replace")
# capsule terminal detail blocks
for m in re.finditer(r".{0,80}Encrypted Info Capsule.{0,400}", events):
    print("---")
    print(m.group(0)[:500])
    print()

print("==== PICKUP CONTEXT ====")
idx = events.find("type=PickUp")
while idx >= 0:
    print(events[max(0,idx-200):idx+400])
    print("---")
    idx = events.find("type=PickUp", idx+1)
    if idx > 0 and events.find("type=PickUp", idx+1) < 0:
        break
    # only first few
    if events[:idx].count("type=PickUp") >= 3:
        break

print("==== 100361 SIFU snippets ====")
for m in re.finditer(r"SimpleItemFullUpdate[^\n]{0,200}100361[^\n]{0,200}|Identity=\(Terminal:57AC323C\)[^\n]{0,500}", events):
    print(m.group(0)[:600])
    print("---")
