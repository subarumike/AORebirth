import csv
from pathlib import Path

root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")

def show(name, idxs):
    rows = list(csv.DictReader((root / name / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))
    print("====", name, "====")
    keys = rows[0].keys() if rows else []
    print("cols:", list(keys)[:12])
    for idx in idxs:
        r = rows[idx]
        ts = r.get("Timestamp") or r.get("Time") or r.get("RelativeMs") or r.get("ElapsedMs") or "?"
        # find any time-like
        for k in keys:
            if "time" in k.lower() or "ms" in k.lower() or "stamp" in k.lower():
                ts = "%s=%s" % (k, r.get(k))
                break
        print("#%d %s %s %s id=%s" % (idx, ts, r.get("Direction"), r.get("N3TypeName"), r.get("IdentityInstance")))

show("20260729-010948", [81, 91, 94, 95, 97])
show("20260729-010949", [55, 60, 66, 68])

# Also print first row keys and sample
r0 = list(csv.DictReader((root / "20260729-010949" / "raw-packets.csv").open(encoding="utf-8-sig", newline="")))[0]
print("all keys:", list(r0.keys()))
