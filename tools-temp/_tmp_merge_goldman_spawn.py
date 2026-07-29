# Merge goldman ambient NPCs into AreteLandingSpawn.cs (skip combat/dedicated runtimes).
import re
from pathlib import Path

SPAWN_CS = Path(r"AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs")
FRAG = Path(r"tools-temp/_tmp_goldman_spawn_npcs.csfrag")

COMBAT_OR_OWNED = re.compile(
    r"^(32-V Docker|Waste Collector|Garbage Flea|Mutated Garbage Flea|"
    r"IIV-X Advanced Docker|Supreme Collector of Waste|Surveillance Droid|"
    r"Engineer Automaton I|Bureaucrat Worker|"
    r"(Malfunctioning |Burning )?Cleaning Robot|Cleanmeister Intelligence Robot)$",
    re.I,
)

ALREADY_NAMED = {
    "Rex Larsson",
    "Marcus Stone",
    "Flint Novak",
    "Alex Gibbs",
    "ICC Immigration Officer Bill",
    "Bodyguard Logan Fixx",
    "Desmond Calitri",
    "Barry the Food Vendor",
    "Bruiser",
    "Kneebreaker Alfonzo Rizzolo",
    "Obedience Enforcement",
    "Protester",
    "Wounded Dockworker",
    "Dockworker",
}

raw = SPAWN_CS.read_bytes()
nl = "\r\n" if b"\r\n" in raw else "\n"
text = raw.decode("utf-8")

existing_inst = set(int(x, 16) for x in re.findall(r"CaptureInstance = unchecked\(\(int\)0x([0-9A-Fa-f]+)\)", text))

frag = FRAG.read_text(encoding="utf-8")
blocks = re.findall(r"            new AreteNpc\n            \{.*?\n            \},", frag, re.S)
keep = []
for b in blocks:
    m_inst = re.search(r"CaptureInstance = unchecked\(\(int\)0x([0-9A-Fa-f]+)\)", b)
    m_name = re.search(r'Name = "([^"]+)"', b)
    if not m_inst or not m_name:
        continue
    inst = int(m_inst.group(1), 16)
    name = m_name.group(1)
    if COMBAT_OR_OWNED.match(name):
        continue
    if name in ALREADY_NAMED:
        continue
    if inst in existing_inst:
        continue
    keep.append(b.replace("\n", nl))
    existing_inst.add(inst)

print(f"adding {len(keep)} ambient NPCs from goldman")
for b in keep:
    m_name = re.search(r'Name = "([^"]+)"', b)
    m_inst = re.search(r"0x([0-9A-Fa-f]+)", b)
    print(" ", m_inst.group(1), m_name.group(1))

insert = nl.join(keep) + nl

clear_idx = text.find("internal static void ClearPlayfield")
if clear_idx < 0:
    raise SystemExit("ClearPlayfield not found")
npcs_end = text.rfind("        };", 0, clear_idx)
if npcs_end < 0:
    raise SystemExit("npcs end not found")

new_text = text[:npcs_end] + insert + text[npcs_end:]
new_text = new_text.replace(
    "Kneebreaker Alfonzo Rizzolo (7981F40C) from capture 20260720-171317.",
    "Kneebreaker Alfonzo Rizzolo (7981F40C) from capture 20260720-171317." + nl
    + "    /// Stan-area ambient NPCs/vendors from capture 20260720-goldman (Stanley Goodman cluster).",
)
new_text = new_text.replace(
    'source=20260720-105157");',
    'source=20260720-goldman+prior");',
)
SPAWN_CS.write_bytes(new_text.encode("utf-8"))
print("updated", SPAWN_CS)
