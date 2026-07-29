p = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\ElysiumEastMobRuntime.cs"
t = open(p, encoding="utf-8").read()
for n in [
    "Callous Mortiig",
    "Hai-Tempterus",
    "Tempterus",
    "Shadowleet",
    "Kolaana",
    "CEO Guardian",
    "Heckler of Stones",
]:
    print(n, t.count('Name = "%s"' % n))
print("PlayfieldId = 4540", t.count("PlayfieldId = 4540"))
print("PlayfieldId = 4543", t.count("PlayfieldId = 4543"))
print("Side = 1", t.count("Side = 1"))
print("Side = 2", t.count("Side = 2"))
print("FindAutomaticAggroTarget", "FindAutomaticAggroTarget" in t)
print("AggroRadiusMeters = 8", "AggroRadiusMeters = 8.0f" in t)
