# Patch DynelCapture + ShapeCatalog + MissionInstanceService for fog shape 1441800
import re, os

ROOT = r"AORebirth/Server/ZoneEngine/Core"

# --- DynelCapture: ShapePlayfieldIds + doors/chests + GetDoors/GetChests ---
dynel = os.path.join(ROOT, "Missions/MissionInstanceDynelCapture.cs")
text = open(dynel, encoding="utf-8").read()
text2 = text.replace(
    "public static readonly int[] ShapePlayfieldIds = { 1443840, 1460226, 1456133, 1419310, 1419335, 1419382, 1419349 };",
    "public static readonly int[] ShapePlayfieldIds = { 1441800, 1443840, 1460226, 1456133, 1419310, 1419335, 1419382, 1419349 };",
)
if text2 == text:
    raise SystemExit("ShapePlayfieldIds replace failed")
text = text2

doors = open(r"tools-temp/_tmp_doors_1441800.csfrag", encoding="utf-8").read().strip()
chests = open(r"tools-temp/_tmp_chests_1441800.csfrag", encoding="utf-8").read().strip()
# insert before Doors_1443840
marker = "        public static readonly string[] Doors_1443840 ="
if marker not in text:
    raise SystemExit("Doors_1443840 marker missing")
if "Doors_1441800" not in text:
    text = text.replace(marker, doors + "\n\n        " + chests + "\n\n" + marker)

# GetDoors / GetChests cases
if "case 1441800: return Doors_1441800;" not in text:
    text = text.replace(
        "case 1443840: return Doors_1443840;",
        "case 1441800: return Doors_1441800;\n                case 1443840: return Doors_1443840;",
    )
if "case 1441800: return Chests_1441800;" not in text:
    text = text.replace(
        "case 1443840: return Chests_1443840;",
        "case 1441800: return Chests_1441800;\n                case 1443840: return Chests_1443840;",
    )
open(dynel, "w", encoding="utf-8", newline="\n").write(text)
print("patched DynelCapture")

# --- Shape catalog: insert shape + generator ---
shape_path = os.path.join(ROOT, "Playfields/MissionInstanceShapeCatalog.cs")
st = open(shape_path, encoding="utf-8").read()
frag = open(r"tools-temp/_tmp_shape_1441800.csfrag", encoding="utf-8").read().strip()
marker = "        // Shape playfield 1443840 from capture 20260725-002423"
if "CapturedPlayfieldId = 1441800" not in st:
    if marker not in st:
        raise SystemExit("shape marker missing")
    st = st.replace(marker, frag + "\n\n        " + marker.lstrip())

gen_hex = open(r"tools-temp/_tmp_080425_gen.hex").read().strip()  # 123 bytes D7417D
# build byte array
bs = bytes.fromhex(gen_hex)
rows = []
for i in range(0, len(bs), 8):
    chunk = bs[i:i+8]
    rows.append("                       " + ", ".join("0x%02X" % b for b in chunk) + ",")
gen_case = (
    "                case 1441800:\n"
    "                    // Gold fog ACG D7417D — capture 20260725-151009 / 080425.\n"
    "                    return new byte[]\n"
    "                    {\n"
    + "\n".join(rows) + "\n"
    "                    };\n"
)
if "case 1441800:" not in st:
    st = st.replace(
        "                case 1443840:",
        gen_case + "                case 1443840:",
        1,
    )
open(shape_path, "w", encoding="utf-8", newline="\n").write(st)
print("patched ShapeCatalog")

# --- Force shape 1441800 in ResolveInstancePlayfieldId ---
svc = os.path.join(ROOT, "Missions/MissionInstanceService.cs")
sv = open(svc, encoding="utf-8").read()
old = """            if (objective == MissionRollType.FindPerson)
            {
                // L7 gold humanoid layout for low QL; L220 layouts for high-QL wire/ACG only.
                matched = missionQl > 0 && missionQl < 50
                              ? new[] { 1443840 }
                              : new[] { 1460226, 1456133 };
            }
            else
            {
                matched = missionQl > 0 && missionQl < 50
                              ? new[] { 1443840, 1419310, 1419335, 1419382 }
                              : new[] { 1460226, 1456133, 1419310, 1419335, 1419382 };
            }"""
new = """            // Force fog-proven shape 1441800 (D7417D, capture 20260725-151009). Keep trash
            // variety via ApplyRandomAppearance; layout/doors/ACG match closed PF Map gold.
            matched = new[] { 1441800 };"""
if old not in sv:
    raise SystemExit("ResolveInstancePlayfieldId block not found")
sv = sv.replace(old, new)
open(svc, "w", encoding="utf-8", newline="\n").write(sv)
print("patched MissionInstanceService shape force")
print("done")
