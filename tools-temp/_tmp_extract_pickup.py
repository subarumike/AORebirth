from pathlib import Path
import re
events = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\events.log").read_text(encoding="utf-8", errors="replace")

# All GenericCmd involving 57AC323C
print("=== GenericCmd 57AC323C ===")
for h in re.finditer(r"GenericCmdMessage \{[^\}]*57AC323C[^\}]*\}[^\n]*", events):
    print(h.group(0)[:400])

print("\n=== OUT GenericCmd near capsule ===")
for h in re.finditer(r"\[OUT[^\]]*\][^\n]*57AC323C[^\n]*", events):
    print(h.group(0)[:400])

print("\n=== pickup window 08:08 ===")
lines = events.splitlines()
for i, line in enumerate(lines):
    if "08:08:4" in line and "57AC323C" in line and "GenericCmd" in line:
        for j in range(max(0,i-15), min(len(lines), i+30)):
            if any(k in lines[j] for k in ["GenericCmd", "57AC323C", "100361", "OwnerType=50000", "ContainerAdd", "TemplateAction", "IN-MISSION-ACTION", "Action="]):
                print(lines[j][:350])
        print("---")
        break

# Also search Use on Terminal around 08:08
for i, line in enumerate(lines):
    if "08:08:" in line and "Action=Use" in line and "57AC323C" in line:
        print("HIT", lines[i][:350])
