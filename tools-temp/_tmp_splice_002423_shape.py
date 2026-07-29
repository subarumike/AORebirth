# Generate + splice shape 1443840 from L7 gold capture 20260725-002423.
from __future__ import print_function
import csv, os, re, struct

ROOT = r"C:\Users\nermi\source\repos\AORebirth"
ASSETS = os.path.join(ROOT, r"tools-temp\_tmp_cap_002423_assets")
CAP = os.path.join(ROOT, r"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260725-002423")
PF = 1443840
FIND = "Malcom Thompon"
SPAWN = (298.199, 5.01, 225.01)

def parse_tex(s):
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
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 2:
            try:
                row = [int(bits[0]), int(bits[1])]
                row.append(int(bits[2]) if len(bits) >= 3 else 0)
                row.append(int(bits[3]) if len(bits) >= 4 else 0)
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

def load_npcs_from_events():
    """Parse unique SCFU with PlayfieldId=1443840 from events.log.

    NPC identity is on the summary [IN-N3] line; DETAIL Identity is the player.
    """
    events = os.path.join(CAP, "events.log")
    by_id = {}
    pending_id = None
    for line in open(events, encoding="utf-8", errors="replace"):
        if "[IN-N3]" in line and "type=SimpleCharFullUpdate" in line:
            mid = re.search(r"identity=\(SimpleChar:([0-9A-F]+)\)", line, re.I)
            pending_id = mid.group(1) if mid else None
            continue
        if "SimpleCharFullUpdateMessage" not in line:
            continue
        if "PlayfieldId=%d" % PF not in line:
            pending_id = None
            continue
        if not pending_id:
            continue
        ident = pending_id
        pending_id = None
        if ident in by_id:
            continue
        mname = re.search(r'Name="([^"]+)"', line)
        if not mname:
            continue
        name = mname.group(1)

        def g(patn, default=0):
            mm = re.search(patn, line)
            return int(mm.group(1)) if mm else default

        pos = re.search(r"Position=\(([^,]+),\s*([^,]+),\s*([^)]+)\)", line)
        x = y = z = 0.0
        if pos:
            x, y, z = float(pos.group(1)), float(pos.group(2)), float(pos.group(3))
        if y > 20:
            continue
        by_id[ident] = {
            "Name": name,
            "Level": g(r"Level=(\d+)"),
            "Health": g(r"Health=(\d+)"),
            "MonsterData": g(r"MonsterData=(\d+)"),
            "MonsterScale": g(r"MonsterScale=(\d+)", 100),
            "HeadMesh": g(r"HeadMesh=(\d+)"),
            "CharacterFlags": g(r"CharacterFlags=(\d+)"),
            "VisibleTitle": g(r"VisibleTitle=(\d+)"),
            "X": x, "Y": y, "Z": z,
            "Identity": ident,
        }
    return by_id

def enrich_from_csv(by_id):
    path = os.path.join(CAP, "scfu-appearance.csv")
    rows = list(csv.DictReader(open(path, encoding="utf-8-sig")))
    for r in rows:
        ident = (r.get("Identity") or "").strip()
        m = re.search(r"SimpleChar:([0-9A-F]+)", ident)
        if not m:
            continue
        key = m.group(1)
        if key not in by_id:
            continue
        by_id[key]["Textures"] = parse_tex(r.get("Textures") or "")
        by_id[key]["Meshes"] = parse_mesh(r.get("Meshes") or "")
        try:
            by_id[key]["Hx"] = float(r.get("HeadingX") or 0)
            by_id[key]["Hy"] = float(r.get("HeadingY") or 0)
            by_id[key]["Hz"] = float(r.get("HeadingZ") or 0)
            by_id[key]["Hw"] = float(r.get("HeadingW") or 1)
        except Exception:
            by_id[key]["Hx"] = by_id[key]["Hy"] = by_id[key]["Hz"] = 0.0
            by_id[key]["Hw"] = 1.0

def gen_shape(by_id):
    # Find target: CharacterFlags 1342706177 preferred, else named FIND at first spawn
    find_id = None
    for ident, r in by_id.items():
        if r.get("CharacterFlags") == 1342706177 and r["Name"] == FIND:
            find_id = ident
            break
    if find_id is None:
        for ident, r in by_id.items():
            if r["Name"] == FIND:
                find_id = ident
                break

    sx, sy, sz = SPAWN
    lines = []
    lines.append("        // Shape playfield %d from capture 20260725-002423 (%d npcs)" % (PF, len(by_id)))
    lines.append("        new MissionShape")
    lines.append("        {")
    lines.append("            CapturedPlayfieldId = %d," % PF)
    lines.append("            SpawnX = %.3ff, SpawnY = %.3ff, SpawnZ = %.3ff," % (sx, sy, sz))
    lines.append("            Npcs = new[]")
    lines.append("            {")
    for ident, r in sorted(by_id.items(), key=lambda kv: (kv[1]["Name"], kv[1]["X"])):
        name = r["Name"]
        role = "MissionNpcRole.FindTarget" if ident == find_id else "MissionNpcRole.Trash"
        lvl = max(1, r["Level"])
        hp = max(1, r["Health"])
        md = r["MonsterData"]
        hm = r["HeadMesh"]
        scale = r.get("MonsterScale") or 100
        tex = r.get("Textures")
        mesh = r.get("Meshes")
        is_grey = "true" if (tex is None or all(len(t) < 2 or t[1] == 0 for t in tex)) else "false"
        safe = name.replace("\\", "\\\\").replace("\"", "\\\"")
        hx = r.get("Hx", 0.0); hy = r.get("Hy", 0.0); hz = r.get("Hz", 0.0); hw = r.get("Hw", 1.0)
        lines.append("                new MissionNpc")
        lines.append("                {")
        lines.append("                    Name = \"%s\"," % safe)
        lines.append("                    Role = %s," % role)
        lines.append("                    Level = %d, Health = %d, MonsterData = %d, Scale = %d, HeadMesh = %d," % (
            lvl, hp, md, scale, hm))
        lines.append("                    X = %.6ff, Y = %.6ff, Z = %.6ff," % (r["X"], r["Y"], r["Z"]))
        lines.append("                    Hx = %.7ff, Hy = %.7ff, Hz = %.7ff, Hw = %.7ff," % (hx, hy, hz, hw))
        lines.append("                    Textures = %s," % csharp_arr2(tex))
        lines.append("                    Meshes = %s," % csharp_arr2(mesh))
        lines.append("                    IsGrey = %s," % is_grey)
        lines.append("                },")
    lines.append("            }")
    lines.append("        },")
    lines.append("")
    return "\n".join(lines), find_id

def gen_string_array(name, path):
    lines = [ln.strip() for ln in open(path, encoding="utf-8").read().splitlines() if ln.strip()]
    cleaned = []
    for hx in lines:
        hx = hx.upper().replace(" ", "")
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

def gen_paf_case():
    pl = bytes.fromhex(open(os.path.join(ASSETS, "paf_%d_payload.hex" % PF)).read().strip())
    parts = ["0x%02X" % b for b in pl]
    rows = []
    for i in range(0, len(parts), 8):
        chunk = parts[i:i+8]
        row = "                       " + ", ".join(chunk)
        if i + 8 < len(parts):
            row += ","
        rows.append(row)
    body = "\n".join(rows)
    return (
        "                case %d:\n"
        "                    // Generator body only — L7 gold 20260725-002423 ACG D74167.\n"
        "                    return new byte[]\n"
        "                    {\n"
        "%s\n"
        "                    };\n" % (PF, body)
    )

by_id = load_npcs_from_events()
print("events npcs", len(by_id))
enrich_from_csv(by_id)
shape_frag, find_id = gen_shape(by_id)
print("find_id", find_id)
open(os.path.join(ASSETS, "shape_1443840.csfrag"), "w", encoding="utf-8").write(shape_frag)
doors_frag = (
    gen_string_array("Doors_1443840", os.path.join(ASSETS, "doors_1443840.txt"))
    + gen_string_array("Chests_1443840", os.path.join(ASSETS, "chests_1443840.txt"))
)
open(os.path.join(ASSETS, "doors_1443840.csfrag"), "w", encoding="utf-8").write(doors_frag)
paf = gen_paf_case()
open(os.path.join(ASSETS, "paf_1443840.csfrag"), "w", encoding="utf-8").write(paf)

# --- splice catalog ---
catalog = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Playfields\MissionInstanceShapeCatalog.cs")
text = open(catalog, encoding="utf-8").read()
if "CapturedPlayfieldId = 1443840" not in text:
    # insert before 1460226 shape
    marker = "        // Shape playfield 1460226 from capture 20260724-224228"
    if marker not in text:
        raise SystemExit("shape marker missing")
    text = text.replace(marker, shape_frag + marker, 1)
    text = text.replace(
        "and Find-Person gold <c>20260724-224228</c> (PFs 1460226 / 1456133).",
        "Find-Person gold <c>20260724-224228</c> (PFs 1460226 / 1456133),\n"
        "    /// and low-QL Find-Person gold <c>20260725-002423</c> (PF 1443840).",
    )
    if "case 1443840:" not in text:
        text = text.replace("                case 1460226:", paf + "                case 1460226:", 1)
    open(catalog, "w", encoding="utf-8", newline="\n").write(text)
    print("spliced shape+PAF")
else:
    print("shape already in catalog")

dynel = os.path.join(ROOT, r"AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceDynelCapture.cs")
dtext = open(dynel, encoding="utf-8").read()
if "Doors_1443840" not in dtext:
    marker = "        public static readonly string[] Doors_1460226 ="
    if marker not in dtext:
        raise SystemExit("doors marker missing")
    dtext = dtext.replace(marker, doors_frag + marker, 1)
    dtext = dtext.replace(
        "public static readonly int[] ShapePlayfieldIds = { 1460226, 1456133, 1419310, 1419335, 1419382, 1419349 };",
        "public static readonly int[] ShapePlayfieldIds = { 1443840, 1460226, 1456133, 1419310, 1419335, 1419382, 1419349 };",
    )
    dtext = dtext.replace(
        "case 1460226: return Doors_1460226;",
        "case 1443840: return Doors_1443840;\n"
        "                case 1460226: return Doors_1460226;",
    )
    dtext = dtext.replace(
        "case 1460226: return Chests_1460226;",
        "case 1443840: return Chests_1443840;\n"
        "                case 1460226: return Chests_1460226;",
    )
    open(dynel, "w", encoding="utf-8", newline="\n").write(dtext)
    print("spliced doors")
else:
    print("doors already present")

print("done")
