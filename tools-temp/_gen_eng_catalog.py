# Generate Engineer pet catalog C# fragments from capture map.
import re
from pathlib import Path

# Shell item SummonPet hash by low id (from items.dat probe)
SHELL_PET_HASH = {
    96196: "PT10",
    150789: "PT10",
    150790: "PT10",
    150786: "PT11",
    150787: "PT11",
    150788: "PT11",
    150794: "PT12",
    150795: "PT12",
    150796: "PT12",
    150797: "PT12",
    150791: "PT13",
    150792: "PT13",
    150793: "PT13",
    96228: "PT13",
    150782: "PT14",
    150783: "PT14",
    150784: "PT14",
    150785: "PT14",
    96215: "PT14",
    150777: "PT15",
    150778: "PT15",
    150779: "PT15",
    150780: "PT15",
    150781: "PT15",
    150775: "PT19",
    150776: "PT19",
    96218: "PT20",
}

# Capture line: nano | shell low/high QLql | PET name | lvl | hp | md | scale | run
pat = re.compile(
    r"^(\d+)\s+\|\s+(\d+)/(\d+)\s+QL(\d+)\s+\|\s+(.+?)\s+\|\s+(\d+)\s+\|\s+(\d+)\s+\|\s+(\d+)\s+\|\s+(\d+)\s+\|\s+(\d+)\s*$"
)

rows = []
for line in Path("tools-temp/_eng_shell_pet_map_clean.txt").read_text(encoding="utf-8").splitlines():
    m = pat.match(line.strip())
    if not m:
        continue
    nano, low, high, ql, name, lvl, hp, md, scale, run = m.groups()
    nano = int(nano)
    low = int(low)
    high = int(high)
    ql = int(ql)
    lvl = int(lvl)
    hp = int(hp)
    md = int(md)
    scale = int(scale)
    run = int(run)
    pet_hash = SHELL_PET_HASH.get(low) or SHELL_PET_HASH.get(high)
    if not pet_hash:
        raise SystemExit(f"No pet hash for shell {low}/{high} nano={nano}")
    # Capture 20260808-131854 SCFU NPCInfo family is 95 for all Engineer pet tiers.
    family = 95
    rows.append((nano, pet_hash, lvl, low, high, ql, name, hp, md, scale, run, family))

rows.sort(key=lambda r: r[0])

out = Path("tools-temp/_eng_catalog_gen.csfrag")
with out.open("w", encoding="utf-8") as f:
    f.write("// PreferredPetHashByNano engineer entries\n")
    for nano, pet_hash, lvl, *_ in rows:
        f.write(f"                {{ {nano}, \"{pet_hash}\" }}, // Eng L{lvl}\n")
    f.write("\n// PreferredPetTypeByNano\n")
    for nano, pet_hash, lvl, *_ in rows:
        f.write(f"                {{ {nano}, {lvl} }},\n")
    f.write("\n// EngineerShellDisplayByNano\n")
    for nano, pet_hash, lvl, low, high, ql, *_ in rows:
        f.write(
            f"                {{ {nano}, new CapturedBureaucratShellDisplay({low}, {high}, {ql}) }},\n"
        )
    f.write("\n// EngineerProfilesByNano\n")
    for nano, pet_hash, lvl, low, high, ql, name, hp, md, scale, run, family in rows:
        f.write(
            "                { "
            f"{nano}, new CapturedBureaucratPetProfile(\"{name}\", {lvl}, {hp}, {md}, {scale}, {run}, npcFamily: {family})"
            " },\n"
        )

print(f"wrote {out} rows={len(rows)}")
for r in rows:
    print(f"{r[0]}\t{r[1]}\tL{r[2]}\t{r[3]}/{r[4]} QL{r[5]}\t{r[6]}")
