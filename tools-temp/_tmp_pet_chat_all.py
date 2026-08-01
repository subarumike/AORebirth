# -*- coding: utf-8 -*-
import pathlib, csv, sys, re
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-164552")

print("=== ALL SystemMessage in chat-dialogue")
for line in (p/"chat-dialogue.log").read_text(encoding="utf-8", errors="replace").splitlines():
    if "SystemMessage" in line:
        print(line)

print("\n=== events with pet/chat")
ev = p/"events.log"
if ev.exists():
    for line in ev.read_text(encoding="utf-8", errors="replace").splitlines():
        if any(x in line for x in ["wish", "Deactivat", "Hello", "Charge", "wait", "follow", "SystemMessage", "PetCommand"]):
            print(line[:300])

# Decode PetCommand command ids from hex - command id appears as 0000000N near end
print("\n=== PetCommand decode")
# From earlier: ...00000001... = Follow, 04=Wait, 07=Attack
# Look for all PetCommands and nearby times vs chat

print("\n=== search wish/deactivat in whole capture dir text files")
for f in p.iterdir():
    if f.suffix.lower() not in {".log", ".csv", ".json", ".txt"}:
        continue
    try:
        text = f.read_text(encoding="utf-8", errors="replace")
    except Exception:
        continue
    if "wish" in text.lower() or "Deactivat" in text or "Hello master" in text:
        print("---", f.name)
        for line in text.splitlines():
            if any(x in line for x in ["wish", "Deactivat", "Hello master", "wait here", "Charge!", "follow you"]):
                print(line[:350])
