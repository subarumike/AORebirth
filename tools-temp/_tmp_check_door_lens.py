import re
d = open(r"tools-temp/_tmp_doors_1441800.csfrag", encoding="utf-8").read()
hs = re.findall(r'"([0-9A-Fa-f]+)"', d)
print("n", len(hs))
print("lens", sorted({len(h)//2 for h in hs}))
print("head0", hs[0][:100] if hs else None)
c = open(r"tools-temp/_tmp_chests_1441800.csfrag", encoding="utf-8").read()
hs2 = re.findall(r'"([0-9A-Fa-f]+)"', c)
print("chests", len(hs2), sorted({len(h)//2 for h in hs2}))
