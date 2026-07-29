import re, subprocess
from collections import defaultdict

PASSAGE_DEST_PF = {
    "Passage to Old Frontier": 4310, "Passage to Frontier Bridge": 4310, "Passage to Frontier Outskirts": 4310,
    "Passage to Misty Dreams Border": 4312, "Passage to The Core": 4312, "Passage to Mil": 4311,
    "Passage to Brawl": 4311, "Passage to Frontier Border": 4311, "Passage to Nascence Wilds": 4311,
    "Passage to The Wetlands": 4311, "Passage to Silence": 4311, "Passage to Steppe of Dispair": 4312,
    "Passage to Two Mountains": 4312,
    "Passage to The Scoop": 4542, "Passage to Barter": 4543, "Passage to Enig": 4541,
    "Passage to Cape Callous": 4544, "Passage to The Fallen Forest": 4540, "Passage to The Sink": 4541,
    "Passage to Archbile": 4543, "Passage to Utopolis": 4542, "Passage to Enclave": 4542,
    "Passage to Chronos Canyon": 4540, "Passage to Corona": 4544, "Passage to Domeview": 4544,
    "Passage to Eastfang": 4541, "Passage to Tinker Tower": 4543, "Passage to The Divide": 4540,
    "Passage to Shell Beach": 4540, "Passage to Remnans": 4540, "Passage to Ripwell": 4542,
    "Passage to Whispervale": 4540, "Passage to The Outer Isles": 4544, "Passage to Acme": 4544,
    "Passage to Cold Rock": 4544, "Passage to Nero": 4542, "Passage to Sabre's Cradle": 4543,
    "Passage to Shunpike": 4543, "Passage to Spade": 4541, "Passage to Stormshelter": 4542,
    "Passage to Time's Tide": 4540, "Passage to The Jagged Coast": 4544, "Passage to Godstrand Cliffs": 4541,
    "Passage to Monopolis": 4543, "Passage to Shattered Heartlands": 4541,
    "Passage to Cutching Light": 4880, "Passage to Giant's Hoof": 4880, "Passage to Mirador": 4880,
    "Passage to The Court": 4880, "Passage to The Highlands": 4881, "Passage to Halls of Scheol": 4881,
    "Passage to Eastern Brink": 4881, "Passage to Marble Orchards": 4881, "Passage to The Temple Bog": 4881,
    "Passage to The Twilight Basin": 4881, "Passage to The Approach": 4881, "Passage to Necropolis": 4881,
    "Passage to City South": 4872, "Passage to City North": 4872, "Passage to Lament Lagoon": 4872,
    "Passage to The Outmost Yard": 4872, "Passage to Watcher's Ocular": 4872, "Passage to The Pool": 4872,
    "Passage to Coral Raft": 4873, "Passage to Dead Ends": 4873, "Passage to Piercing Tundra": 4873,
    "Passage to Blue Mist": 4320, "Passage to Yutto Wasteland": 4320, "Passage to The Ravine": 4320,
    "Passage to Penumbra Fortress": 4321, "Passage to The Pipe": 4321, "Passage to Glacier Hill": 4321,
    "Passage to Path to Fire": 4321, "Passage to Purity": 4321, "Passage to White Citadel": 4321,
    "Passage to Path to fire": 4322, "Passage to Misty Marshes": 4322, "Passage to Dark Hill": 4322,
    "Passage to Razor's Lair": 4322,
    "Passage to Inferno Frontier": 4605, "Passage to Sorrow": 4605, "Passage to Yutto Marshes": 4605,
    "Passage to Dark Marshes": 4605, "Passage to Inferno Barracks": 4605, "Passage to Sorrow Outlook": 4605,
    "Passage to Oasis": 4605, "Passage to Xark's Lair": 4605,
}

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
    4310: 222955, 4311: 222955, 4312: 222955, 4313: 222955,
    4540: 223577, 4541: 223577, 4542: 223577, 4543: 223577, 4544: 223577,
    4880: 223578, 4881: 223578, 4872: 223589, 4873: 223589,
    4320: 224017, 4321: 224017, 4322: 223982, 4605: 223981, 4328: 227466,
}

ZONE_PFS = sorted(set(PASSAGE_DEST_PF.values()) | set(RETURN_STATUE_TEMPLATES.keys()))

GARDEN_RETURN = {}

def query(sql):
    proc = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-N", "-B", "-e", sql],
        capture_output=True, text=True, check=True)
    return [line.split("\t") for line in proc.stdout.splitlines() if line.strip()]

def decode_template(hexstats):
    for m in re.findall(r"CE00(0[0-9A-F]{5})", hexstats.upper()):
        val = int(m, 16)
        if val >= 200000:
            return val
    return None

zone_statues = defaultdict(list)
for pf, x, y, z, hexstats in query(
    f"SELECT Playfield, X, Y, Z, HEX(stats) FROM staticdynels WHERE Playfield IN ({','.join(map(str,ZONE_PFS))})"
):
    t = decode_template(hexstats)
    if t:
        zone_statues[int(pf)].append((t, float(x), float(y), float(z)))

routes = []
missing = []
for name, dest_pf in sorted(PASSAGE_DEST_PF.items()):
    if name in PASSAGE_ARRIVAL:
        pf, x, y, z = PASSAGE_ARRIVAL[name]
    else:
        preferred = RETURN_STATUE_TEMPLATES.get(dest_pf)
        pick = None
        for t, x, y, z in zone_statues.get(dest_pf, []):
            if preferred and t == preferred:
                pick = (dest_pf, x, y, z); break
        if not pick and zone_statues.get(dest_pf):
            t, x, y, z = zone_statues[dest_pf][0]
            pick = (dest_pf, x, y, z)
        if not pick:
            missing.append(name); continue
        pf, x, y, z = pick
    routes.append((name, pf, x, y, z))

# emit routes dictionary lines only
for name, pf, x, y, z in routes:
    esc = name.replace('"', '\\"')
    print(f'                       {{ "{esc}", new NascenceGardenPassageRoute({pf}, {x}f, {y}f, {z}f, "cellao_codex_test") }},')

if missing:
    import sys
    print("MISSING: " + ", ".join(missing), file=sys.stderr)
