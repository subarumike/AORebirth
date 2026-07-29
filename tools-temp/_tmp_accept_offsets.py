import re
h = open(r"AORebirth/Server/ZoneEngine/Core/Missions/MissionAcceptCaptureTemplate.cs", encoding="utf-8").read()
hx = "".join(re.findall(r'"([0-9A-Fa-f]+)"', h))
for needle in ["55509493", "00002C42", "51534F52", "0000DAC3"]:
    print(needle, hx.find(needle) // 2)
