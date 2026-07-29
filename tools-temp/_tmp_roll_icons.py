# Decode roll library: icon vs ShortInfo snippet for each offer
from pathlib import Path
import re

# Pull hex bodies from MissionRollCaptureLibrary.cs
lib = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Missions\MissionRollCaptureLibrary.cs").read_text(encoding="utf-8", errors="replace")
# Also need to deserialize - hard without C#. Instead grep icons from hex by known icon ints as BE.

# MissionIconId is typically in QuestInfo - search for common pattern.
# Icons as 4-byte BE: 11329=0x2C41, 11330=0x2C42, 11335=0x2C47, 11337=0x2C49, 11342=0x2C4E
icons = {
    "2C41": "ReturnItem(11329)",
    "2C42": "Kill(11330)",
    "2C47": "FindPerson(11335)",
    "2C49": "FindItem(11337)",
    "2C4E": "Repair(11342)",
}

hexes = re.findall(r'"([0-9A-Fa-f]{200,})"', lib)
print("bodies", len(hexes))
for bi, h in enumerate(hexes):
    hu = h.upper()
    found = []
    for code, name in icons.items():
        # count occurrences of 0000XXXX pattern? icon often as 00002C41
        pat = "0000" + code
        c = hu.count(pat)
        if c:
            found.append(f"{name}x{c}")
    print(f"roll[{bi}]: {', '.join(found) if found else 'no-icon-hits'}")

# Also check MissionRollCaptureTemplate single template
tpl = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Missions\MissionRollCaptureTemplate.cs").read_text(encoding="utf-8", errors="replace")
th = re.findall(r'"([0-9A-Fa-f]{200,})"', tpl)
print("template bodies", len(th))
for bi, h in enumerate(th[:3]):
    hu = h.upper()
    found = []
    for code, name in icons.items():
        c = hu.count("0000" + code)
        if c:
            found.append(f"{name}x{c}")
    print(f"tpl[{bi}]: {', '.join(found) if found else 'none'}")
