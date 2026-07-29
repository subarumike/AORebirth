p = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\ElysiumEastMobRuntime.cs"
for i, line in enumerate(open(p, encoding="utf-8"), 1):
    if 'string.Equals(name, "Heckler' in line:
        print(i, line.strip())
