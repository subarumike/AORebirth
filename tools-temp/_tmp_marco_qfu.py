# -*- coding: utf-8 -*-
import pathlib, csv, sys, re
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

c1 = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-212713")
c2 = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-212921")

# Extract QuestFullUpdate detail around Buy Nano
for cap in (c1, c2):
    print("="*60, cap.name)
    ev = (cap/"events.log").read_text(encoding="utf-8", errors="replace").splitlines()
    for i, line in enumerate(ev):
        if "QuestFullUpdate" in line or "Buy some Nano" in line or "5572F3B7" in line or "FormatFeedback" in line or "MissionItem" in line or "223373" in line or "ShopUpdate" in line or "Trade" in line and "78E0FC81" in line:
            print(line[:500])
        if "IN-QUEST-FULL" in line or "quest=(Mission" in line:
            print(line[:500])

# Look for QFU raw hex / longer detail in mission-flow
print("\n=== mission-flow full ===")
for cap in (c1, c2):
    print("---", cap.name)
    print((cap/"mission-flow.log").read_text(encoding="utf-8", errors="replace")[:8000])

# inventory around completion in c2
print("\n=== inventory / trade around reward ===")
with (c2/"events.log").open(encoding="utf-8", errors="replace") as fh:
    lines = fh.readlines()
# find FormatFeedback line index
idx = None
for i, line in enumerate(lines):
    if "Received reward: 2581" in line or "FormatFeedback" in line and "2581" in line:
        idx = i
        break
print("feedback idx", idx)
if idx is not None:
    for line in lines[max(0, idx-40):idx+30]:
        if any(x in line for x in ["Trade", "Shop", "Inventory", "TemplateAction", "DeleteItem", "Quest", "FormatFeedback", "Cash=", "XP=", "223373", "24825", "Container", "Overflow", "Action=59", "AddTemplate", "Chest"]):
            print(line[:400].rstrip())
