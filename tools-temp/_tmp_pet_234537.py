# -*- coding: utf-8 -*-
import pathlib, csv, re, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260730-234537")
print("exists", p.exists(), "files", sorted(x.name for x in p.iterdir())[:40])

for fname in ["chat-dialogue.log", "system-messages.log", "events.log"]:
    f = p / fname
    if not f.exists():
        print("missing", fname)
        continue
    print("\n===", fname, "===")
    n = 0
    for line in f.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        if any(k in line for k in ("SystemMessage", "pet", "Pet", "Charge", "follow", "wait", "protect", "stay out", "Many tasks", "Health:", "ChatText", "Bureaucrat")):
            print(line[:350])
            n += 1
            if n >= 40:
                break
    print("hits", n)
