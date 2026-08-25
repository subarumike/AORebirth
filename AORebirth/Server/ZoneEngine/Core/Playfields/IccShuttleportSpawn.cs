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
    /// Capture-backed ICC Shuttleport population (PF 4582 / 0x11E6).
    /// Capture ICC Shuttleport [PF 4582] - 20260818-214552: stable social/vendor NPCs and deduplicated guard positions.
    /// Ordinary enemy, respawn, corpse, and loot behavior remains in the promoted capture evidence for a combat-backed pass.
    /// </summary>
    internal static class IccShuttleportSpawn
    {
        private const int IccShuttleportPlayfieldId = 4582;

        private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

        private const string TemplateHash = "BART";

        private sealed class ShuttleportNpc
        {
            public int SourceNpcId;
            public string Name;
            public string TemplateHash;
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
            public Func<CapturedEnemyCombatContract> CombatContractFactory;
        }

        private static readonly ShuttleportNpc[] Npcs =
        {
            new ShuttleportNpc
            {
                SourceNpcId = 1007858,
                Name = "Island Reet",
                TemplateHash = "A001",
                Level = 1, Health = 12, MonsterData = 30365, Scale = 90, VisualFlags = 31, HeadMesh = 0, RunSpeed = 6,
                NpcFamily = 53, LosHeight = 0, CharacterFlags = 512, AppearanceValue = 0,
                Side = 3, Breed = 6, Gender = 1, Race = 1, Fatness = 1, MovementMode = 1,
                X = 953.147461f, Y = 23.9720612f, Z = 747.212952f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = new[] { new[] { 0, 0 } },
                Meshes = new[] { new[] { 1, 95857, 0, 2 } },
                Waypoints = null,
                CombatContractFactory = IccShuttleportBasicCombatCatalog.IslandReet,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008030,
                Name = "Clan Equipment Vendor",
                Level = 40, Health = 1650, MonsterData = 250381, Scale = 103, VisualFlags = 31, HeadMesh = 40243, RunSpeed = 137,
                NpcFamily = 87, LosHeight = 0, CharacterFlags = 271061505, AppearanceValue = 1865,
                Side = 1, Breed = 2, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 918.30884f, Y = 47.115f, Z = 864.24896f,
                Hx = 0f, Hy = -0.79369f, Hz = 0f, Hw = 0.60832f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 37030 }, new[] { 2, 40903 }, new[] { 3, 37031 }, new[] { 4, 30883 } },
                Meshes = new[] { new[] { 0, 40243, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008034,
                Name = "Clan Recruiter",
                Level = 40, Health = 1650, MonsterData = 165210, Scale = 103, VisualFlags = 31, HeadMesh = 40679, RunSpeed = 137,
                NpcFamily = 104, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1577,
                Side = 1, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 918.67175f, Y = 47.115f, Z = 862.17957f,
                Hx = 0f, Hy = -0.39477f, Hz = 0f, Hw = 0.91878f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 42255 }, new[] { 2, 81913 }, new[] { 3, 42254 }, new[] { 4, 42251 } },
                Meshes = new[] { new[] { 0, 40679, 0, 4 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008037,
                Name = "Adri Afeli",
                Level = 25, Health = 941, MonsterData = 26092, Scale = 100, VisualFlags = 31, HeadMesh = 223803, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 904.0218f, Y = 25.21f, Z = 843.9628f,
                Hx = 0f, Hy = 0.62224f, Hz = 0f, Hw = 0.78282f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 22571 }, new[] { 2, 0 }, new[] { 3, 27422 }, new[] { 4, 22626 } },
                Meshes = new[] { new[] { 0, 223803, 0, 4 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008029,
                Name = "Omni-Trans Equipment Vendor",
                Level = 40, Health = 1650, MonsterData = 250380, Scale = 103, VisualFlags = 31, HeadMesh = 40173, RunSpeed = 137,
                NpcFamily = 88, LosHeight = 0, CharacterFlags = 271061505, AppearanceValue = 1642,
                Side = 2, Breed = 3, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 933.13403f, Y = 47.025f, Z = 859.2879f,
                Hx = 0f, Hy = 0.15235f, Hz = 0f, Hw = 0.98833f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 22579 }, new[] { 2, 9619 }, new[] { 3, 22550 }, new[] { 4, 22638 } },
                Meshes = new[] { new[] { 0, 40173, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008033,
                Name = "Vendor Antonio Stacklund",
                Level = 40, Health = 1650, MonsterData = 26088, Scale = 103, VisualFlags = 31, HeadMesh = 40687, RunSpeed = 137,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 279450113, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 933.7467f, Y = 47.025f, Z = 873.18115f,
                Hx = 0f, Hy = -0.82394f, Hz = 0f, Hw = 0.56667f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 30862 }, new[] { 2, 40903 }, new[] { 3, 30839 }, new[] { 4, 30886 } },
                Meshes = new[] { new[] { 0, 40687, 0, 4 }, new[] { 1, 7777, 0, 2 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008035,
                Name = "Omni-Tek Recruitment Officer",
                Level = 40, Health = 1650, MonsterData = 165190, Scale = 103, VisualFlags = 31, HeadMesh = 40680, RunSpeed = 137,
                NpcFamily = 105, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1578,
                Side = 2, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 935.4956f, Y = 47.025f, Z = 859.2217f,
                Hx = 0f, Hy = 0.21986f, Hz = 0f, Hw = 0.97553f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 14039 }, new[] { 2, 14035 }, new[] { 3, 14044 }, new[] { 4, 14033 } },
                Meshes = new[] { new[] { 0, 40680, 0, 4 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008036,
                Name = "Neutral Observer",
                Level = 40, Health = 1650, MonsterData = 165188, Scale = 103, VisualFlags = 31, HeadMesh = 40690, RunSpeed = 137,
                NpcFamily = 103, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 937.58264f, Y = 47.025f, Z = 871.0066f,
                Hx = 0f, Hy = -0.84476f, Hz = 0f, Hw = 0.53514f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 42262 }, new[] { 2, 42260 }, new[] { 3, 42263 }, new[] { 4, 42261 } },
                Meshes = new[] { new[] { 0, 40690, 0, 4 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008043,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 923.054f, Y = 47.025f, Z = 859.1898f,
                Hx = 0f, Hy = 0.00352f, Hz = 0f, Hw = 0.99999f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008044,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 928.8299f, Y = 47.025f, Z = 858.9181f,
                Hx = 0f, Hy = 0.00194f, Hz = 0f, Hw = 1f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008031,
                Name = "Omni Unicorn Squadleader Fixx",
                Level = 100, Health = 6829, MonsterData = 247041, Scale = 156, VisualFlags = 31, HeadMesh = 0, RunSpeed = 346,
                NpcFamily = 219, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1739,
                Side = 3, Breed = 6, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 912.92267f, Y = 39.665f, Z = 824.8919f,
                Hx = 0f, Hy = -0.50322f, Hz = 0f, Hw = 0.86416f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = new[] { new[] { 1, 233232, 0, 2 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008032,
                Name = "Clan Field Surgeon Elsa Oosta",
                Level = 100, Health = 6829, MonsterData = 26080, Scale = 112, VisualFlags = 31, HeadMesh = 40637, RunSpeed = 346,
                NpcFamily = 87, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1833,
                Side = 1, Breed = 1, Gender = 3, Race = 1, Fatness = 1, MovementMode = 3,
                X = 911.71173f, Y = 39.665f, Z = 827.88544f,
                Hx = 0f, Hy = -0.8384f, Hz = 0f, Hw = 0.54506f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 245161 }, new[] { 2, 14050 }, new[] { 3, 215296 }, new[] { 4, 215294 } },
                Meshes = new[] { new[] { 0, 40637, 0, 4 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008039,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 906.05994f, Y = 40.10264f, Z = 830.1977f,
                Hx = 0f, Hy = -0.71624f, Hz = 0f, Hw = 0.69786f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008040,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 905.59845f, Y = 40.15212f, Z = 827.1122f,
                Hx = 0f, Hy = -0.492f, Hz = 0f, Hw = 0.87059f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008045,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 42536,
                Side = 0, Breed = 1, Gender = 2, Race = 41, Fatness = 1, MovementMode = 1,
                X = 893.06555f, Y = 36.66919f, Z = 824.15204f,
                Hx = 0f, Hy = -0.88792f, Hz = 0f, Hw = 0.46f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008046,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 42536,
                Side = 0, Breed = 1, Gender = 2, Race = 41, Fatness = 1, MovementMode = 1,
                X = 894.3154f, Y = 36.87788f, Z = 828.8113f,
                Hx = 0f, Hy = 0.77544f, Hz = 0f, Hw = -0.63142f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008047,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 42536,
                Side = 0, Breed = 1, Gender = 2, Race = 41, Fatness = 1, MovementMode = 1,
                X = 894.4577f, Y = 36.95649f, Z = 831.9813f,
                Hx = 0f, Hy = -0.76962f, Hz = 0f, Hw = 0.63851f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008048,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 42536,
                Side = 0, Breed = 1, Gender = 2, Race = 41, Fatness = 1, MovementMode = 1,
                X = 894.00574f, Y = 36.82637f, Z = 827.60077f,
                Hx = 0f, Hy = -0.73678f, Hz = 0f, Hw = 0.67613f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008041,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 892.4263f, Y = 39.655f, Z = 784.461f,
                Hx = 0f, Hy = -0.69991f, Hz = 0f, Hw = 0.71423f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008042,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 890.94367f, Y = 39.655f, Z = 782.89465f,
                Hx = 0f, Hy = -0.35492f, Hz = 0f, Hw = 0.9349f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008027,
                Name = "Brandon Thorn",
                Level = 40, Health = 1650, MonsterData = 204985, Scale = 103, VisualFlags = 31, HeadMesh = 40700, RunSpeed = 137,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 934.9187f, Y = 24.35288f, Z = 760.5995f,
                Hx = 0f, Hy = 0.99988f, Hz = 0f, Hw = 0.01557f,
                Textures = new[] { new[] { 0, 85939 }, new[] { 1, 120627 }, new[] { 2, 0 }, new[] { 3, 120625 }, new[] { 4, 120626 } },
                Meshes = new[] { new[] { 0, 40700, 0, 4 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1007985,
                Name = "ICC Bio-Inspector",
                Level = 25, Health = 941, MonsterData = 26151, Scale = 100, VisualFlags = 31, HeadMesh = 223923, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1640,
                Side = 0, Breed = 3, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 835.3192f, Y = 21.8896f, Z = 758.1043f,
                Hx = 0f, Hy = 0.99984f, Hz = 0f, Hw = 0.01802f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 223923, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008028,
                Name = "Manager Travis Molen",
                Level = 100, Health = 6829, MonsterData = 26084, Scale = 112, VisualFlags = 31, HeadMesh = 40689, RunSpeed = 346,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 3,
                X = 928.6173f, Y = 47.025f, Z = 886.61816f,
                Hx = 0f, Hy = -0.14124f, Hz = 0f, Hw = 0.98998f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 164965 }, new[] { 2, 0 }, new[] { 3, 21819 }, new[] { 4, 21831 } },
                Meshes = new[] { new[] { 0, 40689, 0, 4 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008049,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 929.43005f, Y = 47.025f, Z = 889.4424f,
                Hx = 0f, Hy = -0.992f, Hz = 0f, Hw = 0.12625f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
            new ShuttleportNpc
            {
                SourceNpcId = 1008050,
                Name = "ICC Shuttle Guard",
                Level = 25, Health = 941, MonsterData = 254118, Scale = 105, VisualFlags = 31, HeadMesh = 40627, RunSpeed = 129,
                NpcFamily = 0, LosHeight = 0, CharacterFlags = 269095425, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 2, Race = 1, Fatness = 1, MovementMode = 1,
                X = 926.04535f, Y = 47.025f, Z = 889.8097f,
                Hx = 0f, Hy = -0.97285f, Hz = 0f, Hw = 0.23142f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 0, 40627, 0, 4 }, new[] { 1, 262556, 0, 2 }, new[] { 3, 286446, 0, 0 } },
                Waypoints = null,
            },
        };

        public static void ClearPlayfield(int playfieldInstance)
        {
            if (playfieldInstance == IccShuttleportPlayfieldId)
            {
                SpawnedPlayfields.Remove(playfieldInstance);
            }
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

            if (playfieldIdentity.Instance != IccShuttleportPlayfieldId)
            {
                return;
            }

            if (!SpawnedPlayfields.Add(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "IccShuttleportSpawn skip duplicate pf=" + playfieldIdentity.Instance);
                return;
            }

            int spawned = 0;
            foreach (ShuttleportNpc def in Npcs)
            {
                if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    spawned++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "IccShuttleportSpawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned
                + "/" + Npcs.Length);
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            ShuttleportNpc def)
        {
            IccShuttleportPlacementRecord sourcePlacement;
            string placementFailure;
            if (!IccShuttleportPlacementCatalog.TryGetRuntimeActive(
                    def.SourceNpcId,
                    out sourcePlacement,
                    out placementFailure))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "IccShuttleportSpawn placement blocked npcId=" + def.SourceNpcId
                    + " npc=" + def.Name + " reason=" + placementFailure);
                return false;
            }

            if (def.Level < sourcePlacement.MinLevel || def.Level > sourcePlacement.MaxLevel)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "IccShuttleportSpawn placement level blocked npcId=" + def.SourceNpcId
                    + " npc=" + def.Name + " level=" + def.Level
                    + " sourceRange=" + sourcePlacement.MinLevel + ".."
                    + sourcePlacement.MaxLevel);
                return false;
            }

            var npcController = new NPCController
            {
                AiProfile = def.CombatContractFactory == null
                                ? NpcAiProfile.Social
                                : NpcAiProfile.Passive
            };
            string templateHash = string.IsNullOrWhiteSpace(def.TemplateHash)
                                      ? TemplateHash
                                      : def.TemplateHash;
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                templateHash,
                playfieldIdentity,
                new Coordinate
                {
                    x = sourcePlacement.PositionX,
                    y = sourcePlacement.PositionY,
                    z = sourcePlacement.PositionZ
                },
                new Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                npcController,
                def.Level);

            if (mob == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "IccShuttleportSpawn FAILED sourceNpcId=" + sourcePlacement.NpcId
                    + " template=" + templateHash + " npc=" + def.Name);
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
            mob.Coordinates(
                new Coordinate
                {
                    x = sourcePlacement.PositionX,
                    y = sourcePlacement.PositionY,
                    z = sourcePlacement.PositionZ
                });

            if (def.CombatContractFactory != null)
            {
                string combatFailure;
                if (!CapturedEnemyCombatRuntime.PrepareAndRequireCombatReady(
                        mob,
                        npcController,
                        def.CombatContractFactory(),
                        out combatFailure))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "IccShuttleportSpawn combat not ready npc=" + def.Name
                        + " reason=" + combatFailure);
                    return false;
                }
            }

            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return true;
        }

        private static void ApplyAppearance(Character mob, ShuttleportNpc def)
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
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                mob.MeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
                mob.SocialMeshLayer.AddMesh(0, def.HeadMesh, 0, 4);
            }
        }

        private static void ApplyWaypoints(Character mob, NPCController controller, ShuttleportNpc def)
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
