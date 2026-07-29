import json
import re

src = r"C:\Users\nermi\.cursor\browser-logs\cdp-response-Runtime.evaluate-2026-07-24T09-20-44-262Z.json"
out = r"AORebirth\Server\ZoneEngine\XML Data\MissionRewards\MissionRewardNanoNames.txt"

def find_names(obj):
    if isinstance(obj, dict):
        if "names" in obj and isinstance(obj["names"], list):
            return obj["names"]
        for value in obj.values():
            found = find_names(value)
            if found is not None:
                return found
    return None

data = json.load(open(src, encoding="utf-8"))
raw = find_names(data)
cleaned = set()
for name in raw:
    s = (name or "").strip()
    s = re.sub(r"\s+Spec:\d+$", "", s, flags=re.I)
    s = re.sub(r"\s+FP$", "", s, flags=re.I)
    s = re.sub(r"\s+", " ", s).strip()
    if s:
        cleaned.add(s)

lines = sorted(cleaned)
with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")

print("wrote", len(lines), "to", out)
print("sample:", lines[:8])
print("has FP leftover:", any(x.endswith(" FP") or x.endswith("FP") for x in lines[:50]))
