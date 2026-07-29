from pathlib import Path
from datetime import datetime

log = Path(r"C:\xampp\apache\logs\access.log")
lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
print("access.log lines", len(lines))
print("--- last 50 ---")
for line in lines[-50:]:
    print(line)

print("--- last icc-rk / trade host hits ---")
needles = ("uwg.store", "uwg.daily", "uwg.trade", "aomarket", "icc-rk")
hits = [ln for ln in lines if any(n in ln.lower() for n in needles)]
for ln in hits[-40:]:
    print(ln)
