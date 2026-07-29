import os, re
from pathlib import Path

# Search AO install for vgtp protocol / proxy / mapping
root = Path(r"C:\Funcom\Anarchy Online")
needles = [b"vgtp", b"aoshop", b"BrowserModule", b"WebView", b"proxy"]
# prefs often in %LOCALAPPDATA% or AO folder
pref_roots = [
    root,
    Path(os.environ.get("LOCALAPPDATA", "")) / "Funcom",
    Path(os.environ.get("APPDATA", "")) / "Funcom",
    Path(r"C:\Users\nermi\AppData\Local\Funcom"),
]

hits = []
for base in pref_roots:
    if not base.exists():
        print("missing", base)
        continue
    print("scan", base)
    for dirpath, dirnames, filenames in os.walk(base):
        rel = dirpath.lower()
        if any(x in rel for x in ("\\cd_image", "\\videos", "\\music", "\\cache\\cef", "\\gpu")):
            dirnames[:] = []
            continue
        depth = Path(dirpath).relative_to(base).parts
        if len(depth) > 4:
            dirnames[:] = []
            continue
        for fn in filenames:
            ext = os.path.splitext(fn)[1].lower()
            if ext not in (".cfg", ".ini", ".xml", ".txt", ".json", ".prefs", ".dat", ".log", ".html"):
                continue
            path = os.path.join(dirpath, fn)
            try:
                size = os.path.getsize(path)
            except OSError:
                continue
            if size == 0 or size > 2_000_000:
                continue
            try:
                data = open(path, "rb").read()
            except OSError:
                continue
            for n in needles:
                if n in data.lower() if n.islower() else data:
                    # case-insensitive for some
                    pass
            low = data.lower()
            if b"vgtp" in low or b"aoshop" in low or b"dailyrewards" in low or b"uwg.store" in low:
                hits.append(path)
                print("HIT", path)

print("total hits", len(hits))

# Also dump registry-ish: look for Custom Protocol in prefs xml
for p in hits[:20]:
    try:
        t = open(p, "r", encoding="utf-8", errors="replace").read()
    except Exception:
        continue
    for line in t.splitlines():
        low = line.lower()
        if any(x in low for x in ("vgtp", "aoshop", "daily", "store", "market", "browser", "proxy", "url")):
            print(p, ":", line[:200])
