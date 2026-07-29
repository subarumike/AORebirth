from pathlib import Path

dynel = Path(
    r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceDynelCapture.cs"
)
text = dynel.read_text(encoding="utf-8")

doors = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_doors_1441792.csfrag").read_text(
    encoding="utf-8"
)
chests = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_chests_1441792.csfrag").read_text(
    encoding="utf-8"
)

# Fix array declarations to match file style (indented public static)
doors = doors.replace(
    "public static readonly string[] Doors_1441792",
    "        public static readonly string[] Doors_1441792",
)
chests = chests.replace(
    "public static readonly string[] Chests_1441792",
    "        public static readonly string[] Chests_1441792",
)
# indent array body lines that start with 4 spaces to 12
def indent_array(block: str) -> str:
    out = []
    for line in block.splitlines():
        if line.startswith("    "):
            out.append("    " + line)
        elif line.startswith("//"):
            out.append("        " + line)
        else:
            out.append(line)
    return "\n".join(out) + "\n"


doors = indent_array(doors)
chests = indent_array(chests)

radar = (
    "        // Radar Display Terminal SIFU — capture 20260728-093557 (template 100358 / 0x18806).\n"
    "        public static readonly string[] Terminals_1441792 =\n"
    "        {\n"
    '            "000F000A0001009300000DAE765A6D343B11256F0000C73D57AC311D000000000B000000000000000043864CCD40A33333438CB3330000000000000000000000003F80000000160000000F424F00000000006F00001F8800000000200032030000001700018806000002BD0000009A000002BE00018806000002BF000188060000019C00000001000001EB0000000100000000",\n'
    "        };\n\n"
)

old_ids = "public static readonly int[] ShapePlayfieldIds = { 1441800, 1443840, 1460226, 1456133, 1419310, 1419335, 1419382, 1419349 };"
new_ids = "public static readonly int[] ShapePlayfieldIds = { 1441800, 1443840, 1460226, 1456133, 1419310, 1419335, 1419382, 1419349, 1441792 };"
if old_ids not in text:
    raise SystemExit("ShapePlayfieldIds not found")
text = text.replace(old_ids, new_ids)

anchor = "        public static string[] GetDoors(int playfieldId)"
if "Doors_1441792" in text:
    raise SystemExit("already patched")
if anchor not in text:
    raise SystemExit("GetDoors anchor missing")
text = text.replace(anchor, doors + "\n" + chests + "\n" + radar + anchor)

text = text.replace(
    "                case 1419349: return Doors_1419349;\n                default: return Doors_1419310;",
    "                case 1419349: return Doors_1419349;\n                case 1441792: return Doors_1441792;\n                default: return Doors_1419310;",
)
text = text.replace(
    "                case 1419349: return Chests_1419349;\n                default: return Chests_1419310;",
    "                case 1419349: return Chests_1419349;\n                case 1441792: return Chests_1441792;\n                default: return Chests_1419310;",
)
text = text.replace(
    """        public static string[] GetTerminals(int playfieldId)
        {
            // Radar Display replay from a foreign layout crashed some clients on zone-in.
            // Keep Archive Storage only for its captured shape; no always-on hologram flood.
            if (playfieldId == 1419310)
            {
                return Terminals_1419310;
            }

            return new string[0];
        }""",
    """        public static string[] GetTerminals(int playfieldId)
        {
            // Radar Display replay from a foreign layout crashed some clients on zone-in.
            // Keep Archive Storage only for its captured shape; no always-on hologram flood.
            if (playfieldId == 1419310)
            {
                return Terminals_1419310;
            }

            // Capture 20260728-093557 RepairMachine Radar Display (Terminal SIFU).
            if (playfieldId == 1441792)
            {
                return Terminals_1441792;
            }

            return new string[0];
        }""",
)

dynel.write_text(text, encoding="utf-8")
print("DynelCapture patched")
