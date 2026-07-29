# Generate + splice 1460226 / 1456133 shapes, doors, chests, ACG from 20260724-224228.
from __future__ import print_function
import csv, os, re, struct

ROOT = r"C:\Users\nermi\source\repos\AORebirth"
ASSETS = os.path.join(ROOT, r"tools-temp\_tmp_cap_224228_assets")
CAP = os.path.join(ROOT, r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260724-224228")

SHAPES = {
    1460226: {"spawn": (298.199, 5.01, 225.01), "find": "Jeanne Messamore"},
    1456133: {"spawn": (298.199, 5.01, 255.01), "find": "Lanny Marsalis"},
}

def parse_tex(s):
    # 0:9418:0|1:8729:0 -> [[0,9418],[1,8729]]
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 2:
            try:
                out.append([int(bits[0]), int(bits[1])])
            except Exception:
                pass
    return out or None

def parse_mesh(s):
    # 0:20055:0:2|0:40209:0:4
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 2:
            try:
                row = [int(bits[0]), int(bits[1])]
                if len(bits) >= 3:
                    row.append(int(bits[2]))
                else:
                    row.append(0)
                if len(bits) >= 4:
                    row.append(int(bits[3]))
                else:
                    row.append(0)
                out.append(row)
            except Exception:
                pass
    return out or None

def csharp_arr2(arr):
    if not arr:
        return "null"
    parts = []
    for row in arr:
        parts.append("new[] { %s }" % ", ".join(str(x) for x in row))
    return "new[] { %s }" % ", ".join(parts)

def gen_shape(pf):
    scfu = list(csv.DictReader(open(os.path.join(CAP, "scfu-appearance.csv"), encoding="utf-8-sig")))
    by_id = {}
    for r in scfu:
        if (r.get("PlayfieldId") or "") != str(pf):
            continue
        ident = r.get("Identity") or ""
        if ident and ident not in by_id:
            by_id[ident] = r
    find_name = SHAPES[pf]["find"]
    sx, sy, sz = SHAPES[pf]["spawn"]
    npcs = []
    for r in by_id.values():
        name = r.get("Name") or "?"
        if name in ("Cratonera", "Carlo Pinnetti", "CEO Guardian"):
            continue
        role = "MissionNpcRole.FindTarget" if name == find_name else "MissionNpcRole.Trash"
        try:
            lvl = int(float(r.get("Level") or 1))
        except Exception:
            lvl = 1
        try:
            md = int(float(r.get("MonsterData") or 0))
        except Exception:
            md = 0
        try:
            hm = int(float(r.get("HeadMesh") or 0))
        except Exception:
            hm = 0
        x = float(r.get("PositionX") or 0)
        y = float(r.get("PositionY") or 5.01)
        z = float(r.get("PositionZ") or 0)
        tex = parse_tex(r.get("Textures") or "")
        mesh = parse_mesh(r.get("Meshes") or "")
        # grey if no body textures
        is_grey = "true" if (tex is None or all(t[1] == 0 for t in tex if len(t) > 1)) else "false"
        npcs.append((name, role, lvl, md, hm, x, y, z, tex, mesh, is_grey))

    lines = []
    lines.append("        // Shape playfield %d from capture 20260724-224228 (%d npcs)" % (pf, len(npcs)))
    lines.append("        new MissionShape")
    lines.append("        {")
    lines.append("            CapturedPlayfieldId = %d," % pf)
    lines.append("            SpawnX = %.3ff, SpawnY = %.3ff, SpawnZ = %.3ff," % (sx, sy, sz))
    lines.append("            Npcs = new[]")
    lines.append("            {")
    for name, role, lvl, md, hm, x, y, z, tex, mesh, is_grey in sorted(npcs, key=lambda t: t[0]):
        safe = name.replace("\\", "\\\\").replace("\"", "\\\"")
        lines.append("                new MissionNpc")
        lines.append("                {")
        lines.append("                    Name = \"%s\"," % safe)
        lines.append("                    Role = %s," % role)
        lines.append("                    Level = %d, Health = %d, MonsterData = %d, Scale = 100, HeadMesh = %d," % (
            lvl, max(500, lvl * 90), md, hm))
        lines.append("                    X = %.6ff, Y = %.6ff, Z = %.6ff," % (x, y, z))
        lines.append("                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,")
        lines.append("                    Textures = %s," % csharp_arr2(tex))
        lines.append("                    Meshes = %s," % csharp_arr2(mesh))
        lines.append("                    IsGrey = %s," % is_grey)
        lines.append("                },")
    lines.append("            }")
    lines.append("        },")
    lines.append("")
    return "\n".join(lines)

def gen_string_array(name, path):
    lines = open(path, encoding="utf-8").read().splitlines()
    lines = [ln.strip() for ln in lines if ln.strip()]
    # Trim to DoorFullUpdate body style: existing uses from 000x000A...
    cleaned = []
    for hx in lines:
        hx = hx.upper().replace(" ", "")
        # Prefer start at first 000x000A pattern (4 hex seq + 000A)
        m = re.search(r"[0-9A-F]{4}000A000100", hx)
        if m:
            hx = hx[m.start():]
        cleaned.append(hx)
    out = ["        public static readonly string[] %s =" % name, "        {"]
    for hx in cleaned:
        out.append('            "%s",' % hx)
    out.append("        };")
    out.append("")
    return "\n".join(out)

def gen_paf_case(pf):
    pl = bytes.fromhex(open(os.path.join(ASSETS, "paf_%d.hex" % pf)).read().strip())
    parts = ["0x%02X" % b for b in pl]
    rows = []
    for i in range(0, len(parts), 8):
        rows.append("                       " + ", ".join(parts[i:i+8]) + ",")
    # drop trailing comma on last conceptually ok in C# array
    body = "\n".join(rows)
    if body.endswith(","):
        body = body[:-1]
    return (
        "                case %d:\n"
        "                    return new byte[]\n"
        "                    {\n"
        "%s\n"
        "                    };\n" % (pf, body)
    )

# --- write frags ---
shape_frag = gen_shape(1460226) + gen_shape(1456133)
open(os.path.join(ASSETS, "shapes.csfrag"), "w", encoding="utf-8").write(shape_frag)

doors_frag = (
    gen_string_array("Doors_1460226", os.path.join(ASSETS, "doors_1460226.txt"))
    + gen_string_array("Chests_1460226", os.path.join(ASSETS, "chests_1460226.txt"))
    + gen_string_array("Doors_1456133", os.path.join(ASSETS, "doors_1456133.txt"))
    + gen_string_array("Chests_1456133", os.path.join(ASSETS, "chests_1456133.txt"))
)
open(os.path.join(ASSETS, "doors.csfrag"), "w", encoding="utf-8").write(doors_frag)
open(os.path.join(ASSETS, "paf_cases.csfrag"), "w", encoding="utf-8").write(
    gen_paf_case(1460226) + gen_paf_case(1456133)
)

# --- splice shape catalog ---
catalog = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Playfields\MissionInstanceShapeCatalog.cs")
text = open(catalog, encoding="utf-8").read()
marker = "\n        };\n\n        internal static MissionShape PickShape"
if "CapturedPlayfieldId = 1460226" not in text:
    text = text.replace(marker, "\n" + shape_frag + "        };\n\n        internal static MissionShape PickShape", 1)
    # update summary comment
    text = text.replace(
        "and Find-Person capture <c>20260724-mission-find-person</c> (PF 1419349).",
        "Find-Person <c>20260724-mission-find-person</c> (PF 1419349),\n"
        "    /// and Find-Person gold <c>20260724-224228</c> (PFs 1460226 / 1456133).",
    )
    # insert PAF cases before case 1419349
    paf = open(os.path.join(ASSETS, "paf_cases.csfrag"), encoding="utf-8").read()
    if "case 1460226:" not in text:
        text = text.replace("                case 1419349:", paf + "                case 1419349:")
    open(catalog, "w", encoding="utf-8", newline="\n").write(text)
    print("spliced shapes+PAF into catalog")
else:
    print("shapes already in catalog")

# --- splice dynel capture ---
dynel = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceDynelCapture.cs")
dtext = open(dynel, encoding="utf-8").read()
if "Doors_1460226" not in dtext:
    marker = "        // World Terminal SimpleItemFullUpdate"
    if marker not in dtext:
        # fallback: before GetDoors
        marker = "        public static string[] GetDoors"
        insert = doors_frag + "\n        "
        dtext = dtext.replace(marker, insert + marker, 1)
    else:
        dtext = dtext.replace(marker, doors_frag + "\n" + marker, 1)

    dtext = dtext.replace(
        "public static readonly int[] ShapePlayfieldIds = { 1419310, 1419335, 1419382, 1419349 };",
        "public static readonly int[] ShapePlayfieldIds = { 1460226, 1456133, 1419310, 1419335, 1419382, 1419349 };",
    )
    # GetDoors switch
    dtext = dtext.replace(
        "case 1419310: return Doors_1419310;",
        "case 1460226: return Doors_1460226;\n"
        "                case 1456133: return Doors_1456133;\n"
        "                case 1419310: return Doors_1419310;",
    )
    dtext = dtext.replace(
        "case 1419310: return Chests_1419310;",
        "case 1460226: return Chests_1460226;\n"
        "                case 1456133: return Chests_1456133;\n"
        "                case 1419310: return Chests_1419310;",
    )
    open(dynel, "w", encoding="utf-8", newline="\n").write(dtext)
    print("spliced doors into DynelCapture")
else:
    print("doors already present")

print("done")
