# Generate MissionShape 1441800 C# fragment from 151009 NPC list
npcs = [
    ("Mara Sotto", 255.985, 5.01, 255.052, 26155, 3, False),
    ("Love Locknane", 264.907, 5.01, 234.585, 26076, 3, False),
    ("Sina Sosnowski", 257.289, 5.01, 229.188, 26155, 3, False),
    ("Felix Swicord", 256.750, 5.01, 228.576, 26139, 5, True),  # gun mesh in capture
    ("Donald Sosnowski", 243.010, 5.010754, 207.010, 26103, 4, False),  # FindPerson
    ("Byron Lene", 248.988, 5.010753, 195.756, 26159, 3, False),
    ("Probe 2000-2", 240.112, 5.010753, 194.430, 20614, 5, False),
    ("Herb Lindner", 272.844, 5.01, 213.938, 26101, 3, False),
    ("Probe 2000-3", 242.693, 5.01, 252.608, 20614, 5, False),
    ("Probe 2000-1", 271.317, 5.01, 252.365, 20614, 5, False),
    ("Janis Wyles", 234.181, 5.01, 227.146, 26155, 3, False),
    ("Nida Croteau", 243.430, 5.01, 183.010, 26155, 3, False),
    ("Len Fuchs", 242.645, 5.01, 226.583, 26139, 5, True),
    ("Laquanda Gabriel", 256.106, 5.01, 188.158, 26137, 5, False),
    ("Cinda Harrist", 262.672, 5.01, 192.310, 26155, 3, False),
    ("Ma Vallone", 228.495, 5.01, 208.148, 26076, 3, False),
    ("Lashon Timas", 267.651, 5.01, 205.032, 26137, 5, False),
]

# head meshes by md defaults used elsewhere
heads = {26155: 40138, 26076: 40635, 26139: 40249, 26103: 40103, 26159: 40173,
         20614: 0, 26101: 40105, 26137: 40209}

lines = []
lines.append("        // Shape playfield 1441800 from capture 20260725-151009 (fog building D7417D)")
lines.append("        new MissionShape")
lines.append("        {")
lines.append("            CapturedPlayfieldId = 1441800,")
lines.append("            SpawnX = 298.199f, SpawnY = 5.010f, SpawnZ = 235.010f,")
lines.append("            Npcs = new[]")
lines.append("            {")
for name, x, y, z, md, lvl, gun in npcs:
    role = "MissionNpcRole.FindTarget" if name.startswith("Donald") else "MissionNpcRole.Trash"
    hp = 70 if lvl <= 3 else (100 if lvl == 4 else 115)
    head = heads.get(md, 40209)
    # Felix/Len gun: layer-2 weapon mesh like Consuela pattern
    if gun:
        mesh = "new[] { new[] { 0, %d, 0, 4 }, new[] { 1, 30866, 0, 2 } }" % (head if head else 40249)
    elif head > 0:
        mesh = "new[] { new[] { 0, %d, 0, 4 } }" % head
    else:
        mesh = "null"
    lines.append("                new MissionNpc")
    lines.append("                {")
    lines.append('                    Name = "%s",' % name)
    lines.append("                    Role = %s," % role)
    lines.append("                    Level = %d, Health = %d, MonsterData = %d, Scale = 92, HeadMesh = %d," % (lvl, hp, md, head))
    lines.append("                    X = %.6ff, Y = %.6ff, Z = %.6ff," % (x, y, z))
    lines.append("                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,")
    lines.append("                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },")
    lines.append("                    Meshes = %s," % mesh)
    lines.append("                    IsGrey = false,")
    lines.append("                },")
lines.append("            },")
lines.append("        },")
open(r"tools-temp/_tmp_shape_1441800.csfrag", "w", encoding="utf-8").write("\n".join(lines))
print("wrote shape frag", len(npcs))
