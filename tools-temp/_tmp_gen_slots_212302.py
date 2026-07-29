import json

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260720-212302\enemy-dossier.json"
with open(cap, encoding="utf-8-sig") as f:
    d = json.load(f)

want = {
    "Cleaning Robot": [],
    "32-V Docker": [],
    "Waste Collector": [],
    "Garbage Flea": [],
    "Cleanmeister Intelligence Robot": [],
}
for e in d["enemies"]:
    n = e.get("name")
    if n not in want:
        continue
    p = e["position"]
    hp = e.get("maxHealth") or 15
    lvl = e.get("level") or 1
    # Prefer living snapshot positions; still include death sites as spawn points
    want[n].append((float(p["x"]), float(p["y"]), float(p["z"]), int(hp), int(lvl)))

# Dedup Cleaning Robots within 2.5m (respawned same pad)
def dedup(lst, radius=2.5):
    out = []
    for x, y, z, hp, lvl in lst:
        if any((x - ox) ** 2 + (z - oz) ** 2 < radius * radius for ox, oy, oz, *_ in out):
            continue
        out.append((x, y, z, hp, lvl))
    return out

print("// Cleaning Robot slots")
for x, y, z, hp, lvl in dedup(want["Cleaning Robot"], 3.0):
    print("                new[] { %.4ff, %.6ff, %.4ff }, // hp=%d lvl=%d" % (x, y, z, hp, lvl))

print("\n// AlexArea MobSlot lines")
defs = {
    "32-V Docker": ("Docker", 17649, 3, 35, 1019, 110, 11, "NpcAiProfile.Passive", "0f"),
    "Waste Collector": ("WasteCollector", 17714, 2, 29, 1019, 75, 12, "NpcAiProfile.Passive", "0f"),
    "Garbage Flea": ("GarbageFlea", 17657, 2, 24, 25, 125, 8, "NpcAiProfile.Aggressive", "2f"),
    "Cleanmeister Intelligence Robot": ("CleaningRobot", 297023, 2, 180, 1019, 100, 13, "NpcAiProfile.Passive", "0f"),
}
for n, meta in defs.items():
    kind, md, lvl, hp, fam, scale, run, ai, aggro = meta
    slots = dedup(want[n], 2.0)
    print("// %s x%d" % (n, len(slots)))
    for x, y, z, hpo, lvlo in slots:
        use_hp = hpo if hpo > 0 else hp
        use_lvl = lvlo if lvlo > 0 else lvl
        # flea/docker use capture hp when present
        if n == "Garbage Flea":
            use_hp = hpo
            use_lvl = lvlo
            scale = 125
        if n == "32-V Docker":
            use_hp = hpo
            use_lvl = lvlo
        if n == "Waste Collector":
            use_hp = hpo
            use_lvl = lvlo
        print(
            '                new MobSlot("%s", MobKind.%s, %d, %d, %d, %d, %d, %d, %s, %s, %.4ff, %.6ff, %.4ff),'
            % (n, kind, md, use_lvl, use_hp, fam, scale, run, ai, aggro, x, y, z)
        )
