import re
path = r"C:\Users\nermi\Desktop\mission level.txt"
out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_team_ranges.csv"
ranges = {}
with open(path, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        m = re.search(r"L (\d+): Team (\d+)-(\d+)", line)
        if m:
            ranges[int(m.group(1))] = (int(m.group(2)), int(m.group(3)))
with open(out, "w", encoding="utf-8") as f:
    for lvl in sorted(ranges):
        lo, hi = ranges[lvl]
        f.write("%d,%d,%d\n" % (lvl, lo, hi))
print("count=%d min=%s max=%s" % (len(ranges), min(ranges), max(ranges)))
