namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Rome Blue / Omni city population (PF 735 / 0x02DF).
    /// Capture 20260717-210219: 22 city NPCs with captured appearance (textures/meshes).
    /// </summary>
    internal static class RomeBlueCitySpawn
    {
        private const int RomeBluePlayfieldId = 735;

        // Base humanoid template used only to instantiate the Character; all appearance
        // (monsterData, meshes, textures, head, scale) is overridden from the capture.
        private const string TemplateHash = "BART";

        private sealed class CityNpc
        {
            public string Name;
            public int Level;
            public int Health;
            public int MonsterData;
            public int Scale;
            public int VisualFlags;
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
        }

        private static readonly CityNpc[] Npcs =
        {
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 156, Health = 14263, MonsterData = 26088, Scale = 117, VisualFlags = 31, HeadMesh = 40687,
                X = 536.45904f, Y = 17.41f, Z = 446.56003f,
                Hx = 0.0f, Hy = -0.46784f, Hz = 0.0f, Hw = 0.88381f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 161, Health = 15009, MonsterData = 26088, Scale = 118, VisualFlags = 31, HeadMesh = 40687,
                X = 536.373f, Y = 17.41f, Z = 438.6647f,
                Hx = 0.0f, Hy = -0.95597f, Hz = 0.0f, Hw = 0.29346f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Master OT Robotbuilder",
                Level = 196, Health = 23349, MonsterData = 26092, Scale = 121, VisualFlags = 31, HeadMesh = 40694,
                X = 574.5374f, Y = 17.41f, Z = 375.21033f,
                Hx = 0.0f, Hy = 0.96891f, Hz = 0.0f, Hw = 0.24743f,
                Textures = new[] { new[] { 0, 22605 }, new[] { 1, 8731 }, new[] { 2, 9457 }, new[] { 3, 22541 }, new[] { 4, 9456 } },
                Meshes = new[] { new[] { 0, 20098, 0, 2 }, new[] { 0, 40694, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 150, Health = 13369, MonsterData = 26088, Scale = 117, VisualFlags = 31, HeadMesh = 40687,
                X = 639.61176f, Y = 17.41f, Z = 397.18426f,
                Hx = 0.0f, Hy = 0.94245f, Hz = 0.0f, Hw = 0.33434f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Mr. Blake",
                Level = 200, Health = 36434, MonsterData = 245897, Scale = 121, VisualFlags = 31, HeadMesh = 40694,
                X = 645.99994f, Y = 21.415f, Z = 425.99957f,
                Hx = 0.0f, Hy = 0.99953f, Hz = 0.0f, Hw = 0.03056f,
                Textures = new[] { new[] { 0, 40976 }, new[] { 1, 14038 }, new[] { 2, 40903 }, new[] { 3, 14036 }, new[] { 4, 14034 } },
                Meshes = new[] { new[] { 0, 20110, 0, 0 }, new[] { 0, 40694, 0, 4 } },
            },
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 157, Health = 14413, MonsterData = 26088, Scale = 118, VisualFlags = 31, HeadMesh = 40687,
                X = 709.5055f, Y = 17.41f, Z = 384.98563f,
                Hx = 0.0f, Hy = 1.0f, Hz = 0.0f, Hw = 0.00079f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Seasoned OT Mercenary",
                Level = 135, Health = 11133, MonsterData = 26103, Scale = 116, VisualFlags = 31, HeadMesh = 40103,
                X = 709.1299f, Y = 17.41f, Z = 381.69974f,
                Hx = 0.0f, Hy = -0.58586f, Hz = 0.0f, Hw = 0.81041f,
                Textures = new[] { new[] { 0, 9620 }, new[] { 1, 8729 }, new[] { 2, 9424 }, new[] { 3, 9423 }, new[] { 4, 9625 } },
                Meshes = new[] { new[] { 0, 19994, 0, 2 }, new[] { 0, 40103, 0, 4 }, new[] { 1, 15839, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 162, Health = 15158, MonsterData = 26088, Scale = 118, VisualFlags = 31, HeadMesh = 40687,
                X = 661.55994f, Y = 17.41f, Z = 346.5112f,
                Hx = 0.0f, Hy = 0.39127f, Hz = 0.0f, Hw = 0.92028f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Omni-AF Urban Trooper",
                Level = 250, Health = 200000, MonsterData = 26151, Scale = 130, VisualFlags = 31, HeadMesh = 40171,
                X = 723.45966f, Y = 17.41f, Z = 373.3245f,
                Hx = 0.0f, Hy = -0.99402f, Hz = 0.0f, Hw = 0.10919f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new CityNpc
            {
                Name = "Seasoned OT Techhunter",
                Level = 112, Health = 8253, MonsterData = 26147, Scale = 113, VisualFlags = 31, HeadMesh = 40172,
                X = 727.7471f, Y = 17.41f, Z = 349.29074f,
                Hx = 0.0f, Hy = 0.94405f, Hz = 0.0f, Hw = 0.32981f,
                Textures = new[] { new[] { 0, 22605 }, new[] { 1, 8731 }, new[] { 2, 22592 }, new[] { 3, 22541 }, new[] { 4, 9456 } },
                Meshes = new[] { new[] { 0, 20030, 0, 2 }, new[] { 0, 40172, 0, 4 }, new[] { 1, 7777, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Male Captain",
                Level = 165, Health = 15605, MonsterData = 26151, Scale = 118, VisualFlags = 31, HeadMesh = 40171,
                X = 727.3877f, Y = 21.415f, Z = 311.26211f,
                Hx = 0.0f, Hy = 0.70037f, Hz = 0.0f, Hw = 0.71378f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Omni-AF Urban Trooper",
                Level = 250, Health = 200000, MonsterData = 26151, Scale = 130, VisualFlags = 31, HeadMesh = 40171,
                X = 727.77496f, Y = 21.415f, Z = 317.17163f,
                Hx = 0.0f, Hy = 0.70041f, Hz = 0.0f, Hw = 0.71374f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new CityNpc
            {
                Name = "Omni-AF Urban Trooper",
                Level = 250, Health = 200000, MonsterData = 26151, Scale = 130, VisualFlags = 31, HeadMesh = 40171,
                X = 742.5093f, Y = 17.41f, Z = 358.15842f,
                Hx = 0.0f, Hy = -0.73748f, Hz = 0.0f, Hw = 0.67536f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new CityNpc
            {
                Name = "Omni-AF Urban Trooper",
                Level = 250, Health = 200000, MonsterData = 26151, Scale = 130, VisualFlags = 31, HeadMesh = 40171,
                X = 742.28754f, Y = 17.41f, Z = 260.8515f,
                Hx = 0.0f, Hy = -0.73592f, Hz = 0.0f, Hw = 0.67707f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new CityNpc
            {
                Name = "Omni-AF Urban Trooper",
                Level = 250, Health = 200000, MonsterData = 26151, Scale = 130, VisualFlags = 31, HeadMesh = 40171,
                X = 742.403f, Y = 17.41f, Z = 269.5621f,
                Hx = 0.0f, Hy = -0.73876f, Hz = 0.0f, Hw = 0.67397f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20038, 0, 2 }, new[] { 0, 40171, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 11535, 206969, 0 }, new[] { 4, 11535, 206969, 0 }, new[] { 5, 11543, 206969, 0 } },
            },
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 156, Health = 14263, MonsterData = 26088, Scale = 117, VisualFlags = 31, HeadMesh = 40687,
                X = 713.3038f, Y = 17.41f, Z = 246.4288f,
                Hx = 0.0f, Hy = 0.99999f, Hz = 0.0f, Hw = -0.00382f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Rookie OT Mercenary",
                Level = 47, Health = 1665, MonsterData = 26097, Scale = 105, VisualFlags = 31, HeadMesh = 40111,
                X = 659.0563f, Y = 17.41f, Z = 242.46013f,
                Hx = 0.0f, Hy = 0.94218f, Hz = 0.0f, Hw = 0.3351f,
                Textures = new[] { new[] { 0, 27656 }, new[] { 1, 9404 }, new[] { 2, 9407 }, new[] { 3, 22546 }, new[] { 4, 9401 } },
                Meshes = new[] { new[] { 0, 20004, 40993, 2 }, new[] { 0, 40111, 0, 4 }, new[] { 1, 15839, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Omni-AF Urban Trooper",
                Level = 250, Health = 200000, MonsterData = 26137, Scale = 130, VisualFlags = 31, HeadMesh = 40209,
                X = 642.60187f, Y = 17.41f, Z = 314.65375f,
                Hx = 0.0f, Hy = -0.72214f, Hz = 0.0f, Hw = 0.69174f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 204160 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20055, 0, 2 }, new[] { 0, 40209, 0, 4 }, new[] { 1, 209529, 0, 2 }, new[] { 3, 20715, 206969, 0 }, new[] { 4, 20715, 206969, 0 }, new[] { 5, 20714, 206969, 0 } },
            },
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 151, Health = 13518, MonsterData = 26088, Scale = 117, VisualFlags = 31, HeadMesh = 40687,
                X = 615.8673f, Y = 17.41f, Z = 280.82257f,
                Hx = 0.0f, Hy = 0.31487f, Hz = 0.0f, Hw = 0.94913f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 154, Health = 13965, MonsterData = 26088, Scale = 117, VisualFlags = 31, HeadMesh = 40687,
                X = 635.2291f, Y = 17.41f, Z = 230.10336f,
                Hx = 0.0f, Hy = 0.72727f, Hz = 0.0f, Hw = 0.68636f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Male Lieutenant",
                Level = 150, Health = 13369, MonsterData = 26088, Scale = 117, VisualFlags = 31, HeadMesh = 40687,
                X = 551.75903f, Y = 17.41f, Z = 313.58978f,
                Hx = 0.0f, Hy = 1.0f, Hz = 0.0f, Hw = 0.00299f,
                Textures = new[] { new[] { 0, 15806 }, new[] { 1, 15809 }, new[] { 2, 15807 }, new[] { 3, 15808 }, new[] { 4, 15805 } },
                Meshes = new[] { new[] { 0, 20106, 0, 2 }, new[] { 0, 40687, 0, 4 }, new[] { 1, 35566, 0, 2 } },
            },
            new CityNpc
            {
                Name = "Veteran OT Marksman",
                Level = 179, Health = 17692, MonsterData = 26147, Scale = 119, VisualFlags = 31, HeadMesh = 40172,
                X = 583.15234f, Y = 17.41f, Z = 337.49597f,
                Hx = 0.0f, Hy = 0.54306f, Hz = 0.0f, Hw = 0.8397f,
                Textures = new[] { new[] { 0, 9418 }, new[] { 1, 8736 }, new[] { 2, 9420 }, new[] { 3, 9605 }, new[] { 4, 9425 } },
                Meshes = new[] { new[] { 0, 20037, 0, 2 }, new[] { 0, 40172, 0, 4 }, new[] { 1, 15839, 0, 2 } },
            },
        };

        public static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null || activateNpc == null)
            {
                return;
            }

            if (playfieldIdentity.Instance != RomeBluePlayfieldId)
            {
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
                "RomeBlueCitySpawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned
                + "/" + Npcs.Length);
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            CityNpc def)
        {
            var npcController = new NPCController();
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
                    "RomeBlueCitySpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
                return false;
            }

            mob.Name = def.Name;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);
            if (def.Scale > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            }

            if (def.HeadMesh > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)def.HeadMesh);
            }

            ApplyAppearance(mob, def);
            mob.Coordinates(new Coordinate { x = def.X, y = def.Y, z = def.Z });

            mob.DoNotDoTimers = false;
            activateNpc(mob);
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
        }
    }
}
