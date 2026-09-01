namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;
    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    /// <summary>
    /// Capture 20260830-143801 dungeon mob population (A Door / C00110D7).
    /// Combat via capture SAW (aggro/attack/anim). HeadMesh cleared so
    /// monster bodies do not keep the BART human head.
    /// </summary>
    internal static partial class NascenceDungeon4Spawn
    {
        private const string TemplateHash = "BART";

        private const int DefaultMonsterCharacterFlags = 268964353;

        private const float SlotAliveProximityMetersSq = 6.25f; // 2.5m

        private static readonly object RespawnGate = new object();

        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();

        private static readonly Dictionary<int, Identity[]> LivingIdentityByPlayfield =
            new Dictionary<int, Identity[]>();

        // Capture Havaris self illumination body:210954 (reuse D2 wire).
        private static readonly byte[] HavarisExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x73, 0x65, 0x6C, 0x66,
                0x20, 0x69, 0x6C, 0x6C, 0x75, 0x6D, 0x69, 0x6E,
                0x61, 0x74, 0x69, 0x6F, 0x6E, 0x20, 0x62, 0x6F,
                0x64, 0x79, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x03, 0x38, 0x0A, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x5B, 0x42, 0x32, 0x5D, 0x20, 0x6F, 0x70, 0x61,
                0x63, 0x69, 0x74, 0x79, 0x20, 0x6D, 0x61, 0x70,
                0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x03, 0x38, 0x0A, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07,
                0xE2, 0x00, 0x00, 0xCF, 0x1B, 0x00, 0x03, 0xAA,
                0x28, 0x00, 0x00, 0x00, 0x00, 0x00, 0x6B, 0x94,
                0x57, 0x00, 0x68, 0xA3, 0xB4, 0x00
            };

        // Capture 20260830-143801 Smelly Weaver Material #13:235226 (same as D2 weaver).
        private static readonly byte[] SmellyWeaverExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65,
                0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x31, 0x33,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xDA,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
                0x00, 0x00, 0x0B, 0xD3, 0x00, 0x00, 0xCF, 0x1B,
                0x00, 0x03, 0x84, 0x85, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x6B, 0x94, 0x57, 0x00, 0x6B, 0x85, 0xC7,
                0x00, 0x00, 0xCF, 0x1B, 0x00, 0x03, 0x92, 0x66,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x19, 0xFE, 0x5B,
                0x00, 0x19, 0xEF, 0xCB
            };

        // Capture 20260830-143801 Icy Shadow Material #2b:208959.
        private static readonly byte[] IcyShadowExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65,
                0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x32, 0x62,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x30, 0x3F,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture 20260830-143801 Burning Shadow Material #2b:208957.
        private static readonly byte[] BurningShadowExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65,
                0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x32, 0x62,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x30, 0x3D,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
            };

        // Capture 20260830-143801 Guard Turret Material #13:239777 + 1 - Default:239776.
        private static readonly byte[] GuardTurretExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x0B, 0xD3,
                0x4D, 0x61, 0x74, 0x65, 0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x31, 0x33,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0xA8, 0xA1, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x31, 0x20, 0x2D, 0x20, 0x44, 0x65, 0x66, 0x61, 0x75, 0x6C, 0x74, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0xA8, 0xA0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            if (string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])HavarisExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])SmellyWeaverExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Icy Shadow", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])IcyShadowExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Burning Shadow", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])BurningShadowExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Guard Turret", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])GuardTurretExtendedTextureOverrideData.Clone();
                return true;
            }

            data = null;
            return false;
        }

        /// <summary>
        /// Capture 20260830-143801 SCFU: Monster-side trash/boss include IsPet;
        /// Smelly Weaver does not.
        /// </summary>
        internal static bool NeedsCapturedScfuIsPet(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return NascenceDungeon4Rules.IsDungeonCorpseName(name);
        }

        internal static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || !NascenceDungeon4Rules.IsDungeonPlayfield(playfieldIdentity.Instance))
            {
                return;
            }

            int spawned = 0;
            int skipped = 0;
            Identity[] living;
            DateTime[] timers;
            EnsureRespawnState(playfieldIdentity.Instance, out timers, out living);
            for (int i = 0; i < CapturedSpawns.Length; i++)
            {
                try
                {
                    // Idempotent: RegisterCapturedNpcSpawns can run more than once for the
                    // same lease (content modules / rematerialize). Never double-create slots.
                    if (IsSlotLiving(playfield, i, living))
                    {
                        timers[i] = DateTime.MaxValue;
                        skipped++;
                        continue;
                    }

                    Identity spawnedIdentity;
                    if (SpawnOne(
                        playfield,
                        playfieldIdentity,
                        activateNpc,
                        CapturedSpawns[i],
                        out spawnedIdentity))
                    {
                        living[i] = spawnedIdentity;
                        timers[i] = DateTime.MaxValue;
                        spawned++;
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "NascenceDungeon4Spawn failed mob=" + CapturedSpawns[i].Name
                        + " ex=" + ex.GetType().Name + ": " + ex.Message);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon4Spawn pf=" + playfieldIdentity.Instance
                + " spawned=" + spawned
                + " skippedLiving=" + skipped);
        }

        /// <summary>
        /// Capture 20260830-143801: opening certain Treasures spawns a trap mob nearby.
        ///   0x0BAE7770 -> Hued Sewer Scuttler @ (830.46, 52.01, 239.91)
        ///   0x0BAE7778 / 0x0BAE7774 -> Smelly Weaver (trap-only; weaver SCFU near those opens)
        /// </summary>
        internal static bool TrySpawnChestTrap(Playfield playfield, int chestInstance)
        {
            if (playfield == null
                || !NascenceDungeon4Rules.IsDungeonPlayfield(playfield.Identity.Instance))
            {
                return false;
            }

            NascenceDungeon4MobSpawn def;
            if (!TryGetChestTrapDefinition(chestInstance, out def))
            {
                return false;
            }

            Identity spawnedIdentity;
            bool ok = SpawnOne(
                playfield,
                playfield.Identity,
                playfield.ActivateNpc,
                def,
                out spawnedIdentity);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "NascenceDungeon4Spawn chestTrap chest={0:X8} mob={1} ok={2} id={3}",
                    chestInstance,
                    def.Name,
                    ok ? 1 : 0,
                    spawnedIdentity.Instance));
            return ok;
        }

        private static bool TryGetChestTrapDefinition(int chestInstance, out NascenceDungeon4MobSpawn def)
        {
            // Capture evidence: tools-temp/_d4_gen_notes.txt TRAP-LINK rows.
            switch (chestInstance)
            {
                case unchecked((int)0x0BAE7770):
                    def = new NascenceDungeon4MobSpawn
                    {
                        Name = "Hued Sewer Scuttler",
                        Level = 55,
                        Health = 2765,
                        MonsterData = 22794,
                        Scale = 40,
                        RunSpeed = 190,
                        NpcFamily = 37,
                        Appearance = 1483,
                        X = 830.4575f,
                        Y = 52.01003f,
                        Z = 239.9142f,
                        Hx = 0.0f,
                        Hy = 0.0f,
                        Hz = 0.0f,
                        Hw = 1.0f
                    };
                    return true;

                case unchecked((int)0x0BAE7778):
                    def = new NascenceDungeon4MobSpawn
                    {
                        Name = "Smelly Weaver",
                        Level = 55,
                        Health = 2765,
                        MonsterData = 209347,
                        Scale = 40,
                        RunSpeed = 190,
                        NpcFamily = 183,
                        Appearance = 1227,
                        X = 896.1024f,
                        Y = 52.01f,
                        Z = 241.0232f,
                        Hx = 0.0f,
                        Hy = 0.0f,
                        Hz = 0.0f,
                        Hw = 1.0f
                    };
                    return true;

                case unchecked((int)0x0BAE7774):
                    def = new NascenceDungeon4MobSpawn
                    {
                        Name = "Smelly Weaver",
                        Level = 55,
                        Health = 2765,
                        MonsterData = 209347,
                        Scale = 40,
                        RunSpeed = 190,
                        NpcFamily = 183,
                        Appearance = 1227,
                        X = 905.7332f,
                        Y = 52.01f,
                        Z = 234.1705f,
                        Hx = 0.0f,
                        Hy = 0.0f,
                        Hz = 0.0f,
                        Hw = 1.0f
                    };
                    return true;

                default:
                    def = default(NascenceDungeon4MobSpawn);
                    return false;
            }
        }

        /// <summary>
        /// Mike: trash/treasure 5m; Havaris 20m if dungeon empty, 2h if occupied (at schedule time).
        /// </summary>
        internal static void TickRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || !NascenceDungeon4Rules.IsDungeonPlayfield(playfieldIdentity.Instance))
            {
                return;
            }

            Identity[] living;
            DateTime[] timers;
            EnsureRespawnState(playfieldIdentity.Instance, out timers, out living);
            bool dungeonOccupied = HasPlayerInPlayfield(playfield);
            DateTime utcNow = DateTime.UtcNow;

            for (int i = 0; i < CapturedSpawns.Length; i++)
            {
                if (IsSlotLiving(playfield, i, living))
                {
                    timers[i] = DateTime.MaxValue;
                    continue;
                }

                living[i] = Identity.None;
                if (timers[i] == DateTime.MaxValue)
                {
                    timers[i] = utcNow + RespawnDelayFor(CapturedSpawns[i].Name, dungeonOccupied);
                    continue;
                }

                if (timers[i] > utcNow)
                {
                    continue;
                }

                Identity spawnedIdentity;
                try
                {
                    if (!SpawnOne(
                        playfield,
                        playfieldIdentity,
                        activateNpc,
                        CapturedSpawns[i],
                        out spawnedIdentity))
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "NascenceDungeon4Spawn respawn failed mob=" + CapturedSpawns[i].Name
                        + " ex=" + ex.GetType().Name + ": " + ex.Message);
                    continue;
                }

                living[i] = spawnedIdentity;
                timers[i] = DateTime.MaxValue;
            }
        }

        private static void EnsureRespawnState(
            int playfieldInstance,
            out DateTime[] timers,
            out Identity[] living)
        {
            lock (RespawnGate)
            {
                if (!NextRespawnUtcByPlayfield.TryGetValue(playfieldInstance, out timers)
                    || timers == null
                    || timers.Length != CapturedSpawns.Length)
                {
                    timers = new DateTime[CapturedSpawns.Length];
                    for (int i = 0; i < timers.Length; i++)
                    {
                        timers[i] = DateTime.MaxValue;
                    }

                    NextRespawnUtcByPlayfield[playfieldInstance] = timers;
                }

                if (!LivingIdentityByPlayfield.TryGetValue(playfieldInstance, out living)
                    || living == null
                    || living.Length != CapturedSpawns.Length)
                {
                    living = new Identity[CapturedSpawns.Length];
                    LivingIdentityByPlayfield[playfieldInstance] = living;
                }
            }
        }

        private static TimeSpan RespawnDelayFor(string name, bool dungeonOccupied)
        {
            if (string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                return dungeonOccupied
                    ? NascenceDungeon4Rules.HavarisRespawnWhenOccupied
                    : NascenceDungeon4Rules.HavarisRespawnWhenEmpty;
            }

            return NascenceDungeon4Rules.MobRespawnDelay;
        }

        private static bool HasPlayerInPlayfield(Playfield playfield)
        {
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate != null && candidate.Controller is PlayerController)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSlotLiving(Playfield playfield, int slotIndex, Identity[] living)
        {
            Identity tracked = living[slotIndex];
            if (tracked != Identity.None)
            {
                foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
                {
                    if (candidate != null
                        && candidate.Identity == tracked
                        && !(candidate.Controller is PlayerController)
                        && candidate.Stats[StatIds.health].Value > 0)
                    {
                        return true;
                    }
                }
            }

            NascenceDungeon4MobSpawn def = CapturedSpawns[slotIndex];
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || candidate.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                float dx = candidate.Coordinates().x - def.X;
                float dz = candidate.Coordinates().z - def.Z;
                if ((dx * dx) + (dz * dz) <= SlotAliveProximityMetersSq)
                {
                    living[slotIndex] = candidate.Identity;
                    return true;
                }
            }

            return false;
        }

        private static bool SpawnOne(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            NascenceDungeon4MobSpawn def,
            out Identity spawnedIdentity)
        {
            spawnedIdentity = Identity.None;
            var npcController = new NPCController();
            Character mob = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                TemplateHash,
                playfieldIdentity,
                new Coordinate { x = def.X, y = def.Y, z = def.Z },
                new AORebirth.Core.Vector.Quaternion(def.Hx, def.Hy, def.Hz, def.Hw),
                npcController,
                def.Level);

            if (mob == null)
            {
                return false;
            }

            mob.Name = def.Name;
            mob.FirstName = string.Empty;
            mob.LastName = string.Empty;
            mob.Playfield = playfield;

            // BART template carries human MeshLayer/Textures/HeadMesh — clear before monsterdata
            // or Coral Rafter shows a clipped human head under the monster jaw.
            mob.MeshLayer.Clear();
            mob.SocialMeshLayer.Clear();
            mob.Textures.Clear();
            for (int t = 0; t < 5; t++)
            {
                mob.Textures.Add(new AOTextures(t, 0));
            }

            // Live D3 SCFU: Breed=Monster Sex=None Side=Monster for trash/boss.
            int breed = (int)Breed.Monster;
            int sex = (int)Gender.None;
            int appearance = def.Appearance > 0 ? def.Appearance : 1227;

            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterdata, (uint)def.MonsterData);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healinterval, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healdelta, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.level, (uint)def.Level);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, 31U);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)DefaultMonsterCharacterFlags);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.monsterscale, (uint)def.Scale);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)def.NpcFamily);
            int runSpeed = def.RunSpeed;
            bool isGuardTurret = string.Equals(def.Name, "Guard Turret", StringComparison.OrdinalIgnoreCase);
            if (isGuardTurret)
            {
                runSpeed = 0;
            }

            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, (uint)runSpeed);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Monster);
            mob.Stats[StatIds.side].Value = (int)Side.Monster;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.breed, (uint)breed);
            mob.Stats[StatIds.breed].Value = breed;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.sex, (uint)sex);
            mob.Stats[StatIds.sex].Value = sex;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.race, 1u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.fatness, 1u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.headmesh, 0u);
            mob.Stats[StatIds.headmesh].BaseValue = 0;
            mob.Stats[StatIds.headmesh].Value = 0;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.catmesh, 0u);
            mob.Stats[StatIds.catmesh].BaseValue = 0;
            mob.Stats[StatIds.catmesh].Value = 0;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.aggressiveness, 100U);
            // Mission trash death anim (Parameter2=501) so corpse path matches aggressive NPCs.
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.corpseanimkey, 501u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.dieanim, 501u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.itemanim, 501u);

            mob.DoNotDoTimers = false;
            mob.SetFightingTarget(Identity.None);

            bool combatReady = MissionInstanceMobCombat.TryPrepareCombat(mob, npcController, def.Level);
            // Capture 20260830-143801: Mortiig SAW QTCC/BHMI/... not mission SIW1; apply to full D4 roster
            // (Coral Rafter/Kolaana/etc) so non-Mortiig get fight anim + damage, not SIW1-only.
            if (NascenceDungeon4Rules.IsDungeonCorpseName(def.Name) && !isGuardTurret)
            {
                combatReady = NascenceDungeon4MobCombat.TryPrepareMortiigCombat(
                    mob,
                    npcController,
                    def.Level);
            }

            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healinterval, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healdelta, 0u);
            playfield.SuspendNpcRegen(mob);

            if (!isGuardTurret)
            {
                // Room-gated aggro via NascenceDungeon4MobCombat only — do not also register
                // MissionInstanceMobCombat (2m, no door/room check) or mobs aggro through doors.
                NascenceDungeon4MobCombat.RegisterAggressive(mob.Identity);
                npcController.AiProfile = NpcAiProfile.Aggressive;
            }
            else
            {
                // Capture: Guard Turret is stationary (RunSpeed=0) and does not chase.
                npcController.AiProfile = NpcAiProfile.Passive;
            }

            if (!combatReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NascenceDungeon4Spawn combat prepare failed npc=" + def.Name
                    + " id=" + mob.Identity);
            }

            activateNpc(mob);
            playfield.AnnounceSpawnedCharacterVisibility(mob, Identity.None);

            if (string.Equals(def.Name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                foreach (ICharacter viewer in playfield.EnumerateActiveCharacters())
                {
                    if (viewer != null
                        && viewer.Controller != null
                        && viewer.Controller is PlayerController)
                    {
                        playfield.ForceCharacterVisibilityToRecipient(mob, viewer, true);
                    }
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon4Spawn ok name=" + def.Name
                + " id=" + mob.Identity.Instance
                + " md=" + def.MonsterData
                + " combatReady=" + (combatReady ? 1 : 0)
                + " appearance=" + appearance);
            spawnedIdentity = mob.Identity;
            return true;
        }
    }
}
