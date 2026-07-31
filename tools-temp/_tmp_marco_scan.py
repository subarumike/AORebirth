# -*- coding: utf-8 -*-
import pathlib, csv, sys, re
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

caps = [
    pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-212713"),
    pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-212921"),
]

keys = [
    "Marco", "Nano", "Buy some", "Spida", "Stanley", "Container", "Quest", "mission",
    "credit", "XP", "experience", "reward", "555BE9", "Shop", "Vendor", "Trade",
    "1160", "2581", "TemplateAction", "QuestFullUpdate", "Cash", "Item"
]

for cap in caps:
    print("=" * 80)
    print(cap.name)
    for name in ["mission-flow.log", "chat-dialogue.log", "system-messages.log", "npc-interactions.log", "events.log"]:
        f = cap / name
        if not f.exists():
            continue
        print(f"\n--- {name} ---")
        lines = f.read_text(encoding="utf-8", errors="replace").splitlines()
        n = 0
        for line in lines:
            low = line.lower()
            if any(k.lower() in low for k in keys):
                print(line[:350])
                n += 1
                if n >= 80:
                    print("...truncated...")
                    break
        if n == 0:
            print("(no keyword hits)")

    # shop/vendor
    for name in ["shop-updates.csv", "vendor-full-updates.csv", "inventory-updates.csv"]:
        f = cap / name
        if not f.exists():
            continue
        print(f"\n--- {name} head ---")
        with f.open(encoding="utf-8-sig", newline="") as fh:
            r = csv.DictReader(fh)
            cols = r.fieldnames
            print("cols", cols)
            for i, row in enumerate(r):
                s = str(row)
                if any(k.lower() in s.lower() for k in ["nano", "marco", "container", "12E772", "spida"] + [str(x) for x in range(40000, 160000, 1000)]):
                    print({k: row.get(k) for k in (cols or []) if row.get(k)})
                if i > 40 and name != "inventory-updates.csv":
                    break
