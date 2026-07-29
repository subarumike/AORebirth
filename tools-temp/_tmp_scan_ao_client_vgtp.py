import os
from pathlib import Path

root = Path(r"C:\Funcom\Anarchy Online")
needles = [
    b"temporarily unavailable",
    b"temporily unavailable",
    b"uwg.store.icc-rk",
    b"uwg.daily.icc-rk",
    b"uwg.trade.omni-rk",
    b"aomarket.funcom.com",
    b"vgtp://",
    b"icc-rk",
    b"Item Store",
    b"Daily Login",
]

# Prefer likely config/UI paths first
priority = []
for dirpath, dirnames, filenames in os.walk(root):
    rel = os.path.relpath(dirpath, root).lower()
    # skip cd images / huge media
    if any(x in rel for x in ("cd_image", "videos", "music", "patch", "download")):
        dirnames[:] = []
        continue
    for fn in filenames:
        ext = os.path.splitext(fn)[1].lower()
        path = os.path.join(dirpath, fn)
        try:
            size = os.path.getsize(path)
        except OSError:
            continue
        score = 0
        fl = fn.lower()
        if any(k in fl for k in ("pref", "host", "vgtp", "browser", "web", "cfg", "ini", "xml")):
            score += 5
        if ext in (".cfg", ".ini", ".xml", ".txt", ".html", ".htm", ".js", ".json", ".prefs"):
            score += 3
        if ext in (".exe", ".dll") and size < 30_000_000:
            score += 1
        if size == 0 or size > 40_000_000:
            continue
        priority.append((score, size, path))

priority.sort(reverse=True)
print("candidates", len(priority))
hits = []
# scan top scored then rest in batches
for score, size, path in priority:
    try:
        data = open(path, "rb").read()
    except OSError:
        continue
    for n in needles:
        if n in data:
            hits.append((n.decode("latin1", "replace"), path, size, score))
            print("HIT", n.decode("latin1", "replace"), path)
            break
    if len(hits) >= 40:
        break

print("done hits", len(hits))
for h in hits:
    print("\t".join(map(str, h)))
