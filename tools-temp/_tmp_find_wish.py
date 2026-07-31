# -*- coding: utf-8 -*-
import pathlib, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
root = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures")
needle = "wish"
for d in sorted(root.iterdir()):
    if not d.is_dir():
        continue
    for name in ("chat-dialogue.log", "events.log"):
        f = d / name
        if not f.exists():
            continue
        # stream
        try:
            with f.open(encoding="utf-8", errors="replace") as fh:
                for i, line in enumerate(fh):
                    if needle in line.lower():
                        print(d.name, name, line[:250])
                        break
        except Exception as e:
            print("err", f, e)
