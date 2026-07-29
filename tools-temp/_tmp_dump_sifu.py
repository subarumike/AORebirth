import csv

path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-093557\raw-packets.csv"
needle = "57AC311D"
with open(path, newline="", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") != "SimpleItemFullUpdate":
            continue
        hx = (row.get("RawHex") or "").upper()
        if needle not in hx:
            continue
        print("utc", row.get("CapturedUtc"))
        print("HEX", hx)
        break

# kit 57AC311E
print("---KIT---")
needle = "57AC311E"
with open(path, newline="", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") != "SimpleItemFullUpdate":
            continue
        hx = (row.get("RawHex") or "").upper()
        if needle not in hx:
            continue
        print("utc", row.get("CapturedUtc"))
        print("HEX", hx)
        break
