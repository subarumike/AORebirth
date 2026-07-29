# -*- coding: utf-8 -*-
path = r"AORebirth/Server/ZoneEngine/Core/Playfields/AreteAlienAreaMobRuntime.cs"
with open(path, encoding="utf-8") as f:
    lines = f.readlines()

out = []
for line in lines:
    if "new MobSlot(" in line and any(
        n in line for n in ("Angry Minibull", "Saltworm", "Harvey the Bully")
    ):
        line = line.replace(
            "NpcAiProfile.Passive, 0f,",
            "NpcAiProfile.Aggressive, WildlifeAggroRadiusMeters,",
        )
    out.append(line)

text = "".join(out)
text = text.replace(
    """        // Capture 20260726-230559: alien-area Spider/Scout/Specialist/Minibull/Saltworm are
        // passive until the player attacks (no automatic aggro).
        private const float WildlifeAggroRadiusMeters = 0.0f;""",
    """        // Minibull / Saltworm / Harvey / Rollerrat AOS at 5m.
        // Capture 20260726-230559: Spider / Scout / Specialist are passive until player attacks.
        private const float WildlifeAggroRadiusMeters = 5.0f;""",
)

with open(path, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)

a = sum(1 for l in text.splitlines() if "new MobSlot" in l and "Aggressive" in l)
p = sum(1 for l in text.splitlines() if "new MobSlot" in l and "Passive" in l)
print("aggressive slots", a, "passive slots", p)
