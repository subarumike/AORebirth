# Merge Heckler slots from capture 20260727-190145 into ElysiumEastMobRuntime.cs
# (replace existing Heckler MobSlots; keep all other wildlife).
import csv
import os
import re
from collections import OrderedDict

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260727-190145"
cs_path = (
    r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine"
    r"\Core\Playfields\ElysiumEastMobRuntime.cs"
)

by_id = OrderedDict()
with open(os.path.join(cap, "scfu-appearance.csv"), encoding="utf-8-sig", newline="") as f:
    for row in csv.DictReader(f):
        if row.get("CharacterInfoType") != "NPCInfo":
            continue
        name = (row.get("Name") or "").strip()
        if not name.startswith("Heckler"):
            continue
        by_id[row["Identity"]] = row


def fnum(v, default=0.0):
    try:
        return float(v)
    except Exception:
        return default


def inum(v, default=0):
    try:
        return int(float(v))
    except Exception:
        return default


lines_out = []
for ident, r in by_id.items():
    name = (r.get("Name") or "").strip()
    md = inum(r.get("MonsterData"))
    level = inum(r.get("Level"))
    health = inum(r.get("Health") or r.get("Life") or r.get("MaxHealth"))
    if health <= 0:
        health = inum(r.get("LifeBase"))
    fam = inum(r.get("NpcFamily"), 171)
    scale = inum(r.get("MonsterScale") or r.get("Scale"), 100)
    run = inum(r.get("RunSpeedBase") or r.get("RunSpeed"), 440)
    flags = inum(r.get("CharacterFlags") or r.get("Flags"), 268964353)
    vflags = inum(r.get("VisualFlags"), 31)
    hm = inum(r.get("HeadMesh"))
    x = fnum(r.get("PositionX") or r.get("X"))
    y = fnum(r.get("PositionY") or r.get("Y"))
    z = fnum(r.get("PositionZ") or r.get("Z"))
    hy = fnum(r.get("HeadingY"))
    hw = fnum(r.get("HeadingW"), 1.0)
    lines_out.append(
        "                new MobSlot { Name = \"%s\", MonsterData = %d, Level = %d, Health = %d, "
        "NpcFamily = %d, Scale = %d, RunSpeed = %d, CharacterFlags = %d, VisualFlags = %d, "
        "HeadMesh = %d, X = %.3ff, Y = %.3ff, Z = %.3ff, HeadingY = %.6ff, HeadingW = %.6ff, "
        "Textures = null, Meshes = null },"
        % (name, md, level, health, fam, scale, run, flags, vflags, hm, x, y, z, hy, hw)
    )

text = open(cs_path, encoding="utf-8").read()

# Drop existing Heckler MobSlot lines
text2 = re.sub(
    r"^[ \t]*new MobSlot \{ Name = \"Heckler[^\n]+\n",
    "",
    text,
    flags=re.M,
)

# Insert new Heckler slots before the closing of Slots array.
m = re.search(
    r"(private static readonly MobSlot\[\] Slots\s*=\s*\n\s*\{)(.*?)(\n            \};)",
    text2,
    flags=re.S,
)
if not m:
    raise SystemExit("Slots array not found")

body = m.group(2).rstrip()
if body and not body.endswith(","):
    body = body + ","
insert = "\n".join(lines_out)
new_body = body + "\n" + insert
text2 = text2[: m.start()] + m.group(1) + new_body + m.group(3) + text2[m.end() :]

# Ensure Heckler of Elements in name allowlists (wildlife + scfu unk1)
for needle in (
    'if (string.Equals(name, "Heckler of Stones", StringComparison.OrdinalIgnoreCase))',
):
    if 'Heckler of Elements' not in text2:
        text2 = text2.replace(
            needle,
            'if (string.Equals(name, "Heckler of Elements", StringComparison.OrdinalIgnoreCase))\n'
            "            {\n"
            "                return true;\n"
            "            }\n"
            "            " + needle,
        )

# Summary / log source
text2 = text2.replace(
    "Capture 20260727-182451 East of Elysium (PF 4543): wildlife + guards.\n"
    "    /// Appearance/ExtTex from SCFU; fight anim/damage/XP deferred.",
    "Capture 20260727-182451 + Heckler densify 20260727-190145 East of Elysium (PF 4543).\n"
    "    /// Heckler fight anim/damage from 190145 SAW/AttackInfo; other wildlife combat deferred.",
)
text2 = text2.replace(
    'source=20260727-182451");',
    'source=20260727-182451+190145-hecklers");',
)

open(cs_path, "w", encoding="utf-8", newline="\n").write(text2)
print("hecklers merged", len(lines_out))
print("heckler lines now", text2.count('Name = "Heckler'))
