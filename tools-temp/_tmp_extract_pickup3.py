from pathlib import Path
events = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\events.log").read_text(encoding="utf-8", errors="replace")
lines = events.splitlines()

print("=== all 57AC323C lines ===")
for i, line in enumerate(lines):
    if "57AC323C" in line:
        print(f"{i}: {line[:280]}")

print("\n=== Action=146 context ===")
for i, line in enumerate(lines):
    if "Action=146" in line and "57AC323C" in line:
        for j in range(max(0,i-20), min(len(lines), i+25)):
            l = lines[j]
            if any(k in l for k in ["OUT-N3", "GenericCmd", "57AC323C", "Action=146", "OwnerType", "Despawn", "Container", "Trade", "TemplateAction", "IN-MISSION"]):
                print(l[:340])
        print("---")
