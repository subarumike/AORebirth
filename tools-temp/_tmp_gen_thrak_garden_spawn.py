# Generate ThrakOmniGardenSpawn.cs from capture 20260718-165625 (PF 4677).
import csv
import os

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-165625"
src = os.path.join(cap, "scfu-appearance.csv")
out = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\ThrakOmniGardenSpawn.cs"

rows = []
seen = set()
with open(src, newline="", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("PlayfieldId") != "4677":
            continue
        name = (row.get("Name") or "").strip()
        if not name or name == "Cratonera":
            continue
        ident = row.get("Identity") or ""
        if ident in seen:
            continue
        seen.add(ident)
        rows.append(row)

rows.sort(key=lambda r: r.get("Name") or "")


def fnum(s, d="0"):
    return s if s not in (None, "") else d


def parse_tex(s):
    out = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        place, tex = int(f[0]), int(f[1])
        if tex > 0:
            out.append((place, tex))
    return out


def parse_mesh(s):
    out = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        out.append((int(f[0]), int(f[1]), int(f[2]), int(f[3])))
    return out


def csharp_tex(texs):
    if not texs:
        return "null"
    parts = ", ".join("new[] {{ {0}, {1} }}".format(a, b) for a, b in texs)
    return "new[] {{ {0} }}".format(parts)


def csharp_mesh(meshes):
    if not meshes:
        return "null"
    parts = ", ".join(
        "new[] {{ {0}, {1}, {2}, {3} }}".format(a, b, c, d) for a, b, c, d in meshes
    )
    return "new[] {{ {0} }}".format(parts)


blocks = []
for row in rows:
    name = row["Name"].replace("\\", "\\\\").replace('"', '\\"')
    texs = parse_tex(row.get("Textures"))
    meshes = parse_mesh(row.get("Meshes"))
    head = int(fnum(row.get("HeadMesh"), "0"))
    blocks.append(
        """            new GardenNpc
            {{
                Name = "{name}",
                Level = {level}, Health = {health}, MonsterData = {md}, Scale = {scale}, VisualFlags = {vf}, HeadMesh = {head},
                X = {x}f, Y = {y}f, Z = {z}f,
                Hx = {hx}f, Hy = {hy}f, Hz = {hz}f, Hw = {hw}f,
                Textures = {tex},
                Meshes = {mesh},
            }}""".format(
            name=name,
            level=int(fnum(row.get("Level"), "1")),
            health=int(fnum(row.get("Health"), "1")),
            md=int(fnum(row.get("MonsterData"), "0")),
            scale=int(fnum(row.get("MonsterScale"), "100")),
            vf=int(fnum(row.get("VisualFlags"), "31")),
            head=head,
            x=fnum(row.get("PositionX")),
            y=fnum(row.get("PositionY")),
            z=fnum(row.get("PositionZ")),
            hx=fnum(row.get("HeadingX")),
            hy=fnum(row.get("HeadingY")),
            hz=fnum(row.get("HeadingZ")),
            hw=fnum(row.get("HeadingW"), "1"),
            tex=csharp_tex(texs),
            mesh=csharp_mesh(meshes),
        )
    )

body = ",\n".join(blocks)

cs = open(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_thrak_garden_spawn_template.txt",
    encoding="utf-8",
).read()
cs = cs.replace("__COUNT__", str(len(rows))).replace("__BODY__", body)

with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write(cs)

print("wrote", out, "npcs", len(rows))
for r in rows:
    print(" -", r["Name"])
