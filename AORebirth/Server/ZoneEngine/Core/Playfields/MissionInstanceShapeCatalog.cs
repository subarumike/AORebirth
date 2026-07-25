namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Items;

    #endregion

    /// <summary>
    /// Capture-backed RK mission interior shapes from
    /// <c>20260719-5-different-shape-fo-mish</c> (PFs 1419310 / 1419335 / 1419382)
    /// Find-Item capture <c>20260724-mission-find-item</c> (PF 1441804),
    /// Find-Person <c>20260724-mission-find-person</c> (PF 1419349),
    /// Find-Person gold <c>20260724-224228</c> (PFs 1460226 / 1456133),
    /// and low-QL Find-Person gold <c>20260725-002423</c> (PF 1443840).
    /// Holds precise SCFU textures/meshes/positions for trash + FindTarget + KillBoss roles.
    /// Layout generator payloads and door/chest wire are stored separately for replay.
    /// </summary>
    internal enum MissionNpcRole
    {
        Trash = 0,
        FindTarget = 1,
        KillBoss = 2,
        KillGuard = 3,
        BrokenMachine = 4,
        LootProp = 5
    }

    internal sealed class MissionNpc
    {
        public string Name;
        public MissionNpcRole Role;
        public int Level;
        public int Health;
        public int MonsterData;
        public int Scale;
        public int HeadMesh;
        public float X;
        public float Y;
        public float Z;
        public float Hx;
        public float Hy;
        public float Hz;
        public float Hw;
        public int[][] Textures;
        public int[][] Meshes;

        /// <summary>Grey trash (no side textures) does not raise token %.</summary>
        public bool IsGrey;
    }

    internal sealed class MissionShape
    {
        public int CapturedPlayfieldId;
        public float SpawnX;
        public float SpawnY;
        public float SpawnZ;
        public MissionNpc[] Npcs;
    }

    internal static class MissionInstanceShapeCatalog
    {
        // Capture ACG building generator type/instance from enter teleports (ACGBuildingGeneratorData:D74044..).
        internal const int CapturedBuildingType = unchecked((int)0x0000C79F);
        // Prefer D74044 as default building instance; live used D74044/45/46/48 per enter.
        internal const int CapturedBuildingInstance = unchecked((int)0x00D74044);

        internal static readonly MissionShape[] Shapes =
        {
// AUTO from capture 20260719-5-different-shape-fo-mish
        // Shape playfield 1419310 (33 npcs)
        new MissionShape
        {
            CapturedPlayfieldId = 1419310,
            // ACG entrance (client lands here). Capture SpawnX was the outdoor exit door at ~298,115.
            SpawnX = 14.0f, SpawnY = 5.01f, SpawnZ = 205.0f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Berneice Cornelius",
                    Role = MissionNpcRole.FindTarget,
                    Level = 154, Health = 13965, MonsterData = 26076, Scale = 117, HeadMesh = 40635,
                    X = 241.1f, Y = 5.01000166f, Z = 138.599991f,
                    Hx = 0f, Hy = -0.749054432f, Hz = 0f, Hw = 0.662508547f,
                    Textures = new[] { new[] { 1, 81911 }, new[] { 2, 81913 }, new[] { 3, 81908 }, new[] { 4, 81916 } },
                    Meshes = new[] { new[] { 0, 40635, 0, 4 } },
                },
                new MissionNpc
                {
                    Name = "Bileswarm Breeder",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14413, MonsterData = 31907, Scale = 118, HeadMesh = 0,
                    X = 74.45385f, Y = 5.315f, Z = 183.65213f,
                    Hx = 0f, Hy = 0.9989062f, Hz = 0f, Hw = 0.04675881f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Bileswarm Breeder",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14413, MonsterData = 31907, Scale = 118, HeadMesh = 0,
                    X = 34.641346f, Y = 5.01000166f, Z = 194.630112f,
                    Hx = 0f, Hy = 0.0944616944f, Hz = 0f, Hw = 0.9955285f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Bileswarm Breeder",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 14263, MonsterData = 31907, Scale = 117, HeadMesh = 0,
                    X = 44.8125458f, Y = 5.06044674f, Z = 242.739563f,
                    Hx = 0f, Hy = 0.2377207f, Hz = 0f, Hw = 0.971333563f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Bileswarm Breeder",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 14263, MonsterData = 31907, Scale = 117, HeadMesh = 0,
                    X = 46.444046f, Y = 5.01f, Z = 194.859283f,
                    Hx = 0f, Hy = -0.5537137f, Hz = 0f, Hw = 0.8327071f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Bileswarm Breeder",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 14263, MonsterData = 31907, Scale = 117, HeadMesh = 0,
                    X = 23.7551975f, Y = 5.629463f, Z = 182.604233f,
                    Hx = 0f, Hy = 0.7084841f, Hz = 0f, Hw = 0.7057268f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Bioarranged Beast - Model 666",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14413, MonsterData = 17720, Scale = 118, HeadMesh = 0,
                    X = 55.7659073f, Y = 5.010002f, Z = 194.612549f,
                    Hx = 0f, Hy = -0.5505029f, Hz = 0f, Hw = 0.834833264f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Bioarranged Beast - Model 666",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 11878, MonsterData = 17720, Scale = 116, HeadMesh = 0,
                    X = 63.485733f, Y = 6.36257362f, Z = 203.346588f,
                    Hx = -0.049754072f, Hy = 0.9975491f, Hz = 0.002450672f, Hw = 0.0491352f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Bioarranged Beast - Model 666",
                    Role = MissionNpcRole.Trash,
                    Level = 143, Health = 12325, MonsterData = 17720, Scale = 116, HeadMesh = 0,
                    X = 70.47762f, Y = 5.315f, Z = 195.374542f,
                    Hx = 0f, Hy = 0.9830113f, Hz = 0f, Hw = 0.183544934f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "CEO Guardian",
                    Role = MissionNpcRole.KillGuard,
                    Level = 215, Health = 34513, MonsterData = 227701, Scale = 125, HeadMesh = 0,
                    X = 297.775452f, Y = 5.01f, Z = 113.040253f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = null,
                    Meshes = new[] { new[] { 1, 273304, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Carlo Pinnetti",
                    Role = MissionNpcRole.KillBoss,
                    Level = 220, Health = 55687, MonsterData = 258209, Scale = 130, HeadMesh = 40121,
                    X = 297.2772f, Y = 5.01f, Z = 118.657127f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 1, 284557 }, new[] { 2, 247977 }, new[] { 3, 247887 }, new[] { 4, 248016 } },
                    Meshes = new[] { new[] { 0, 204896, 0, 0 }, new[] { 0, 40121, 0, 4 }, new[] { 1, 29084, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Hellhound",
                    Role = MissionNpcRole.Trash,
                    Level = 148, Health = 26141, MonsterData = 17720, Scale = 117, HeadMesh = 0,
                    X = 63.0229225f, Y = 6.370332f, Z = 203.575027f,
                    Hx = 0.00753578f, Hy = -0.9872682f, Hz = 0.049237866f, Hw = -0.151064351f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Hellhound",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 24054, MonsterData = 17720, Scale = 116, HeadMesh = 0,
                    X = 82.9325f, Y = 5.07310438f, Z = 175.658539f,
                    Hx = -0.01346237f, Hy = -0.5569894f, Hz = 0.0688850954f, Hw = 0.827548444f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Hellhound",
                    Role = MissionNpcRole.Trash,
                    Level = 153, Health = 27632, MonsterData = 17720, Scale = 117, HeadMesh = 0,
                    X = 64.49255f, Y = 5.142084f, Z = 253.756775f,
                    Hx = 0.0151015669f, Hy = 0.8406672f, Hz = -0.0685798451f, Hw = 0.5369799f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Hellhound",
                    Role = MissionNpcRole.Trash,
                    Level = 150, Health = 26738, MonsterData = 17720, Scale = 117, HeadMesh = 0,
                    X = 75.45261f, Y = 5.093064f, Z = 237.338028f,
                    Hx = -0.0215079952f, Hy = -0.455298f, Hz = 0.06681166f, Hw = 0.887568235f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Hellhound",
                    Role = MissionNpcRole.Trash,
                    Level = 152, Health = 27334, MonsterData = 17720, Scale = 117, HeadMesh = 0,
                    X = 35.80704f, Y = 5.01000166f, Z = 154.873932f,
                    Hx = 0f, Hy = -0.9503959f, Hz = 0f, Hw = 0.311042935f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Virusbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 155, Health = 14114, MonsterData = 26151, Scale = 117, HeadMesh = 40171,
                    X = 53.682f, Y = 5.010039f, Z = 214.99f,
                    Hx = 0f, Hy = 0.679914534f, Hz = 0f, Hw = 0.7332914f,
                    Textures = new[] { new[] { 0, 14048 }, new[] { 1, 8731 }, new[] { 2, 9457 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Master Virusbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 149, Health = 13220, MonsterData = 26151, Scale = 117, HeadMesh = 40171,
                    X = 71.75723f, Y = 5.325133f, Z = 177.322952f,
                    Hx = 0f, Hy = 0.770263135f, Hz = 0f, Hw = 0.6377262f,
                    Textures = new[] { new[] { 0, 9454 }, new[] { 1, 8731 }, new[] { 2, 9457 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Medium Intestine Horror",
                    Role = MissionNpcRole.Trash,
                    Level = 152, Health = 19134, MonsterData = 40484, Scale = 117, HeadMesh = 0,
                    X = 65.39021f, Y = 5.06980944f, Z = 217.167877f,
                    Hx = -0.0244040322f, Hy = -0.41606006f, Hz = 0.06580293f, Hw = 0.906624734f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Medium Intestine Horror",
                    Role = MissionNpcRole.Trash,
                    Level = 154, Health = 19551, MonsterData = 40484, Scale = 117, HeadMesh = 0,
                    X = 84.01891f, Y = 5.22767448f, Z = 225.19487f,
                    Hx = 0.1321659f, Hy = -0.9875636f, Hz = 0.01129481f, Hw = 0.08439653f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Medium Intestine Horror",
                    Role = MissionNpcRole.Trash,
                    Level = 148, Health = 18299, MonsterData = 40484, Scale = 117, HeadMesh = 0,
                    X = 12.99f, Y = 5.01000357f, Z = 244.813171f,
                    Hx = 0f, Hy = -0.8210568f, Hz = 0f, Hw = 0.5708465f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Medium Intestine Horror",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 20178, MonsterData = 40484, Scale = 118, HeadMesh = 0,
                    X = 84.6421f, Y = 5.01000166f, Z = 205.973816f,
                    Hx = 0f, Hy = -0.150283933f, Hz = 0f, Hw = 0.9886429f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Seasoned Bountyhunter",
                    Role = MissionNpcRole.Trash,
                    Level = 144, Health = 12475, MonsterData = 26097, Scale = 117, HeadMesh = 40111,
                    X = 235.899292f, Y = 5.01000166f, Z = 113.235451f,
                    Hx = 0f, Hy = -0.482402146f, Hz = 0f, Hw = 0.87594986f,
                    Textures = new[] { new[] { 0, 8745 }, new[] { 1, 15813 }, new[] { 2, 8743 }, new[] { 3, 8730 }, new[] { 4, 8747 } },
                    Meshes = new[] { new[] { 0, 20002, 0, 2 }, new[] { 0, 40111, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Engineer",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 11878, MonsterData = 26103, Scale = 116, HeadMesh = 40103,
                    X = 254.489944f, Y = 5.01000166f, Z = 104.654205f,
                    Hx = 0f, Hy = -0.8778746f, Hz = 0f, Hw = 0.478890568f,
                    Textures = new[] { new[] { 0, 9454 }, new[] { 1, 8731 }, new[] { 2, 22592 }, new[] { 3, 9455 }, new[] { 4, 22622 } },
                    Meshes = new[] { new[] { 0, 19997, 31719, 2 }, new[] { 0, 40103, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Hunter",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 12176, MonsterData = 26076, Scale = 116, HeadMesh = 40635,
                    X = 233.711121f, Y = 5.01000166f, Z = 124.508377f,
                    Hx = 0f, Hy = 0.4312552f, Hz = 0f, Hw = 0.902229965f,
                    Textures = new[] { new[] { 0, 8745 }, new[] { 1, 8739 }, new[] { 2, 8743 }, new[] { 3, 15812 }, new[] { 4, 8747 } },
                    Meshes = new[] { new[] { 0, 20090, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Trader",
                    Role = MissionNpcRole.Trash,
                    Level = 148, Health = 13071, MonsterData = 26082, Scale = 117, HeadMesh = 40634,
                    X = 275.50824f, Y = 5.01000166f, Z = 94.40512f,
                    Hx = 0f, Hy = 0.928656459f, Hz = 0f, Hw = 0.370940924f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 42244 }, new[] { 2, 9450 }, new[] { 3, 8815 }, new[] { 4, 8813 } },
                    Meshes = new[] { new[] { 0, 20082, 0, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Nanoshifter",
                    Role = MissionNpcRole.Trash,
                    Level = 146, Health = 12773, MonsterData = 26076, Scale = 117, HeadMesh = 40635,
                    X = 232.674057f, Y = 5.01000166f, Z = 146.801971f,
                    Hx = 0f, Hy = 0.9060736f, Hz = 0f, Hw = 0.423120171f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 42244 }, new[] { 2, 8814 }, new[] { 3, 42246 }, new[] { 4, 42245 } },
                    Meshes = new[] { new[] { 0, 20082, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Robotbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 11878, MonsterData = 26082, Scale = 116, HeadMesh = 40634,
                    X = 273.901123f, Y = 5.01000166f, Z = 134.07843f,
                    Hx = 0f, Hy = -0.5244444f, Hz = 0f, Hw = 0.851444662f,
                    Textures = new[] { new[] { 0, 22605 }, new[] { 1, 8731 }, new[] { 2, 22592 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20081, 31719, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Small Intestine Horror",
                    Role = MissionNpcRole.Trash,
                    Level = 144, Health = 17464, MonsterData = 40484, Scale = 117, HeadMesh = 0,
                    X = 23.2642765f, Y = 5.46918631f, Z = 181.9833f,
                    Hx = -0.108457446f, Hy = 0.94683975f, Hz = -0.0168844257f, Hw = 0.302401036f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Small Intestine Horror",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 16629, MonsterData = 40484, Scale = 116, HeadMesh = 0,
                    X = 4.511465f, Y = 5.01000357f, Z = 233.653839f,
                    Hx = 0f, Hy = -0.453771144f, Hz = 0f, Hw = 0.8911183f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Small Intestine Horror",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 17047, MonsterData = 40484, Scale = 116, HeadMesh = 0,
                    X = 73.651474f, Y = 5.010039f, Z = 225.131363f,
                    Hx = 0f, Hy = -0.956970155f, Hz = 0f, Hw = 0.290186375f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Ruffian",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 19969, MonsterData = 26137, Scale = 117, HeadMesh = 40209,
                    X = 240.4633f, Y = 5.01000166f, Z = 136.824432f,
                    Hx = 0f, Hy = -0.7644909f, Hz = 0f, Hw = 0.6446345f,
                    Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 15807 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 20055, 0, 2 }, new[] { 0, 40209, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Veteran Ruffian",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 20178, MonsterData = 26137, Scale = 118, HeadMesh = 40209,
                    X = 261.700836f, Y = 5.01000166f, Z = 146.903915f,
                    Hx = 0f, Hy = 0.6260152f, Hz = 0f, Hw = 0.779810846f,
                    Textures = new[] { new[] { 0, 15806 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 15805 } },
                    Meshes = new[] { new[] { 0, 20055, 0, 2 }, new[] { 0, 40209, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                },
            },
        },
        // Shape playfield 1419335 (28 npcs)
        new MissionShape
        {
            CapturedPlayfieldId = 1419335,
            // ACG entrance (not the high-X exit door from capture).
            SpawnX = 14.0f, SpawnY = 5.01f, SpawnZ = 175.0f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Boosted Slugger",
                    Role = MissionNpcRole.Trash,
                    Level = 152, Health = 19134, MonsterData = 26137, Scale = 117, HeadMesh = 40209,
                    X = 207.367935f, Y = 5.01f, Z = 117.615822f,
                    Hx = 0f, Hy = -0.8912741f, Hz = 0f, Hw = 0.453464985f,
                    Textures = new[] { new[] { 0, 15806 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 20055, 0, 2 }, new[] { 0, 40209, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "CEO Guardian",
                    Role = MissionNpcRole.KillGuard,
                    Level = 215, Health = 34513, MonsterData = 227701, Scale = 125, HeadMesh = 0,
                    X = 296.510071f, Y = 5.01f, Z = 147.651581f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = null,
                    Meshes = new[] { new[] { 1, 273304, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Carlo Pinnetti",
                    Role = MissionNpcRole.KillBoss,
                    Level = 220, Health = 55687, MonsterData = 258209, Scale = 130, HeadMesh = 40121,
                    X = 296.845428f, Y = 5.01f, Z = 148.637161f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 1, 284557 }, new[] { 2, 247977 }, new[] { 3, 247887 }, new[] { 4, 248016 } },
                    Meshes = new[] { new[] { 0, 204896, 0, 0 }, new[] { 0, 40121, 0, 4 }, new[] { 1, 29084, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Hardened Nanohoarder",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 9742, MonsterData = 26139, Scale = 116, HeadMesh = 40249,
                    X = 275.114838f, Y = 5.01f, Z = 68.08682f,
                    Hx = 0f, Hy = -0.701046169f, Hz = 0f, Hw = 0.7131159f,
                    Textures = new[] { new[] { 0, 40975 }, new[] { 1, 9410 }, new[] { 2, 9413 }, new[] { 3, 9603 }, new[] { 4, 9411 } },
                    Meshes = new[] { new[] { 0, 20063, 0, 2 }, new[] { 0, 40249, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Hardened Techrejecter",
                    Role = MissionNpcRole.Trash,
                    Level = 148, Health = 13071, MonsterData = 26135, Scale = 117, HeadMesh = 40271,
                    X = 231.454727f, Y = 5.01f, Z = 134.755692f,
                    Hx = 0f, Hy = -0.6502726f, Hz = 0f, Hw = 0.759700954f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 9611 }, new[] { 2, 9450 }, new[] { 3, 9604 }, new[] { 4, 9624 } },
                    Meshes = new[] { new[] { 0, 40271, 0, 4 }, new[] { 1, 30238, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Master Virusbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 153, Health = 13816, MonsterData = 26151, Scale = 117, HeadMesh = 40171,
                    X = 287.881256f, Y = 5.01f, Z = 173.476074f,
                    Hx = 0f, Hy = -0.467166126f, Hz = 0f, Hw = 0.8841696f,
                    Textures = new[] { new[] { 0, 9454 }, new[] { 1, 8731 }, new[] { 2, 9457 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Nichole Orender",
                    Role = MissionNpcRole.FindTarget,
                    Level = 154, Health = 13965, MonsterData = 26137, Scale = 117, HeadMesh = 40209,
                    X = 225.01f, Y = 5.01000166f, Z = 115.01f,
                    Hx = 0f, Hy = 0.975192249f, Hz = 0f, Hw = 0.221360147f,
                    Textures = new[] { new[] { 1, 81911 }, new[] { 2, 81913 }, new[] { 3, 81908 }, new[] { 4, 81916 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                },
                new MissionNpc
                {
                    Name = "Rough Clan Informer",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14413, MonsterData = 26139, Scale = 118, HeadMesh = 40249,
                    X = 229.585358f, Y = 4.01f, Z = 105.451004f,
                    Hx = 0f, Hy = -0.221360087f, Hz = 0f, Hw = 0.9751921f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 8740 }, new[] { 2, 9450 }, new[] { 3, 8815 }, new[] { 4, 8813 } },
                    Meshes = new[] { new[] { 0, 20075, 0, 2 }, new[] { 0, 40249, 0, 4 }, new[] { 1, 35542, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Bountyhunter",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 12176, MonsterData = 26088, Scale = 116, HeadMesh = 40687,
                    X = 213.27684f, Y = 5.01f, Z = 135.018219f,
                    Hx = 0f, Hy = -0.8209091f, Hz = 0f, Hw = 0.5710589f,
                    Textures = new[] { new[] { 0, 8745 }, new[] { 1, 8739 }, new[] { 2, 15811 }, new[] { 3, 15812 }, new[] { 4, 8747 } },
                    Meshes = new[] { new[] { 0, 20107, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Doctor",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12027, MonsterData = 26151, Scale = 116, HeadMesh = 40171,
                    X = 254.583832f, Y = 5.01f, Z = 164.618988f,
                    Hx = 0f, Hy = 0.9624015f, Hz = 0f, Hw = 0.271630943f,
                    Textures = new[] { new[] { 0, 9454 }, new[] { 1, 8731 }, new[] { 2, 9616 }, new[] { 3, 9455 }, new[] { 4, 9623 } },
                    Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Soldier",
                    Role = MissionNpcRole.Trash,
                    Level = 144, Health = 12475, MonsterData = 26074, Scale = 117, HeadMesh = 40691,
                    X = 274.979156f, Y = 5.01f, Z = 166.131348f,
                    Hx = 0f, Hy = -0.34360078f, Hz = 0f, Hw = 0.9391158f,
                    Textures = new[] { new[] { 0, 9620 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9605 }, new[] { 4, 9425 } },
                    Meshes = new[] { new[] { 0, 20095, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Soldier",
                    Role = MissionNpcRole.Trash,
                    Level = 145, Health = 12624, MonsterData = 26074, Scale = 117, HeadMesh = 40691,
                    X = 230.971008f, Y = 5.01f, Z = 166.761978f,
                    Hx = 0f, Hy = 0.9984639f, Hz = 0f, Hw = -0.0554068051f,
                    Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 20095, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Nanoshifter",
                    Role = MissionNpcRole.Trash,
                    Level = 143, Health = 12325, MonsterData = 26076, Scale = 116, HeadMesh = 40635,
                    X = 274.638f, Y = 5.01f, Z = 94.10261f,
                    Hx = 0f, Hy = -0.6587472f, Hz = 0f, Hw = 0.752364337f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 8740 }, new[] { 2, 8814 }, new[] { 3, 42246 }, new[] { 4, 42245 } },
                    Meshes = new[] { new[] { 0, 20082, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Nanoshifter",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 12176, MonsterData = 26076, Scale = 116, HeadMesh = 40635,
                    X = 222.245377f, Y = 5.01f, Z = 96.4871445f,
                    Hx = 0f, Hy = -0.309690624f, Hz = 0f, Hw = 0.9508374f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 42244 }, new[] { 2, 8814 }, new[] { 3, 8815 }, new[] { 4, 42245 } },
                    Meshes = new[] { new[] { 0, 20082, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Robotbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 11878, MonsterData = 26082, Scale = 116, HeadMesh = 40634,
                    X = 282.623657f, Y = 5.01f, Z = 163.256012f,
                    Hx = 0f, Hy = 0.026496416f, Hz = 0f, Hw = 0.9996489f,
                    Textures = new[] { new[] { 0, 9454 }, new[] { 1, 8731 }, new[] { 2, 22592 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20081, 0, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Robotbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 11878, MonsterData = 26082, Scale = 116, HeadMesh = 40634,
                    X = 275.6466f, Y = 5.01f, Z = 119.563438f,
                    Hx = 0f, Hy = -0.421832919f, Hz = 0f, Hw = 0.9066736f,
                    Textures = new[] { new[] { 0, 9454 }, new[] { 1, 8731 }, new[] { 2, 9457 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20081, 0, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Gridrunner",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12027, MonsterData = 26092, Scale = 116, HeadMesh = 40694,
                    X = 225.374329f, Y = 4.81051731f, Z = 147.07106f,
                    Hx = 0f, Hy = -0.263024151f, Hz = 0f, Hw = 0.9647893f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 22570 }, new[] { 2, 22594 }, new[] { 3, 9451 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20099, 0, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Lasersniper",
                    Role = MissionNpcRole.Trash,
                    Level = 146, Health = 12773, MonsterData = 26101, Scale = 117, HeadMesh = 40105,
                    X = 229.630722f, Y = 5.01f, Z = 159.65918f,
                    Hx = 0f, Hy = 0.03183827f, Hz = 0f, Hw = 0.999493062f,
                    Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 20004, 0, 2 }, new[] { 0, 40105, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Nanoshifter",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 11878, MonsterData = 26074, Scale = 116, HeadMesh = 40691,
                    X = 272.683868f, Y = 5.01f, Z = 134.093155f,
                    Hx = 0f, Hy = -0.722928464f, Hz = 0f, Hw = 0.6909229f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 42244 }, new[] { 2, 9450 }, new[] { 3, 8815 }, new[] { 4, 8813 } },
                    Meshes = new[] { new[] { 0, 20099, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Nanoshifter",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 11878, MonsterData = 26074, Scale = 116, HeadMesh = 40691,
                    X = 242.33699f, Y = 5.01f, Z = 105.350746f,
                    Hx = 0f, Hy = -0.6253892f, Hz = 0f, Hw = 0.780312955f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 8740 }, new[] { 2, 9450 }, new[] { 3, 42246 }, new[] { 4, 8813 } },
                    Meshes = new[] { new[] { 0, 20109, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Bully",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 19969, MonsterData = 26101, Scale = 118, HeadMesh = 40105,
                    X = 203.720886f, Y = 5.01f, Z = 106.86795f,
                    Hx = 0f, Hy = -0.328064948f, Hz = 0f, Hw = 0.9446552f,
                    Textures = new[] { new[] { 0, 15806 }, new[] { 1, 8729 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                    Meshes = new[] { new[] { 0, 20005, 0, 2 }, new[] { 0, 40105, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Criminal",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14413, MonsterData = 26090, Scale = 118, HeadMesh = 40629,
                    X = 226.595642f, Y = 4.00999832f, Z = 118.080772f,
                    Hx = 0f, Hy = 0.999793351f, Hz = 0f, Hw = 0.0203284249f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 9611 }, new[] { 2, 9617 }, new[] { 3, 9451 }, new[] { 4, 9624 } },
                    Meshes = new[] { new[] { 0, 40629, 0, 4 }, new[] { 1, 30238, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Nanogun",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 11411, MonsterData = 26090, Scale = 117, HeadMesh = 40629,
                    X = 242.854462f, Y = 5.01f, Z = 117.178391f,
                    Hx = 0f, Hy = 0.472566724f, Hz = 0f, Hw = 0.8812949f,
                    Textures = new[] { new[] { 0, 9409 }, new[] { 1, 9410 }, new[] { 2, 9413 }, new[] { 3, 9603 }, new[] { 4, 9411 } },
                    Meshes = new[] { new[] { 0, 20080, 0, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Nanogun",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 11411, MonsterData = 26090, Scale = 117, HeadMesh = 40629,
                    X = 266.5529f, Y = 5.01f, Z = 95.18013f,
                    Hx = 0f, Hy = 0.752364457f, Hz = 0f, Hw = 0.658747137f,
                    Textures = new[] { new[] { 0, 40975 }, new[] { 1, 9410 }, new[] { 2, 9413 }, new[] { 3, 9603 }, new[] { 4, 9411 } },
                    Meshes = new[] { new[] { 0, 20080, 0, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Plunderer",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14413, MonsterData = 26135, Scale = 118, HeadMesh = 40271,
                    X = 295.175537f, Y = 5.01f, Z = 153.8352f,
                    Hx = 0f, Hy = -0.9983632f, Hz = 0f, Hw = 0.05719237f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 8815 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 20065, 0, 2 }, new[] { 0, 40271, 0, 4 }, new[] { 1, 35542, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Plunderer",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14413, MonsterData = 26135, Scale = 118, HeadMesh = 40271,
                    X = 257.761444f, Y = 5.01f, Z = 94.38502f,
                    Hx = 0f, Hy = 0.8780799f, Hz = 0f, Hw = 0.478514075f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8740 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 8813 } },
                    Meshes = new[] { new[] { 0, 20065, 0, 2 }, new[] { 0, 40271, 0, 4 }, new[] { 1, 35542, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Rascal",
                    Role = MissionNpcRole.Trash,
                    Level = 155, Health = 19761, MonsterData = 26101, Scale = 118, HeadMesh = 40105,
                    X = 293.32196f, Y = 5.01f, Z = 95.3954544f,
                    Hx = 0f, Hy = 0.8237906f, Hz = 0f, Hw = 0.5668942f,
                    Textures = new[] { new[] { 0, 15806 }, new[] { 1, 8729 }, new[] { 2, 15807 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 20005, 0, 2 }, new[] { 0, 40105, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Torpedo",
                    Role = MissionNpcRole.Trash,
                    Level = 154, Health = 19552, MonsterData = 26137, Scale = 118, HeadMesh = 40209,
                    X = 293.681274f, Y = 5.01f, Z = 103.897652f,
                    Hx = 0f, Hy = 0.421428055f, Hz = 0f, Hw = 0.906861842f,
                    Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 15807 }, new[] { 3, 9419 }, new[] { 4, 15805 } },
                    Meshes = new[] { new[] { 0, 20055, 0, 2 }, new[] { 0, 40209, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                },
            },
        },
        // Shape playfield 1419382 (16 npcs)
        new MissionShape
        {
            CapturedPlayfieldId = 1419382,
            SpawnX = 1.80102539f, SpawnY = 5.01f, SpawnZ = 195.01001f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "CEO Guardian",
                    Role = MissionNpcRole.KillGuard,
                    Level = 215, Health = 34513, MonsterData = 227701, Scale = 125, HeadMesh = 0,
                    X = 5.64194632f, Y = 5.01f, Z = 196.771484f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = null,
                    Meshes = new[] { new[] { 1, 273304, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Carlo Pinnetti",
                    Role = MissionNpcRole.KillBoss,
                    Level = 220, Health = 55687, MonsterData = 258209, Scale = 130, HeadMesh = 40121,
                    X = 3.95614f, Y = 5.01f, Z = 196.733566f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 1, 284557 }, new[] { 2, 247977 }, new[] { 3, 247887 }, new[] { 4, 248016 } },
                    Meshes = new[] { new[] { 0, 204896, 0, 0 }, new[] { 0, 40121, 0, 4 }, new[] { 1, 29084, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Chae Aronstein",
                    Role = MissionNpcRole.FindTarget,
                    Level = 154, Health = 13965, MonsterData = 26137, Scale = 117, HeadMesh = 40209,
                    X = 61.3000031f, Y = 5.01000166f, Z = 151.299988f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 1, 81911 }, new[] { 2, 81913 }, new[] { 3, 81908 }, new[] { 4, 81916 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                },
                new MissionNpc
                {
                    Name = "Master Virusbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14413, MonsterData = 26151, Scale = 118, HeadMesh = 40171,
                    X = 37.71422f, Y = 5.01000166f, Z = 183.3908f,
                    Hx = 0f, Hy = 0.7844009f, Hz = 0f, Hw = 0.6202542f,
                    Textures = new[] { new[] { 0, 14048 }, new[] { 1, 8731 }, new[] { 2, 9457 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Rough Clan Informer",
                    Role = MissionNpcRole.Trash,
                    Level = 154, Health = 13965, MonsterData = 26139, Scale = 117, HeadMesh = 40249,
                    X = 44.6844368f, Y = 5.01f, Z = 217.853271f,
                    Hx = 0f, Hy = -0.06551641f, Hz = 0f, Hw = 0.9978515f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 8814 }, new[] { 3, 9451 }, new[] { 4, 8813 } },
                    Meshes = new[] { new[] { 0, 20075, 0, 2 }, new[] { 0, 40249, 0, 4 }, new[] { 1, 35542, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Agent",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 12176, MonsterData = 26133, Scale = 116, HeadMesh = 40251,
                    X = 57.342514f, Y = 5.01000166f, Z = 168.07785f,
                    Hx = 0f, Hy = -0.730649233f, Hz = 0f, Hw = 0.682753f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 22570 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20065, 0, 2 }, new[] { 0, 40251, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Bodyguard",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 16838, MonsterData = 26082, Scale = 116, HeadMesh = 40634,
                    X = 74.6288f, Y = 5.01f, Z = 172.714157f,
                    Hx = 0f, Hy = -0.9025372f, Hz = 0f, Hw = 0.430611819f,
                    Textures = new[] { new[] { 0, 15806 }, new[] { 1, 8729 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                    Meshes = new[] { new[] { 0, 20089, 0, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Bountyhunter",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 12176, MonsterData = 26088, Scale = 116, HeadMesh = 40687,
                    X = 63.92965f, Y = 5.01f, Z = 182.175537f,
                    Hx = 0f, Hy = -0.925629139f, Hz = 0f, Hw = 0.3784319f,
                    Textures = new[] { new[] { 0, 15810 }, new[] { 1, 8739 }, new[] { 2, 15811 }, new[] { 3, 8730 }, new[] { 4, 15804 } },
                    Meshes = new[] { new[] { 0, 20103, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Scout",
                    Role = MissionNpcRole.Trash,
                    Level = 149, Health = 13220, MonsterData = 26133, Scale = 117, HeadMesh = 40251,
                    X = 53.3309937f, Y = 5.01000166f, Z = 172.752655f,
                    Hx = 0f, Hy = -0.803865552f, Hz = 0f, Hw = 0.594811f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 9604 }, new[] { 4, 9624 } },
                    Meshes = new[] { new[] { 0, 40251, 0, 4 }, new[] { 1, 30238, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Spy",
                    Role = MissionNpcRole.Trash,
                    Level = 144, Health = 12475, MonsterData = 26076, Scale = 117, HeadMesh = 40635,
                    X = 45.6324768f, Y = 5.01f, Z = 186.443481f,
                    Hx = 0f, Hy = -0.45154354f, Hz = 0f, Hw = 0.8922491f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 22594 }, new[] { 3, 9451 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 20082, 31720, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Assassin",
                    Role = MissionNpcRole.Trash,
                    Level = 144, Health = 12475, MonsterData = 26135, Scale = 117, HeadMesh = 40271,
                    X = 54.72754f, Y = 5.01000166f, Z = 162.413452f,
                    Hx = 0f, Hy = 0.214568734f, Hz = 0f, Hw = 0.9767089f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20065, 31720, 2 }, new[] { 0, 40271, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Assassin",
                    Role = MissionNpcRole.Trash,
                    Level = 145, Health = 12624, MonsterData = 26135, Scale = 117, HeadMesh = 40271,
                    X = 65.674614f, Y = 5.01000166f, Z = 204.463715f,
                    Hx = 0f, Hy = -0.851207f, Hz = 0f, Hw = 0.524830043f,
                    Textures = new[] { new[] { 0, 22607 }, new[] { 1, 22570 }, new[] { 2, 9450 }, new[] { 3, 22543 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 20065, 0, 2 }, new[] { 0, 40271, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Nanoshifter",
                    Role = MissionNpcRole.Trash,
                    Level = 148, Health = 13071, MonsterData = 26076, Scale = 117, HeadMesh = 40635,
                    X = 41.8528175f, Y = 5.01f, Z = 236.805283f,
                    Hx = 0f, Hy = -0.3048885f, Hz = 0f, Hw = 0.952388048f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 8740 }, new[] { 2, 9450 }, new[] { 3, 8815 }, new[] { 4, 42245 } },
                    Meshes = new[] { new[] { 0, 20092, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Clan Diversionist",
                    Role = MissionNpcRole.Trash,
                    Level = 155, Health = 14115, MonsterData = 26125, Scale = 118, HeadMesh = 40215,
                    X = 24.2658749f, Y = 5.01000166f, Z = 185.320572f,
                    Hx = 0f, Hy = 0.995621f, Hz = 0f, Hw = 0.0934813246f,
                    Textures = new[] { new[] { 0, 22607 }, new[] { 1, 8732 }, new[] { 2, 22594 }, new[] { 3, 22543 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20048, 31720, 2 }, new[] { 0, 40215, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Tough Clan Diversionist",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 14264, MonsterData = 26125, Scale = 118, HeadMesh = 40215,
                    X = 44.5677528f, Y = 5.01f, Z = 218.738022f,
                    Hx = 0f, Hy = 0.997851551f, Hz = 0f, Hw = 0.06551641f,
                    Textures = new[] { new[] { 0, 22607 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 22543 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20048, 31720, 2 }, new[] { 0, 40215, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Veteran Functionary",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 11411, MonsterData = 26155, Scale = 117, HeadMesh = 40138,
                    X = 27.459465f, Y = 5.01000166f, Z = 172.9627f,
                    Hx = 0f, Hy = -0.922786832f, Hz = 0f, Hw = 0.3853109f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 9623 } },
                    Meshes = new[] { new[] { 0, 20014, 0, 2 }, new[] { 0, 40138, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
            },
        },

        // Shape playfield 1441804 from capture 20260724-mission-find-item (33 trash)
        new MissionShape
        {
            CapturedPlayfieldId = 1441804,
            SpawnX = 1.80102539f, SpawnY = 5.01f, SpawnZ = 205.01001f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Mission Cube",
                    Role = MissionNpcRole.FindTarget,
                    Level = 1, Health = 999999, MonsterData = 26092, Scale = 40, HeadMesh = 0,
                    X = 98.41754f, Y = 5.100035f, Z = 178.7181f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Dealer",
                    Role = MissionNpcRole.Trash,
                    Level = 162, Health = 15158, MonsterData = 26123, Scale = 118, HeadMesh = 40214,
                    X = 14.7782631f, Y = 5.01000166f, Z = 205.239456f,
                    Hx = 0.0f, Hy = -0.9565039f, Hz = 0.0f, Hw = 0.2917195f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Functionary",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 11531, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 45.77344f, Y = 5.01f, Z = 204.18103f,
                    Hx = 0.0f, Hy = -0.14418751f, Hz = 0.0f, Hw = 0.9895504f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Clan Functionary",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 12842, MonsterData = 26143, Scale = 118, HeadMesh = 40137,
                    X = 28.99f, Y = 5.01f, Z = 233.1249f,
                    Hx = 0.0f, Hy = 0.236827686f, Hz = 0.0f, Hw = 0.9715517f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Techno-Architect",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 35.3955574f, Y = 5.010003f, Z = 227.405579f,
                    Hx = 0.0f, Hy = -0.9742546f, Hz = 0.0f, Hw = 0.2254504f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Dealer",
                    Role = MissionNpcRole.Trash,
                    Level = 162, Health = 15158, MonsterData = 26123, Scale = 118, HeadMesh = 40214,
                    X = 39.0758438f, Y = 5.01000166f, Z = 174.853638f,
                    Hx = 0.0f, Hy = 0.209602475f, Hz = 0.0f, Hw = 0.9777867f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Rustler",
                    Role = MissionNpcRole.Trash,
                    Level = 166, Health = 15754, MonsterData = 26133, Scale = 118, HeadMesh = 40251,
                    X = 39.2914543f, Y = 5.01000166f, Z = 175.333435f,
                    Hx = 0.0f, Hy = 0.616593361f, Hz = 0.0f, Hw = 0.787281752f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Techno-Architect",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 55.0371323f, Y = 5.01000166f, Z = 216.1886f,
                    Hx = 0.0f, Hy = 0.931588f, Hz = 0.0f, Hw = 0.3635159f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Rough Clan Informer",
                    Role = MissionNpcRole.Trash,
                    Level = 160, Health = 14860, MonsterData = 26139, Scale = 118, HeadMesh = 40249,
                    X = 57.66246f, Y = 5.01000166f, Z = 208.092163f,
                    Hx = 0.0f, Hy = 0.8980559f, Hz = 0.0f, Hw = 0.439881355f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Rustler",
                    Role = MissionNpcRole.Trash,
                    Level = 165, Health = 15605, MonsterData = 26133, Scale = 118, HeadMesh = 40251,
                    X = 58.21275f, Y = 5.01000166f, Z = 152.367966f,
                    Hx = 0.0f, Hy = 0.996965766f, Hz = 0.0f, Hw = -0.07784045f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Techno-Architect",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 56.96526f, Y = 5.01000166f, Z = 144.427856f,
                    Hx = 0.0f, Hy = 0.09667658f, Hz = 0.0f, Hw = 0.99531585f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Poacher",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26090, Scale = 118, HeadMesh = 40629,
                    X = 71.92619f, Y = 5.01000166f, Z = 170.706467f,
                    Hx = 0.0f, Hy = -0.962225f, Hz = 0.0f, Hw = 0.272255272f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Rough Clan Informer",
                    Role = MissionNpcRole.Trash,
                    Level = 163, Health = 15307, MonsterData = 26139, Scale = 118, HeadMesh = 40249,
                    X = 66.743f, Y = 5.01000166f, Z = 162.280319f,
                    Hx = 0.0f, Hy = -0.272255272f, Hz = 0.0f, Hw = -0.9622251f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Rough Clan Informer",
                    Role = MissionNpcRole.Trash,
                    Level = 151, Health = 13518, MonsterData = 26139, Scale = 117, HeadMesh = 40249,
                    X = 23.06019f, Y = 5.01f, Z = 178.786621f,
                    Hx = 0.0f, Hy = -0.3375258f, Hz = 0.0f, Hw = 0.941316247f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Rustler",
                    Role = MissionNpcRole.Trash,
                    Level = 165, Health = 15605, MonsterData = 26133, Scale = 118, HeadMesh = 40251,
                    X = 46.8244934f, Y = 5.01000166f, Z = 193.6612f,
                    Hx = 0.0f, Hy = -0.307778716f, Hz = 0.0f, Hw = 0.951458f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Techno-Architect",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 57.64913f, Y = 5.01f, Z = 149.955643f,
                    Hx = 0.0f, Hy = 0.488147259f, Hz = 0.0f, Hw = 0.8727613f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Rough Clan Informer",
                    Role = MissionNpcRole.Trash,
                    Level = 151, Health = 13518, MonsterData = 26139, Scale = 117, HeadMesh = 40249,
                    X = 26.10097f, Y = 5.01000166f, Z = 175.069443f,
                    Hx = 0.0f, Hy = 0.8433504f, Hz = 0.0f, Hw = 0.537364f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Rustler",
                    Role = MissionNpcRole.Trash,
                    Level = 165, Health = 15605, MonsterData = 26133, Scale = 118, HeadMesh = 40251,
                    X = 47.8670921f, Y = 5.01f, Z = 192.825562f,
                    Hx = 0.0f, Hy = -0.49789694f, Hz = 0.0f, Hw = 0.8672362f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Dealer",
                    Role = MissionNpcRole.Trash,
                    Level = 162, Health = 15158, MonsterData = 26123, Scale = 118, HeadMesh = 40214,
                    X = 39.91947f, Y = 5.01f, Z = 180.8197f,
                    Hx = 0.0f, Hy = 0.317358047f, Hz = 0.0f, Hw = 0.9483058f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Rustler",
                    Role = MissionNpcRole.Trash,
                    Level = 166, Health = 15754, MonsterData = 26133, Scale = 118, HeadMesh = 40251,
                    X = 46.16773f, Y = 5.01f, Z = 166.228348f,
                    Hx = 0.0f, Hy = 0.28407535f, Hz = 0.0f, Hw = 0.958802f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Rustler",
                    Role = MissionNpcRole.Trash,
                    Level = 165, Health = 15605, MonsterData = 26133, Scale = 118, HeadMesh = 40251,
                    X = 47.491375f, Y = 5.01f, Z = 193.045074f,
                    Hx = 0.0f, Hy = -0.4977043f, Hz = 0.0f, Hw = 0.867346764f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Techno-Architect",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 42.4278641f, Y = 5.01f, Z = 125.299225f,
                    Hx = 0.0f, Hy = -0.185026631f, Hz = 0.0f, Hw = 0.9827335f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Ruffian",
                    Role = MissionNpcRole.Trash,
                    Level = 154, Health = 19551, MonsterData = 26137, Scale = 117, HeadMesh = 40209,
                    X = 37.93821f, Y = 5.01f, Z = 126.623955f,
                    Hx = 0.0f, Hy = -0.9990369f, Hz = 0.0f, Hw = 0.043878153f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Techno-Architect",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 26.15962f, Y = 5.01000166f, Z = 125.160065f,
                    Hx = 0.0f, Hy = 0.94901f, Hz = 0.0f, Hw = 0.3152459f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Rough Clan Informer",
                    Role = MissionNpcRole.Trash,
                    Level = 158, Health = 14562, MonsterData = 26139, Scale = 118, HeadMesh = 40249,
                    X = 14.4653845f, Y = 5.01000166f, Z = 145.616837f,
                    Hx = 0.0f, Hy = 0.9999993f, Hz = 0.0f, Hw = -0.00116793672f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Virusbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 153, Health = 13816, MonsterData = 26151, Scale = 117, HeadMesh = 40171,
                    X = 51.6340446f, Y = 5.01f, Z = 125.264458f,
                    Hx = 0.0f, Hy = 0.5528775f, Hz = 0.0f, Hw = 0.833262563f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Virusbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 162, Health = 15158, MonsterData = 26151, Scale = 118, HeadMesh = 40171,
                    X = 67.44747f, Y = 5.01000166f, Z = 127.228241f,
                    Hx = 0.0f, Hy = 0.7474788f, Hz = 0.0f, Hw = 0.6642857f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Clan Functionary",
                    Role = MissionNpcRole.Trash,
                    Level = 164, Health = 12365, MonsterData = 26143, Scale = 118, HeadMesh = 40137,
                    X = 73.72305f, Y = 5.01000166f, Z = 125.30249f,
                    Hx = 0.0f, Hy = 0.936933f, Hz = 0.0f, Hw = 0.3495091f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Tough Clan Diversionist",
                    Role = MissionNpcRole.Trash,
                    Level = 162, Health = 15158, MonsterData = 26125, Scale = 118, HeadMesh = 40215,
                    X = 88.47811f, Y = 5.01f, Z = 138.101f,
                    Hx = 0.0f, Hy = 0.009102894f, Hz = 0.0f, Hw = 0.9999586f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Dealer",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26123, Scale = 118, HeadMesh = 40214,
                    X = 82.6614456f, Y = 5.01000166f, Z = 147.599472f,
                    Hx = 0.0f, Hy = 0.992518842f, Hz = 0.0f, Hw = 0.122091331f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Techno-Architect",
                    Role = MissionNpcRole.Trash,
                    Level = 168, Health = 16052, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 15.401803f, Y = 5.01000166f, Z = 163.724609f,
                    Hx = 0.0f, Hy = 0.870859f, Hz = 0.0f, Hw = 0.491532862f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Poacher",
                    Role = MissionNpcRole.Trash,
                    Level = 163, Health = 15307, MonsterData = 26090, Scale = 118, HeadMesh = 40629,
                    X = 23.1397152f, Y = 5.01000166f, Z = 186.269531f,
                    Hx = 0.0f, Hy = 0.7436464f, Hz = 0.0f, Hw = 0.668573141f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Master Virusbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 152, Health = 13667, MonsterData = 26151, Scale = 117, HeadMesh = 40171,
                    X = 87.04694f, Y = 5.01000166f, Z = 194.508041f,
                    Hx = 0.0f, Hy = 0.6429453f, Hz = 0.0f, Hw = 0.7659121f,
                    Textures = null,
                    Meshes = null,
                },
                new MissionNpc
                {
                    Name = "Veteran Functionary",
                    Role = MissionNpcRole.Trash,
                    Level = 162, Health = 12127, MonsterData = 26155, Scale = 118, HeadMesh = 40138,
                    X = 94.5845947f, Y = 5.01000166f, Z = 175.303558f,
                    Hx = 0.0f, Hy = 0.8821801f, Hz = 0.0f, Hw = 0.470912158f,
                    Textures = null,
                    Meshes = null,
                },
            },
        },

        // Shape playfield 1419349 from capture 20260725-185432 (mobs/doors) + 184103 enter/fog
        new MissionShape
        {
            CapturedPlayfieldId = 1419349,
            // Gold PAF CharacterCoordinates (enter).
            SpawnX = 1.80102539f, SpawnY = 5.01f, SpawnZ = 95.01001f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Levi McDannold",
                    Role = MissionNpcRole.FindTarget,
                    Level = 5, Health = 115, MonsterData = 26097, Scale = 93, HeadMesh = 40111,
                    X = 81.300000f, Y = 5.115000f, Z = 130.900000f,
                    Hx = 0.000000000f, Hy = 0.000000000f, Hz = 0.000000000f, Hw = 1.000000000f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 81911 }, new[] { 2, 81913 }, new[] { 3, 81908 }, new[] { 4, 81916 } },
                    Meshes = new[] { new[] { 0, 40111, 0, 4 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Clan Gridcourier",
                    Role = MissionNpcRole.Trash,
                    Level = 6, Health = 138, MonsterData = 26135, Scale = 93, HeadMesh = 40271,
                    X = 44.156273f, Y = 5.010000f, Z = 127.933655f,
                    Hx = 0.000000000f, Hy = 0.953698456f, Hz = 0.000000000f, Hw = 0.300764471f,
                    Textures = new[] { new[] { 0, 40975 }, new[] { 1, 8732 }, new[] { 2, 40903 }, new[] { 3, 40892 }, new[] { 4, 40907 } },
                    Meshes = new[] { new[] { 0, 40271, 0, 4 }, new[] { 1, 35542, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Clan Hired Killer",
                    Role = MissionNpcRole.Trash,
                    Level = 4, Health = 93, MonsterData = 26092, Scale = 92, HeadMesh = 40694,
                    X = 56.905980f, Y = 5.010000f, Z = 102.572029f,
                    Hx = 0.000000000f, Hy = 0.560572500f, Hz = 0.000000000f, Hw = 0.828105330f,
                    Textures = new[] { new[] { 0, 40975 }, new[] { 1, 21824 }, new[] { 2, 9615 }, new[] { 3, 21819 }, new[] { 4, 21831 } },
                    Meshes = new[] { new[] { 0, 20108, 17998, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Clan Lookout",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26074, Scale = 93, HeadMesh = 40691,
                    X = 85.850720f, Y = 5.010000f, Z = 114.010000f,
                    Hx = 0.000000000f, Hy = 0.410778100f, Hz = 0.000000000f, Hw = 0.911735356f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 22571 }, new[] { 2, 45792 }, new[] { 3, 42254 }, new[] { 4, 42251 } },
                    Meshes = new[] { new[] { 0, 40691, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Exterminator",
                    Role = MissionNpcRole.Trash,
                    Level = 6, Health = 110, MonsterData = 26137, Scale = 93, HeadMesh = 40209,
                    X = 76.719040f, Y = 5.010000f, Z = 113.576485f,
                    Hx = 0.000000000f, Hy = -0.065047555f, Hz = 0.000000000f, Hw = 0.997882100f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 42249 }, new[] { 2, 42260 }, new[] { 3, 42252 }, new[] { 4, 42248 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Hired Killer",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26123, Scale = 93, HeadMesh = 40214,
                    X = 63.818893f, Y = 5.010000f, Z = 105.868000f,
                    Hx = 0.000000000f, Hy = 0.181935459f, Hz = 0.000000000f, Hw = 0.983310461f,
                    Textures = new[] { new[] { 0, 40975 }, new[] { 1, 21824 }, new[] { 2, 9615 }, new[] { 3, 21819 }, new[] { 4, 21831 } },
                    Meshes = new[] { new[] { 0, 20057, 17998, 2 }, new[] { 0, 40214, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Lookout",
                    Role = MissionNpcRole.Trash,
                    Level = 6, Health = 138, MonsterData = 26074, Scale = 93, HeadMesh = 40691,
                    X = 75.680984f, Y = 5.010000f, Z = 121.504959f,
                    Hx = 0.000000000f, Hy = 0.988147438f, Hz = 0.000000000f, Hw = 0.153507754f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 22571 }, new[] { 2, 45792 }, new[] { 3, 42254 }, new[] { 4, 42251 } },
                    Meshes = new[] { new[] { 0, 40691, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Marauder",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 161, MonsterData = 26101, Scale = 93, HeadMesh = 40105,
                    X = 75.247200f, Y = 5.010000f, Z = 134.207840f,
                    Hx = 0.000000000f, Hy = 0.671994900f, Hz = 0.000000000f, Hw = 0.740555763f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 22586 }, new[] { 2, 9619 }, new[] { 3, 22557 }, new[] { 4, 22645 } },
                    Meshes = new[] { new[] { 0, 40105, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Marksman",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26103, Scale = 93, HeadMesh = 40103,
                    X = 85.481224f, Y = 5.010000f, Z = 95.302590f,
                    Hx = 0.000000000f, Hy = 0.126859400f, Hz = 0.000000000f, Hw = 0.991920700f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 9404 }, new[] { 2, 40903 }, new[] { 3, 42241 }, new[] { 4, 42242 } },
                    Meshes = new[] { new[] { 0, 40103, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Nanorobber",
                    Role = MissionNpcRole.Trash,
                    Level = 4, Health = 93, MonsterData = 26090, Scale = 92, HeadMesh = 40629,
                    X = 84.989006f, Y = 5.010000f, Z = 147.131226f,
                    Hx = 0.000000000f, Hy = -0.997125500f, Hz = 0.000000000f, Hw = 0.075767204f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 42243 }, new[] { 2, 45792 }, new[] { 3, 42241 }, new[] { 4, 42245 } },
                    Meshes = new[] { new[] { 0, 40629, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Fresh Swindler",
                    Role = MissionNpcRole.Trash,
                    Level = 4, Health = 93, MonsterData = 26137, Scale = 92, HeadMesh = 40209,
                    X = 36.419743f, Y = 5.010000f, Z = 72.798780f,
                    Hx = 0.000000000f, Hy = -0.986643255f, Hz = 0.000000000f, Hw = 0.162895769f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 42244 }, new[] { 2, 45792 }, new[] { 3, 42241 }, new[] { 4, 42245 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                },
                new MissionNpc
                {
                    Name = "Garbage Flea",
                    Role = MissionNpcRole.Trash,
                    Level = 6, Health = 67, MonsterData = 17657, Scale = 38, HeadMesh = 0,
                    X = 73.947075f, Y = 5.010000f, Z = 134.615845f,
                    Hx = 0.000000000f, Hy = -0.839266700f, Hz = 0.000000000f, Hw = 0.543719947f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                }
            },

        },

// Shape playfield 1441800 from capture 20260725-151009 (fog building D7417D)
        new MissionShape
        {
            CapturedPlayfieldId = 1441800,
            SpawnX = 298.199f, SpawnY = 5.010f, SpawnZ = 235.010f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Mara Sotto",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 255.985000f, Y = 5.010000f, Z = 255.052000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Love Locknane",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26076, Scale = 92, HeadMesh = 40635,
                    X = 264.907000f, Y = 5.010000f, Z = 234.585000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40635, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Sina Sosnowski",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 257.289000f, Y = 5.010000f, Z = 229.188000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Felix Swicord",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26139, Scale = 92, HeadMesh = 40249,
                    X = 256.750000f, Y = 5.010000f, Z = 228.576000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40249, 0, 4 }, new[] { 1, 30866, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Donald Sosnowski",
                    Role = MissionNpcRole.FindTarget,
                    Level = 4, Health = 100, MonsterData = 26103, Scale = 92, HeadMesh = 40103,
                    X = 243.010000f, Y = 5.010754f, Z = 207.010000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40103, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Byron Lene",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26159, Scale = 92, HeadMesh = 40173,
                    X = 248.988000f, Y = 5.010753f, Z = 195.756000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40173, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Probe 2000-2",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 20614, Scale = 92, HeadMesh = 0,
                    X = 240.112000f, Y = 5.010753f, Z = 194.430000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Herb Lindner",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26101, Scale = 92, HeadMesh = 40105,
                    X = 272.844000f, Y = 5.010000f, Z = 213.938000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40105, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Probe 2000-3",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 20614, Scale = 92, HeadMesh = 0,
                    X = 242.693000f, Y = 5.010000f, Z = 252.608000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Probe 2000-1",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 20614, Scale = 92, HeadMesh = 0,
                    X = 271.317000f, Y = 5.010000f, Z = 252.365000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Janis Wyles",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 234.181000f, Y = 5.010000f, Z = 227.146000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Nida Croteau",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 243.430000f, Y = 5.010000f, Z = 183.010000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Len Fuchs",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26139, Scale = 92, HeadMesh = 40249,
                    X = 242.645000f, Y = 5.010000f, Z = 226.583000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40249, 0, 4 }, new[] { 1, 30866, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Laquanda Gabriel",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26137, Scale = 92, HeadMesh = 40209,
                    X = 256.106000f, Y = 5.010000f, Z = 188.158000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Cinda Harrist",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 262.672000f, Y = 5.010000f, Z = 192.310000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Ma Vallone",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26076, Scale = 92, HeadMesh = 40635,
                    X = 228.495000f, Y = 5.010000f, Z = 208.148000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40635, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Lashon Timas",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26137, Scale = 92, HeadMesh = 40209,
                    X = 267.651000f, Y = 5.010000f, Z = 205.032000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                    IsGrey = false,
                },
            },
        },

        // Shape playfield 1443840 from capture 20260725-002423 (20 npcs)
        new MissionShape
        {
            CapturedPlayfieldId = 1443840,
            SpawnX = 298.199f, SpawnY = 5.010f, SpawnZ = 225.010f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Ammie Fukuda",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26137, Scale = 93, HeadMesh = 40209,
                    X = 248.348500f, Y = 5.010000f, Z = 208.065700f,
                    Hx = 0.0000000f, Hy = -0.3550935f, Hz = 0.0000000f, Hw = 0.9348308f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 40901 }, new[] { 2, 40902 }, new[] { 3, 40896 }, new[] { 4, 40918 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Antoine Waldram",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26088, Scale = 92, HeadMesh = 40687,
                    X = 229.282500f, Y = 5.010000f, Z = 193.234200f,
                    Hx = 0.0000000f, Hy = -0.9912866f, Hz = 0.0000000f, Hw = -0.1317218f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 9615 }, new[] { 3, 87436 }, new[] { 4, 30886 } },
                    Meshes = new[] { new[] { 0, 40687, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Ardath Pourier",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26137, Scale = 93, HeadMesh = 40209,
                    X = 211.884500f, Y = 5.010000f, Z = 247.870100f,
                    Hx = 0.0000000f, Hy = -0.8577592f, Hz = 0.0000000f, Hw = 0.5140517f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 87438 }, new[] { 4, 22627 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Britni Annunziata",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26137, Scale = 93, HeadMesh = 40209,
                    X = 212.523000f, Y = 5.010000f, Z = 255.076100f,
                    Hx = 0.0000000f, Hy = 0.3397982f, Hz = 0.0000000f, Hw = 0.9404984f,
                    Textures = new[] { new[] { 0, 40973 }, new[] { 1, 40944 }, new[] { 2, 40963 }, new[] { 3, 40923 }, new[] { 4, 40983 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Carina Markarian",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26076, Scale = 92, HeadMesh = 40635,
                    X = 227.867900f, Y = 4.010002f, Z = 254.909800f,
                    Hx = 0.0000000f, Hy = -0.5202850f, Hz = 0.0000000f, Hw = 0.8539927f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 42244 }, new[] { 2, 42260 }, new[] { 3, 42246 }, new[] { 4, 42234 } },
                    Meshes = new[] { new[] { 0, 40635, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Carrol Welding",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26159, Scale = 92, HeadMesh = 40173,
                    X = 263.974300f, Y = 5.010000f, Z = 226.593500f,
                    Hx = 0.0000000f, Hy = 0.9997669f, Hz = 0.0000000f, Hw = -0.0215904f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 21827 }, new[] { 2, 9615 }, new[] { 3, 21822 }, new[] { 4, 19698 } },
                    Meshes = new[] { new[] { 0, 40173, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Consuela Danczak",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 223.373500f, Y = 5.010000f, Z = 266.974300f,
                    Hx = 0.0000000f, Hy = -0.4773278f, Hz = 0.0000000f, Hw = 0.8787253f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 45759 }, new[] { 2, 9615 }, new[] { 3, 45758 }, new[] { 4, 45760 } },
                    Meshes = new[] { new[] { 0, 20023, 56000, 2 }, new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Dana Baptist",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26088, Scale = 92, HeadMesh = 40687,
                    X = 238.010000f, Y = 5.010000f, Z = 262.479400f,
                    Hx = 0.0000000f, Hy = 0.4578915f, Hz = 0.0000000f, Hw = 0.8890081f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 40898 }, new[] { 2, 40903 }, new[] { 3, 40892 }, new[] { 4, 40907 } },
                    Meshes = new[] { new[] { 0, 40687, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Elvia Caris",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26137, Scale = 93, HeadMesh = 40209,
                    X = 202.286300f, Y = 5.010000f, Z = 205.651200f,
                    Hx = 0.0000000f, Hy = 0.9996983f, Hz = 0.0000000f, Hw = 0.0245639f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 37030 }, new[] { 2, 87445 }, new[] { 3, 87438 }, new[] { 4, 87425 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Getkeep",
                    Role = MissionNpcRole.Trash,
                    Level = 7, Health = 259, MonsterData = 0, Scale = 100, HeadMesh = 40111,
                    X = 298.199000f, Y = 5.010000f, Z = 225.010000f,
                    Hx = 0.0000000f, Hy = -0.7071068f, Hz = 0.0000000f, Hw = 0.7071068f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 0, 40111, 0, 4 }, new[] { 1, 96309, 0, 2 } },
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Joseph Schuemann",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26139, Scale = 93, HeadMesh = 40249,
                    X = 256.854900f, Y = 5.010000f, Z = 246.838100f,
                    Hx = 0.0000000f, Hy = -0.9762043f, Hz = 0.0000000f, Hw = 0.2168528f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 30848 }, new[] { 2, 9615 }, new[] { 3, 30831 }, new[] { 4, 22641 } },
                    Meshes = new[] { new[] { 0, 40249, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Kirk Danczak",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26159, Scale = 92, HeadMesh = 40173,
                    X = 234.490800f, Y = 5.010000f, Z = 174.748500f,
                    Hx = 0.0000000f, Hy = -0.0314947f, Hz = 0.0000000f, Hw = 0.9995039f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 27463 }, new[] { 2, 9615 }, new[] { 3, 22536 }, new[] { 4, 22617 } },
                    Meshes = new[] { new[] { 0, 20040, 22653, 2 }, new[] { 0, 40173, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Leota Kimpton",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 230.846300f, Y = 5.010000f, Z = 187.453600f,
                    Hx = 0.0000000f, Hy = 0.4001852f, Hz = 0.0000000f, Hw = 0.9164342f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 87438 }, new[] { 4, 22627 } },
                    Meshes = new[] { new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Malcom Thompon",
                    Role = MissionNpcRole.FindTarget,
                    Level = 4, Health = 93, MonsterData = 26103, Scale = 92, HeadMesh = 40103,
                    X = 236.400000f, Y = 5.010002f, Z = 192.600000f,
                    Hx = 0.0000000f, Hy = -0.9164343f, Hz = 0.0000000f, Hw = 0.4001852f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 81911 }, new[] { 2, 81913 }, new[] { 3, 81908 }, new[] { 4, 81916 } },
                    Meshes = new[] { new[] { 0, 40103, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Malcom Thompon",
                    Role = MissionNpcRole.Trash,
                    Level = 4, Health = 93, MonsterData = 26101, Scale = 92, HeadMesh = 40105,
                    X = 251.904800f, Y = 5.010000f, Z = 251.701600f,
                    Hx = 0.0000000f, Hy = 0.8812256f, Hz = 0.0000000f, Hw = 0.4726958f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 30862 }, new[] { 2, 9615 }, new[] { 3, 87433 }, new[] { 4, 9622 } },
                    Meshes = new[] { new[] { 0, 40105, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Myrtle Phare",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26076, Scale = 92, HeadMesh = 40635,
                    X = 215.559200f, Y = 5.010000f, Z = 231.097900f,
                    Hx = 0.0000000f, Hy = -0.9714530f, Hz = 0.0000000f, Hw = 0.2372321f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 30851 }, new[] { 2, 9615 }, new[] { 3, 87434 }, new[] { 4, 30880 } },
                    Meshes = new[] { new[] { 0, 40635, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Sid Basso",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26139, Scale = 93, HeadMesh = 40249,
                    X = 254.539100f, Y = 5.010000f, Z = 225.078600f,
                    Hx = 0.0000000f, Hy = -0.3902048f, Hz = 0.0000000f, Hw = 0.9207281f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 22560 }, new[] { 2, 9615 }, new[] { 3, 22534 }, new[] { 4, 22616 } },
                    Meshes = new[] { new[] { 0, 20074, 22652, 2 }, new[] { 0, 40249, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Tari Galanga",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 234.762100f, Y = 5.010000f, Z = 215.386800f,
                    Hx = 0.0000000f, Hy = 0.5519127f, Hz = 0.0000000f, Hw = 0.8339019f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 87438 }, new[] { 4, 22627 } },
                    Meshes = new[] { new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Tilda Konecny",
                    Role = MissionNpcRole.Trash,
                    Level = 5, Health = 115, MonsterData = 26137, Scale = 93, HeadMesh = 40209,
                    X = 255.687700f, Y = 5.010000f, Z = 234.985900f,
                    Hx = 0.0000000f, Hy = -0.6729826f, Hz = 0.0000000f, Hw = 0.7396583f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 87438 }, new[] { 4, 22627 } },
                    Meshes = new[] { new[] { 0, 40209, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Trinh Alsaqri",
                    Role = MissionNpcRole.Trash,
                    Level = 3, Health = 70, MonsterData = 26155, Scale = 92, HeadMesh = 40138,
                    X = 228.265300f, Y = 4.009999f, Z = 245.130100f,
                    Hx = 0.0000000f, Hy = 0.2940645f, Hz = 0.0000000f, Hw = 0.9557856f,
                    Textures = new[] { new[] { 0, 40973 }, new[] { 1, 40944 }, new[] { 2, 40963 }, new[] { 3, 40923 }, new[] { 4, 40983 } },
                    Meshes = new[] { new[] { 0, 40138, 0, 4 } },
                    IsGrey = false,
                },
            }
        },
        // Shape playfield 1460226 from capture 20260724-224228 (21 npcs)
        new MissionShape
        {
            CapturedPlayfieldId = 1460226,
            SpawnX = 298.199f, SpawnY = 5.010f, SpawnZ = 225.010f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Cyborg 1st Lieutenant",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14130, MonsterData = 17641, Scale = 100, HeadMesh = 0,
                    X = 264.083500f, Y = 5.010000f, Z = 200.458237f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 1, 99144, 0, 2 } },
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Cyborg 1st Lieutenant",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14130, MonsterData = 17641, Scale = 100, HeadMesh = 0,
                    X = 253.672714f, Y = 5.010000f, Z = 202.140564f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = new[] { new[] { 1, 99144, 0, 2 } },
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Jeanne Messamore",
                    Role = MissionNpcRole.FindTarget,
                    Level = 154, Health = 13860, MonsterData = 26076, Scale = 100, HeadMesh = 40635,
                    X = 218.400000f, Y = 5.010002f, Z = 248.300000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 81911 }, new[] { 2, 81913 }, new[] { 3, 81908 }, new[] { 4, 81916 } },
                    Meshes = new[] { new[] { 0, 40635, 0, 4 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Life Drainer",
                    Role = MissionNpcRole.Trash,
                    Level = 152, Health = 13680, MonsterData = 32419, Scale = 100, HeadMesh = 0,
                    X = 205.357285f, Y = 5.010000f, Z = 227.303375f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Life Drainer",
                    Role = MissionNpcRole.Trash,
                    Level = 151, Health = 13590, MonsterData = 32419, Scale = 100, HeadMesh = 0,
                    X = 245.429779f, Y = 3.109998f, Z = 223.771042f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Malfunctional Slayerdroid",
                    Role = MissionNpcRole.Trash,
                    Level = 148, Health = 13320, MonsterData = 22821, Scale = 100, HeadMesh = 0,
                    X = 202.331268f, Y = 5.010000f, Z = 234.335480f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Malfunctional Slayerdroid",
                    Role = MissionNpcRole.Trash,
                    Level = 149, Health = 13410, MonsterData = 22821, Scale = 100, HeadMesh = 0,
                    X = 263.279900f, Y = 5.010000f, Z = 222.895630f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Python",
                    Role = MissionNpcRole.Trash,
                    Level = 143, Health = 12870, MonsterData = 32419, Scale = 100, HeadMesh = 0,
                    X = 273.498077f, Y = 5.010000f, Z = 196.621368f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Python",
                    Role = MissionNpcRole.Trash,
                    Level = 143, Health = 12870, MonsterData = 32419, Scale = 100, HeadMesh = 0,
                    X = 284.645355f, Y = 5.010000f, Z = 238.128372f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Bodyguard",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 12600, MonsterData = 26082, Scale = 100, HeadMesh = 40634,
                    X = 265.062744f, Y = 5.010000f, Z = 262.114000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 15805 } },
                    Meshes = new[] { new[] { 0, 20089, 0, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Bodyguard",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 12600, MonsterData = 26082, Scale = 100, HeadMesh = 40634,
                    X = 248.383926f, Y = 5.010000f, Z = 260.787537f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 15806 }, new[] { 1, 8729 }, new[] { 2, 15807 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 20089, 0, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 7826, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Bountyhunter",
                    Role = MissionNpcRole.Trash,
                    Level = 144, Health = 12960, MonsterData = 26088, Scale = 100, HeadMesh = 40687,
                    X = 283.344147f, Y = 5.010000f, Z = 263.470337f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 15810 }, new[] { 1, 8739 }, new[] { 2, 8743 }, new[] { 3, 15812 }, new[] { 4, 15804 } },
                    Meshes = new[] { new[] { 0, 20107, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Doctor",
                    Role = MissionNpcRole.Trash,
                    Level = 143, Health = 12870, MonsterData = 26151, Scale = 100, HeadMesh = 40171,
                    X = 263.221741f, Y = 5.010000f, Z = 194.918945f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 14048 }, new[] { 1, 8731 }, new[] { 2, 9457 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Doctor",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 12600, MonsterData = 26151, Scale = 100, HeadMesh = 40171,
                    X = 263.024170f, Y = 5.010000f, Z = 187.800446f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 14048 }, new[] { 1, 9610 }, new[] { 2, 9457 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Hitman",
                    Role = MissionNpcRole.Trash,
                    Level = 143, Health = 12870, MonsterData = 26103, Scale = 100, HeadMesh = 40103,
                    X = 286.077900f, Y = 5.010000f, Z = 272.046356f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 19994, 0, 2 }, new[] { 0, 40103, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Scout",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 12780, MonsterData = 26133, Scale = 100, HeadMesh = 40251,
                    X = 275.659515f, Y = 5.010000f, Z = 224.950974f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 9611 }, new[] { 2, 9617 }, new[] { 3, 9604 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 40251, 0, 4 }, new[] { 1, 30238, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Soldier",
                    Role = MissionNpcRole.Trash,
                    Level = 142, Health = 12780, MonsterData = 26074, Scale = 100, HeadMesh = 40691,
                    X = 255.541809f, Y = 5.010000f, Z = 264.942780f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 20105, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Soldier",
                    Role = MissionNpcRole.Trash,
                    Level = 143, Health = 12870, MonsterData = 26074, Scale = 100, HeadMesh = 40691,
                    X = 285.412933f, Y = 5.010000f, Z = 247.198600f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9620 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9605 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 20105, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Soldier",
                    Role = MissionNpcRole.Trash,
                    Level = 148, Health = 13320, MonsterData = 26074, Scale = 100, HeadMesh = 40691,
                    X = 203.717224f, Y = 5.010000f, Z = 255.647568f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9620 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9605 }, new[] { 4, 9425 } },
                    Meshes = new[] { new[] { 0, 20095, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Spy",
                    Role = MissionNpcRole.Trash,
                    Level = 147, Health = 13230, MonsterData = 26076, Scale = 100, HeadMesh = 40635,
                    X = 234.632172f, Y = 5.010000f, Z = 244.354553f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 22543 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20082, 31720, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Clan Spy",
                    Role = MissionNpcRole.Trash,
                    Level = 144, Health = 12960, MonsterData = 26076, Scale = 100, HeadMesh = 40635,
                    X = 277.588959f, Y = 5.010000f, Z = 264.539100f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 22594 }, new[] { 3, 22543 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 20082, 31720, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
            }
        },
        // Shape playfield 1456133 from capture 20260724-224228 (24 npcs)
        new MissionShape
        {
            CapturedPlayfieldId = 1456133,
            SpawnX = 298.199f, SpawnY = 5.010f, SpawnZ = 255.010f,
            Npcs = new[]
            {
                new MissionNpc
                {
                    Name = "Bileswarm Breeder",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 14040, MonsterData = 31907, Scale = 100, HeadMesh = 0,
                    X = 294.115356f, Y = 5.010000f, Z = 243.126938f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Bileswarm Breeder",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 14040, MonsterData = 31907, Scale = 100, HeadMesh = 0,
                    X = 184.698761f, Y = 5.010000f, Z = 252.892258f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Bileswarm Breeder",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14130, MonsterData = 31907, Scale = 100, HeadMesh = 0,
                    X = 196.475861f, Y = 5.010000f, Z = 244.377800f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Bileswarm Dominator",
                    Role = MissionNpcRole.Trash,
                    Level = 151, Health = 13590, MonsterData = 31909, Scale = 100, HeadMesh = 0,
                    X = 285.473600f, Y = 5.010000f, Z = 255.336487f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Bileswarm Dominator",
                    Role = MissionNpcRole.Trash,
                    Level = 146, Health = 13140, MonsterData = 31909, Scale = 100, HeadMesh = 0,
                    X = 202.804108f, Y = 5.010000f, Z = 266.189941f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Lanny Marsalis",
                    Role = MissionNpcRole.FindTarget,
                    Level = 154, Health = 13860, MonsterData = 38389, Scale = 100, HeadMesh = 0,
                    X = 213.600000f, Y = 5.010002f, Z = 258.600000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                    Meshes = null,
                    IsGrey = true,
                },
                new MissionNpc
                {
                    Name = "Seasoned Bountyhunter",
                    Role = MissionNpcRole.Trash,
                    Level = 144, Health = 12960, MonsterData = 26097, Scale = 100, HeadMesh = 40111,
                    X = 236.771408f, Y = 5.010000f, Z = 295.393036f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 8745 }, new[] { 1, 8739 }, new[] { 2, 15811 }, new[] { 3, 8730 }, new[] { 4, 8747 } },
                    Meshes = new[] { new[] { 0, 20002, 0, 2 }, new[] { 0, 40111, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Hunter",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12690, MonsterData = 26076, Scale = 100, HeadMesh = 40635,
                    X = 275.635468f, Y = 5.010000f, Z = 228.477737f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 15810 }, new[] { 1, 8739 }, new[] { 2, 8743 }, new[] { 3, 8730 }, new[] { 4, 8747 } },
                    Meshes = new[] { new[] { 0, 20086, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Hunter",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 12600, MonsterData = 26076, Scale = 100, HeadMesh = 40635,
                    X = 245.647049f, Y = 5.010000f, Z = 203.545639f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 8745 }, new[] { 1, 8739 }, new[] { 2, 8743 }, new[] { 3, 8730 }, new[] { 4, 8747 } },
                    Meshes = new[] { new[] { 0, 20086, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Manhunter",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 12600, MonsterData = 26151, Scale = 100, HeadMesh = 40171,
                    X = 288.316467f, Y = 5.010000f, Z = 235.668564f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 14048 }, new[] { 1, 8731 }, new[] { 2, 9457 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Mercenary",
                    Role = MissionNpcRole.Trash,
                    Level = 148, Health = 13320, MonsterData = 26103, Scale = 100, HeadMesh = 40103,
                    X = 224.667114f, Y = 5.010000f, Z = 281.904724f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9422 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 9425 } },
                    Meshes = new[] { new[] { 0, 20004, 0, 2 }, new[] { 0, 40103, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Mercenary",
                    Role = MissionNpcRole.Trash,
                    Level = 146, Health = 13140, MonsterData = 26103, Scale = 100, HeadMesh = 40103,
                    X = 264.752625f, Y = 5.010000f, Z = 272.200378f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8729 }, new[] { 2, 9420 }, new[] { 3, 9419 }, new[] { 4, 9421 } },
                    Meshes = new[] { new[] { 0, 19994, 0, 2 }, new[] { 0, 40103, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Scout",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12690, MonsterData = 26088, Scale = 100, HeadMesh = 40687,
                    X = 218.210541f, Y = 5.010000f, Z = 265.805500f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 9611 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 40687, 0, 4 }, new[] { 1, 30238, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Scout",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12690, MonsterData = 26088, Scale = 100, HeadMesh = 40687,
                    X = 256.152924f, Y = 5.010000f, Z = 214.136032f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 9611 }, new[] { 2, 9617 }, new[] { 3, 9604 }, new[] { 4, 9624 } },
                    Meshes = new[] { new[] { 0, 40687, 0, 4 }, new[] { 1, 30238, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Seasoned Spy",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12690, MonsterData = 26076, Scale = 100, HeadMesh = 40635,
                    X = 263.197266f, Y = 5.010000f, Z = 265.570100f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 22594 }, new[] { 3, 9451 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20082, 31720, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Assassin",
                    Role = MissionNpcRole.Trash,
                    Level = 140, Health = 12600, MonsterData = 26135, Scale = 100, HeadMesh = 40271,
                    X = 285.323944f, Y = 5.010000f, Z = 255.318451f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 22607 }, new[] { 1, 22570 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20065, 31720, 2 }, new[] { 0, 40271, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Assassin",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12690, MonsterData = 26135, Scale = 100, HeadMesh = 40271,
                    X = 275.511658f, Y = 5.010000f, Z = 204.933853f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 22607 }, new[] { 1, 22570 }, new[] { 2, 22594 }, new[] { 3, 9451 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 20065, 0, 2 }, new[] { 0, 40271, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Nanoshifter",
                    Role = MissionNpcRole.Trash,
                    Level = 145, Health = 13050, MonsterData = 26076, Scale = 100, HeadMesh = 40635,
                    X = 231.331375f, Y = 5.010000f, Z = 197.187332f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 8740 }, new[] { 2, 9450 }, new[] { 3, 8815 }, new[] { 4, 8813 } },
                    Meshes = new[] { new[] { 0, 20082, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Nanoshifter",
                    Role = MissionNpcRole.Trash,
                    Level = 143, Health = 12870, MonsterData = 26076, Scale = 100, HeadMesh = 40635,
                    X = 293.170700f, Y = 5.010000f, Z = 214.487640f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 8816 }, new[] { 1, 42244 }, new[] { 2, 8814 }, new[] { 3, 8815 }, new[] { 4, 42245 } },
                    Meshes = new[] { new[] { 0, 20092, 0, 2 }, new[] { 0, 40635, 0, 4 }, new[] { 1, 99154, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Robotbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12690, MonsterData = 26082, Scale = 100, HeadMesh = 40634,
                    X = 234.024719f, Y = 5.010000f, Z = 202.165726f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9454 }, new[] { 1, 8731 }, new[] { 2, 22592 }, new[] { 3, 9455 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20081, 0, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Skilled Clan Robotbuilder",
                    Role = MissionNpcRole.Trash,
                    Level = 141, Health = 12690, MonsterData = 26082, Scale = 100, HeadMesh = 40634,
                    X = 273.987500f, Y = 5.010000f, Z = 198.152847f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9454 }, new[] { 1, 8731 }, new[] { 2, 22592 }, new[] { 3, 22541 }, new[] { 4, 9456 } },
                    Meshes = new[] { new[] { 0, 20081, 31719, 2 }, new[] { 0, 40634, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Tough Clan Diversionist",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 14040, MonsterData = 26125, Scale = 100, HeadMesh = 40215,
                    X = 208.872757f, Y = 5.010000f, Z = 283.254730f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 22607 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 22625 } },
                    Meshes = new[] { new[] { 0, 20048, 0, 2 }, new[] { 0, 40215, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Tough Clan Diversionist",
                    Role = MissionNpcRole.Trash,
                    Level = 156, Health = 14040, MonsterData = 26125, Scale = 100, HeadMesh = 40215,
                    X = 265.738831f, Y = 5.010000f, Z = 248.547958f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 20048, 0, 2 }, new[] { 0, 40215, 0, 4 }, new[] { 1, 15839, 0, 2 } },
                    IsGrey = false,
                },
                new MissionNpc
                {
                    Name = "Veteran Functionary",
                    Role = MissionNpcRole.Trash,
                    Level = 157, Health = 14130, MonsterData = 26155, Scale = 100, HeadMesh = 40138,
                    X = 284.467468f, Y = 5.010000f, Z = 234.947000f,
                    Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                    Textures = new[] { new[] { 0, 9452 }, new[] { 1, 8732 }, new[] { 2, 9450 }, new[] { 3, 9451 }, new[] { 4, 9453 } },
                    Meshes = new[] { new[] { 0, 20014, 0, 2 }, new[] { 0, 40138, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                    IsGrey = false,
                },
            }
        },
        };

        internal static MissionShape PickShape(int playfieldInstance, Random rng)
        {
            if (Shapes == null || Shapes.Length == 0)
            {
                return null;
            }

            int shapeId = playfieldInstance;
            int mapped;
            if (ZoneEngine.Core.Missions.MissionInstanceService.TryGetShapeSource(playfieldInstance, out mapped)
                && mapped > 0)
            {
                shapeId = mapped;
            }

            for (int i = 0; i < Shapes.Length; i++)
            {
                if (Shapes[i].CapturedPlayfieldId == shapeId)
                {
                    return Shapes[i];
                }
            }

            if (rng == null)
            {
                rng = new Random(playfieldInstance);
            }

            // Prefer door-backed shapes only (indexes 0..2 historically; match ShapePlayfieldIds).
            int[] doorIds = ZoneEngine.Core.Missions.MissionInstanceDynelCapture.ShapePlayfieldIds;
            if (doorIds != null && doorIds.Length > 0)
            {
                int pick = doorIds[Math.Abs(playfieldInstance) % doorIds.Length];
                for (int i = 0; i < Shapes.Length; i++)
                {
                    if (Shapes[i].CapturedPlayfieldId == pick)
                    {
                        return Shapes[i];
                    }
                }
            }

            return Shapes[Math.Abs(playfieldInstance) % Shapes.Length];
        }


        internal static byte[] GetGeneratorPayload(int playfieldInstance)
        {
            MissionShape shape = PickShape(playfieldInstance, null);
            int capturedPlayfieldId = shape != null ? shape.CapturedPlayfieldId : playfieldInstance;
            switch (capturedPlayfieldId)
            {
                case 1419310:
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x40, 0x48,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x55,
                       0x1E, 0x1E, 0x1E, 0x00, 0x00, 0x00, 0x15, 0x00,
                       0x13, 0x00, 0x00, 0x09, 0x03, 0x00, 0x64, 0x00,
                       0x01, 0x07, 0x01, 0x00, 0x02, 0x00, 0x03, 0x0B,
                       0x03, 0x00, 0x20, 0x00, 0x02, 0x0A, 0x00, 0x00,
                       0x61, 0x00, 0x04, 0x05, 0x03, 0x00, 0x18, 0x00,
                       0x01, 0x06, 0x03, 0x00, 0x22, 0x00, 0x05, 0x09,
                       0x03, 0x00, 0x15, 0x00, 0x06, 0x0A, 0x03, 0x00,
                       0x20, 0x00, 0x06, 0x0C, 0x01, 0x00, 0x1C, 0x00,
                       0x01, 0x0D, 0x00, 0x00, 0x0B, 0x00, 0x03, 0x0A,
                       0x03, 0x00, 0x33, 0x00, 0x06, 0x04, 0x00, 0x00,
                       0x38, 0x00, 0x06, 0x08, 0x01, 0x00, 0x3B, 0x00,
                       0x08, 0x07, 0x01, 0x00, 0x38, 0x00, 0x07, 0x06,
                       0x01, 0x00, 0x38, 0x00, 0x04, 0x05, 0x03, 0x00,
                       0x38, 0x00, 0x01, 0x05, 0x00, 0x00, 0x38, 0x00,
                       0x00, 0x06, 0x03, 0x00, 0x0B, 0x00, 0x08, 0x09,
                       0x01, 0x00, 0x3B, 0x00, 0x09, 0x0C, 0x01, 0x00,
                       0x0B, 0x00, 0x03, 0x0E, 0x01, 0xFF, 0xFF, 0xFF,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                    };
                case 1419335:
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x40, 0x46,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x40,
                       0x64, 0x64, 0x64, 0x00, 0x00, 0x00, 0x19, 0x00,
                       0x21, 0x00, 0x1D, 0x0F, 0x02, 0x00, 0x4C, 0x00,
                       0x19, 0x0D, 0x03, 0x00, 0x34, 0x00, 0x18, 0x10,
                       0x01, 0x00, 0x36, 0x00, 0x1B, 0x10, 0x00, 0x00,
                       0x3E, 0x00, 0x16, 0x0C, 0x02, 0x00, 0x15, 0x00,
                       0x1C, 0x0D, 0x03, 0x00, 0x46, 0x00, 0x15, 0x11,
                       0x02, 0x00, 0x3A, 0x00, 0x1C, 0x11, 0x00, 0x00,
                       0x50, 0x00, 0x1A, 0x13, 0x00, 0x00, 0x2E, 0x00,
                       0x16, 0x0F, 0x03, 0x00, 0x12, 0x00, 0x16, 0x0B,
                       0x00, 0x00, 0x10, 0x00, 0x1C, 0x0C, 0x00, 0x00,
                       0x1B, 0x00, 0x1D, 0x0E, 0x03, 0x00, 0x06, 0x00,
                       0x18, 0x13, 0x01, 0x00, 0x06, 0x00, 0x14, 0x13,
                       0x03, 0x00, 0x20, 0x00, 0x18, 0x12, 0x03, 0x00,
                       0x10, 0x00, 0x14, 0x12, 0x03, 0x00, 0x0E, 0x00,
                       0x14, 0x11, 0x03, 0x00, 0x12, 0x00, 0x17, 0x10,
                       0x00, 0x00, 0x06, 0x00, 0x16, 0x14, 0x02, 0x00,
                       0x01, 0x00, 0x15, 0x10, 0x00, 0x00, 0x06, 0x00,
                       0x1D, 0x13, 0x02, 0x00, 0x01, 0x00, 0x19, 0x14,
                       0x03, 0x00, 0x01, 0x00, 0x1D, 0x14, 0x01, 0x00,
                       0x06, 0x00, 0x1B, 0x17, 0x02, 0xFF, 0xFF, 0xFF,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                    };
                case 1419382:
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x40, 0x45,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x41,
                       0x96, 0x96, 0x96, 0x00, 0x00, 0x00, 0x0B, 0x00,
                       0x34, 0x00, 0x00, 0x0A, 0x00, 0x00, 0x41, 0x00,
                       0x01, 0x09, 0x01, 0x00, 0x15, 0x00, 0x05, 0x0D,
                       0x00, 0x00, 0x1E, 0x00, 0x04, 0x07, 0x03, 0x00,
                       0x22, 0x00, 0x03, 0x0B, 0x02, 0x00, 0x22, 0x00,
                       0x02, 0x0C, 0x02, 0x00, 0x10, 0x00, 0x06, 0x09,
                       0x01, 0x00, 0x35, 0x00, 0x06, 0x0B, 0x03, 0x00,
                       0x35, 0x00, 0x04, 0x0B, 0x01, 0x00, 0x3A, 0x00,
                       0x07, 0x0C, 0x03, 0x00, 0x0F, 0x00, 0x04, 0x06,
                       0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                       0xFF
                    };
                case 1441800:
                    // Gold fog ACG D7417D — capture 20260725-151009 / 080425.
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x41, 0x7D,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x40,
                       0x64, 0x64, 0x64, 0x00, 0x00, 0x00, 0x0E, 0x00,
                       0x23, 0x00, 0x1D, 0x06, 0x02, 0x00, 0x4B, 0x00,
                       0x19, 0x04, 0x03, 0x00, 0x39, 0x00, 0x19, 0x05,
                       0x00, 0x00, 0x44, 0x00, 0x17, 0x08, 0x03, 0x00,
                       0x01, 0x00, 0x1B, 0x08, 0x01, 0x00, 0x20, 0x00,
                       0x18, 0x04, 0x01, 0x00, 0x01, 0x00, 0x1B, 0x04,
                       0x01, 0x00, 0x08, 0x00, 0x17, 0x07, 0x00, 0x00,
                       0x06, 0x00, 0x18, 0x0B, 0x02, 0x00, 0x20, 0x00,
                       0x18, 0x07, 0x02, 0x00, 0x1B, 0x00, 0x19, 0x0B,
                       0x00, 0x00, 0x10, 0x00, 0x1A, 0x0A, 0x01, 0x00,
                       0x1B, 0x00, 0x16, 0x09, 0x01, 0x00, 0x09, 0x00,
                       0x1A, 0x09, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                       0xFF, 0xFF, 0xFF,
                    };
                case 1443840:
                    // Generator body only — L7 gold 20260725-002423 ACG D74167.
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x41, 0x67,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x40,
                       0x64, 0x64, 0x64, 0x00, 0x00, 0x00, 0x14, 0x00,
                       0x22, 0x00, 0x1D, 0x07, 0x02, 0x00, 0x4E, 0x00,
                       0x16, 0x07, 0x03, 0x00, 0x3E, 0x00, 0x15, 0x09,
                       0x00, 0x00, 0x46, 0x00, 0x16, 0x04, 0x02, 0x00,
                       0x20, 0x00, 0x1B, 0x0A, 0x00, 0x00, 0x1B, 0x00,
                       0x18, 0x09, 0x01, 0x00, 0x08, 0x00, 0x1C, 0x09,
                       0x01, 0x00, 0x20, 0x00, 0x15, 0x07, 0x01, 0x00,
                       0x08, 0x00, 0x1A, 0x07, 0x03, 0x00, 0x30, 0x00,
                       0x14, 0x09, 0x02, 0x00, 0x20, 0x00, 0x17, 0x0C,
                       0x00, 0x00, 0x08, 0x00, 0x19, 0x06, 0x01, 0x00,
                       0x12, 0x00, 0x15, 0x06, 0x03, 0x00, 0x0E, 0x00,
                       0x19, 0x05, 0x01, 0x00, 0x08, 0x00, 0x15, 0x05,
                       0x03, 0x00, 0x1B, 0x00, 0x19, 0x04, 0x03, 0x00,
                       0x09, 0x00, 0x15, 0x04, 0x03, 0x00, 0x20, 0x00,
                       0x18, 0x03, 0x02, 0x00, 0x20, 0x00, 0x17, 0x03,
                       0x02, 0x00, 0x20, 0x00, 0x16, 0x03, 0x02, 0xFF,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                    };
                case 1460226:
                    // Generator body only (second C79F). Do not include PlayfieldAnarchyF
                    // PlayfieldId1/PlayfieldId2 wrapper — that makes the client fail ACG and
                    // look up the remapped live playfield (0x151000+) as static PF data.
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x99, 0x90,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x40,
                       0x64, 0x64, 0x64, 0x00, 0x00, 0x00, 0x18, 0x00,
                       0x23, 0x00, 0x1D, 0x07, 0x02, 0x00, 0x4F, 0x00,
                       0x17, 0x05, 0x03, 0x00, 0x33, 0x00, 0x16, 0x08,
                       0x03, 0x00, 0x40, 0x00, 0x16, 0x03, 0x00, 0x00,
                       0x3A, 0x00, 0x19, 0x03, 0x00, 0x00, 0x26, 0x00,
                       0x1A, 0x08, 0x01, 0x00, 0x3D, 0x00, 0x14, 0x05,
                       0x02, 0x00, 0x24, 0x00, 0x18, 0x07, 0x00, 0x00,
                       0x26, 0x00, 0x1C, 0x03, 0x03, 0x00, 0x2A, 0x00,
                       0x17, 0x0B, 0x02, 0x00, 0x01, 0x00, 0x15, 0x09,
                       0x03, 0x00, 0x12, 0x00, 0x19, 0x09, 0x01, 0x00,
                       0x01, 0x00, 0x15, 0x03, 0x03, 0x00, 0x0E, 0x00,
                       0x18, 0x03, 0x03, 0x00, 0x1F, 0x00, 0x1A, 0x07,
                       0x02, 0x00, 0x0E, 0x00, 0x1A, 0x0B, 0x02, 0x00,
                       0x0E, 0x00, 0x1B, 0x0A, 0x01, 0x00, 0x09, 0x00,
                       0x14, 0x04, 0x00, 0x00, 0x06, 0x00, 0x1C, 0x06,
                       0x02, 0x00, 0x09, 0x00, 0x1C, 0x02, 0x00, 0x00,
                       0x01, 0x00, 0x1B, 0x03, 0x03, 0x00, 0x30, 0x00,
                       0x19, 0x0C, 0x00, 0x00, 0x09, 0x00, 0x16, 0x0C,
                       0x03, 0x00, 0x0E, 0x00, 0x17, 0x0E, 0x02, 0xFF,
                       0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                    };
                case 1456133:
                    // Generator body only — see case 1460226.
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x99, 0x92,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x40,
                       0x64, 0x64, 0x64, 0x00, 0x00, 0x00, 0x1A, 0x00,
                       0x22, 0x00, 0x1D, 0x04, 0x02, 0x00, 0x4E, 0x00,
                       0x16, 0x04, 0x03, 0x00, 0x35, 0x00, 0x17, 0x06,
                       0x00, 0x00, 0x3E, 0x00, 0x15, 0x01, 0x00, 0x00,
                       0x35, 0x00, 0x1B, 0x07, 0x00, 0x00, 0x38, 0x00,
                       0x18, 0x06, 0x00, 0x00, 0x3B, 0x00, 0x1C, 0x06,
                       0x00, 0x00, 0x25, 0x00, 0x13, 0x04, 0x00, 0x00,
                       0x38, 0x00, 0x1A, 0x02, 0x00, 0x00, 0x40, 0x00,
                       0x16, 0x09, 0x01, 0x00, 0x01, 0x00, 0x14, 0x01,
                       0x03, 0x00, 0x1B, 0x00, 0x17, 0x00, 0x02, 0x00,
                       0x0E, 0x00, 0x1B, 0x0A, 0x02, 0x00, 0x20, 0x00,
                       0x19, 0x08, 0x03, 0x00, 0x10, 0x00, 0x18, 0x05,
                       0x00, 0x00, 0x08, 0x00, 0x18, 0x09, 0x02, 0x00,
                       0x12, 0x00, 0x1D, 0x05, 0x00, 0x00, 0x30, 0x00,
                       0x1D, 0x08, 0x01, 0x00, 0x09, 0x00, 0x12, 0x04,
                       0x03, 0x00, 0x06, 0x00, 0x13, 0x05, 0x02, 0x00,
                       0x09, 0x00, 0x14, 0x03, 0x00, 0x00, 0x30, 0x00,
                       0x15, 0x05, 0x01, 0x00, 0x12, 0x00, 0x1B, 0x02,
                       0x01, 0x00, 0x1B, 0x00, 0x1A, 0x01, 0x02, 0x00,
                       0x08, 0x00, 0x1A, 0x05, 0x02, 0x00, 0x09, 0x00,
                       0x15, 0x0A, 0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                       0xFF, 0xFF, 0xFF
                    };
                case 1419349:
                    // Fog gold ACG D7418B — capture 20260725-184103.
                    return new byte[]
                    {
                       0x00, 0x00, 0xC7, 0x9F, 0x00, 0xD7, 0x41, 0x8B,
                       0x00, 0x00, 0x00, 0x02, 0x00, 0x03, 0x00, 0x1E,
                       0x00, 0x1E, 0x00, 0x40, 0x00, 0x00, 0x01, 0x40,
                       0x64, 0x64, 0x64, 0x00, 0x00, 0x00, 0x0E, 0x00,
                       0x23, 0x00, 0x00, 0x14, 0x00, 0x00, 0x4A, 0x00,
                       0x01, 0x12, 0x01, 0x00, 0x2E, 0x00, 0x08, 0x10,
                       0x03, 0x00, 0x38, 0x00, 0x07, 0x11, 0x02, 0x00,
                       0x20, 0x00, 0x07, 0x15, 0x00, 0x00, 0x1B, 0x00,
                       0x05, 0x13, 0x02, 0x00, 0x08, 0x00, 0x04, 0x11,
                       0x00, 0x00, 0x12, 0x00, 0x04, 0x17, 0x02, 0x00,
                       0x20, 0x00, 0x03, 0x13, 0x02, 0x00, 0x20, 0x00,
                       0x03, 0x16, 0x01, 0x00, 0x08, 0x00, 0x08, 0x0F,
                       0x00, 0x00, 0x06, 0x00, 0x06, 0x13, 0x03, 0x00,
                       0x0E, 0x00, 0x06, 0x11, 0x03, 0x00, 0x20, 0x00,
                       0x07, 0x10, 0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                       0xFF, 0xFF, 0xFF,
                    };
                default:
                    return null;
            }
        }

        /// <summary>
        /// Building instance from generator payload bytes 4..7 (must match door FullUpdates).
        /// </summary>
        internal static int GetBuildingInstance(byte[] payload)
        {
            if (payload == null || payload.Length < 8)
            {
                return CapturedBuildingInstance;
            }

            return (payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7];
        }

        internal static bool IsCapturedShapePlayfield(int playfieldInstance)
        {
            for (int i = 0; i < Shapes.Length; i++)
            {
                if (Shapes[i].CapturedPlayfieldId == playfieldInstance)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Corpse loot observed inside capture 20260719-5-different-shape-fo-mish (Items=low:high:ql:qty).
    /// </summary>
    internal static class MissionInstanceLootCatalog
    {
        internal sealed class LootDrop
        {
            public int MonsterData;
            public int LowId;
            public int HighId;
            public int Quality;
        }

        internal static readonly LootDrop[] CapturedDrops =
        {
            // Gold 20260725-185432 corpse open: Fresh Lookout Items=124444:124445:5:1
            new LootDrop { MonsterData = 26074, LowId = 124444, HighId = 124445, Quality = 5 },
            // Distinct templates — salt picks among these so corpses do not share one drop.
            // Prefer ids known present in local items.dat; missing ids are skipped at resolve.
            new LootDrop { MonsterData = 26137, LowId = 192317, HighId = 192318, Quality = 1 },
            new LootDrop { MonsterData = 26135, LowId = 100010, HighId = 100010, Quality = 1 },
            new LootDrop { MonsterData = 26090, LowId = 165839, HighId = 165840, Quality = 1 },
            new LootDrop { MonsterData = 26155, LowId = 100292, HighId = 100292, Quality = 1 },
            new LootDrop { MonsterData = 26076, LowId = 100299, HighId = 100299, Quality = 1 },
            new LootDrop { MonsterData = 26101, LowId = 100344, HighId = 100344, Quality = 1 },
            new LootDrop { MonsterData = 26103, LowId = 100349, HighId = 100349, Quality = 1 },
            new LootDrop { MonsterData = 26097, LowId = 100361, HighId = 100361, Quality = 1 },
            new LootDrop { MonsterData = 26111, LowId = 121567, HighId = 121567, Quality = 1 },
            new LootDrop { MonsterData = 26113, LowId = 122123, HighId = 122123, Quality = 1 },
        };

        // QuestAlternative FindItem templates from capture 20260719-Rolling different mishes:
        // Radioactive Isotope Container (FindItemA) / Encrypted Info Capsule (FindItemB).
        internal static readonly LootDrop FindItemA =
            new LootDrop { MonsterData = 0, LowId = 100010, HighId = 100010, Quality = 1 };

        internal static readonly LootDrop FindItemB =
            new LootDrop { MonsterData = 0, LowId = 165839, HighId = 165840, Quality = 1 };

        internal static LootDrop ResolveFindItemDrop(int salt)
        {
            return (salt & 1) == 0 ? FindItemA : FindItemB;
        }

        internal static bool TryGetDrop(int monsterData, out LootDrop drop)
        {
            drop = null;
            for (int i = 0; i < CapturedDrops.Length; i++)
            {
                if (CapturedDrops[i].MonsterData == monsterData)
                {
                    drop = CapturedDrops[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mission trash remaps MonsterData via appearance remix, so exact capture MonsterData
        /// almost never matches. Fall back to a captured drop template scaled to mission QL.
        /// </summary>
        internal static bool TryGetMissionTrashDrop(int monsterData, int missionQl, int salt, out LootDrop drop)
        {
            // Always vary by salt — exact MonsterData match made remixed trash share one drop.
            drop = null;
            if (CapturedDrops == null || CapturedDrops.Length == 0)
            {
                return false;
            }

            int start = Math.Abs(salt) % CapturedDrops.Length;
            LootDrop src = null;
            for (int i = 0; i < CapturedDrops.Length; i++)
            {
                LootDrop candidate = CapturedDrops[(start + i) % CapturedDrops.Length];
                if (candidate == null || candidate.LowId <= 0)
                {
                    continue;
                }

                int high = candidate.HighId > 0 ? candidate.HighId : candidate.LowId;
                if (!ItemLoader.ItemList.ContainsKey(candidate.LowId)
                    || !ItemLoader.ItemList.ContainsKey(high))
                {
                    continue;
                }

                src = candidate;
                break;
            }

            if (src == null)
            {
                return false;
            }

            int ql = missionQl > 0 ? missionQl : (src.Quality > 0 ? src.Quality : 1);
            if (ql < 1)
            {
                ql = 1;
            }

            drop = new LootDrop
                   {
                       MonsterData = monsterData,
                       LowId = src.LowId,
                       HighId = src.HighId > 0 ? src.HighId : src.LowId,
                       Quality = ql
                   };
            return true;
        }
    }
}
