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

    #endregion

    /// <summary>
    /// Capture-backed ICC Holodeck (PF 7001 / 0x1B59) ambient NPC population.
    /// Capture 20260719-155043. Excludes Carlo Pinnetti / CEO Guardian (quest escorts).
    /// </summary>
    internal static class HoloDeckSpawn
    {
        private const int HoloDeckPlayfieldId = 7001;

        private static readonly HashSet<int> SpawnedPlayfields = new HashSet<int>();

        private const string TemplateHash = "BART";

        private sealed class HoloNpc
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
            public int CharacterFlags;
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

        private static readonly HoloNpc[] Npcs =
        {
            new HoloNpc
            {
                Name = "Adeline Guerra [Freelancers Inc.]",
                Level = 210, Health = 31900, MonsterData = 26155, Scale = 130, VisualFlags = 31,
                HeadMesh = 40138, RunSpeed = 632, NpcFamily = 1020, CharacterFlags = 277615105,
                X = 213.4695f, Y = 1.02f, Z = 207.9243f,
                Hx = 0.0f, Hy = 0.000318f, Hz = 0.0f, Hw = 1.0f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 247933 }, new[] { 2, 247977 }, new[] { 3, 247887 }, new[] { 4, 248016 } },
                Meshes = new[] { new[] { 0, 204941, 0, 0 }, new[] { 0, 40138, 0, 4 }, new[] { 1, 29084, 0, 2 } },
            },
            new HoloNpc
            {
                Name = "Arbiter Vincenzo Palmiero",
                Level = 220, Health = 203721, MonsterData = 26092, Scale = 100, VisualFlags = 31,
                HeadMesh = 40694, RunSpeed = 749, NpcFamily = 3, CharacterFlags = 277615105,
                X = 229.31f, Y = 1.02f, Z = 198.0397f,
                Hx = 0.0f, Hy = 0.002199f, Hz = 0.0f, Hw = 0.999998f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40694, 0, 4 }, new[] { 1, 258459, 0, 2 }, new[] { 3, 286446, 0, 0 } },
            },
            new HoloNpc
            {
                Name = "Arbiter's Guardian",
                Level = 220, Health = 101861, MonsterData = 165196, Scale = 125, VisualFlags = 31,
                HeadMesh = 40117, RunSpeed = 749, NpcFamily = 137, CharacterFlags = 269226497,
                X = 221.8891f, Y = 1.02f, Z = 205.7225f,
                Hx = 0.0f, Hy = -0.926655f, Hz = 0.0f, Hw = 0.375912f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286466, 0, 0 } },
            },
            new HoloNpc
            {
                Name = "Arbiter's Guardian",
                Level = 220, Health = 101861, MonsterData = 165196, Scale = 125, VisualFlags = 31,
                HeadMesh = 40117, RunSpeed = 749, NpcFamily = 137, CharacterFlags = 269226497,
                X = 221.9611f, Y = 1.02f, Z = 188.022f,
                Hx = 0.0f, Hy = -0.393288f, Hz = 0.0f, Hw = 0.919415f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286466, 0, 0 } },
            },
            new HoloNpc
            {
                Name = "Arbiter's Guardian",
                Level = 220, Health = 101861, MonsterData = 165196, Scale = 125, VisualFlags = 31,
                HeadMesh = 40117, RunSpeed = 749, NpcFamily = 137, CharacterFlags = 269226497,
                X = 181.0215f, Y = 1.02f, Z = 193.9571f,
                Hx = 0.0f, Hy = 0.701076f, Hz = 0.0f, Hw = 0.713087f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286466, 0, 0 } },
            },
            new HoloNpc
            {
                Name = "Arbiter's Guardian",
                Level = 220, Health = 101861, MonsterData = 165196, Scale = 125, VisualFlags = 31,
                HeadMesh = 40117, RunSpeed = 749, NpcFamily = 137, CharacterFlags = 269226497,
                X = 181.0337f, Y = 1.02f, Z = 199.8694f,
                Hx = 0.0f, Hy = 0.708745f, Hz = 0.0f, Hw = 0.705465f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 40117, 0, 4 }, new[] { 1, 99154, 0, 2 }, new[] { 3, 286466, 0, 0 } },
            },
            new HoloNpc
            {
                Name = "Arbitration Drone",
                Level = 100, Health = 13658, MonsterData = 260229, Scale = 100, VisualFlags = 31,
                HeadMesh = 0, RunSpeed = 346, NpcFamily = 3, CharacterFlags = 277369345,
                X = 227.2182f, Y = 1.02f, Z = 193.0427f,
                Hx = 0.0f, Hy = -0.682641f, Hz = 0.0f, Hw = 0.730759f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = new int[0][],
            },
            new HoloNpc
            {
                Name = "RALPH",
                Level = 200, Health = 36434, MonsterData = 96056, Scale = 110, VisualFlags = 31,
                HeadMesh = 0, RunSpeed = 515, NpcFamily = 103, CharacterFlags = 277615105,
                X = 191.6574f, Y = 1.02f, Z = 197.0204f,
                Hx = 0.0f, Hy = -0.709236f, Hz = 0.0f, Hw = 0.704971f,
                Textures = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } },
                Meshes = new int[0][],
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

            if (playfieldIdentity.Instance != HoloDeckPlayfieldId)
            {
                return;
            }

            if (!SpawnedPlayfields.Add(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "HoloDeckSpawn skip duplicate pf=" + playfieldIdentity.Instance);
                return;
            }

            int spawned = 0;
            foreach (HoloNpc def in Npcs)
            {
                if (SpawnOne(playfield, playfieldIdentity, activateNpc, def))
                {
                    spawned++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "HoloDeckSpawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned
                + "/" + Npcs.Length);
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            HoloNpc def)
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
                    "HoloDeckSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
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
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)def.CharacterFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.accountflags, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.expansion, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.profession, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualprofession, 0);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.currentmovementmode, 3);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.prevmovementmode, 3);
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
            mob.Coordinates(new Coordinate { x = def.X, y = def.Y, z = def.Z });

            mob.DoNotDoTimers = false;
            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);
            return true;
        }

        private static void ApplyAppearance(Character mob, HoloNpc def)
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
