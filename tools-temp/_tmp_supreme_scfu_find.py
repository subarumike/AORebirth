from pathlib import Path

root = Path(r"tools-temp/AOSharpLiveCapture/bin/Debug/captures")
name_hex = bytes("Supreme Collector of Waste", "ascii").hex().upper()
hits = []
for cap in sorted(root.iterdir()):
    if not cap.is_dir():
        continue
    for fn in ("events.log", "packets.hex.log", "enemy-dossier.json"):
        p = cap / fn
        if not p.exists():
            continue
        try:
            t = p.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        if "Supreme Collector" not in t and name_hex not in t.upper():
            continue
        detail = False
        if 'Name="Supreme Collector of Waste"' in t:
            detail = True
        if fn == "packets.hex.log" and name_hex in t.upper():
            detail = True
        hits.append((cap.name, fn, detail, t.count("Supreme Collector")))

for h in hits:
    print(h)
print("total", len(hits))
