import re
from collections import Counter
t = open(r"AORebirth/Server/ZoneEngine/Core/Playfields/AndromedaIccHqSpawn.cs", encoding="utf-8").read()
names = re.findall(r'Name = "([^"]+)"', t)
c = Counter(names)
print("Natalia count", c.get("Natalia Akcora"))
print("dups", [(n, k) for n, k in c.items() if k > 1])
print("total npcs", len(names))
