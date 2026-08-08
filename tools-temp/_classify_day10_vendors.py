# -*- coding: utf-8 -*-
from __future__ import print_function
import json
import re

names_sql = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\SqlTables\itemnames.sql"
nanos_json = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_day10_vendor_nanos.json"

with open(nanos_json, encoding="utf-8") as f:
    data = json.load(f)

all_ids = set()
for t in data["openOrder"]:
    for x in data["vendors"][t]:
        all_ids.add(x["lowId"])

# ( 26166 , 'Name' , 'Item', '...' )
id_re = re.compile(r"\(\s*(\d+)\s*,\s*'((?:''|[^'])*)'\s*,")

found = {}
with open(names_sql, encoding="utf-8", errors="replace") as f:
    for line in f:
        for m in id_re.finditer(line):
            iid = int(m.group(1))
            if iid in all_ids:
                found[iid] = m.group(2).replace("''", "'")

print("resolved names", len(found), "/", len(all_ids))

# Explicit profession markers in nano crystal names
markers = [
    ("Soldier", ["Soldier", " Nano Crystal (Full Auto", "Assault Rifle", "Clip Junkie", "Missile Mastery"]),
    ("MartialArtist", ["Martial Artist", "Dimach", "Evades", "MA:"]),
    ("Engineer", ["Engineer", "Engi ", "Mech. Phys.", "Pet Profession", "Guard"]),
    ("Fixer", ["Fixer", "Grid ", "Gridspace", "Yuttos"]),
    ("Agent", ["Agent", "Aimed Shot", "Concentration"]),
    ("Adventurer", ["Adventurer", "Morph ", "Complete Healing", "Lesser Behemoth", "Wolf Form"]),
    ("Trader", ["Trader", "Divert Energy", "Draw Essence"]),
    ("Bureaucrat", ["Bureaucrat", "Charm", "Corporate"]),
    ("Enforcer", ["Enforcer", "Enf ", "Damage Shield", "Heckler"]),
    ("Doctor", ["Doctor", "Team Heal", "Complete Healing", "Vaccine", "Life Channel"]),
    ("Nanotechnician", ["Nano-Technician", "Nanotechnician", "NT:", "Nullity Sphere", "Neutron"]),
    ("Metaphysicist", ["Meta-Physicist", "Metaphysicist", "MP:", "Mezz", "Pet"]),
    ("Keeper", ["Keeper", "Blessing of", "Aura of"]),
    ("Shade", ["Shade", "Spirit Siphon", "Shade:"]),
]

print("\n=== VENDOR PROFESSION SCORES ===")
assignments = []
for idx, t in enumerate(data["openOrder"]):
    items = data["vendors"][t]
    names = [found.get(x["lowId"], "") for x in items]
    scores = {p: 0 for p, _ in markers}
    for n in names:
        for p, keys in markers:
            for k in keys:
                if k.lower() in n.lower():
                    scores[p] += 1
                    break
    ranked = sorted(scores.items(), key=lambda kv: -kv[1])
    sample = [found.get(x["lowId"], "?") for x in items[:8]]
    print("%02d count=%d top=%s" % (idx+1, len(items), ranked[:4]))
    for s in sample:
        print("   ", s[:90])
    assignments.append((t, ranked[0][0] if ranked[0][1] > 0 else "UNKNOWN", ranked))

# Write classified pools keyed by profession enum name
# Prefer unambiguous assignment: if top score unique
prof_pools = {}
for t, best, ranked in assignments:
    if ranked[0][1] == 0:
        continue
    # if tie, leave UNKNOWN for manual
    if len(ranked) > 1 and ranked[0][1] == ranked[1][1]:
        print("TIE", t, ranked[:3])
        continue
    prof = ranked[0][0]
    entries = []
    for x in data["vendors"][t]:
        entries.append({
            "itemId": x["lowId"],
            "quality": x["quality"],
            "name": found.get(x["lowId"], "")
        })
    if prof in prof_pools:
        print("DUP PROF", prof, t)
    else:
        prof_pools[prof] = entries

out = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_day10_profession_pools.json"
with open(out, "w", encoding="utf-8") as f:
    json.dump({"professions": prof_pools, "assignments": [
        {"vendor": t, "profession": best, "scores": ranked[:5]} for t, best, ranked in assignments
    ]}, f, indent=2)
print("WROTE", out, "profs", list(prof_pools.keys()))
