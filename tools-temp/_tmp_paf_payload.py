import csv

path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260728-093557\raw-packets.csv"
with open(path, newline="", encoding="utf-8-sig", errors="replace") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") != "PlayfieldAnarchyF":
            continue
        hx = (row.get("RawHex") or "").upper()
        if "D79A93" not in hx:
            continue
        print("utc", row.get("CapturedUtc"), "len", len(hx) // 2)
        print("FULL", hx)
        # find C79F00D79A93 and dump following bytes as candidate payload
        i = hx.find("00C79F00D79A93")
        print("payload start idx", i)
        if i >= 0:
            # existing payloads start with 0000C79F 00D7xxxx
            # here we have 00C79F00D79A93 - need leading 00?
            payload = "00" + hx[i:]  # try prepend
            # better: find 0000C79F
            j = hx.find("0000C79F00D79A93")
            print("alt idx", j)
            if j >= 0:
                # payload until end or until next major section
                print("PAYLOAD", hx[j:])
        break
