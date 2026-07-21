import csv, os

cap = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260717-210219"
src = os.path.join(cap, "scfu-appearance.csv")
out = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\RomeBlueCitySpawn.cs"

rows = []
seen = set()
with open(src, newline="", encoding="utf-8") as f:
    r = csv.DictReader(f)
    for row in r:
        if row["PlayfieldId"] != "735":
            continue
        if row["CharacterInfoType"] != "NPCInfo":
            continue
        ident = row["Identity"]
        if ident in seen:
            continue
        seen.add(ident)
        rows.append(row)

def fnum(s, d="0"):
    return (s if s not in (None, "") else d)

def parse_tex(s):
    out = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        # place:texture:overlay
        out.append((int(f[0]), int(f[1])))
    return out

def parse_mesh(s):
    out = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        # position:mesh:overridetexture:layer
        out.append((int(f[0]), int(f[1]), int(f[2]), int(f[3])))
    return out

defs = []
for row in rows:
    name = row["Name"].replace('"', '\\"')
    defs.append({
        "name": name,
        "level": int(fnum(row["Level"], "1")),
        "health": int(fnum(row["Health"], "1")),
        "monsterData": int(fnum(row["MonsterData"], "0")),
        "scale": int(fnum(row["MonsterScale"], "100")),
        "visualFlags": int(fnum(row["VisualFlags"], "0")),
        "headMesh": int(fnum(row["HeadMesh"], "0")),
        "x": float(fnum(row["PositionX"], "0")),
        "y": float(fnum(row["PositionY"], "0")),
        "z": float(fnum(row["PositionZ"], "0")),
        "hx": float(fnum(row["HeadingX"], "0")),
        "hy": float(fnum(row["HeadingY"], "0")),
        "hz": float(fnum(row["HeadingZ"], "0")),
        "hw": float(fnum(row["HeadingW"], "1")),
        "tex": parse_tex(row["Textures"]),
        "mesh": parse_mesh(row["Meshes"]),
    })

def cf(v):
    return repr(round(v, 5)) + "f"

lines = []
w = lines.append
w("namespace AORebirth.Core.Playfields")
w("{")
w("    #region Usings ...")
w("")
w("    using System;")
w("")
w("    using AORebirth.Core.Entities;")
w("    using AORebirth.Core.NPCHandler;")
w("    using AORebirth.Core.Textures;")
w("    using AORebirth.Enums;")
w("    using AORebirth.Interfaces;")
w("")
w("    using SmokeLounge.AOtomation.Messaging.GameData;")
w("")
w("    using Utility;")
w("")
w("    using ZoneEngine.Core.Controllers;")
w("")
w("    using Coordinate = AORebirth.Core.Vector.Coordinate;")
w("    using Quaternion = AORebirth.Core.Vector.Quaternion;")
w("")
w("    #endregion")
w("")
w("    /// <summary>")
w("    /// Capture-backed Rome Blue / Omni city population (PF 735 / 0x02DF).")
w("    /// Capture 20260717-210219: %d city NPCs with captured appearance (textures/meshes)." % len(defs))
w("    /// </summary>")
w("    internal static class RomeBlueCitySpawn")
w("    {")
w("        private const int RomeBluePlayfieldId = 735;")
w("")
w("        // Base humanoid template used only to instantiate the Character; all appearance")
w("        // (monsterData, meshes, textures, head, scale) is overridden from the capture.")
w('        private const string TemplateHash = "BART";')
w("")
w("        private sealed class CityNpc")
w("        {")
w("            public string Name;")
w("            public int Level;")
w("            public int Health;")
w("            public int MonsterData;")
w("            public int Scale;")
w("            public int VisualFlags;")
w("            public int HeadMesh;")
w("            public float X;")
w("            public float Y;")
w("            public float Z;")
w("            public float Hx;")
w("            public float Hy;")
w("            public float Hz;")
w("            public float Hw;")
w("            public int[][] Textures;")
w("            public int[][] Meshes;")
w("        }")
w("")
w("        private static readonly CityNpc[] Npcs =")
w("        {")
for d in defs:
    w("            new CityNpc")
    w("            {")
    w('                Name = "%s",' % d["name"])
    w("                Level = %d, Health = %d, MonsterData = %d, Scale = %d, VisualFlags = %d, HeadMesh = %d," % (
        d["level"], d["health"], d["monsterData"], d["scale"], d["visualFlags"], d["headMesh"]))
    w("                X = %s, Y = %s, Z = %s," % (cf(d["x"]), cf(d["y"]), cf(d["z"])))
    w("                Hx = %s, Hy = %s, Hz = %s, Hw = %s," % (cf(d["hx"]), cf(d["hy"]), cf(d["hz"]), cf(d["hw"])))
    tex = ", ".join("new[] { %d, %d }" % (p, t) for (p, t) in d["tex"])
    w("                Textures = new[] { %s }," % tex)
    mesh = ", ".join("new[] { %d, %d, %d, %d }" % (a, b, c, e) for (a, b, c, e) in d["mesh"])
    w("                Meshes = new[] { %s }," % mesh)
    w("            },")
w("        };")
w("")
w("        public static void SpawnForPlayfield(")
w("            Playfield playfield,")
w("            Identity playfieldIdentity,")
w("            Action<ICharacter> activateNpc)")
w("        {")
w("            if (playfield == null || activateNpc == null)")
w("            {")
w("                return;")
w("            }")
w("")
w("            if (playfieldIdentity.Instance != RomeBluePlayfieldId)")
w("            {")
w("                return;")
w("            }")
w("")
w("            int spawned = 0;")
w("            foreach (CityNpc def in Npcs)")
w("            {")
w("                if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))")
w("                {")
w("                    spawned++;")
w("                }")
w("            }")
w("")
w("            LogUtil.Debug(")
w("                DebugInfoDetail.Engine,")
w('                "RomeBlueCitySpawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned')
w('                + "/" + Npcs.Length);')
w("        }")
w("")
w("        private static bool SpawnOne(")
w("            Playfield playfield,")
w("            Identity playfieldIdentity,")
w("            Action<ICharacter> activateNpc,")
w("            CityNpc def)")
w("        {")
w("            var npcController = new NPCController();")
w("            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(")
w("                TemplateHash,")
w("                playfieldIdentity,")
w("                new Coordinate { x = def.X, y = def.Y, z = def.Z },")
w("                new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),")
w("                npcController,")
w("                def.Level);")
w("")
w("            if (mob == null)")
w("            {")
w("                LogUtil.Debug(")
w("                    DebugInfoDetail.Error,")
w('                    "RomeBlueCitySpawn FAILED template=" + TemplateHash + " npc=" + def.Name);')
w("                return false;")
w("            }")
w("")
w("            mob.Name = def.Name;")
w("            mob.Playfield = playfield;")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);")
w("            if (def.Scale > 0)")
w("            {")
w("                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);")
w("            }")
w("")
w("            if (def.HeadMesh > 0)")
w("            {")
w("                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)def.HeadMesh);")
w("            }")
w("")
w("            ApplyAppearance(mob, def);")
w("            mob.Coordinates(new Coordinate { x = def.X, y = def.Y, z = def.Z });")
w("")
w("            mob.DoNotDoTimers = false;")
w("            activateNpc(mob);")
w("            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);")
w("            return true;")
w("        }")
w("")
w("        private static void ApplyAppearance(Character mob, CityNpc def)")
w("        {")
w("            if (def.Textures != null && def.Textures.Length > 0)")
w("            {")
w("                mob.Textures.Clear();")
w("                foreach (int[] t in def.Textures)")
w("                {")
w("                    mob.Textures.Add(new AOTextures(t[0], t[1]));")
w("                }")
w("            }")
w("")
w("            if (def.Meshes != null && def.Meshes.Length > 0)")
w("            {")
w("                mob.MeshLayer.Clear();")
w("                mob.SocialMeshLayer.Clear();")
w("                foreach (int[] m in def.Meshes)")
w("                {")
w("                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);")
w("                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);")
w("                }")
w("            }")
w("        }")
w("    }")
w("}")

with open(out, "w", encoding="utf-8") as f:
    f.write("\n".join(lines) + "\n")

print("Wrote %s with %d NPCs" % (out, len(defs)))
for d in defs:
    print("  %-24s L%-4d md=%-7d head=%-6d tex=%d mesh=%d" % (
        d["name"], d["level"], d["monsterData"], d["headMesh"], len(d["tex"]), len(d["mesh"])))
