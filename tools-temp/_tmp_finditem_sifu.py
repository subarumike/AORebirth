from pathlib import Path
import re, struct

cap = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-095215")
# Prefer packet dumps if present
for name in ["packets.hex", "n3.hex", "wire.log", "events.log"]:
    p = cap / name
    print(name, "exists", p.exists(), "size", p.stat().st_size if p.exists() else 0)

events = (cap / "events.log").read_text(encoding="utf-8", errors="replace")
# find SimpleItemFullUpdate around Terminal:57AC323C with detail
for m in re.finditer(r"\[IN-N3\][^\n]*SimpleItemFullUpdate[^\n]*57AC323C[^\n]*", events):
    start = m.start()
    print("LINE:", m.group(0)[:300])
    # print following detail lines
    chunk = events[start:start+2500]
    print(chunk[:2000])
    print("====")

# look for hex dumps of that identity
for pat in [r"57AC323C[0-9A-Fa-f]{20,}", r"identity=\(Terminal:57AC323C\).{0,800}"]:
    hits = list(re.finditer(pat, events, flags=re.S))
    print("pat", pat, "hits", len(hits))
    if hits:
        print(hits[0].group(0)[:800])

# list capture files
print("files:", sorted(x.name for x in cap.iterdir())[:40])
