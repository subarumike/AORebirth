# Splice Doors_1419349 / Chests_1419349 into MissionInstanceDynelCapture.cs
from pathlib import Path

frag = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_doors_1419349.csfrag").read_text(encoding="utf-8")
target = Path(
    r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceDynelCapture.cs"
)
text = target.read_text(encoding="utf-8")

# Insert before Terminals_1419310
marker = "        // World Terminal SimpleItemFullUpdate"
if "Doors_1419349" in text:
    print("already present")
else:
    insert = (
        "        // Capture 20260724-mission-find-person PF 1419349\n"
        + frag.replace("public static", "        public static")
        + "\n\n"
    )
    # frag already has indentation on strings; fix class-level decl
    insert = insert.replace(
        "        public static readonly string[] Doors_1419349",
        "        public static readonly string[] Doors_1419349",
    )
    text = text.replace(marker, insert + marker)

text = text.replace(
    "public static readonly int[] ShapePlayfieldIds = { 1419310, 1419335, 1419382 };",
    "public static readonly int[] ShapePlayfieldIds = { 1419310, 1419335, 1419382, 1419349 };",
)

old_doors = """        public static string[] GetDoors(int playfieldId)
        {
            switch (playfieldId)
            {
                case 1419310: return Doors_1419310;
                case 1419335: return Doors_1419335;
                case 1419382: return Doors_1419382;
                default: return Doors_1419310;
            }
        }"""
new_doors = """        public static string[] GetDoors(int playfieldId)
        {
            switch (playfieldId)
            {
                case 1419310: return Doors_1419310;
                case 1419335: return Doors_1419335;
                case 1419382: return Doors_1419382;
                case 1419349: return Doors_1419349;
                default: return Doors_1419310;
            }
        }"""
if old_doors not in text:
    raise SystemExit("GetDoors block not found")
text = text.replace(old_doors, new_doors)

old_chests = """        public static string[] GetChests(int playfieldId)
        {
            switch (playfieldId)
            {
                case 1419310: return Chests_1419310;
                case 1419335: return Chests_1419335;
                case 1419382: return Chests_1419382;
                default: return Chests_1419310;
            }
        }"""
# read actual GetChests from file if slightly different
import re
m = re.search(r"public static string\[\] GetChests\(int playfieldId\)\s*\{.*?default: return Chests_1419310;\s*\}", text, re.S)
if not m:
    raise SystemExit("GetChests not found")
new_chests = """public static string[] GetChests(int playfieldId)
        {
            switch (playfieldId)
            {
                case 1419310: return Chests_1419310;
                case 1419335: return Chests_1419335;
                case 1419382: return Chests_1419382;
                case 1419349: return Chests_1419349;
                default: return Chests_1419310;
            }
        }"""
text = text[: m.start()] + new_chests + text[m.end() :]

target.write_text(text, encoding="utf-8")
print("patched", target)
print("has Doors_1419349", "Doors_1419349" in text)
print("has case 1419349 doors", "case 1419349: return Doors_1419349" in text)
