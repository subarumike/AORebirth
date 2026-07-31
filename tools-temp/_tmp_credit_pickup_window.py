# -*- coding: utf-8 -*-
import pathlib, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-214622")
# Full pickup window
lines = (p/"events.log").read_text(encoding="utf-8", errors="replace").splitlines()
# find pickup feedback
for i, line in enumerate(lines):
    if "pick up the credit card" in line.lower() or "57A9CCBE" in line or "297302" in line or "297315" in line:
        start = max(0, i-5)
        end = min(len(lines), i+25)
        print("--- around", i)
        for j in range(start, end):
            if any(x in lines[j] for x in ["57A9CCBE", "297302", "297315", "credit", "Credit", "Quest", "FormatFeedback", "TemplateAction", "GenericCmd", "Despawn", "Delete", "Overflow", "Cash", "Inventory", "SimpleItem", "DYNEL"]):
                print(lines[j][:450])
        print()
