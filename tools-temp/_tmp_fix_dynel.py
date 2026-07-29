from pathlib import Path

path = Path(
    r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceDynelCapture.cs"
)
text = path.read_text(encoding="utf-8")

# Fix awkward indentation from splice for Doors_1441792 / Chests_1441792
old_doors = """        // Shape 1441792 capture 20260728-093557 unique doors=18
            public static readonly string[] Doors_1441792 =
{
"""
new_doors = """        // Shape 1441792 capture 20260728-093557 unique doors=18
        public static readonly string[] Doors_1441792 =
        {
"""
old_chests = """        // Shape 1441792 capture 20260728-093557 unique chests=14
            public static readonly string[] Chests_1441792 =
{
"""
new_chests = """        // Shape 1441792 capture 20260728-093557 unique chests=14
        public static readonly string[] Chests_1441792 =
        {
"""
if old_doors not in text:
    raise SystemExit("doors header missing")
if old_chests not in text:
    raise SystemExit("chests header missing")
text = text.replace(old_doors, new_doors).replace(old_chests, new_chests)

# Fix closing braces that lost indent
text = text.replace(
    """        \"00FA000A000100C700000DAE765A6D34365A50710000C748109AD249000000000B0000000000000000436FFFBE40A00000437F0000000000003F3504F300000000BF3504F300160000000F424F00000001006F00002F4C0000000080081443000000170000A255000002BD00000000000002BE00000000000002BF000000000000019C00000001000000FC00000000000000C000000000000000C100000000000000C3000000000000010300000000000000000000000200000032000003F10000000200110004\",\n};\n\n        // Shape 1441792 capture 20260728-093557 unique chests=14""",
    """        \"00FA000A000100C700000DAE765A6D34365A50710000C748109AD249000000000B0000000000000000436FFFBE40A00000437F0000000000003F3504F300000000BF3504F300160000000F424F00000001006F00002F4C0000000080081443000000170000A255000002BD00000000000002BE00000000000002BF000000000000019C00000001000000FC00000000000000C000000000000000C100000000000000C3000000000000010300000000000000000000000200000032000003F10000000200110004\",\n        };\n\n        // Shape 1441792 capture 20260728-093557 unique chests=14""",
)
text = text.replace(
    """        \"01E8000A0001007F00000DAE765A6D34465A5D730000C7490B605758000000000B0000C350765A6D3400160000000F424F00000000015200001B97000000000000000100000017000462CF000002BD00000001000002BE000462CF000002BF000462CF0000019C00000001000000000000000200000032000003F100000003\",\n};\n\n        // Radar Display Terminal SIFU""",
    """        \"01E8000A0001007F00000DAE765A6D34465A5D730000C7490B605758000000000B0000C350765A6D3400160000000F424F00000000015200001B97000000000000000100000017000462CF000002BD00000001000002BE000462CF000002BF000462CF0000019C00000001000000000000000200000032000003F100000003\",\n        };\n\n        // Radar Display Terminal SIFU""",
)

# Indent hex lines inside those two arrays (lines that start with 8 spaces + quote after the Doors header)
# Simpler: replace "        \"" that are only 8 spaces before quote between doors/chests - already 8 spaces which is wrong; should be 12.
# Current hex lines use 8 spaces; style uses 12. Leave as-is if compile-safe — other arrays in file use 12.
# Normalize only the 1441792 block hex lines that currently have 8 spaces.
import re

def indent_block(src: str, start_marker: str, end_marker: str) -> str:
    i = src.find(start_marker)
    j = src.find(end_marker, i)
    if i < 0 or j < 0:
        raise SystemExit("block markers missing: " + start_marker)
    block = src[i:j]
    fixed = []
    for line in block.splitlines(True):
        if line.startswith('        "') and not line.startswith('            "'):
            fixed.append("    " + line)
        else:
            fixed.append(line)
    return src[:i] + "".join(fixed) + src[j:]

text = indent_block(text, "public static readonly string[] Doors_1441792", "public static readonly string[] Chests_1441792")
text = indent_block(text, "public static readonly string[] Chests_1441792", "public static readonly string[] Terminals_1441792")

path.write_text(text, encoding="utf-8")
print("DynelCapture formatting fixed")
