import json

paths = [
    r"tools-temp\icc-rk-local-web\daily\rewards.json",
    r"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json",
    r"C:\xampp\htdocs\daily\rewards.json",
]

for p in paths:
    with open(p, encoding="utf-8") as f:
        d = json.load(f)
    day = d["days"]["2"]
    day["amount"] = 50000
    day["note"] = "Day2 random ammo box x50000"
    img = day.get("image")
    if isinstance(img, str) and img.endswith("-1.png"):
        day["image"] = img.replace("-1.png", "-50000.png")
    with open(p, "w", encoding="utf-8") as f:
        json.dump(d, f, indent=2)
        f.write("\n")
    print(p, day["amount"], day.get("image"))
