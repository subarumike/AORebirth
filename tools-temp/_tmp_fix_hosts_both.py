import os, socket, urllib.request

hosts = os.path.join(os.environ["SystemRoot"], "System32", "drivers", "etc", "hosts")
text = open(hosts, encoding="utf-8", errors="replace").read()
needed = ["uwg.daily.icc-rk", "uwg.store.icc-rk", "uwg.trade.omni-rk", "aomarket.funcom.com"]
lines = []
seen = set()
for raw in text.splitlines():
    s = raw.strip()
    if not s or s.startswith("#"):
        lines.append(raw.rstrip())
        continue
    parts = s.split()
    if len(parts) >= 2 and parts[0].startswith("127.0.0.1"):
        # one host per line
        for host in parts[1:]:
            host = host.strip()
            if not host:
                continue
            key = host.lower()
            if key in seen:
                continue
            seen.add(key)
            lines.append("127.0.0.1    " + host)
        continue
    lines.append(raw.rstrip())

for host in needed:
    if host.lower() not in seen:
        lines.append("127.0.0.1    " + host)
        seen.add(host.lower())

open(hosts, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
print("hosts now:")
for line in lines:
    if any(h in line for h in ("uwg.", "aomarket")):
        print(repr(line))

os.system("ipconfig /flushdns >nul")

for host in needed:
    try:
        print("DNS", host, "->", socket.gethostbyname(host))
    except Exception as e:
        print("DNS FAIL", host, e)

for host, title in (("uwg.daily.icc-rk", "Daily Login Rewards"), ("uwg.store.icc-rk", "Item Store")):
    try:
        with urllib.request.urlopen("http://%s/index.app" % host, timeout=5) as r:
            body = r.read(250).decode("utf-8", "replace")
            ok = title in body
            print("HTTP", host, r.status, "OK" if ok else "BAD", body[body.find("<title>"):body.find("</title>")+8] if "<title>" in body else "?")
    except Exception as e:
        print("HTTP FAIL", host, e)
