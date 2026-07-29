from pathlib import Path
import csv

path = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260715-194155\raw-packets.csv"
)
want = ("17:42:02.787", "17:42:32.882", "17:42:33.061", "17:42:34.119", "17:42:34.877")
with path.open(newline="", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row["N3TypeName"] != "CharacterAction":
            continue
        if not any(w in row["CapturedUtc"] for w in want):
            continue
        hx = row["RawHex"].upper()
        i = hx.find("5E477770")
        print(row["CapturedUtc"], row["Direction"], "len", len(hx) // 2)
        print(" full", hx)
        print(" body", hx[i + 8 :])
        print()
