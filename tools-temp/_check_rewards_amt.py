import json
for path in [
    r"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json",
    r"C:\xampp\htdocs\daily\rewards.json",
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\rewards.json",
]:
    d = json.load(open(path))
    for day in ("3", "4", "17", "26"):
        e = d["days"][day]
        print(path.split("\\")[-2] if "xampp" in path or "icc-rk" in path else "repo", day, e.get("itemId"), e.get("amount"), e.get("qualityMode"))
