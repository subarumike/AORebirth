import csv
p = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-230406\scfu-appearance.csv"
with open(p, newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        ident = r.get("Identity") or ""
        if "7963A853" in ident.upper().replace("0X", ""):
            print("FOUND", ident, r.get("Name"), r.get("CharacterInfoType"))
        # also try numeric
        if "A853" in ident:
            print("A853", ident, r.get("Name"), r.get("CharacterInfoType"), r.get("PlayfieldId"))

# list first 20 unique identities
seen = set()
with open(p, newline="", encoding="utf-8-sig") as f:
    for r in csv.DictReader(f):
        i = r.get("Identity")
        if i in seen:
            continue
        seen.add(i)
        if len(seen) <= 30:
            print(i, r.get("Name"), r.get("CharacterInfoType"), r.get("PlayfieldId"))
