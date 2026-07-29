from pathlib import Path
events = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215\events.log").read_text(encoding="utf-8", errors="replace")
# find ContainerAddItem with Terminal source near pickup
needle = "source=(Terminal:57AC323C)"
idx = events.find(needle)
chunk = events[max(0, idx-2500):idx+1200]
Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_pickup_chunk.txt").write_text(chunk, encoding="utf-8")
print("wrote", len(chunk))

# also OUT-N3 PickUp messages with detail
import re
for m in re.finditer(r"\[OUT-N3\][^\n]*PickUp[^\n]*\n(?:\[OUT-N3-DETAIL\][^\n]*\n)?", events):
    print(m.group(0)[:400])
# CharacterAction 146
for m in re.finditer(r"CharacterAction[^\n]{0,200}146[^\n]{0,200}|Action=146[^\n]{0,300}", events):
    print("CA146:", m.group(0)[:350])
    break
# GenericCmd targeting terminal
for m in re.finditer(r"GenericCmd[^\n]{0,120}57AC323C[^\n]{0,200}|Target=\(Terminal:57AC323C\)[^\n]{0,300}", events):
    print("GC:", m.group(0)[:400])
