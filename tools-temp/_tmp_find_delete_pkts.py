import csv
import sys

p = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-185306\raw-packets.csv"
n = 0
hits = 0
with open(p, newline="", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        n += 1
        name = row.get("N3TypeName") or ""
        hx = (row.get("RawHex") or "").upper()
        if name in ("CharacterAction", "Quest") and "DAC35556893A" in hx:
            hits += 1
            print(row["CapturedUtc"], row["Direction"], row["Sequence"], name, len(hx) // 2)
            print(hx)
            print("---")
        if name == "QuestFullUpdate" and "16:57:24" in row["CapturedUtc"]:
            hits += 1
            print(row["CapturedUtc"], row["Direction"], row["Sequence"], name, len(hx) // 2)
            print("markers 3C16/3C17/893A:", "DAC355563C16" in hx, "DAC355563C17" in hx, "DAC35556893A" in hx)
print("rows", n, "hits", hits, file=sys.stderr)
