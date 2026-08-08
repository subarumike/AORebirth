import json
p = r"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json"
d = json.load(open(p, encoding="utf-8"))
for day in ("3", "4", "14", "17", "26"):
    e = d["days"][day]
    pool = e.get("randomItemIds") or []
    print(
        day,
        "id", e.get("itemId"),
        "amt", e.get("amount"),
        "ql", e.get("quality"),
        "mode", e.get("qualityMode"),
        "name", e.get("itemName"),
        "img", e.get("image"),
        "pool", len(pool),
    )
