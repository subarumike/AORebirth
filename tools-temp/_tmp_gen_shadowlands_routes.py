import re
import subprocess

# passage name -> destination playfield (from AO Shadowlands garden routing)
PASSAGE_DEST_PF = {
    # Nascence
    "Passage to Old Frontier": 4310,
    "Passage to Frontier Bridge": 4310,
    "Passage to Frontier Outskirts": 4310,
    "Passage to Misty Dreams Border": 4312,
    "Passage to The Core": 4312,
    "Passage to Mil": 4311,
    "Passage to Brawl": 4311,
    "Passage to Frontier Border": 4311,
    "Passage to Nascence Wilds": 4311,
    "Passage to The Wetlands": 4311,
    "Passage to Silence": 4311,
    "Passage to Steppe of Dispair": 4312,
    "Passage to Two Mountains": 4312,
    # Elysium
    "Passage to The Scoop": 4542,
    "Passage to Barter": 4543,
    "Passage to Enig": 4541,
    "Passage to Cape Callous": 4544,
    "Passage to The Fallen Forest": 4540,
    "Passage to The Sink": 4541,
    "Passage to Archbile": 4543,
    "Passage to Utopolis": 4542,
    "Passage to Enclave": 4542,
    "Passage to Chronos Canyon": 4540,
    "Passage to Corona": 4544,
    "Passage to Domeview": 4544,
    "Passage to Eastfang": 4541,
    "Passage to Tinker Tower": 4543,
    "Passage to The Divide": 4540,
    "Passage to Shell Beach": 4540,
    "Passage to Remnans": 4540,
    "Passage to Ripwell": 4542,
    "Passage to Whispervale": 4540,
    "Passage to The Outer Isles": 4544,
    "Passage to Acme": 4544,
    "Passage to Cold Rock": 4544,
    "Passage to Nero": 4542,
    "Passage to Sabre's Cradle": 4543,
    "Passage to Shunpike": 4543,
    "Passage to Spade": 4541,
    "Passage to Stormshelter": 4542,
    "Passage to Time's Tide": 4540,
    "Passage to The Jagged Coast": 4544,
    "Passage to Godstrand Cliffs": 4541,
    "Passage to Monopolis": 4543,
    "Passage to Shattered Heartlands": 4541,
    # Scheol
    "Passage to Cutching Light": 4880,
    "Passage to Giant's Hoof": 4880,
    "Passage to Mirador": 4880,
    "Passage to The Court": 4880,
    "Passage to The Highlands": 4881,
    "Passage to Halls of Scheol": 4881,
    "Passage to Eastern Brink": 4881,
    "Passage to Marble Orchards": 4881,
    "Passage to The Temple Bog": 4881,
    "Passage to The Twilight Basin": 4881,
    "Passage to The Approach": 4881,
    "Passage to Necropolis": 4881,
    # Adonis
    "Passage to City South": 4872,
    "Passage to City North": 4872,
    "Passage to Lament Lagoon": 4872,
    "Passage to The Outmost Yard": 4872,
    "Passage to Watcher's Ocular": 4872,
    "Passage to The Pool": 4872,
    "Passage to Coral Raft": 4873,
    "Passage to Dead Ends": 4873,
    "Passage to Piercing Tundra": 4873,
    # Penumbra
    "Passage to Blue Mist": 4320,
    "Passage to Yutto Wasteland": 4320,
    "Passage to The Ravine": 4320,
    "Passage to Penumbra Fortress": 4321,
    "Passage to The Pipe": 4321,
    "Passage to Glacier Hill": 4321,
    "Passage to Path to Fire": 4321,
    "Passage to Purity": 4321,
    "Passage to White Citadel": 4321,
    "Passage to Path to fire": 4322,
    "Passage to Misty Marshes": 4322,
    "Passage to Dark Hill": 4322,
    "Passage to Razor's Lair": 4322,
    # Inferno
    "Passage to Inferno Frontier": 4605,
    "Passage to Sorrow": 4605,
    "Passage to Yutto Marshes": 4605,
    "Passage to Dark Marshes": 4605,
    "Passage to Inferno Barracks": 4605,
    "Passage to Sorrow Outlook": 4605,
    "Passage to Oasis": 4605,
    "Passage to Xark's Lair": 4605,
    # Pandemonium
    "Passage to Inferno Frontier": 4328,  # pandemonium garden may use different - verify
}

# Capture-backed nascence arrivals (override generic nearest-statue pick)
PASSAGE_ARRIVAL = {
    "Passage to Old Frontier": (4310, 858.0, 31.52, 1479.0),
    "Passage to Frontier Bridge": (4310, 792.0, 31.81, 1149.0),
    "Passage to Frontier Outskirts": (4310, 684.0, 29.41, 1898.0),
    "Passage to Misty Dreams Border": (4312, 1544.0, 52.83, 680.0),
    "Passage to The Core": (4312, 1630.0, 43.96, 1469.0),
    "Passage to Mil": (4311, 274.0, 77.716, 1665.0),
    "Passage to Brawl": (4311, 242.0, 105.01, 1035.0),
    "Passage to Frontier Border": (4311, 608.0, 13.81, 556.0),
}

RETURN_STATUE_TEMPLATES = {
    4310: 222955,
    4311: 222955,
    4312: 222955,
    4540: 223577,
    4541: 223577,
    4542: 223577,
    4543: 223577,
    4544: 223577,
    4880: 223578,
    4881: 223578,
    4872: 223589,
    4873: 223589,
    4320: 224017,
    4321: 224017,
    4322: 223982,
    4605: 223981,
    4328: 227466,
}

ZONE_PFS = sorted(set(PASSAGE_DEST_PF.values()) | set(RETURN_STATUE_TEMPLATES.keys()))


def query(sql):
    proc = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-N", "-B", "-e", sql],
        capture_output=True,
        text=True,
        check=True,
    )
    return [line.split("\t") for line in proc.stdout.splitlines() if line.strip()]


def decode_template(hexstats):
    matches = re.findall(r"CE00(0[0-9A-F]{5})", hexstats.upper())
    for m in matches:
        val = int(m, 16)
        if val >= 200000:
            return val
    return None


def load_zone_statues():
    pf_csv = ",".join(str(p) for p in ZONE_PFS)
    rows = query(
        f"SELECT Playfield, X, Y, Z, HEX(stats) FROM staticdynels WHERE Playfield IN ({pf_csv})"
    )
    by_pf = {}
    for pf, x, y, z, hexstats in rows:
        pf = int(pf)
        template = decode_template(hexstats)
        if template is None:
            continue
        by_pf.setdefault(pf, []).append((template, float(x), float(y), float(z)))
    return by_pf


zone_statues = load_zone_statues()

missing = []
routes = []
for name, dest_pf in sorted(PASSAGE_DEST_PF.items()):
    if name in PASSAGE_ARRIVAL:
        pf, x, y, z = PASSAGE_ARRIVAL[name]
        routes.append((name, pf, x, y, z))
        continue
    preferred = RETURN_STATUE_TEMPLATES.get(dest_pf)
    candidates = zone_statues.get(dest_pf, [])
    pick = None
    if preferred:
        for t, x, y, z in candidates:
            if t == preferred:
                pick = (dest_pf, x, y, z)
                break
    if pick is None and candidates:
        t, x, y, z = candidates[0]
        pick = (dest_pf, x, y, z)
    if pick is None:
        missing.append(name)
        continue
    routes.append((name, pick[0], pick[1], pick[2], pick[3]))

print("// Generated from cellao_codex_test")
print("private static readonly Dictionary<string, ShadowlandsGardenPassageRoute> RoutesByName =")
print("    new Dictionary<string, ShadowlandsGardenPassageRoute>(StringComparer.OrdinalIgnoreCase)")
print("    {")
for name, pf, x, y, z in routes:
    esc = name.replace('"', '\\"')
    print(
        f'        {{ "{esc}", new ShadowlandsGardenPassageRoute({pf}, {x}f, {y}f, {z}f, "cellao_codex_test") }},'
    )
print("    };")
if missing:
    print("\n// MISSING:", ", ".join(missing), file=__import__("sys").stderr)
