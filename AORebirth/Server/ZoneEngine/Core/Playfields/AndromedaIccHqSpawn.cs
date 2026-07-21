namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// Capture-backed ICC HQ Andromeda population (PF 655 / 0x028F).
    /// Capture 20260719-ICC-Capture: 52 city NPCs (players excluded; Karrec trio owned separately).
    /// </summary>
    internal static class AndromedaIccHqSpawn
    {
        private const int AndromedaPlayfieldId = 655;

        private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

        // Capture 20260719-ICC-Capture Peacekeeper Constad HasExtendedTextures (Material #468 → 272342).
        private static readonly byte[] ConstadExtendedTextureOverrideData =
            new byte[]
                {
                    0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x34,
                    0x36, 0x38, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x27, 0xD6, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00
                };

        // Base humanoid template used only to instantiate the Character; all appearance
        // (monsterData, meshes, textures, head, scale, identity) is overridden from the capture.
        private const string TemplateHash = "BART";

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            if (string.Equals(name, "Peacekeeper Constad", StringComparison.Ordinal))
            {
                data = (byte[])ConstadExtendedTextureOverrideData.Clone();
                return true;
            }

            data = null;
            return false;
        }

        internal static bool NeedsNataliaScfuFlag7(string name)
        {
            return string.Equals(name, "Natalia Akcora", StringComparison.Ordinal);
        }

        internal static bool IsAndromedaCityNpcPlayfield(int playfieldInstance)
        {
            return playfieldInstance == AndromedaPlayfieldId;
        }

        private sealed class CityNpc
        {
            public string Name;
            public int Level;
            public int Health;
            public int MonsterData;
            public int Scale;
            public int VisualFlags;
            public int HeadMesh;
            public int RunSpeed;
            public int NpcFamily;
            public int LosHeight;
            public int CharacterFlags;
            public int AppearanceValue;
            public int Side;
            public int Breed;
            public int Gender;
            public int Race;
            public int Fatness;
            public int MovementMode;
            public float X;
            public float Y;
            public float Z;
            public float Hx;
            public float Hy;
            public float Hz;
            public float Hw;
            public int[][] Textures;
            public int[][] Meshes;
            public float[][] Waypoints;
        }

        private static readonly CityNpc[] Npcs =
        {
            new CityNpc
            {
                Name = "Leet",
                Level = 1, Health = 20, MonsterData = 17655, Scale = 90, VisualFlags = 31, HeadMesh = 0, RunSpeed = 5,
                NpcFamily = 36, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1483,
                Side = 3, Breed = 6, Gender = 1, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3239.99121f, Y = 39.925f, Z = 861.58905f,
                Hx = 0.0f, Hy = -0.2101f, Hz = 0.0f, Hw = 0.97768f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = null,
                Waypoints = new[] { new[] { 3239.99121f, 39.925f, 861.58905f }, new[] { 3239.13159f, 39.92641f, 865.15796f } },
            },
            new CityNpc
            {
                Name = "Dockworker",
                Level = 5, Health = 115, MonsterData = 26074, Scale = 93, VisualFlags = 31, HeadMesh = 40691, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3150.30347f, Y = 35.11f, Z = 830.0021f,
                Hx = 0.0f, Hy = -0.00267f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205120, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 258954, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3156.34546f, Y = 35.11f, Z = 800.0122f,
                Hx = 0.0f, Hy = -0.11249f, Hz = 0.0f, Hw = 0.99365f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = new[] { new[] { 3156.34546f, 35.11f, 800.0122f }, new[] { 3153.93457f, 35.11f, 811.0076f } },
            },
            new CityNpc
            {
                Name = "Kate Hayes - Rubi-Ka Tours",
                Level = 100, Health = 6829, MonsterData = 262895, Scale = 112, VisualFlags = 31, HeadMesh = 40650, RunSpeed = 346,
                NpcFamily = 3, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3262.41357f, Y = 39.925f, Z = 861.05286f,
                Hx = 0.0f, Hy = -0.55578f, Hz = 0.0f, Hw = 0.83133f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 247955 }, new[] { 2, 247989 }, new[] { 3, 247909 }, new[] { 4, 248030 } },
                Meshes = new[] { new[] { 0, 204941, 0, 0 }, new[] { 0, 40650, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper Commander",
                Level = 250, Health = 200000, MonsterData = 26092, Scale = 110, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3240.47778f, Y = 36.4838f, Z = 881.81525f,
                Hx = 0.0f, Hy = -0.83571f, Hz = 0.0f, Hw = 0.54917f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 264698, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = new[] { new[] { 3240.47778f, 36.4838f, 881.81525f }, new[] { 3236.08862f, 37.40736f, 879.9187f } },
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3245.65625f, Y = 35.11f, Z = 910.61005f,
                Hx = 0.0f, Hy = 0.71215f, Hz = 0.0f, Hw = 0.70203f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = new[] { new[] { 3245.65625f, 35.11f, 910.61005f }, new[] { 3251.70142f, 35.10998f, 910.5235f } },
            },
            new CityNpc
            {
                Name = "Cody Monkie",
                Level = 89, Health = 6671, MonsterData = 26092, Scale = 100, VisualFlags = 31, HeadMesh = 40704, RunSpeed = 318,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3231.83545f, Y = 36.165f, Z = 946.1915f,
                Hx = 0.0f, Hy = 0.66442f, Hz = 0.0f, Hw = 0.74736f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 42253 }, new[] { 2, 81913 }, new[] { 3, 42252 }, new[] { 4, 42250 } },
                Meshes = new[] { new[] { 0, 40704, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Secretary",
                Level = 220, Health = 101861, MonsterData = 284218, Scale = 90, VisualFlags = 31, HeadMesh = 40171, RunSpeed = 749,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1640,
                Side = 0, Breed = 3, Gender = 2, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3245.51245f, Y = 35.715f, Z = 935.02893f,
                Hx = 0.0f, Hy = 1.0f, Hz = 0.0f, Hw = 1e-05f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 164968 }, new[] { 2, 0 }, new[] { 3, 22537 }, new[] { 4, 22618 } },
                Meshes = new[] { new[] { 0, 40171, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Secretary",
                Level = 220, Health = 101861, MonsterData = 284218, Scale = 90, VisualFlags = 31, HeadMesh = 40171, RunSpeed = 749,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1640,
                Side = 0, Breed = 3, Gender = 2, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3245.593f, Y = 35.715f, Z = 953.962f,
                Hx = 0.0f, Hy = 0.00132f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 164968 }, new[] { 2, 0 }, new[] { 3, 22537 }, new[] { 4, 22618 } },
                Meshes = new[] { new[] { 0, 40171, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Arbiter's Guardian",
                Level = 220, Health = 101861, MonsterData = 165196, Scale = 125, VisualFlags = 31, HeadMesh = 40117, RunSpeed = 749,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 269226497, AppearanceValue = 1672,
                Side = 0, Breed = 4, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3244.0105f, Y = 35.715f, Z = 939.70355f,
                Hx = 0.0f, Hy = 1.0f, Hz = 0.0f, Hw = 0.00016f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286466, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Arbiter's Guardian",
                Level = 220, Health = 101861, MonsterData = 165196, Scale = 125, VisualFlags = 31, HeadMesh = 40117, RunSpeed = 749,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 269226497, AppearanceValue = 1672,
                Side = 0, Breed = 4, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3247.36743f, Y = 35.715f, Z = 939.7799f,
                Hx = 0.0f, Hy = 1.0f, Hz = 0.0f, Hw = -0.00287f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286466, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Des Morck",
                Level = 102, Health = 8480, MonsterData = 274385, Scale = 100, VisualFlags = 31, HeadMesh = 223890, RunSpeed = 351,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 279450113, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 8,
                X = 3259.52979f, Y = 36.175f, Z = 941.8552f,
                Hx = 0.0f, Hy = -0.76827f, Hz = 0.0f, Hw = 0.64013f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 22587 }, new[] { 2, 248878 }, new[] { 3, 22558 }, new[] { 4, 22646 } },
                Meshes = new[] { new[] { 0, 20076, 0, 0 }, new[] { 0, 223890, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Peacekeeper Constad",
                Level = 200, Health = 72868, MonsterData = 26092, Scale = 100, VisualFlags = 31, HeadMesh = 40694, RunSpeed = 515,
                NpcFamily = 3, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3277.36279f, Y = 35.555f, Z = 921.896f,
                Hx = 0.0f, Hy = -0.76602f, Hz = 0.0f, Hw = 0.64282f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 20110, 0, 0 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 268615, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Confused Colonist",
                Level = 177, Health = 17394, MonsterData = 204067, Scale = 119, VisualFlags = 31, HeadMesh = 40117, RunSpeed = 447,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3319.996f, Y = 35.915f, Z = 876.43054f,
                Hx = 0.0f, Hy = 1.0f, Hz = 0.0f, Hw = -0.00233f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 21825 }, new[] { 2, 9619 }, new[] { 3, 21820 }, new[] { 4, 21832 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Merchant",
                Level = 190, Health = 19332, MonsterData = 165186, Scale = 120, VisualFlags = 31, HeadMesh = 40681, RunSpeed = 454,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3305.7207f, Y = 36.16026f, Z = 871.3621f,
                Hx = 0.0f, Hy = -0.39142f, Hz = 0.0f, Hw = 0.92021f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 9611 }, new[] { 2, 14050 }, new[] { 3, 9604 }, new[] { 4, 14034 } },
                Meshes = new[] { new[] { 0, 40681, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Colonist",
                Level = 157, Health = 14413, MonsterData = 165196, Scale = 118, VisualFlags = 31, HeadMesh = 40117, RunSpeed = 436,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3319.02832f, Y = 35.915f, Z = 860.0258f,
                Hx = 0.0f, Hy = -0.00179f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 22571 }, new[] { 2, 9407 }, new[] { 3, 156739 }, new[] { 4, 8813 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Young Man",
                Level = 174, Health = 16947, MonsterData = 204985, Scale = 119, VisualFlags = 31, HeadMesh = 40700, RunSpeed = 445,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3314.62134f, Y = 35.915f, Z = 855.3346f,
                Hx = 0.0f, Hy = -0.00122f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 22582 }, new[] { 2, 9450 }, new[] { 3, 22553 }, new[] { 4, 22641 } },
                Meshes = new[] { new[] { 0, 40700, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Old Lady",
                Level = 186, Health = 18736, MonsterData = 165178, Scale = 120, VisualFlags = 31, HeadMesh = 40660, RunSpeed = 452,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3314.15161f, Y = 35.915f, Z = 860.12427f,
                Hx = 0.0f, Hy = -0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = new[] { new[] { 0, 40660, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Colonist",
                Level = 32, Health = 1156, MonsterData = 165179, Scale = 102, VisualFlags = 31, HeadMesh = 40624, RunSpeed = 110,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3307.46851f, Y = 35.915f, Z = 859.61743f,
                Hx = 0.0f, Hy = -0.8402f, Hz = 0.0f, Hw = 0.54227f,
                Textures = new[] { new[] { 0, 155956 }, new[] { 1, 155954 }, new[] { 2, 14045 }, new[] { 3, 155953 }, new[] { 4, 155957 } },
                Meshes = new[] { new[] { 0, 40624, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Old Geezer",
                Level = 53, Health = 2520, MonsterData = 165212, Scale = 106, VisualFlags = 31, HeadMesh = 40116, RunSpeed = 183,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3306.849f, Y = 36.16614f, Z = 873.7261f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 296298 }, new[] { 2, 9619 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40116, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Young Woman",
                Level = 38, Health = 1526, MonsterData = 165181, Scale = 103, VisualFlags = 31, HeadMesh = 40648, RunSpeed = 131,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3313.809f, Y = 35.915f, Z = 856.26105f,
                Hx = 0.0f, Hy = -0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 14027 }, new[] { 2, 14045 }, new[] { 3, 22543 }, new[] { 4, 30885 } },
                Meshes = new[] { new[] { 0, 40648, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Natalia Akcora",
                Level = 15, Health = 393, MonsterData = 26076, Scale = 97, VisualFlags = 31, HeadMesh = 40635, RunSpeed = 52,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3286.631f, Y = 35.11f, Z = 860.87915f,
                Hx = 0.0f, Hy = 0.43681f, Hz = 0.0f, Hw = 0.89955f,
                Textures = new[] { new[] { 0, 284555 }, new[] { 1, 247933 }, new[] { 2, 284553 }, new[] { 3, 247887 }, new[] { 4, 284556 } },
                Meshes = new[] { new[] { 0, 40635, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Hologram of Adeline Guerra",
                Level = 200, Health = 26675, MonsterData = 26155, Scale = 130, VisualFlags = 31, HeadMesh = 40138, RunSpeed = 515,
                NpcFamily = 1020, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1896,
                Side = 0, Breed = 3, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3311.442f, Y = 35.11f, Z = 840.4632f,
                Hx = 0.0f, Hy = -0.67649f, Hz = 0.0f, Hw = 0.73646f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 247933 }, new[] { 2, 247977 }, new[] { 3, 247887 }, new[] { 4, 248016 } },
                Meshes = new[] { new[] { 0, 204941, 0, 0 }, new[] { 0, 40138, 0, 4 }, new[] { 1, 29084, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Jacinto Clemente",
                Level = 46, Health = 2020, MonsterData = 26139, Scale = 105, VisualFlags = 31, HeadMesh = 40279, RunSpeed = 158,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3304.41333f, Y = 36.485f, Z = 855.23676f,
                Hx = 0.0f, Hy = -0.68719f, Hz = 0.0f, Hw = 0.72648f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40279, 0, 4 }, new[] { 1, 258990, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Bored Traveller",
                Level = 70, Health = 3955, MonsterData = 165182, Scale = 108, VisualFlags = 31, HeadMesh = 40666, RunSpeed = 246,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3316.80786f, Y = 35.915f, Z = 872.65027f,
                Hx = 0.0f, Hy = 0.15215f, Hz = 0.0f, Hw = 0.98836f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 81912 }, new[] { 2, 40903 }, new[] { 3, 87439 }, new[] { 4, 40907 } },
                Meshes = new[] { new[] { 0, 20110, 0, 0 }, new[] { 0, 40666, 0, 4 } },
                Waypoints = new[] { new[] { 3316.80786f, 35.915f, 872.65027f }, new[] { 3317.25342f, 35.91179f, 874.06506f } },
            },
            new CityNpc
            {
                Name = "Curious Colonist",
                Level = 134, Health = 10984, MonsterData = 165193, Scale = 116, VisualFlags = 31, HeadMesh = 40158, RunSpeed = 424,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1896,
                Side = 0, Breed = 3, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3311.41431f, Y = 36.485f, Z = 881.3146f,
                Hx = 0.0f, Hy = -0.75404f, Hz = 0.0f, Hw = 0.65683f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 40946 }, new[] { 2, 42235 }, new[] { 3, 40925 }, new[] { 4, 40913 } },
                Meshes = new[] { new[] { 0, 40158, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3298.858f, Y = 35.11f, Z = 929.8612f,
                Hx = 0.0f, Hy = 0.71507f, Hz = 0.0f, Hw = 0.69905f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = new[] { new[] { 3298.858f, 35.11f, 929.8612f }, new[] { 3314.681f, 35.11f, 929.5027f } },
            },
            new CityNpc
            {
                Name = "Colonist",
                Level = 186, Health = 18736, MonsterData = 165192, Scale = 120, VisualFlags = 31, HeadMesh = 40267, RunSpeed = 452,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3322.99048f, Y = 35.915f, Z = 854.61896f,
                Hx = 0.0f, Hy = -0.00378f, Hz = 0.0f, Hw = 0.99999f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 37030 }, new[] { 2, 22595 }, new[] { 3, 37032 }, new[] { 4, 22626 } },
                Meshes = new[] { new[] { 0, 45772, 0, 0 }, new[] { 0, 40267, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Helpful Colonist",
                Level = 28, Health = 910, MonsterData = 165185, Scale = 101, VisualFlags = 31, HeadMesh = 40710, RunSpeed = 97,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3331.92651f, Y = 35.915f, Z = 869.87225f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 42255 }, new[] { 2, 162142 }, new[] { 3, 81907 }, new[] { 4, 22640 } },
                Meshes = new[] { new[] { 0, 40710, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Middle-aged Guy",
                Level = 18, Health = 493, MonsterData = 165187, Scale = 98, VisualFlags = 31, HeadMesh = 40687, RunSpeed = 62,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3329.59058f, Y = 35.915f, Z = 856.39197f,
                Hx = 0.0f, Hy = 0.33176f, Hz = 0.0f, Hw = 0.94337f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 9610 }, new[] { 2, 9616 }, new[] { 3, 9602 }, new[] { 4, 22639 } },
                Meshes = new[] { new[] { 0, 40687, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3350.153f, Y = 35.11f, Z = 870.1036f,
                Hx = 0.0f, Hy = 1.0f, Hz = 0.0f, Hw = 0.0022f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3351.29346f, Y = 35.11f, Z = 862.2358f,
                Hx = 0.0f, Hy = 0.00434f, Hz = 0.0f, Hw = 0.99999f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Traveller",
                Level = 191, Health = 19481, MonsterData = 165180, Scale = 120, VisualFlags = 31, HeadMesh = 40628, RunSpeed = 454,
                NpcFamily = 100, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3330.13354f, Y = 36.485f, Z = 882.1767f,
                Hx = 0.0f, Hy = 1.0f, Hz = 0.0f, Hw = -0.00042f,
                Textures = new[] { new[] { 0, 85939 }, new[] { 1, 30846 }, new[] { 2, 30865 }, new[] { 3, 30828 }, new[] { 4, 30877 } },
                Meshes = new[] { new[] { 0, 40628, 0, 4 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3333.869f, Y = 35.11f, Z = 946.94336f,
                Hx = 0.0f, Hy = 0.99989f, Hz = 0.0f, Hw = 0.01509f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3269.07324f, Y = 35.11f, Z = 820.8667f,
                Hx = 0.0f, Hy = -0.58331f, Hz = 0.0f, Hw = 0.81225f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = new[] { new[] { 3269.07324f, 35.11f, 820.8667f }, new[] { 3259.6145f, 35.10998f, 824.0559f } },
            },
            new CityNpc
            {
                Name = "Arbiter's Guardian",
                Level = 220, Health = 101861, MonsterData = 165196, Scale = 125, VisualFlags = 31, HeadMesh = 40117, RunSpeed = 749,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 269226497, AppearanceValue = 1672,
                Side = 0, Breed = 4, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3311.90332f, Y = 35.11f, Z = 837.6541f,
                Hx = 0.0f, Hy = -0.60102f, Hz = 0.0f, Hw = 0.79924f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286466, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3282.052f, Y = 35.11f, Z = 819.01526f,
                Hx = 0.0f, Hy = -0.94112f, Hz = 0.0f, Hw = 0.33807f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = new[] { new[] { 3282.052f, 35.11f, 819.01526f }, new[] { 3277.121f, 35.11f, 813.0375f } },
            },
            new CityNpc
            {
                Name = "Cedrick Gaviglia",
                Level = 79, Health = 4715, MonsterData = 26139, Scale = 109, VisualFlags = 31, HeadMesh = 223900, RunSpeed = 280,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3211.56421f, Y = 35.11f, Z = 847.1196f,
                Hx = 0.0f, Hy = -0.70913f, Hz = 0.0f, Hw = 0.70508f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 223900, 0, 4 }, new[] { 1, 258990, 0, 2 } },
                Waypoints = new[] { new[] { 3211.56421f, 35.11f, 847.1196f }, new[] { 3243.56543f, 36.04992f, 851.82953f } },
            },
            new CityNpc
            {
                Name = "Robin Marksward",
                Level = 20, Health = 559, MonsterData = 26101, Scale = 99, VisualFlags = 31, HeadMesh = 40105, RunSpeed = 69,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1672,
                Side = 0, Breed = 4, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3235.61157f, Y = 35.11f, Z = 918.99817f,
                Hx = 0.0f, Hy = -0.00039f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 35585 }, new[] { 1, 35589 }, new[] { 2, 35587 }, new[] { 3, 35586 }, new[] { 4, 35588 } },
                Meshes = new[] { new[] { 0, 20003, 35590, 2 }, new[] { 0, 40105, 0, 4 }, new[] { 2, 288657, 0, 2 }, new[] { 3, 291884, 0, 0 }, new[] { 4, 291884, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Representative of IPS",
                Level = 25, Health = 724, MonsterData = 26088, Scale = 100, VisualFlags = 31, HeadMesh = 40687, RunSpeed = 86,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3185.85986f, Y = 35.915f, Z = 861.4865f,
                Hx = 0.0f, Hy = 0.70486f, Hz = 0.0f, Hw = 0.70935f,
                Textures = new[] { new[] { 0, 213851 }, new[] { 1, 213751 }, new[] { 2, 213807 }, new[] { 3, 213708 }, new[] { 4, 213925 } },
                Meshes = new[] { new[] { 0, 214654, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 5, 214715, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Douglass Guynes",
                Level = 26, Health = 786, MonsterData = 26139, Scale = 101, VisualFlags = 31, HeadMesh = 40282, RunSpeed = 90,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1608,
                Side = 0, Breed = 2, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3195.70166f, Y = 36.52931f, Z = 857.5897f,
                Hx = 0.0f, Hy = -0.96262f, Hz = 0.0f, Hw = 0.27086f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40282, 0, 4 }, new[] { 1, 29084, 0, 2 } },
                Waypoints = new[] { new[] { 3195.70166f, 36.52931f, 857.5897f }, new[] { 3197.90576f, 36.52911f, 862.36292f } },
            },
            new CityNpc
            {
                Name = "Transportation Officer Darren Plush",
                Level = 220, Health = 101861, MonsterData = 26088, Scale = 125, VisualFlags = 31, HeadMesh = 40687, RunSpeed = 749,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3176.1582f, Y = 35.915f, Z = 880.26483f,
                Hx = 0.0f, Hy = 0.91929f, Hz = 0.0f, Hw = 0.39359f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 20110, 0, 0 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 268615, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "ICC Peacekeeper",
                Level = 250, Health = 200000, MonsterData = 26090, Scale = 100, VisualFlags = 31, HeadMesh = 40629, RunSpeed = 1100,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 2,
                X = 3187.47681f, Y = 35.11f, Z = 890.35455f,
                Hx = 0.0f, Hy = 0.79556f, Hz = 0.0f, Hw = 0.60588f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265787, 286562, 2 }, new[] { 0, 40629, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286467, 0, 0 } },
                Waypoints = new[] { new[] { 3187.47681f, 35.11f, 890.35455f }, new[] { 3189.396f, 35.11f, 889.8253f } },
            },
            new CityNpc
            {
                Name = "Engineer Automaton I",
                Level = 5, Health = 138, MonsterData = 17649, Scale = 93, VisualFlags = 31, HeadMesh = 0, RunSpeed = 32,
                NpcFamily = 95, LosHeight = 0, CharacterFlags = 403182081, AppearanceValue = 1514,
                Side = 0, Breed = 7, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3181.492f, Y = 35.915f, Z = 877.11926f,
                Hx = 0.0f, Hy = 0.0f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = null,
                Waypoints = new[] { new[] { 3181.492f, 35.915f, 877.11926f }, new[] { 3234.08765f, 35.61f, 834.31366f } },
            },
            new CityNpc
            {
                // Capture 20260721-finish Engineer Warbot 7984BF25 near ICC HQ arrival plaza.
                Name = "Engineer Warbot",
                Level = 121, Health = 11184, MonsterData = 17697, Scale = 114, VisualFlags = 31, HeadMesh = 0, RunSpeed = 132,
                NpcFamily = 95, LosHeight = 0, CharacterFlags = 403182081, AppearanceValue = 1513,
                Side = 1, Breed = 7, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3338.26416f, Y = 36.4849968f, Z = 862.944153f,
                Hx = 0.0f, Hy = -0.194869325f, Hz = 0.0f, Hw = 0.980829239f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = null,
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Dockworker",
                Level = 5, Health = 115, MonsterData = 26074, Scale = 93, VisualFlags = 31, HeadMesh = 40691, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3186.671f, Y = 35.11f, Z = 834.5417f,
                Hx = 0.0f, Hy = 0.45529f, Hz = 0.0f, Hw = 0.89034f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205120, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 81800, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Dockworker",
                Level = 5, Health = 115, MonsterData = 26143, Scale = 93, VisualFlags = 31, HeadMesh = 40137, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1896,
                Side = 0, Breed = 3, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3174.30029f, Y = 35.11f, Z = 772.5452f,
                Hx = 0.0f, Hy = 0.45133f, Hz = 0.0f, Hw = 0.89236f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205118, 0, 2 }, new[] { 0, 40137, 0, 4 }, new[] { 1, 264730, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Dockworker",
                Level = 5, Health = 115, MonsterData = 26074, Scale = 93, VisualFlags = 31, HeadMesh = 40691, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3175.012f, Y = 35.11f, Z = 795.6567f,
                Hx = 0.0f, Hy = 0.4566f, Hz = 0.0f, Hw = 0.88967f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205120, 0, 2 }, new[] { 0, 40691, 0, 4 }, new[] { 1, 264730, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Dockworker",
                Level = 5, Health = 115, MonsterData = 203740, Scale = 93, VisualFlags = 31, HeadMesh = 40127, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3164.65063f, Y = 35.11f, Z = 783.6056f,
                Hx = 0.0f, Hy = 0.00395f, Hz = 0.0f, Hw = 0.99999f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40127, 0, 4 }, new[] { 1, 258954, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Dockworker",
                Level = 5, Health = 115, MonsterData = 203740, Scale = 93, VisualFlags = 31, HeadMesh = 40127, RunSpeed = 19,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1416,
                Side = 0, Breed = 4, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3206.93774f, Y = 35.11f, Z = 775.53357f,
                Hx = 0.0f, Hy = 0.4511f, Hz = 0.0f, Hw = 0.89247f,
                Textures = new[] { new[] { 0, 295555 }, new[] { 1, 295553 }, new[] { 2, 295554 }, new[] { 3, 295552 }, new[] { 4, 295556 } },
                Meshes = new[] { new[] { 0, 205110, 0, 2 }, new[] { 0, 40127, 0, 4 }, new[] { 1, 258954, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Fia Lou",
                Level = 10, Health = 227, MonsterData = 26090, Scale = 95, VisualFlags = 31, HeadMesh = 223846, RunSpeed = 34,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 1,
                X = 3240.556f, Y = 36.34524f, Z = 772.71185f,
                Hx = 0.0f, Hy = -0.99472f, Hz = 0.0f, Hw = 0.10267f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 247971 }, new[] { 2, 248000 }, new[] { 3, 247924 }, new[] { 4, 248037 } },
                Meshes = new[] { new[] { 0, 223846, 0, 4 }, new[] { 2, 95786, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Polly Delenick",
                Level = 83, Health = 5053, MonsterData = 26090, Scale = 110, VisualFlags = 31, HeadMesh = 40644, RunSpeed = 295,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3297.49316f, Y = 35.11f, Z = 765.3954f,
                Hx = 0.0f, Hy = 0.62913f, Hz = 0.0f, Hw = 0.7773f,
                Textures = new[] { new[] { 0, 155947 }, new[] { 1, 155944 }, new[] { 2, 155945 }, new[] { 3, 155946 }, new[] { 4, 155943 } },
                Meshes = new[] { new[] { 0, 40644, 0, 4 }, new[] { 1, 258990, 0, 2 } },
                Waypoints = null,
            },
            new CityNpc
            {
                Name = "Karima Bunke",
                Level = 179, Health = 17692, MonsterData = 26090, Scale = 119, VisualFlags = 31, HeadMesh = 40637, RunSpeed = 508,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 268964353, AppearanceValue = 1832,
                Side = 0, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3266.016f, Y = 17.11f, Z = 750.6178f,
                Hx = 0.0f, Hy = -0.92655f, Hz = 0.0f, Hw = 0.37618f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 296303 }, new[] { 2, 155955 }, new[] { 3, 245697 }, new[] { 4, 296305 } },
                Meshes = new[] { new[] { 0, 40637, 0, 4 }, new[] { 1, 258990, 0, 2 } },
                Waypoints = null,
            },
        };

        internal static void ClearPlayfield(int playfieldInstance)
        {
            SpawnedPlayfields.Remove(playfieldInstance);
        }

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            if (playfieldIdentity.Instance != AndromedaPlayfieldId)
            {
                return;
            }

            // Idempotent: RegisterCapturedNpcSpawns can be invoked by multiple content modules.
            if (!SpawnedPlayfields.Add(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AndromedaIccHqSpawn skip duplicate pf=" + playfieldIdentity.Instance);
                return;
            }

            int spawned = 0;
            foreach (CityNpc def in Npcs)
            {
                if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    spawned++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AndromedaIccHqSpawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned
                + "/" + Npcs.Length);
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            CityNpc def)
        {
            var npcController = new NPCController { AiProfile = NpcAiProfile.Social };
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = def.X, y = def.Y, z = def.Z },
                new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                npcController,
                def.Level);

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AndromedaIccHqSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
                return false;
            }

            mob.Name = def.Name;
            mob.FirstName = string.Empty;
            mob.LastName = string.Empty;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)def.NpcFamily);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.losheight, (uint)def.LosHeight);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)def.CharacterFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)def.Side);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.breed, (uint)def.Breed);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.sex, (uint)def.Gender);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.race, (uint)def.Race);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.fatness, (uint)def.Fatness);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.accountflags, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.expansion, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.profession, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualprofession, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.currentmovementmode, (uint)def.MovementMode);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.prevmovementmode, (uint)def.MovementMode);
            if (def.Scale > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            }

            if (def.HeadMesh > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)def.HeadMesh);
            }

            if (def.RunSpeed > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, (uint)def.RunSpeed);
            }

            ApplyAppearance(mob, def);
            ApplyWaypoints(mob, npcController, def);
            mob.Coordinates(new Coordinate { x = def.X, y = def.Y, z = def.Z });

            mob.DoNotDoTimers = false;
            activateNpc(mob);
            if (string.Equals(def.Name, "Natalia Akcora", StringComparison.Ordinal))
            {
                AndromedaIccHqIdleGestureRuntime.RegisterNatalia(mob);
            }

            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return true;
        }

        private static void ApplyAppearance(Character mob, CityNpc def)
        {
            if (def.Textures != null && def.Textures.Length > 0)
            {
                mob.Textures.Clear();
                foreach (int[] t in def.Textures)
                {
                    mob.Textures.Add(new AOTextures(t[0], t[1]));
                }
            }

            if (def.Meshes != null && def.Meshes.Length > 0)
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                foreach (int[] m in def.Meshes)
                {
                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                }
            }
            else if (def.HeadMesh > 0)
            {
                // Capture had headmesh only (e.g. Natalia) — still emit head layer.
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                mob.MeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
                mob.SocialMeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
            }
        }

        private static void ApplyWaypoints(Character mob, NPCController controller, CityNpc def)
        {
            if (def.Waypoints == null || def.Waypoints.Length < 2)
            {
                return;
            }

            mob.Waypoints.Clear();
            foreach (float[] wp in def.Waypoints)
            {
                mob.AddWaypoint(new Vector3(wp[0], wp[1], wp[2]), false);
            }

            controller.State = CharacterState.Patrolling;
        }
    }
}
