import os, socket

hosts = os.path.join(os.environ["SystemRoot"], "System32", "drivers", "etc", "hosts")
lines = open(hosts, encoding="utf-8", errors="replace").read().splitlines()
out = []
for line in lines:
    if "uwg.daily.icc-rk" in line or "uwg.store.icc-rk" in line:
        parts = line.split()
        if len(parts) >= 2:
            out.append(parts[0] + "    " + parts[1])
            continue
    out.append(line.rstrip())
open(hosts, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("hosts:")
for line in out:
    if "icc-rk" in line or "uwg.trade" in line or "aomarket" in line:
        print(repr(line))

for host in ("uwg.daily.icc-rk", "uwg.store.icc-rk", "uwg.trade.omni-rk"):
    print("DNS", host, "->", socket.gethostbyname(host))

for port in (80, 443, 8181):
    s = socket.socket(); s.settimeout(1)
    try:
        s.connect(("127.0.0.1", port)); print("127.0.0.1:"+str(port), "OPEN")
    except Exception as e:
        print("127.0.0.1:"+str(port), "closed")
    finally:
        s.close()

# Prefer known Funcom dims file paths / Prefs for install hints
prefs = [
    r"C:\Users\nermi\AppData\Local\Funcom\Anarchy Online\70dad3e6\Anarchy Online\Prefs\Prefs.xml",
    r"C:\Users\nermi\AppData\Local\Funcom\Anarchy Online\c91ef40a\client\Prefs\Prefs.xml",
]
for p in prefs:
    if os.path.isfile(p):
        t = open(p, encoding="utf-8", errors="replace").read()
        print("PREF", p, "len", len(t))
        for key in ("Path", "Install", "Dir", "Web", "Browser", "URL", "Host", "vgtp", "store", "daily"):
            if key.lower() in t.lower():
                print(" contains", key)

# quick look in common steam path
steam = r"C:\Program Files (x86)\Steam\steamapps\common"
if os.path.isdir(steam):
    print("steam commons", os.listdir(steam)[:30])

log = r"C:\xampp\apache\logs\access.log"
lines = open(log, encoding="utf-8", errors="replace").read().splitlines()
print("access last 10:")
for line in lines[-10:]:
    print(line)
