from pathlib import Path

ids = [
    "150274", "150273",  # Carbonrich Rock
    "144770", "144769", "144768", "144767",  # Carbonrich Ore
    "144800", "144799",  # Pure Carbon Crystal
    "150281", "150275",  # Jensen
    "149822", "144785",  # Isotope
    "149823", "144786",  # Neutron
]
p = Path(r"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\tradeskill.sql")
counts = {i: 0 for i in ids}
samples = []
with p.open("r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if not line.startswith("INSERT INTO `tradeskill`"):
            continue
        for i in ids:
            # match as Id1 or Id2: VALUES (id, or ,id,
            if f"VALUES ({i}," in line or f",{i}," in line[:80] or f"VALUES ({i}," in line:
                counts[i] += 1
            elif f"({i}," in line[:60]:
                counts[i] += 1
        if any(x in line[:50] for x in ("150274", "150273", "144770", "144800")) and len(samples) < 10:
            samples.append(line.strip()[:220])

print("id occurrence counts (approx):")
for i, c in counts.items():
    print(i, c)
print("samples:")
for s in samples:
    print(s)
