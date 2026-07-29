import os
root = r"C:\Users\nermi\AppData\Local\Funcom"
for dirpath, dirnames, filenames in os.walk(root):
    rel = os.path.relpath(dirpath, root)
    depth = 0 if rel == "." else rel.count(os.sep) + 1
    if depth <= 2:
        print("DIR", dirpath)
    if depth > 4:
        dirnames[:] = []
        continue
    for f in filenames:
        p = os.path.join(dirpath, f)
        print(" FILE", p)
        try:
            raw = open(p, "rb").read(500000)
        except Exception:
            continue
        low = raw.lower()
        for needle in (b"vgtp", b"icc-rk", b"uwg.store", b"uwg.daily", b"uwg.trade", b"temporarily unavailable", b"index.app", b"aomarket"):
            if needle in low:
                print("  HIT", needle.decode())
