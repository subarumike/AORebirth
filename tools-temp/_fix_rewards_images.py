import json

paths = [
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\rewards.json",
    r"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json",
    r"C:\xampp\htdocs\daily\rewards.json",
]

fixes = {
    "3": ("291082", 50),
    "4": ("291043", 25),
    "17": ("291082", 50),
    "26": ("291043", 25),
}

for path in paths:
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    days = data.get("days") or {}
    for day, (item_id, amount) in fixes.items():
        entry = days.get(day)
        if not entry:
            continue
        entry["itemId"] = int(item_id)
        entry["amount"] = amount
        entry["qualityMode"] = "characterLevel"
        entry["image"] = "assets/cells/day%s-%s-%s.png" % (day, item_id, amount)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    print("updated", path)
