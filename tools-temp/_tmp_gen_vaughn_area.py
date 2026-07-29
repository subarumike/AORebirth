# -*- coding: utf-8 -*-
import csv
from pathlib import Path

spawn = Path(r"AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs").read_text(
    encoding="utf-8"
)

skip_mobs = {
    "Garbage Flea",
    "Mutated Garbage Flea",
    "Engineer Automaton I",
    "Alisabai",
    "Mrmrsol",
}

seen = set()
missing = []
with open(
    r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-finish/scfu-appearance.csv",
    encoding="utf-8-sig",
    newline="",
) as f:
    for row in csv.DictReader(f):
        if row.get("PlayfieldId") != "1044525":
            continue
        n = row["Name"]
        ident = row["Identity"]
        if ident in seen or n in skip_mobs:
            continue
        seen.add(ident)
        x, z = float(row["PositionX"]), float(row["PositionZ"])
        if not (3360 <= x <= 3450 and 790 <= z <= 890):
            continue
        # always include Vaughn even if somehow present
        name_present = ('Name = "%s"' % n) in spawn
        if (not name_present) or n == "Vaughn Hammond":
            missing.append(row)

print("missing/need", len(missing))
for row in missing:
    print(row["Name"], row["Identity"], row["PositionX"], row["PositionZ"])


def parse_tex(s):
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 2:
            out.append((int(bits[0]), int(bits[1])))
    return out


def parse_mesh(s):
    if not s:
        return None
    out = []
    for part in s.split("|"):
        bits = part.split(":")
        if len(bits) >= 4:
            out.append(tuple(int(x) for x in bits[:4]))
    return out


side_map = {"Neutral": 0, "Clan": 1, "OmniTek": 1, "Monster": 3, "Omni": 1}
breed_map = {
    "Solitus": 1,
    "Opifex": 2,
    "Nanomage": 1,
    "Atrox": 4,
    "Monster": 6,
    "HumanMonster": 7,
    "Human": 1,
}
gender_map = {"Male": 1, "Uni": 1, "Female": 3, "Neuter": 1}


def side(v):
    return side_map.get(v, 0)


def breed(v):
    return breed_map.get(v, 1)


def gender(v):
    return gender_map.get(v, 1)


lines = []
for row in missing:
    n = row["Name"]
    hx = row["Identity"].split(":")[1].rstrip(")").upper()
    tex = parse_tex(row["Textures"])
    mesh = parse_mesh(row["Meshes"])
    hm = int(row["HeadMesh"] or 0)
    md = int(row["MonsterData"] or 0)
    lines.append("            new AreteNpc")
    lines.append("            {")
    lines.append("                // Capture 20260721-finish %s" % hx)
    lines.append("                CaptureInstance = unchecked((int)0x%s)," % hx)
    lines.append('                Name = "%s",' % n)
    lines.append(
        "                Level = %s, Health = %s, MonsterData = %s, Scale = %s, "
        "VisualFlags = %s, HeadMesh = %s, RunSpeed = %s,"
        % (
            row["Level"],
            row["Health"],
            md,
            row["MonsterScale"],
            row["VisualFlags"],
            hm,
            int(float(row["RunSpeedBase"] or 0)),
        )
    )
    lines.append(
        "                NpcFamily = %s, LosHeight = 0, CharacterFlags = %s, "
        "AppearanceValue = %s,"
        % (row["NpcFamily"] or 0, row["CharacterFlags"], row["AppearanceValue"] or 0)
    )
    lines.append(
        "                Side = %d, Breed = %d, Gender = %d, Race = %s, Fatness = 1, "
        "MovementMode = 3,"
        % (side(row["Side"]), breed(row["Breed"]), gender(row["Gender"]), row["Race"] or 1)
    )
    lines.append(
        "                X = %sf, Y = %sf, Z = %sf,"
        % (row["PositionX"], row["PositionY"], row["PositionZ"])
    )
    hy = row["HeadingY"] or "0"
    hw = row["HeadingW"] or "1"
    lines.append(
        "                Hx = 0.0f, Hy = %sf, Hz = 0.0f, Hw = %sf," % (hy, hw)
    )
    if tex:
        parts = ", ".join("new[] { %d, %d }" % t for t in tex)
        lines.append("                Textures = new[] { %s }," % parts)
    else:
        lines.append(
            "                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, "
            "new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },"
        )
    if mesh:
        parts = ", ".join(
            "new[] { %d, %d, %d, %d }" % m for m in mesh
        )
        lines.append("                Meshes = new[] { %s }," % parts)
    else:
        lines.append("                Meshes = null,")
    lines.append("            },")

out = Path(r"tools-temp/_tmp_vaughn_area_spawn.csfrag")
out.write_text("\n".join(lines) + "\n", encoding="utf-8")
print("wrote", out, "npcs", len(missing))

# loralei exttex extract for Lolly
print("--- LOLLY EXTTEX ---")
with open(
    r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-loralei/scfu-appearance.csv",
    encoding="utf-8-sig",
    newline="",
) as f:
    for row in csv.DictReader(f):
        if row["Name"] != "Lolly the Reet":
            continue
        if "7985CAEC" not in row["Identity"]:
            continue
        body = bytes.fromhex(row["RawBodyHex"])
        idx = body.find(b"cute_birdy")
        start = idx - 4
        chunk = body[start : start + 48]
        print("ident", row["Identity"], "flags", row["FlagsNumeric"])
        print("bytes", list(chunk))
        print("hex", chunk.hex())
        break
