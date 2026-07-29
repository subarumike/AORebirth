import os
from pathlib import Path

needles = [
    b"temporarily unavailable",
    b"temporily unavailable",
    b"uwg.store.icc-rk",
    b"uwg.daily.icc-rk",
    b"uwg.trade.omni-rk",
    b"aomarket.funcom.com",
    b"vgtp://",
    b"icc-rk",
]

roots = []
for p in [
    r"C:\Users\nermi\AppData\Local\Funcom",
    r"C:\Users\nermi\AppData\Roaming\Funcom",
    r"C:\Program Files (x86)\Funcom",
    r"C:\Program Files\Funcom",
    r"D:\Games",
    r"D:\Anarchy Online",
    r"C:\Games",
    r"C:\Anarchy Online",
    r"C:\AO",
    r"E:\Anarchy Online",
    r"C:\Users\nermi\Documents\Anarchy Online",
]:
    if os.path.isdir(p):
        roots.append(p)
        print("ROOT", p)

# also find AnarchyOnline.exe
for drive in "CDEF":
    for rel in [
        rf"{drive}:\Anarchy Online",
        rf"{drive}:\Games\Anarchy Online",
        rf"{drive}:\Program Files (x86)\Funcom\Anarchy Online",
        rf"{drive}:\Program Files\Funcom\Anarchy Online",
    ]:
        if os.path.isdir(rel) and rel not in roots:
            roots.append(rel)
            print("ROOT", rel)

hits = []
for root in roots:
    for dirpath, dirnames, filenames in os.walk(root):
        # skip huge caches
        low = dirpath.lower()
        if any(x in low for x in ("\\cache", "\\temp", "\\logs", "\\cd_image", "\\prefs\\browser")):
            dirnames[:] = [d for d in dirnames if d.lower() not in ("cache", "temp", "logs")]
        depth = dirpath[len(root):].count(os.sep)
        if depth > 5:
            dirnames[:] = []
            continue
        for fn in filenames:
            ext = os.path.splitext(fn)[1].lower()
            if ext not in (".cfg", ".ini", ".xml", ".txt", ".json", ".html", ".htm", ".js", ".dat", ".prefs", ".url", ".app", ""):
                # also scan small binaries/dll names only if name interesting
                if ext in (".exe", ".dll", ".bin", ".pak"):
                    pass
                else:
                    continue
            path = os.path.join(dirpath, fn)
            try:
                size = os.path.getsize(path)
            except OSError:
                continue
            if size > 8_000_000 or size == 0:
                continue
            try:
                data = open(path, "rb").read()
            except OSError:
                continue
            for n in needles:
                if n in data:
                    hits.append((n.decode("latin1", "replace"), path, size))
                    break

print("HITS", len(hits))
for n, p, s in hits[:60]:
    print(f"{n}\t{p}\t{s}")
