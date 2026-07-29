from pathlib import Path
import re

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215")
replay = (cap / "mission-flow.replay.log").read_text(encoding="utf-8", errors="replace")
events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")

# Accepted offers
for mid in ["556D768E", "556D76DD", "556D7692", "556D76DE"]:
    print(f"\n===== OFFER/QUEST {mid} =====")
    for m in re.finditer(rf"mission=\(Mission:{mid}\)[^\n]*", replay):
        print(m.group(0)[:900])
        print("---")

# Finish window around UseItemOnItem count=129
print("\n===== FINISH WINDOW (replay) =====")
lines = replay.splitlines()
for i, line in enumerate(lines):
    if "count=129" in line or "UseItemOnItem" in line and "C0020320" in line:
        for j in range(max(0, i - 2), min(len(lines), i + 25)):
            print(lines[j][:350])
        print("=====")

# Item templates around finish / pickup in instance
print("\n===== EVENTS: SimpleItem on mission PFs / finish =====")
for pat in [
    r"playfieldId=1492999[^\n]{0,200}StaticInstance=\d+[^\n]{0,120}",
    r"PlayfieldId=1492999[^\n]{0,300}StaticInstance=\d+[^\n]{0,200}",
    r"PlayfieldId=1468417[^\n]{0,300}StaticInstance=\d+[^\n]{0,200}",
    r"Identity=\(Terminal:57AC323C\)[^\n]*",
    r"StaticInstance=100351[^\n]*",
    r"Inventory:0048[^\n]*",
    r"Inventory:48[^\n]*",
    r"awarded a token[^\n]*",
    r"Received[^\n]{0,200}",
    r"FormatFeedback[^\n]{0,250}",
    r"TemplateAction[^\n]{0,250}",
]:
    hits = re.findall(pat, events)
    if hits:
        print(f"\n## {pat} ({len(hits)})")
        for h in hits[:5]:
            print(h[:450])

# SCFU player spawn in instances
print("\n===== SPAWN / ACG =====")
for pat in [
    r"PLAYFIELD-INIT\] (\d+)",
    r"ACGBuildingGeneratorData:([A-F0-9]+)",
    r"PlayfieldId=1492999[^\n]{0,80}Position=\([^)]+\)[^\n]{0,40}Name=\"Engynera\"",
    r"PlayfieldId=1468417[^\n]{0,80}Position=\([^)]+\)[^\n]{0,40}Name=\"Engynera\"",
    r"SimpleItemFullUpdateMessage[^\n]*100351[^\n]*",
    r"SimpleItemFullUpdateMessage[^\n]*57AC323C[^\n]*",
    r"ContainerAddItem[^\n]*",
]:
    hits = re.findall(pat, events)
    if hits:
        print(f"\n## {pat} ({len(hits)})")
        for h in hits[:8]:
            print(h[:500] if isinstance(h, str) else h)
