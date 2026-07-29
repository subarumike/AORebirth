from pathlib import Path
import re

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215")

# Extract packet hex for Terminal 57AC323C from packets.hex.log / raw-packets
for fname in ["packets.hex.log", "raw-packets.csv"]:
    p = cap / fname
    text = p.read_text(encoding="utf-8", errors="replace")
    print("===", fname, "size", len(text), "hits", text.count("57AC323C"), "100361", text.upper().count("00018809") if "00018809" else 0)
    # 100361 = 0x18809
    print("18809 count", text.upper().count("00018809"), "C73D57AC323C", text.upper().count("C73D57AC323C"))

# Find hex lines containing the terminal identity
hexlog = (cap / "packets.hex.log").read_text(encoding="utf-8", errors="replace")
# lines with C73D57AC323C (Terminal type 0xC73D)
hits = []
for line in hexlog.splitlines():
    u = line.upper().replace(" ", "")
    if "C73D57AC323C" in u or "57AC323C" in u:
        hits.append(line)
print("hexlog hits", len(hits))
for h in hits[:8]:
    print(h[:400])
    print("---")

# Also check raw-packets for SIFU around that time
raw = (cap / "raw-packets.csv").read_text(encoding="utf-8", errors="replace")
# find rows mentioning SimpleItem and 57AC323C
for line in raw.splitlines():
    if "57AC323C" in line.upper() or "57ac323c" in line:
        print("RAW:", line[:500])
        break

# events detail around first SIFU of capsule - look for Action=146 CharacterAction
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")
idx = events.find("Encrypted Info Capsule")
# find PickUp near ContainerAddItem source Terminal
idx2 = events.find("source=(Terminal:57AC323C)")
print("\nCONTAINER ADD context:")
print(events[max(0,idx2-1500):idx2+800])
