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

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields.Content;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Aban Redeemed garden population extras — PF 4676.
    /// Capture 20260823-205320: Or-Mada vendors, El-Mada, Forrester Aban Cama.
    /// Lux-Wei remains in NascenceLifeSpawn — do not duplicate here.
    /// </summary>
    internal static class AbanGardenSpawn
    {
        private const string TemplateHash = "BART";

        private sealed class GardenNpc
        {
            public string Name;
            public int Level;
            public int Health;
            public int MonsterData;
            public int Scale;
            public int VisualFlags;
            public int HeadMesh;
            /// <summary>SCFU CharacterFlags; 0 = leave template default.</summary>
            public int CharacterFlags;
            /// <summary>Stat side; -1 = leave template default. Clan=1.</summary>
            public int Side = -1;
            public int Profession;
            public int Breed;
            public int RunSpeed;
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

        private static readonly GardenNpc[] Npcs =
        {
            new GardenNpc
            {
                // Capture 20260823-205320 SCFU SimpleChar:7A2013B7
                Name = "Or-Mada of the Furious Fists",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = 271061505, Side = (int)Side.Clan, Breed = (int)Breed.Monster, RunSpeed = 103,
                X = 401.440765f, Y = 112.386261f, Z = 428.459961f,
                Hx = 0f, Hy = -0.6433794f, Hz = 0f, Hw = -0.765547752f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
            },
            new GardenNpc
            {
                // Capture 20260823-205320 SCFU SimpleChar:7A2013B4 — Protection near Preservation
                Name = "Or-Mada of Protection",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = 271061505, Side = (int)Side.Clan, Breed = (int)Breed.Monster, RunSpeed = 103,
                X = 404.2271f, Y = 112.752884f, Z = 439.988525f,
                Hx = 0f, Hy = 0.999763548f, Hz = 0f, Hw = 0.0217469819f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
            },
            new GardenNpc
            {
                // Capture 20260823-205320 SCFU SimpleChar:7A2013B5
                Name = "Or-Mada of Preservation",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = 271061505, Side = (int)Side.Clan, Breed = (int)Breed.Monster, RunSpeed = 103,
                X = 411.703918f, Y = 110.80838f, Z = 438.303162f,
                Hx = 0f, Hy = -0.9111272f, Hz = 0f, Hw = 0.41212526f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
            },
            new GardenNpc
            {
                // Capture 20260823-205320 SCFU SimpleChar:7A2013B8
                Name = "Or-Mada of Flaming Barrels",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = 271061505, Side = (int)Side.Clan, Breed = (int)Breed.Monster, RunSpeed = 103,
                X = 414.512177f, Y = 110.409264f, Z = 438.450684f,
                Hx = 0f, Hy = -0.838834047f, Hz = 0f, Hw = 0.5443871f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
            },
            new GardenNpc
            {
                // Capture 20260823-205320 SCFU SimpleChar:7A2013B6 — Protection near Gear & Ammo
                Name = "Or-Mada of Protection",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = 271061505, Side = (int)Side.Clan, Breed = (int)Breed.Monster, RunSpeed = 103,
                X = 421.6585f, Y = 108.365479f, Z = 429.6127f,
                Hx = 0f, Hy = -0.519817f, Hz = 0f, Hw = 0.854277849f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
            },
            new GardenNpc
            {
                // Capture 20260823-205320 SCFU SimpleChar:7A2013B9
                Name = "Or-Mada of Gear & Ammo",
                Level = 30, Health = 32800, MonsterData = 236640, Scale = 100, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = 271061505, Side = (int)Side.Clan, Breed = (int)Breed.Monster, RunSpeed = 103,
                X = 422.326477f, Y = 107.48468f, Z = 425.36972f,
                Hx = 0f, Hy = -0.327861339f, Hz = 0f, Hw = 0.9447259f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
            },
            new GardenNpc
            {
                // Capture 20260823-205320 CHAR-SEEN SimpleChar:7A2013BA — no SCFU heading/mesh/scale.
                Name = "El-Mada, Official of Consistency",
                Level = 40, Health = 46400, MonsterData = 214083, Scale = 0, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = 0, Side = (int)Side.Clan, Breed = (int)Breed.Solitus, Profession = -1,
                X = 422.274628f, Y = 114.262131f, Z = 444.5668f,
                Hx = 0f, Hy = 0f, Hz = 0f, Hw = 1f,
                Textures = null,
                Meshes = null,
            },
            new GardenNpc
            {
                // Capture 20260823-205320 SCFU Forrester Aban Cama
                Name = "Forrester Aban Cama",
                Level = 40, Health = 2320, MonsterData = 214078, Scale = 140, VisualFlags = 31, HeadMesh = 0,
                CharacterFlags = 268964353, Side = (int)Side.Clan,
                X = 369.3538f, Y = 118.611183f, Z = 413.6092f,
                Hx = 0f, Hy = 0.469584f, Hz = 0f, Hw = 0.88288784f,
                Textures = null,
                Meshes = new[] { new[] { 1, 234636, 0, 2 } },
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

            if (playfieldIdentity.Instance != NascenceLifeContentModule.GardenOfAbanPlayfieldId)
            {
                return;
            }

            int spawned = 0;
            for (int i = 0; i < Npcs.Length; i++)
            {
                if (SpawnOne(playfield, playfieldIdentity, activateNpc, Npcs[i]))
                {
                    spawned++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AbanGardenSpawn pf=" + playfieldIdentity.Instance
                + " spawned=" + spawned + "/" + Npcs.Length);
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            GardenNpc def)
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
                    "AbanGardenSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
                return false;
            }

            mob.Name = def.Name;
            mob.Playfield = playfield;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, (uint)def.VisualFlags);
            if (def.CharacterFlags != 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)def.CharacterFlags);
            }

            if (def.Side >= 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)def.Side);
                mob.Stats[StatIds.side].Value = def.Side;
            }

            if (def.Profession > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.profession, (uint)def.Profession);
            }

            if (def.Breed > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.breed, (uint)def.Breed);
            }

            if (def.RunSpeed > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, (uint)def.RunSpeed);
            }

            if (def.Scale > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            }

            if (def.HeadMesh > 0)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, (uint)def.HeadMesh);
            }

            if (def.Textures != null && def.Textures.Length > 0)
            {
                mob.Textures.Clear();
                foreach (int[] t in def.Textures)
                {
                    if (t == null || t.Length < 2 || t[1] <= 0)
                    {
                        continue;
                    }

                    mob.Textures.Add(new AOTextures(t[0], t[1]));
                }
            }

            if (def.Meshes != null && def.Meshes.Length > 0)
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                foreach (int[] m in def.Meshes)
                {
                    if (m == null || m.Length < 4 || m[1] <= 0)
                    {
                        continue;
                    }

                    mob.MeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                    mob.SocialMeshLayer.AddMesh(m[0], m[1], m[2], m[3]);
                }
            }

            string combatFailure;
            CapturedEnemyCombatRuntime.Prepare(
                mob,
                npcController,
                CapturedEnemyCombatContract.Unresolved(
                    "20260823-205320 Aban garden captured actor has no source-local WIFU/attack-start/AttackInfo contract mapped; npc="
                    + def.Name + " monsterData=" + def.MonsterData + " level=" + def.Level,
                    true),
                out combatFailure);

            activateNpc(mob);
            return true;
        }
    }
}
