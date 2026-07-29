import re
import subprocess

GARDEN_PFS = list(range(4676, 4700))
ZONE_PFS = (
    list(range(4310, 4314))
    + list(range(4320, 4323))
    + list(range(4328, 4332))
    + [4006, 4605, 4872, 4873, 4877, 4880, 4881, 4540, 4541, 4542, 4543, 4544]
)


def query(sql):
    proc = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-N", "-B", "-e", sql],
        capture_output=True,
        text=True,
        check=True,
    )
    return [line.split("\t") for line in proc.stdout.splitlines() if line.strip()]


def decode_template_from_hex(hexstats):
    # ACGItemTemplateID values in repo blobs use 4-byte big-endian after CE marker.
    matches = re.findall(r"CE00(0[0-9A-F]{5})", hexstats.upper())
    if not matches:
        matches = re.findall(r"CE0003([0-9A-F]{4})", hexstats.upper())
        if matches:
            return int(matches[0], 16)
        return None
    # pick the template id occurrence (usually 3BCxx / 3BBxx / 366Ex)
    for m in matches:
        val = int(m, 16)
        if val >= 200000:
            return val
    return int(matches[0], 16)


def load_names():
    names = {}
    for row in query("SELECT id, name FROM itemnames"):
        names[int(row[0])] = row[1]
    return names


def load_staticdynels(playfields):
    pf_csv = ",".join(str(p) for p in playfields)
    rows = query(
        f"SELECT Id, Instance, Playfield, X, Y, Z, HEX(stats) FROM staticdynels WHERE Playfield IN ({pf_csv}) ORDER BY Playfield, Id"
    )
    out = []
    for parts in rows:
        template = decode_template_from_hex(parts[6])
        out.append(
            {
                "id": int(parts[0]),
                "instance": int(parts[1]),
                "playfield": int(parts[2]),
                "x": float(parts[3]),
                "y": float(parts[4]),
                "z": float(parts[5]),
                "template": template,
            }
        )
    return out


names = load_names()
gardens = load_staticdynels(GARDEN_PFS)
zones = load_staticdynels(ZONE_PFS)

passage_templates = {}
for row in query("SELECT id, name FROM itemnames WHERE name LIKE 'Passage to %' ORDER BY id"):
    passage_templates[int(row[0])] = row[1]

print("=== GARDEN PASSAGES ===")
garden_passages = {}
for g in gardens:
    if g["template"] is None:
        continue
    name = names.get(g["template"]) or passage_templates.get(g["template"], "")
    if "Passage to" not in name:
        continue
    garden_passages.setdefault(name, []).append(g)

for name in sorted(garden_passages):
    pfs = sorted({g["playfield"] for g in garden_passages[name]})
    print(f"{name}\ttemplate={garden_passages[name][0]['template']}\tgardens={pfs}")

print("\n=== ZONE RETURN STATUES (222955/222890) ===")
zone_returns = {}
for z in zones:
    if z["template"] not in (222955, 222890):
        continue
    zone_returns.setdefault(z["playfield"], []).append(z)
    name = names.get(z["template"], str(z["template"]))
    print(f"PF {z['playfield']}\t{z['template']}\t{name}\t({z['x']}, {z['y']}, {z['z']})")

print("\n=== ZONE STATICDYNELS ALL ===")
zone_all = {}
for z in zones:
    if z["template"] is None:
        continue
    n = names.get(z["template"], "")
    zone_all.setdefault(z["playfield"], []).append((z["template"], n, z["x"], z["y"], z["z"]))
for pf in sorted(zone_all):
    print(f"\nPF {pf}:")
    for t, n, x, y, z in zone_all[pf]:
        print(f"  {t}\t{n}\t({x}, {y}, {z})")
