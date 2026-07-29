import os, re

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper"
out = open(r"tools-temp\_tmp_keeper_urls.txt", "w", encoding="utf-8")

def p(*a):
    out.write(" ".join(str(x) for x in a) + "\n")

pat = re.compile(r"https?://[^\s\"'<>]+|www\.[^\s\"'<>]+|funcom|grid\.|/shop|/reward|/daily|startwindow|StartWindow", re.I)

for name in ("events.log", "chat-dialogue.log", "system-messages.log", "packets.hex.log"):
    path = os.path.join(cap, name)
    p("====", name)
    if not os.path.isfile(path):
        continue
    n = 0
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        for i, line in enumerate(f):
            if pat.search(line):
                p(line.rstrip()[:500])
                n += 1
                if n >= 50:
                    break
            # also search ascii urls in hex log by decoding? skip heavy
    p("matched", n)

# decode raw hex for ascii strings containing http
import csv
rows = list(csv.DictReader(open(os.path.join(cap, "raw-packets.csv"), encoding="utf-8-sig", errors="replace")))
p("==== raw ascii urls/strings")
n = 0
for r in rows:
    hx = r.get("RawHex") or ""
    if not hx:
        continue
    try:
        b = bytes.fromhex(hx)
    except Exception:
        continue
    # extract printable runs
    s = ""
    runs = []
    for ch in b:
        if 32 <= ch < 127:
            s += chr(ch)
        else:
            if len(s) >= 8:
                runs.append(s)
            s = ""
    if len(s) >= 8:
        runs.append(s)
    for run in runs:
        low = run.lower()
        if any(k in low for k in ("http", "shop", "reward", "gift", "daily", "funcom", "grid", "claim", "store", "market")):
            p(r.get("CapturedUtc"), r.get("N3TypeName"), run)
            n += 1
p("ascii matches", n)
out.close()
print("done")
