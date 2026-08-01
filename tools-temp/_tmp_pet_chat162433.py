import pathlib, csv, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-162433")
for name in ["chat-dialogue.log", "chat.csv", "system-messages.csv"]:
    f = p / name
    print("===", name, "exists", f.exists())
    if not f.exists():
        continue
    text = f.read_text(encoding="utf-8", errors="replace")
    for line in text.splitlines():
        if any(x in line.lower() for x in ["charge", "follow", "bureaucrat", "pet", "system"]):
            print(line[:300])
