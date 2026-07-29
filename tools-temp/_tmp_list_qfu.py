import re
path = r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-224228\packets.hex.log"
rows = []
with open(path, "r", encoding="utf-8", errors="ignore") as f:
    for line in f:
        if "n3=QuestFullUpdate" not in line:
            continue
        m = re.search(r"len=(\d+).*hex=([0-9A-Fa-f]+)", line)
        if not m:
            continue
        ln = int(m.group(1))
        h = m.group(1)
        hx = m.group(2).upper()
        icon = "2C47" if "00002C47" in hx else ("2C42" if "00002C42" in hx else "?")
        # count icons
        c47 = hx.count("00002C47")
        c42 = hx.count("00002C42")
        rows.append((ln, icon, c47, c42, hx[:40]))

for r in rows:
    print(r)
print("total", len(rows))
