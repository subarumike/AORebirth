import ssl
import urllib.request

ctx = ssl._create_unverified_context()
for host in ("uwg.daily.icc-rk", "uwg.store.icc-rk", "uwg.trade.omni-rk", "aomarket.funcom.com"):
    for scheme in ("http", "https"):
        url = f"{scheme}://127.0.0.1/index.app"
        req = urllib.request.Request(url, headers={"Host": host})
        try:
            with urllib.request.urlopen(req, context=ctx if scheme == "https" else None, timeout=5) as r:
                body = r.read(800).decode("utf-8", "replace")
                title = ""
                low = body.lower()
                if "<title>" in low:
                    i = low.index("<title>") + 7
                    title = body[i:i + 60].split("<")[0]
                flag = ""
                if "temporarily unavailable" in low or "temporily unavailable" in low:
                    flag = " FUNCOM_ERROR"
                print(f"{scheme} Host={host} -> {r.status} title={title!r}{flag}")
        except Exception as e:
            print(f"{scheme} Host={host} -> FAIL {e}")
