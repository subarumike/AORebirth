# Expand AlexAreaMobRuntime slots with goldman combat positions not near existing slots.
import re
from pathlib import Path

SUMMARY = Path(r"tools-temp/_tmp_goldman_spawn_summary.txt")
ALEX = Path(r"AORebirth/Server/ZoneEngine/Core/Playfields/AlexAreaMobRuntime.cs")

COMBAT = {
    "32-V Docker": ("Docker", 17649, 3, 35, 1019, 110, 11, "Passive", 0.0),
    "Waste Collector": ("WasteCollector", 17714, 2, 29, 1019, 75, 12, "Passive", 0.0),
    "Garbage Flea": ("GarbageFlea", 17657, 2, 24, 25, 125, 8, "Aggressive", 2.0),
    "Mutated Garbage Flea": ("GarbageFlea", 17657, 7, 69, 25, 125, 8, "Aggressive", 2.0),
    "IIV-X Advanced Docker": ("Docker", 17649, 4, 323, 1019, 110, 15, "Passive", 0.0),
    "Supreme Collector of Waste": ("WasteCollector", 17714, 4, 60, 1019, 75, 12, "Passive", 0.0),
}

text = ALEX.read_text(encoding="utf-8")
existing = []
for m in re.finditer(
    r'new MobSlot\("([^"]+)", MobKind\.\w+, [^,]+, [^,]+, [^,]+, [^,]+, [^,]+, [^,]+, [^,]+, [^,]+, ([-\d.]+)f, ([-\d.]+)f, ([-\d.]+)f\)',
    text,
):
    existing.append((m.group(1), float(m.group(2)), float(m.group(3)), float(m.group(4))))


def near(x, z, name, thresh=5.0):
    for n, ex, ey, ez in existing:
        if n != name and not (name.startswith("Garbage") and n.startswith("Garbage")):
            # same family proximity for fleas
            if not (
                (name in ("Waste Collector", "Supreme Collector of Waste") and n in ("Waste Collector", "Supreme Collector of Waste"))
                or (name in ("32-V Docker", "IIV-X Advanced Docker") and n in ("32-V Docker", "IIV-X Advanced Docker"))
                or ("Flea" in name and "Flea" in n)
            ):
                continue
        dx = ex - x
        dz = ez - z
        if (dx * dx + dz * dz) <= thresh * thresh:
            return True
    return False


adds = []
for line in SUMMARY.read_text(encoding="utf-8").splitlines():
    if "\t" not in line or line.startswith("count="):
        continue
    parts = line.split("\t")
    if len(parts) < 5:
        continue
    iid, name, lvl, md, pos = parts[0], parts[1], parts[2], parts[3], parts[4]
    if name not in COMBAT:
        continue
    xyz = [float(v) for v in pos.split(",")]
    x, y, z = xyz[0], xyz[1], xyz[2]
    if near(x, z, name):
        continue
    kind, monster, level, health, fam, scale, run, ai, aggro = COMBAT[name]
    # Prefer capture level/health if present
    try:
        level = int(lvl.lstrip("L"))
    except Exception:
        pass
    adds.append(
        f'                new MobSlot("{name}", MobKind.{kind}, {monster}, {level}, {health}, {fam}, {scale}, {run}, NpcAiProfile.{ai}, {aggro}f, {x}f, {y}f, {z}f),'
    )
    existing.append((name, x, y, z))

print(f"adding {len(adds)} combat slots")
for a in adds:
    print(a)

if not adds:
    raise SystemExit(0)

marker = '                new MobSlot("Cleanmeister Intelligence Robot", MobKind.CleaningRobot, 297023, 2, 180, 1019, 100, 13, NpcAiProfile.Passive, 0f, 3544.5f, 5.31f, 872.4f)\n            };'
if marker not in text:
    raise SystemExit("alex marker not found")
insert = "\n".join(adds) + "\n"
replacement = (
    '                new MobSlot("Cleanmeister Intelligence Robot", MobKind.CleaningRobot, 297023, 2, 180, 1019, 100, 13, NpcAiProfile.Passive, 0f, 3544.5f, 5.31f, 872.4f),\n'
    + insert
    + "            };"
)
# also update source comment
text2 = text.replace(marker, replacement)
text2 = text2.replace(
    "// Capture 20260720-204431 (known-good Alex pad population) + 30s respawn.",
    "// Capture 20260720-204431 (Alex pad) + 20260720-goldman combat extras + 30s respawn.",
)
ALEX.write_text(text2, encoding="utf-8")
print("updated", ALEX)
