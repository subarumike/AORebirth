# -*- coding: utf-8 -*-
from __future__ import print_function
import re
import os

html_path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_aog_proto.html"
out_path = r"C:\Users\nermi\Desktop\zadaily\prototyprsymb.txt"

with open(html_path, "rb") as f:
    raw = f.read()
html = raw.decode("utf-8", "replace")

# Show HTML around first Prototype Brain Symbiant link
m = re.search(r'item\.php\?aoid=305057[^>]*>.*?</tr>', html, re.I | re.S)
if not m:
    m = re.search(r'item\.php\?aoid=305057.{0,800}', html, re.I | re.S)
print("--- SAMPLE ---")
print(m.group(0)[:800] if m else "no sample")

# Try row-based: <tr>...aoid...name...</tr>
row_pat = re.compile(r"<tr[^>]*>\s*(.*?)\s*</tr>", re.I | re.S)
aoid_pat = re.compile(r'item\.php\?aoid=(\d+)[^>]*>\s*([^<]+)\s*<', re.I)
# quality often in a <td>NN</td>
td_nums = re.compile(r"<td[^>]*>\s*(\d{1,3})\s*</td>", re.I)

rows_out = []
for row in row_pat.findall(html):
    am = aoid_pat.search(row)
    if not am:
        continue
    aoid = int(am.group(1))
    name = re.sub(r"\s+", " ", am.group(2)).strip()
    if "prototype" not in name.lower() or "symbiant" not in name.lower():
        continue
    nums = [int(x) for x in td_nums.findall(row)]
    # Prefer 1 or 250 typical prototype range; else last td number
    ql = None
    for n in nums:
        if n in (1, 250, 200, 300):
            ql = n
            break
    if ql is None and nums:
        ql = nums[-1]
    if ql is None:
        continue
    rows_out.append((name, ql, aoid))

print("row parse count", len(rows_out))
for r in rows_out[:8]:
    print(r)
for r in rows_out[-4:]:
    print("tail", r)

# Fallback if row parse weak: consecutive same-name pairs QL1 then QL250 by aoid order
if len(rows_out) < 100:
    print("FALLBACK consecutive pair heuristic")
    links = aoid_pat.findall(html)
    items = []
    for aoid_s, name in links:
        name = re.sub(r"\s+", " ", name).strip()
        if "prototype" not in name.lower() or "symbiant" not in name.lower():
            continue
        items.append((int(aoid_s), name))
    # Group by name preserving order
    from collections import OrderedDict
    by_name = OrderedDict()
    for aoid, name in items:
        by_name.setdefault(name, []).append(aoid)
    rows_out = []
    for name, aoids in by_name.items():
        # Unique aoids keep order
        seen = []
        for a in aoids:
            if a not in seen:
                seen.append(a)
        if len(seen) == 2:
            # lower aoid often QL1, higher QL250 on aogalaxy listing order
            rows_out.append((name, 1, seen[0]))
            rows_out.append((name, 250, seen[1]))
        elif len(seen) == 1:
            rows_out.append((name, 1, seen[0]))
        else:
            # more than 2 — assign first QL1, second 250, rest unknown skip extras keep listed
            rows_out.append((name, 1, seen[0]))
            rows_out.append((name, 250, seen[1]))
    print("fallback count", len(rows_out))

os.makedirs(os.path.dirname(out_path), exist_ok=True)
with open(out_path, "w", encoding="utf-8", newline="\n") as f:
    f.write("Name\tQL\tID\n")
    f.write("Source: https://www.aogalaxy.com/_items/index.php?searchBy=name&itemName=prototype+symbiant\n")
    f.write("Filter: Prototype Symbiants only (Implant), not other Prototype items\n\n")
    for name, ql, aoid in rows_out:
        f.write("%s\t%s\t%s\n" % (name, ql, aoid))
print("WROTE", out_path, "lines", len(rows_out))
