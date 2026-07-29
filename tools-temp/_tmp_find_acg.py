import csv

path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-093557\raw-packets.csv"
needle = "00D79A93"
hits = 0
with open(path, newline="", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        hx = (row.get("RawHex") or "").upper()
        if needle not in hx and "D79A93" not in hx:
            continue
        hits += 1
        if hits <= 8:
            print(row.get("CapturedUtc"), row.get("N3TypeName"), row.get("Direction"), "len", len(hx) // 2)
            # print snippet around building id
            i = hx.find("D79A93")
            if i < 0:
                i = hx.find(needle)
            print("  around", hx[max(0, i - 16) : i + 48])
print("total hits", hits)
