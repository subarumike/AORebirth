from pathlib import Path

text = Path("tools-temp/_tmp_repair_machine_extract.txt").read_text(encoding="utf-8")
doors = []
chests = []
for line in text.splitlines():
    if line.startswith("DOOR_"):
        doors.append(line.split("=", 1)[1])
    elif line.startswith("CHEST_"):
        idx = int(line.split("=", 1)[0].split("_")[1])
        if idx <= 4:
            chests.append(line.split("=", 1)[1])

disp = (
    "000E000A0001009300000DBE7996C0283B11256F0000C73D5796D7EC"
    "000000000B0000000000000000421866684121999A438D014800000000"
    "00000000000000003F8000000016C847000F424F00000000006F00001F88"
    "000000002000320300000017000187F9000002BD0000009A000002BE"
    "000187F9000002BF000187F90000019C00000001000001EB0000000100000000"
)

lines = []
lines.append(
    "        // Capture 20260727-mission-repair-machine-new PF 1493063 (0x16C847) ACG D7425E."
)
lines.append("        public static readonly string[] Doors_1493063 =")
lines.append("        {")
for h in doors:
    lines.append('            "' + h + '",')
lines.append("        };")
lines.append("")
lines.append("        public static readonly string[] Chests_1493063 =")
lines.append("        {")
for h in chests:
    lines.append('            "' + h + '",')
lines.append("        };")
lines.append("")
lines.append("        // Theft Secure Food Dispenser template 100345 (0x187F9).")
lines.append("        public static readonly string[] Terminals_1493063 =")
lines.append("        {")
lines.append('            "' + disp + '",')
lines.append("        };")
lines.append("")

out = Path("tools-temp/_tmp_dynel_1493063.csfrag")
out.write_text("\n".join(lines) + "\n", encoding="utf-8")
print("doors", len(doors), "chests", len(chests), "->", out)
