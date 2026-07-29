import re

spawn = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\NascenceLifeSpawn.cs"
blocks_path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_230406_missing_blocks.txt"
garden = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\ThrakOmniGardenSpawn.cs"

text = open(spawn, encoding="utf-8").read()
raw = open(blocks_path, encoding="utf-8").read()
# strip comment header line
new_blocks = re.findall(r"new LifeNpc\s*\{.*?\n\s*\},", raw, re.S)
print("new_blocks", len(new_blocks))

# Parse existing into list of (pf, name, identity_sort, full_block)
# Keep everything before Npcs array and after last entry intact.
m = re.search(
    r"(private static readonly LifeNpc\[\] Npcs =\s*\{\n)(.*)(\n        \};)",
    text,
    re.S,
)
if not m:
    raise SystemExit("Npcs array not found")

prefix, body, suffix = m.group(1), m.group(2), m.group(3)
existing = re.findall(r"            new LifeNpc\s*\{.*?\n            \},?", body, re.S)
# normalize trailing commas
norm = []
for b in existing:
    b = b.rstrip()
    if not b.endswith(","):
        b += ","
    norm.append(b)
existing = norm


def meta(block):
    pf = int(re.search(r"PlayfieldId\s*=\s*(\d+)", block).group(1))
    name = re.search(r'Name\s*=\s*"([^"]*)"', block).group(1)
    return pf, name


# Drop any existing Drake so we can replace cleanly
filtered = []
for b in existing:
    pf, name = meta(b)
    if name == "Scientist Drake Rodriguez":
        print("removing existing Drake")
        continue
    filtered.append(b)

# Avoid exact coord duplicates for new blocks
def key_pos(block):
    pf, name = meta(block)
    x = float(re.search(r"X\s*=\s*([^f,\n]+)f?", block).group(1))
    y = float(re.search(r"Y\s*=\s*([^f,\n]+)f?", block).group(1))
    z = float(re.search(r"Z\s*=\s*([^f,\n]+)f?", block).group(1))
    return (pf, name, round(x, 2), round(y, 2), round(z, 2))


have = {key_pos(b) for b in filtered}
added = 0
for b in new_blocks:
    b = b.rstrip()
    if not b.endswith(","):
        b += ","
    # indent fix: generated already has correct indent
    k = key_pos(b)
    if k in have:
        print("skip dup", k)
        continue
    filtered.append(b)
    have.add(k)
    added += 1

filtered.sort(key=lambda b: meta(b))
new_body = "\n".join(filtered)
new_text = text[: m.start()] + prefix + new_body + suffix + text[m.end() :]

# Update header counts/comment
count = len(filtered)
by_pf = {}
for b in filtered:
    pf, _ = meta(b)
    by_pf[pf] = by_pf.get(pf, 0) + 1

new_text = re.sub(
    r"/// Captures: .*?\n(/// Total \d+ NPCs \(.*?\).\n)?",
    "/// Captures: 20260718-170408 (4310 Frontier), 20260718-173204 (4311 Crippler cave),\n"
    "    /// 20260718-174130 (4311 Two Mountains), 20260718-180726 (4312 East / Core; Hecklers excluded),\n"
    "    /// 20260718-230406 (4310 Drake + missing frontier roamers; NPCInfo only).\n"
    "    /// Total %d NPCs (4310=%d, 4311=%d, 4312=%d).\n"
    % (count, by_pf.get(4310, 0), by_pf.get(4311, 0), by_pf.get(4312, 0)),
    new_text,
    count=1,
    flags=re.S,
)

open(spawn, "w", encoding="utf-8", newline="\n").write(new_text)
print("spawn written count", count, "added", added, "by_pf", by_pf)

# Remove Executron (player-like MonsterData=0 VisualFlags=127) from Thrak garden.
g = open(garden, encoding="utf-8").read()
g2, n = re.subn(
    r"\n            new GardenNpc\n            \{\n                Name = \"Executron\",.*?\n            \},",
    "",
    g,
    count=1,
    flags=re.S,
)
if n:
    g2 = g2.replace("11 NPCs.", "10 NPCs (Executron removed — player-like VisualFlags/MonsterData).")
    open(garden, "w", encoding="utf-8", newline="\n").write(g2)
    print("removed Executron")
else:
    print("Executron not found or already removed")
