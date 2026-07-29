from pathlib import Path

log = Path(r"C:\xampp\apache\logs\access.log")
lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
print("total", len(lines))
print("--- last 30 ---")
for ln in lines[-30:]:
    print(ln)

print("--- Awesomium hits today ---")
for ln in lines:
    if "Awesomium" in ln and ("22/Jul/2026" in ln or "Jul/2026:10" in ln or "Jul/2026:09" in ln or "Jul/2026:1" in ln):
        if any(x in ln for x in ("shop", "store", "daily", "market", "aoshop", "index.app", "22/Jul")):
            print(ln)

print("--- any Awesomium after 10:10 ---")
for ln in lines:
    if "Awesomium" in ln and "22/Jul/2026:10:" in ln:
        print(ln)
