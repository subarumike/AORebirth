# -*- coding: utf-8 -*-
import pathlib, csv, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-214622")
keys = ["credit", "Card", "297302", "297315", "57A4218D", "Leonora", "Marty", "15000", "FormatFeedback",
        "SimpleItem", "World", "pickup", "TemplateAction", "GenericCmd", "Use", "Overflow", "Quest",
        "Received reward", "Cash=", "Inventory"]

for name in ["mission-flow.log", "chat-dialogue.log", "system-messages.log", "npc-interactions.log", "events.log"]:
    f = p / name
    if not f.exists():
        continue
    print("=" * 70, name)
    n = 0
    for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
        low = line.lower()
        if any(k.lower() in low for k in keys):
            print(line[:400])
            n += 1
            if n >= 100:
                print("...truncated...")
                break
    if n == 0:
        print("(no hits)")

# inventory updates
inv = p / "inventory-updates.csv"
if inv.exists():
    print("=" * 70, "inventory-updates")
    with inv.open(encoding="utf-8-sig", newline="") as fh:
        for i, row in enumerate(csv.DictReader(fh)):
            print(row)
            if i > 40:
                break
