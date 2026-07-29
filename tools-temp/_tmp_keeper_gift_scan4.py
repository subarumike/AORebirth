import csv, os, re

cap = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260722-keeper"
out = open(r"tools-temp\_tmp_keeper_gift_out3.txt", "w", encoding="utf-8")

def p(*a):
    out.write(" ".join(str(x) for x in a) + "\n")

# Full events around gift claim 04:30:45-04:30:50
p("==== events around gift claim")
with open(os.path.join(cap, "events.log"), encoding="utf-8-sig", errors="replace") as f:
    for line in f:
        if "04:30:4" in line or "04:30:5" in line[:30]:
            if any(x in line for x in ("04:30:45", "04:30:46", "04:30:47", "04:30:48", "04:30:49", "04:30:50")):
                p(line.rstrip()[:500])

# All OUT packets before 04:31
p("==== all OUT early")
rows = list(csv.DictReader(open(os.path.join(cap, "raw-packets.csv"), encoding="utf-8-sig", errors="replace")))
for r in rows:
    if r.get("Direction") != "OUT":
        continue
    utc = r.get("CapturedUtc") or ""
    if utc > "2026-07-22T04:31:40":
        break
    p(utc, r.get("N3TypeName"), "idType", r.get("IdentityType"), "inst", r.get("IdentityInstance"), "len", r.get("PacketLength"))
    hx = r.get("RawHex") or ""
    if hx:
        p(" ", hx[:300])

# Look for Marketrader / Terminal / Vending / CanbeAffected with shop-like
p("==== CHAR-SEEN all")
with open(os.path.join(cap, "events.log"), encoding="utf-8-sig", errors="replace") as f:
    for line in f:
        if "[CHAR-SEEN]" in line:
            p(line.rstrip()[:350])

out.close()
print("done")
