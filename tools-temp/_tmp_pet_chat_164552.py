# -*- coding: utf-8 -*-
import pathlib, csv, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
p = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260730-164552")
print("=== chat-dialogue.log")
log = p / "chat-dialogue.log"
if log.exists():
    for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
        if "pet" in line.lower() or "SystemMessage" in line or "Charge" in line or "master" in line or "Deactivat" in line or "wait" in line.lower() or "follow" in line.lower() or "Hello" in line or "wish" in line.lower():
            print(line[:400])
print("=== system-messages.log")
sm = p / "system-messages.log"
if sm.exists():
    for line in sm.read_text(encoding="utf-8", errors="replace").splitlines():
        if "pet" in line.lower() or "master" in line.lower() or "Charge" in line or "Deactivat" in line or "wish" in line.lower() or "Hello" in line or "wait" in line.lower():
            print(line[:400])
print("=== PetCommand from raw")
with (p/"raw-packets.csv").open(encoding="utf-8-sig", newline="") as fh:
    for row in csv.DictReader(fh):
        if row.get("N3TypeName") == "PetCommand" or "PetCommand" in (row.get("N3TypeName") or ""):
            print(row.get("CapturedUtc"), row.get("N3TypeName"), (row.get("RawHex") or "")[:120])
