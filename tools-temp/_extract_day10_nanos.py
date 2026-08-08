# -*- coding: utf-8 -*-
from __future__ import print_function
import csv
import collections
import json
import os

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260808-043332"
shop = os.path.join(cap, "shop-updates.csv")
events = os.path.join(cap, "events.log")

# Order vendors were first opened (from npc-interactions / events).
order = []
seen = set()
with open(events, "r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if "ShopUpdate identity=" in line:
            # [IN-N3] #1 type=ShopUpdate identity=(VendingMachine:12FE8DA2)
            i = line.find("identity=")
            if i < 0:
                continue
            ident = line[i + len("identity="):].strip()
            if ident not in seen:
                seen.add(ident)
                order.append(ident)

by = collections.OrderedDict()
with open(shop, newline="", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        t = row["TerminalIdentity"]
        lid = int(row["LowId"])
        hid = int(row["HighId"])
        q = int(row["Quality"])
        by.setdefault(t, []).append({"lowId": lid, "highId": hid, "quality": q})

print("OPEN_ORDER", len(order))
for i, t in enumerate(order):
    items = by.get(t, [])
    uniq = sorted(set(x["lowId"] for x in items))
    qls = [x["quality"] for x in items]
    print("%02d %s count=%d unique=%d ql=%d-%d" % (
        i + 1, t, len(items), len(uniq), min(qls) if qls else 0, max(qls) if qls else 0))

# Dedup per vendor: keep (lowId, quality) unique
out = {}
for t, items in by.items():
    uniq = {}
    for it in items:
        key = (it["lowId"], it["quality"])
        uniq[key] = it
    out[t] = [uniq[k] for k in sorted(uniq)]

out_path = os.path.join(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp",
    "_day10_vendor_nanos.json")
with open(out_path, "w", encoding="utf-8") as f:
    json.dump({"openOrder": order, "vendors": out}, f, indent=2)
print("WROTE", out_path)
