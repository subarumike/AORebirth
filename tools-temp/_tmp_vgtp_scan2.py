import os, csv, re

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-093728"
out = open(r"tools-temp\_tmp_vgtp_out2.txt", "w", encoding="utf-8")

def p(*a):
    out.write(" ".join(str(x) for x in a) + "\n")

keys = ("vgtp", "uwg", "daily", "store", "index.app", "icc-rk", "Item Store", "Login Reward", "Claim Items", "browser", "http")

for name in ("events.log", "system-messages.log", "chat-dialogue.log", "npc-interactions.log", "mission-flow.log"):
    path = os.path.join(cap, name)
    p("====", name)
    if not os.path.isfile(path):
        continue
    n = 0
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        for line in f:
            low = line.lower()
            if any(k.lower() in low for k in keys):
                p(line.rstrip()[:400])
                n += 1
                if n >= 40:
                    break
    p("matched", n)

# scan ALL raw for printable strings containing store/daily/uwg/app
rows = list(csv.DictReader(open(os.path.join(cap, "raw-packets.csv"), encoding="utf-8-sig", errors="replace")))
p("==== raw printable interesting")
n = 0
seen = set()
for r in rows:
    hx = r.get("RawHex") or ""
    if not hx:
        continue
    try:
        b = bytes.fromhex(hx)
    except Exception:
        continue
    s = ""
    runs = []
    for ch in b:
        if 32 <= ch < 127:
            s += chr(ch)
        else:
            if len(s) >= 6:
                runs.append(s)
            s = ""
    if len(s) >= 6:
        runs.append(s)
    for run in runs:
        low = run.lower()
        if any(k in low for k in ("vgtp", "uwg", "daily", "store", "index.app", "icc-rk", "reward", "claim")):
            if run in seen:
                continue
            seen.add(run)
            p(r.get("CapturedUtc"), r.get("N3TypeName"), run)
            n += 1
p("hits", n)

# capture_info
import json
info = json.load(open(os.path.join(cap, "capture_info.json"), encoding="utf-8-sig"))
p("==== capture_info character", info.get("characterName"), "pf", info.get("playfieldId"))
p("packetCounts keys sample", list((info.get("packetCounts") or {}).keys())[:20])

out.close()
print("done")
