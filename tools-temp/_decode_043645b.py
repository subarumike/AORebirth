import csv
from pathlib import Path

cap = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260729-043645")

# Feedback / ChatText / system
for name in ("system-messages.log", "chat-dialogue.log", "events.log"):
    p = cap / name
    if not p.exists():
        continue
    print("====", name, "====")
    for line in p.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        low = line.lower()
        if any(k in low for k in ("high", "low", "level", "team", "xp", "invite", "feedback", "format")):
            print(line[:350])

# OUT packets only summary
rows = list(csv.DictReader((cap / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
print("==== ALL OUT ====")
for i, r in enumerate(rows):
    if r.get("Direction") != "OUT":
        continue
    print(i, r.get("CapturedUtc"), r.get("N3TypeName"), "id", r.get("IdentityInstance"))

# XP window check 175 vs 60
# from TeamXpShareWindow table key 175 -> 133-220
print("==== level window ====")
print("Marokanac 175 window ~133-220; Ziziadw 60 is BELOW min (too low, not too high)")
