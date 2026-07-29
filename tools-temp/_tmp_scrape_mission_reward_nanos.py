# Scrape aogalaxy Mission Reward nano names (server-rendered tables).
from __future__ import print_function
import html
import re
import urllib.request

profs = list(range(1, 16))
locs = [
    "Mission+Reward",
    "Mission+Reward+%2F+Inferno+Garden",
    "Mission+Reward+%2F+Penumbra+Garden",
]
names = set()
ql_by_name = {}
href_pat = re.compile(
    r'<a[^>]*href=(["\'])([^"\']+)\1[^>]*>([^<]+)</a>',
    re.I,
)

for pid in profs:
    for loc in locs:
        url = "https://www.aogalaxy.com/nanos?aoProfID=%d&aoLoc=%s" % (pid, loc)
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
            data = urllib.request.urlopen(req, timeout=45).read().decode("utf-8", "replace")
        except Exception as e:
            print("fail", pid, loc, e)
            continue

        rows = re.split(r"<tr[^>]*>", data, flags=re.I)
        hit = 0
        for row in rows:
            if "Mission Reward" not in row:
                continue
            n = None
            for a in href_pat.finditer(row):
                txt = html.unescape(a.group(3)).strip()
                if not txt or "Collapse" in txt or txt.startswith("AO "):
                    continue
                n = txt
                break
            if not n:
                m = re.search(r"<td[^>]*>\s*([^<]{2,80})\s*<", row)
                if m:
                    n = html.unescape(m.group(1)).strip()
            if not n or n in ("Name", "Location", "Collapse All") or len(n) > 90:
                continue
            names.add(n)
            hit += 1
            nums = [int(x) for x in re.findall(r">(\d{1,3})<", row)]
            if nums:
                ql_by_name[n] = max(ql_by_name.get(n, 0), max(nums))
        print("pid", pid, "loc", loc, "rows", hit, "total", len(names))

print("TOTAL", len(names))
out = r"tools-temp/_tmp_mission_reward_nanos.txt"
with open(out, "w", encoding="utf-8") as f:
    f.write("\n".join(sorted(names)))
with open(r"tools-temp/_tmp_mission_reward_nanos_ql.txt", "w", encoding="utf-8") as f:
    for k in sorted(names):
        f.write("%s\t%s\n" % (k, ql_by_name.get(k, "")))
print("wrote", out)
