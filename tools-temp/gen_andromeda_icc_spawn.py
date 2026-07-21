import csv
import os
from collections import defaultdict

cap = r"tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260719-ICC-Capture"
src = os.path.join(cap, "scfu-appearance.csv")
mov = os.path.join(cap, "movement-packets.csv")
out = r"AORebirth/Server/ZoneEngine/Core/Playfields/AndromedaIccHqSpawn.cs"

EXCLUDE = {"Windcaller Karrec", "Annoying Dude", "Maddy Cardile"}

SIDE = {"Neutral": 0, "Clan": 1, "Omni": 2, "Monster": 3, "Advisor": 4, "Guardian": 5, "Pet": 6, "Shadow": 7}
BREED = {"None": 0, "Solitus": 1, "Opifex": 2, "Nanomage": 3, "Atrox": 4, "Special": 5, "Monster": 6, "HumanMonster": 7}
# Capture Gender strings map to SmokeLounge Gender enum
GENDER = {"None": 0, "Neutral": 1, "Male": 2, "Female": 3, "Uni": 1, "Neuter": 1}
FATNESS = {"Thin": 0, "Normal": 1, "Fat": 2}

paths = defaultdict(list)
with open(mov, newline="", encoding="utf-8") as f:
    for r in csv.DictReader(f):
        if r.get("MessageType") != "FollowTarget" or r.get("FollowKind") != "NpcPath":
            continue
        if not r.get("CurrentX") or not r.get("DestinationX"):
            continue
        inst = (r.get("SourceInstance") or "").upper()
        paths[inst].append(
            (
                float(r["CurrentX"]),
                float(r["CurrentY"]),
                float(r["CurrentZ"]),
                float(r["DestinationX"]),
                float(r["DestinationY"]),
                float(r["DestinationZ"]),
            )
        )

rows = []
seen = set()
with open(src, newline="", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        if row["PlayfieldId"] != "655":
            continue
        if row["CharacterInfoType"] != "NPCInfo":
            continue
        if row["Name"] in EXCLUDE:
            continue
        ident = row["Identity"]
        if ident in seen:
            continue
        seen.add(ident)
        rows.append(row)


def fnum(s, d="0"):
    return s if s not in (None, "") else d


def parse_tex(s):
    outl = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        outl.append((int(f[0]), int(f[1])))
    return outl


def parse_mesh(s):
    outl = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        outl.append((int(f[0]), int(f[1]), int(f[2]), int(f[3])))
    return outl


def parse_wp(s):
    outl = []
    for part in (s or "").split("|"):
        if not part:
            continue
        f = part.split(":")
        outl.append((float(f[0]), float(f[1]), float(f[2])))
    return outl


def identity_hex(ident):
    return ident.split(":")[-1].rstrip(")").upper()


def movement_mode(row):
    hx = (row.get("ScfuUnknown1Hex") or "").replace(" ", "")
    if len(hx) >= 26:
        try:
            return int(hx[24:26], 16)
        except ValueError:
            pass
    return 1


def waypoints_for(row):
    wp = parse_wp(row.get("Waypoints") or "")
    if len(wp) >= 2:
        return wp
    inst = identity_hex(row["Identity"])
    samples = paths.get(inst) or []
    if not samples:
        return []
    sx = float(fnum(row["PositionX"]))
    sy = float(fnum(row["PositionY"]))
    sz = float(fnum(row["PositionZ"]))
    best = None
    best_d = -1.0
    for cx, cy, cz, dx, dy, dz in samples:
        d = (dx - sx) ** 2 + (dy - sy) ** 2 + (dz - sz) ** 2
        if d > best_d:
            best_d = d
            best = (dx, dy, dz)
    if best is None or best_d < 0.25:
        return []
    return [(sx, sy, sz), best]


defs = []
for row in rows:
    name = row["Name"].replace("\\", "\\\\").replace('"', '\\"')
    defs.append(
        {
            "name": name,
            "level": int(fnum(row["Level"], "1")),
            "health": int(fnum(row["Health"], "1")),
            "monsterData": int(fnum(row["MonsterData"], "0")),
            "scale": int(fnum(row["MonsterScale"], "100")),
            "visualFlags": int(fnum(row["VisualFlags"], "0")),
            "headMesh": int(fnum(row["HeadMesh"], "0")),
            "runSpeed": int(fnum(row["RunSpeedBase"], "0")),
            "npcFamily": int(fnum(row["NpcFamily"], "0")),
            "losHeight": int(fnum(row["NpcLosHeight"], "0")),
            "characterFlags": int(fnum(row["CharacterFlags"], "0")),
            "appearanceValue": int(fnum(row["AppearanceValue"], "0")),
            "side": SIDE.get(row.get("Side") or "Neutral", 0),
            "breed": BREED.get(row.get("Breed") or "Solitus", 1),
            "gender": GENDER.get(row.get("Gender") or "Male", 2),
            "race": int(fnum(row["Race"], "1")),
            "fatness": FATNESS.get(row.get("Fatness") or "Normal", 1),
            "movementMode": movement_mode(row),
            "x": float(fnum(row["PositionX"])),
            "y": float(fnum(row["PositionY"])),
            "z": float(fnum(row["PositionZ"])),
            "hx": float(fnum(row["HeadingX"])),
            "hy": float(fnum(row["HeadingY"])),
            "hz": float(fnum(row["HeadingZ"])),
            "hw": float(fnum(row["HeadingW"], "1")),
            "tex": parse_tex(row["Textures"]),
            "mesh": parse_mesh(row["Meshes"]),
            "wp": waypoints_for(row),
        }
    )


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
w("    using ZoneEngine.Core;")
w("    using ZoneEngine.Core.Controllers;")
w("")
w("    using Coordinate = AORebirth.Core.Vector.Coordinate;")
w("    using Quaternion = AORebirth.Core.Vector.Quaternion;")
w("    using Vector3 = AORebirth.Core.Vector.Vector3;")
w("")
w("    #endregion")
w("")
w("    /// <summary>")
w("    /// Capture-backed ICC HQ Andromeda population (PF 655 / 0x028F).")
w(
    "    /// Capture 20260719-ICC-Capture: %d city NPCs (players excluded; Karrec trio owned separately)."
    % len(defs)
)
w("    /// </summary>")
w("    internal static class AndromedaIccHqSpawn")
w("    {")
w("        private const int AndromedaPlayfieldId = 655;")
w("")
w("        // Base humanoid template used only to instantiate the Character; all appearance")
w("        // (monsterData, meshes, textures, head, scale, identity) is overridden from the capture.")
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
w("            public int RunSpeed;")
w("            public int NpcFamily;")
w("            public int LosHeight;")
w("            public int CharacterFlags;")
w("            public int AppearanceValue;")
w("            public int Side;")
w("            public int Breed;")
w("            public int Gender;")
w("            public int Race;")
w("            public int Fatness;")
w("            public int MovementMode;")
w("            public float X;")
w("            public float Y;")
w("            public float Z;")
w("            public float Hx;")
w("            public float Hy;")
w("            public float Hz;")
w("            public float Hw;")
w("            public int[][] Textures;")
w("            public int[][] Meshes;")
w("            public float[][] Waypoints;")
w("        }")
w("")
w("        private static readonly CityNpc[] Npcs =")
w("        {")
for d in defs:
    w("            new CityNpc")
    w("            {")
    w('                Name = "%s",' % d["name"])
    w(
        "                Level = %d, Health = %d, MonsterData = %d, Scale = %d, VisualFlags = %d, HeadMesh = %d, RunSpeed = %d,"
        % (
            d["level"],
            d["health"],
            d["monsterData"],
            d["scale"],
            d["visualFlags"],
            d["headMesh"],
            d["runSpeed"],
        )
    )
    w(
        "                NpcFamily = %d, LosHeight = %d, CharacterFlags = %d, AppearanceValue = %d,"
        % (d["npcFamily"], d["losHeight"], d["characterFlags"], d["appearanceValue"])
    )
    w(
        "                Side = %d, Breed = %d, Gender = %d, Race = %d, Fatness = %d, MovementMode = %d,"
        % (d["side"], d["breed"], d["gender"], d["race"], d["fatness"], d["movementMode"])
    )
    w("                X = %s, Y = %s, Z = %s," % (cf(d["x"]), cf(d["y"]), cf(d["z"])))
    w(
        "                Hx = %s, Hy = %s, Hz = %s, Hw = %s,"
        % (cf(d["hx"]), cf(d["hy"]), cf(d["hz"]), cf(d["hw"]))
    )
    tex = ", ".join("new[] { %d, %d }" % (p, t) for (p, t) in d["tex"])
    w("                Textures = new[] { %s }," % tex)
    if d["mesh"]:
        mesh = ", ".join(
            "new[] { %d, %d, %d, %d }" % (a, b, c, e) for (a, b, c, e) in d["mesh"]
        )
        w("                Meshes = new[] { %s }," % mesh)
    else:
        w("                Meshes = null,")
    if d["wp"]:
        wp = ", ".join(
            "new[] { %s, %s, %s }" % (cf(x), cf(y), cf(z)) for (x, y, z) in d["wp"]
        )
        w("                Waypoints = new[] { %s }," % wp)
    else:
        w("                Waypoints = null,")
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
w("            if (playfieldIdentity.Instance != AndromedaPlayfieldId)")
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
w(
    '                "AndromedaIccHqSpawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned'
)
w('                + "/" + Npcs.Length);')
w("        }")
w("")
w("        private static bool SpawnOne(")
w("            Playfield playfield,")
w("            Identity playfieldIdentity,")
w("            Action<ICharacter> activateNpc,")
w("            CityNpc def)")
w("        {")
w("            var npcController = new NPCController { AiProfile = NpcAiProfile.Social };")
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
w(
    '                    "AndromedaIccHqSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);'
)
w("                return false;")
w("            }")
w("")
w("            mob.Name = def.Name;")
w("            mob.FirstName = string.Empty;")
w("            mob.LastName = string.Empty;")
w("            mob.Playfield = playfield;")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)def.NpcFamily);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.losheight, (uint)def.LosHeight);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)def.CharacterFlags);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)def.Side);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.breed, (uint)def.Breed);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.sex, (uint)def.Gender);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.race, (uint)def.Race);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.fatness, (uint)def.Fatness);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.accountflags, 0);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.expansion, 0);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.currentmovementmode, (uint)def.MovementMode);")
w("            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.prevmovementmode, (uint)def.MovementMode);")
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
w("            if (def.RunSpeed > 0)")
w("            {")
w("                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, (uint)def.RunSpeed);")
w("            }")
w("")
w("            ApplyAppearance(mob, def);")
w("            ApplyWaypoints(mob, npcController, def);")
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
w("            else if (def.HeadMesh > 0)")
w("            {")
w("                // Capture had headmesh only (e.g. Natalia) — still emit head layer.")
w("                mob.MeshLayer.Clear();")
w("                mob.SocialMeshLayer.Clear();")
w("                mob.MeshLayer.AddMesh(0, def.HeadMesh, 0, 4);")
w("                mob.SocialMeshLayer.AddMesh(0, def.HeadMesh, 0, 4);")
w("            }")
w("        }")
w("")
w("        private static void ApplyWaypoints(Character mob, NPCController controller, CityNpc def)")
w("        {")
w("            if (def.Waypoints == null || def.Waypoints.Length < 2)")
w("            {")
w("                return;")
w("            }")
w("")
w("            mob.Waypoints.Clear();")
w("            foreach (float[] wp in def.Waypoints)")
w("            {")
w("                mob.AddWaypoint(new Vector3(wp[0], wp[1], wp[2]), false);")
w("            }")
w("")
w("            controller.State = CharacterState.Patrolling;")
w("        }")
w("    }")
w("}")

with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")

wp_count = sum(1 for d in defs if d["wp"] and len(d["wp"]) >= 2)
print("Wrote %s npcs=%d with_waypoints=%d" % (out, len(defs), wp_count))
for d in defs:
    if d["name"] in (
        "Peacekeeper Constad",
        "Transportation Officer Darren Plush",
        "Natalia Akcora",
    ):
        print(
            "  FIX %-36s L%-4d family=%d flags=%d head=%d mode=%d mesh=%d"
            % (
                d["name"],
                d["level"],
                d["npcFamily"],
                d["characterFlags"],
                d["headMesh"],
                d["movementMode"],
                len(d["mesh"] or []),
            )
        )
