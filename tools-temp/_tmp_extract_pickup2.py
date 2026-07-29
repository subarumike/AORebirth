from pathlib import Path
import re
events = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\events.log").read_text(encoding="utf-8", errors="replace")
lines = events.splitlines()

# Find first line where 57AC323C has OwnerType=50000
for i, line in enumerate(lines):
    if "57AC323C" in line and "OwnerType=50000" in line:
        print("FIRST INVENTORY", i, line[:300])
        for j in range(max(0, i-40), i+5):
            l = lines[j]
            if any(k in l for k in ["GenericCmd", "57AC323C", "OUT-", "SMOKE] OUT", "TemplateAction", "ContainerAdd", "Trade", "Action="]):
                print(l[:360])
        break

print("\n=== all OUT GenericCmd with Action=Use around 08:08-08:10 ===")
for i, line in enumerate(lines):
    if ("08:08:" in line or "08:09:" in line or "08:10:0" in line or "08:10:1" in line or "08:10:2" in line) and "GenericCmd" in line and ("OUT" in line or "SMOKE] OUT" in line or "Action=Use" in line):
        if "765A6D34" in line or "OUT" in line:
            print(line[:360])

print("\n=== npc-interactions capsule ===")
npc = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\npc-interactions.log").read_text(encoding="utf-8", errors="replace")
for h in re.finditer(r"[^\n]*57AC323C[^\n]*", npc):
    print(h.group(0)[:400])
