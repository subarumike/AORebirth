import os, glob, socket, urllib.request

# Fix hosts trailing spaces
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
print("hosts fixed:")
for line in out:
    if "icc-rk" in line or "uwg.trade" in line or "aomarket" in line:
        print(repr(line))

for host in ("uwg.daily.icc-rk", "uwg.store.icc-rk", "uwg.trade.omni-rk"):
    try:
        print("DNS", host, "->", socket.gethostbyname(host))
    except Exception as e:
        print("DNS", host, e)

# Find Anarchy.exe
hits = []
for root in [r"C:\Users\nermi", r"C:\Program Files", r"C:\Program Files (x86)", r"D:\\", r"E:\\"]:
    if not os.path.isdir(root):
        continue
    for dirpath, dirnames, filenames in os.walk(root):
        # prune heavy
        base = os.path.basename(dirpath).lower()
        if base in ("windows", "node_modules", ".git", "appdata\\local\\temp", "temp", "packages"):
            dirnames[:] = []
            continue
        if "anarchy.exe" in [f.lower() for f in filenames]:
            for f in filenames:
                if f.lower() == "anarchy.exe":
                    hits.append(os.path.join(dirpath, f))
        # limit walk breadth for user profile
        if root.startswith(r"C:\Users") and dirpath.count(os.sep) - root.count(os.sep) > 5:
            dirnames[:] = [d for d in dirnames if d.lower() in ("funcom", "anarchy online", "games", "games", "steam", "steamapps", "common")]
print("Anarchy.exe hits", hits[:10])

# Check HTTPS connectivity to our hosts
for host in ("uwg.daily.icc-rk", "uwg.store.icc-rk"):
    for port in (80, 443):
        s = socket.socket()
        s.settimeout(1)
        try:
            s.connect(("127.0.0.1", port))
            print("port", host, port, "OPEN")
        except Exception as e:
            print("port", host, port, "closed", type(e).__name__)
        finally:
            s.close()

# Apache access log tail
log = r"C:\xampp\apache\logs\access.log"
if os.path.isfile(log):
    lines = open(log, encoding="utf-8", errors="replace").read().splitlines()
    print("access log last 15:")
    for line in lines[-15:]:
        print(line)
