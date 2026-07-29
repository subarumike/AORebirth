import os
roots = [
    r"C:\Users\nermi\AppData\Local\Funcom",
    r"C:\Users\nermi\AppData\Roaming\Funcom",
    r"C:\Program Files (x86)\Funcom",
    r"C:\Program Files\Funcom",
    r"D:\Funcom",
    r"C:\AO",
    r"C:\Games\Anarchy Online",
]
for r in roots:
    print(r, "EXISTS" if os.path.isdir(r) else "no")

# search common AO prefs for vgtp / store
candidates = []
for root in roots:
    if not os.path.isdir(root):
        continue
    for dirpath, dirnames, filenames in os.walk(root):
        # limit depth
        rel = os.path.relpath(dirpath, root)
        if rel.count(os.sep) > 3:
            dirnames[:] = []
            continue
        for f in filenames:
            fl = f.lower()
            if fl.endswith((".cfg", ".ini", ".xml", ".txt", ".json", ".prefs")) or "pref" in fl or "host" in fl:
                candidates.append(os.path.join(dirpath, f))

print("candidate files", len(candidates))
for p in candidates[:80]:
    try:
        data = open(p, "rb").read(200000)
    except Exception:
        continue
    if b"vgtp" in data or b"icc-rk" in data or b"uwg.store" in data or b"uwg.daily" in data or b"temporarily unavailable" in data:
        print("HIT", p)
