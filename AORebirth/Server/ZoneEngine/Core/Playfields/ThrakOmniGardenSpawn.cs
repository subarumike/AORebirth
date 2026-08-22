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

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture-backed Thrak Omni (Unredeemed) garden population — PF 4677.
    /// Capture 20260718-165625 (Nascence Thrak Omni Garden): 10 NPCs (Executron removed — player-like VisualFlags/MonsterData).
    /// </summary>
    internal static class ThrakOmniGardenSpawn
    {
        internal const int ThrakOmniGardenPlayfieldId = 4677;

        private const string TemplateHash = "BART";

        // Capture 20260821-225658 Hyp SCFU ActiveNanos (NCU window names from nano DB):
        // Edge of Steel 202776, Thicken Skin 117681, Thug's Delight 43371, Arctic Cloak 55743,
        // plus monster wait nano 239872.
        private static readonly int[] HypnagogicBuffNanoIds =
        {
            202776, 117681, 43371, 55743, 239872
        };

        private const int HypnagogicBuffDurationCentiseconds = 1440000;
        private const int HypnagogicWaitNanoDurationCentiseconds = 7050327;

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
            /// <summary>Stat side; -1 = leave template default. Omni=2.</summary>
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
                Name = "Craig-Or of Flaming Barrels",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 490.974152f, Y = 33.01f, Z = 311.799866f,
                Hx = 0f, Hy = -0.836958647f, Hz = 0f, Hw = 0.5472661f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } }, // capture 20260821-214904
            },
            new GardenNpc
            {
                Name = "Craig-Or of Gear & Ammo",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 491.040436f, Y = 33.01f, Z = 305.692566f,
                Hx = 0f, Hy = -0.557796538f, Hz = 0f, Hw = 0.829977751f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } }, // capture 20260821-214904
            },
            new GardenNpc
            {
                Name = "Craig-Or of Preservation",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 490.879669f, Y = 33.06743f, Z = 317.171753f,
                Hx = 0f, Hy = -0.673385739f, Hz = 0f, Hw = 0.7392913f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } },
            },
            new GardenNpc
            {
                Name = "Craig-Or of Protection",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 491.143738f, Y = 33.01f, Z = 299.771362f,
                Hx = 0f, Hy = -0.8378405f, Hz = 0f, Hw = 0.5459151f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } }, // capture 20260821-214904
            },
            new GardenNpc
            {
                Name = "Craig-Or of the Furious Fists",
                Level = 30, Health = 32800, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 491.397156f, Y = 33.0415535f, Z = 323.112885f,
                Hx = 0f, Hy = 0.655906737f, Hz = 0f, Hw = 0.7548419f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } }, // capture 20260821-214904
            },
            new GardenNpc
            {
                Name = "Garboil Ixi Thrak",
                Level = 40, Health = 2320, MonsterData = 208635, Scale = 150, VisualFlags = 31, HeadMesh = 0,
                X = 445.24f, Y = 33.01f, Z = 522.828735f,
                Hx = 0f, Hy = 0.9999998f, Hz = 0f, Hw = -0.0005906065f,
                Textures = null,
                Meshes = new[] { new[] { 1, 233207, 0, 2 } },
            },
            new GardenNpc
            {
                Name = "Hypnagogic Urga-Lum Thrak",
                Level = 40, Health = 3078, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                // Capture 20260821-225658: CharacterFlags=277352961 (HasBlueName), Side=Omni, SCFU IsPet.
                // Capture 20260821-233119 inspect/dossier: HP 3078, profession 9, breed 6, runSpeed 139.
                CharacterFlags = 277352961, Side = 2, Profession = 9, Breed = 6, RunSpeed = 139,
                X = 464.0864f, Y = 33.37679f, Z = 359.307526f,
                Hx = 0f, Hy = 0.9436898f, Hz = 0f, Hw = 0.330831528f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } }, // capture 20260821-214904
            },
            new GardenNpc
            {
                Name = "Operator Pi-Ixi Thrak",
                Level = 40, Health = 2320, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 320.015869f, Y = 25.01f, Z = 342.412933f,
                Hx = 0f, Hy = 0.708159864f, Hz = 0f, Hw = 0.7060521f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } },
            },
            new GardenNpc
            {
                Name = "Son-Len, Official of Power",
                Level = 40, Health = 46400, MonsterData = 208646, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 447.16275f, Y = 33.01f, Z = 318.59845f,
                Hx = 0f, Hy = -0.345530778f, Hz = 0f, Hw = 0.9384074f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209532, 0, 2 } }, // capture 20260821-214904
            },
            new GardenNpc
            {
                Name = "Visionist Eckel-Lum Thrak",
                Level = 40, Health = 2320, MonsterData = 208640, Scale = 200, VisualFlags = 31, HeadMesh = 0,
                X = 418.79776f, Y = 33.5518875f, Z = 359.832642f,
                Hx = 0f, Hy = 0.359957576f, Hz = 0f, Hw = 0.9329687f,
                Textures = null,
                Meshes = new[] { new[] { 1, 209541, 0, 2 } }, // capture 20260821-214904
            }
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

            if (playfieldIdentity.Instance != ThrakOmniGardenPlayfieldId)
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
                "ThrakOmniGardenSpawn pf=" + playfieldIdentity.Instance
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
                    "ThrakOmniGardenSpawn FAILED template=" + TemplateHash + " npc=" + def.Name);
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
                    "20260718-165625 Thrak garden captured actor has no source-local WIFU/attack-start/AttackInfo contract mapped; npc="
                    + def.Name + " monsterData=" + def.MonsterData + " level=" + def.Level,
                    true),
                out combatFailure);

            if (NeedsCapturedScfuIsPet(def.Name))
            {
                ApplyHypnagogicCapturedBuffs(mob);
            }

            activateNpc(mob);
            return true;
        }

        /// <summary>
        /// Capture 20260821-225658 Hyp SCFU includes IsPet (blue-name friendly NPC wire).
        /// </summary>
        internal static bool NeedsCapturedScfuIsPet(string name)
        {
            return string.Equals(name, "Hypnagogic Urga-Lum Thrak", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyHypnagogicCapturedBuffs(ICharacter mob)
        {
            if (mob == null)
            {
                return;
            }

            for (int i = 0; i < HypnagogicBuffNanoIds.Length; i++)
            {
                int nanoId = HypnagogicBuffNanoIds[i];
                int duration = nanoId == 239872
                                   ? HypnagogicWaitNanoDurationCentiseconds
                                   : HypnagogicBuffDurationCentiseconds;
                ActiveNanoRuntimeService.Default.ApplyActiveNano(
                    mob,
                    nanoId,
                    duration,
                    mob.Identity,
                    0,
                    true);
            }
        }
    }
}
