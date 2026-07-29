import os, re, urllib.request

hosts = os.path.join(os.environ["SystemRoot"], "System32", "drivers", "etc", "hosts")
with open(hosts, "r", encoding="utf-8", errors="replace") as f:
    text = f.read()

fixed = text.replace("aomarket.funcom.com127.0.0.1", "aomarket.funcom.com\n127.0.0.1")
# ensure both entries exist on own lines
for host in ("uwg.daily.icc-rk", "uwg.store.icc-rk"):
    if not re.search(r"(?m)^\s*127\.0\.0\.1\s+" + re.escape(host) + r"\s*$", fixed):
        if host not in fixed:
            fixed = fixed.rstrip() + "\n127.0.0.1    " + host + "\n"

with open(hosts, "w", encoding="utf-8", newline="\n") as f:
    f.write(fixed)

print("hosts lines:")
for line in fixed.splitlines():
    if "aomarket" in line or "icc-rk" in line or "uwg.trade" in line:
        print(repr(line))

for url in ("http://uwg.daily.icc-rk/index.app", "http://uwg.store.icc-rk/index.app"):
    try:
        with urllib.request.urlopen(url, timeout=5) as r:
            body = r.read(200).decode("utf-8", "replace")
            print(url, r.status, body.split("<title>")[-1][:40] if "<title>" in body else body[:40])
    except Exception as e:
        print(url, "ERR", e)
