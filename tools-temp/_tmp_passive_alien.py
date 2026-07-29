# -*- coding: utf-8 -*-
path = r"AORebirth/Server/ZoneEngine/Core/Playfields/AreteAlienAreaMobRuntime.cs"
with open(path, encoding="utf-8") as f:
    lines = f.readlines()

out = []
for line in lines:
    if "new MobSlot(" in line and "Rollerrat" not in line:
        line = line.replace("NpcAiProfile.Aggressive", "NpcAiProfile.Passive")
        line = line.replace(", WildlifeAggroRadiusMeters,", ", 0f,")
        # numeric aggro radii like 12.0f, 15.0f after Passive
        import re
        line = re.sub(
            r"(NpcAiProfile\.Passive),\s*[0-9]+(?:\.[0-9]+)?f,",
            r"\1, 0f,",
            line,
        )
    out.append(line)

with open(path, "w", encoding="utf-8", newline="\n") as f:
    f.writelines(out)

aggressive = sum(1 for l in out if "NpcAiProfile.Aggressive" in l and "new MobSlot" in l)
passive = sum(1 for l in out if "NpcAiProfile.Passive" in l and "new MobSlot" in l)
print("slot aggressive", aggressive, "passive", passive)
