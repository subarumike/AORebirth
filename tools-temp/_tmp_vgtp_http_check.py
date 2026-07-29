import urllib.request

tests = [
    ("uwg.daily.icc-rk", "http://uwg.daily.icc-rk/index.app"),
    ("uwg.store.icc-rk", "http://uwg.store.icc-rk/index.app"),
    ("uwg.trade.omni-rk", "http://uwg.trade.omni-rk/index.app"),
    ("127 daily Host", "http://127.0.0.1/index.app"),
]

for label, url in tests[:3]:
    req = urllib.request.Request(url)
    try:
        with urllib.request.urlopen(req, timeout=5) as r:
            body = r.read(300).decode("utf-8", "replace")
            print(label, r.status, "ct=", r.headers.get("Content-Type"), "title=", body[body.find("<title>"):body.find("</title>")+8] if "<title>" in body else body[:80].replace("\n"," "))
    except Exception as e:
        print(label, "ERR", e)

# Host header to 127
for host in ("uwg.daily.icc-rk", "uwg.store.icc-rk", "uwg.trade.omni-rk"):
    req = urllib.request.Request("http://127.0.0.1/index.app", headers={"Host": host})
    try:
        with urllib.request.urlopen(req, timeout=5) as r:
            body = r.read(200).decode("utf-8", "replace")
            print("127 Host", host, r.status, body[:60].replace("\n"," "))
    except Exception as e:
        print("127 Host", host, "ERR", e)
