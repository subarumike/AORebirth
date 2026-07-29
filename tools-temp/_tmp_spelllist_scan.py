import csv
import os

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper-exect-nano"
for name in ("events.log", "system-messages.log", "raw-packets.csv"):
    p = os.path.join(cap, name)
    print("====", name, "exists", os.path.isfile(p))
    if not os.path.isfile(p):
        continue
    if name.endswith(".csv"):
        rows = list(csv.DictReader(open(p, encoding="utf-8-sig", errors="replace")))
        print("cols", list(rows[0].keys()) if rows else None, "count", len(rows))
        n = 0
        for r in rows:
            blob = " ".join(str(v) for v in r.values() if v)
            if any(k in blob for k in ("SpellList", "Ambient", "CF4A", "43BD71C7", "A871", "0xCF4A")):
                print("--- row")
                for k, v in r.items():
                    if not v:
                        continue
                    if k.lower() in ("rawpackethex", "payloadhex", "hex"):
                        print(k, "=", str(v)[:500])
                    elif len(str(v)) < 400:
                        print(k, "=", v)
                n += 1
                if n >= 20:
                    break
        print("matched", n)
    else:
        n = 0
        with open(p, encoding="utf-8", errors="replace") as f:
            for line in f:
                if any(k in line for k in ("SpellList", "Ambient Restoration", "CF4A", "43BD71C7", "A871")):
                    print(line.rstrip()[:500])
                    n += 1
                    if n >= 40:
                        break
        print("matched", n)
