hx = open(r"tools-temp/_tmp_tilda_hex_only.txt").read().strip()
parts = []
for i in range(0, len(hx), 100):
    parts.append('"' + hx[i : i + 100] + '"')
open(r"tools-temp/_tmp_tilda_csharp.txt", "w").write("\n+ ".join(parts))
print(len(hx) // 2, "bytes")
