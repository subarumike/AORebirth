import csv

path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-093557\raw-packets.csv"
# Terminal:57AC311D instance = 0x57AC311D
needle = "57AC311D"
hits = []
with open(path, newline="", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        hx = (row.get("RawHex") or "").upper()
        if needle not in hx:
            continue
        hits.append((row.get("CapturedUtc"), row.get("N3TypeName"), row.get("Direction"), hx))
print("hits", len(hits))
for h in hits[:12]:
    print(h[0], h[1], h[2], "len", len(h[3]) // 2)
# also 100348 template
print("---100348---")
n2 = 0
with open(path, newline="", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        hx = (row.get("RawHex") or "").upper()
        if "000187BC" in hx or "100348" in (row.get("N3TypeName") or ""):  # 100348=0x187BC
            if "000187BC" in hx:
                n2 += 1
                if n2 <= 5:
                    print(row.get("CapturedUtc"), row.get("N3TypeName"), len(hx) // 2)
print("187BC hits", n2)
