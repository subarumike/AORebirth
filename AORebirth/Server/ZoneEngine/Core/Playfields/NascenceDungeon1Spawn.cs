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
    /// Capture 20260823-171238 dungeon mob population.
    /// Combat via MissionInstanceMobCombat SIW1 (aggro/attack/anim). HeadMesh cleared so
    /// monster bodies do not keep the BART human head (Rafter human-head bug).
    /// </summary>
    internal static partial class NascenceDungeon1Spawn
    {
        private const string TemplateHash = "BART";

        private const int DefaultMonsterCharacterFlags = 268964353;

        private const float SlotAliveProximityMetersSq = 6.25f; // 2.5m

        private static readonly object RespawnGate = new object();

        private static readonly Dictionary<int, DateTime[]> NextRespawnUtcByPlayfield =
            new Dictionary<int, DateTime[]>();

        private static readonly Dictionary<int, Identity[]> LivingIdentityByPlayfield =
            new Dictionary<int, Identity[]>();

        // Capture 20260823-171238 Wailing Spirit HasExtendedTextures (dual mesh ids).
        private static readonly byte[] WailingSpiritExtendedTextureOverrideData =
            {
                0x00, 0x00, 0x0B, 0xD3, 0x68, 0x65, 0x61, 0x64,
                0x5F, 0x73, 0x70, 0x69, 0x72, 0x69, 0x74, 0x20,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x66, 0x65, 0x6D, 0x61, 0x6C, 0x65, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x96, 0xB9,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x72, 0x65, 0x64, 0x65, 0x65, 0x6D, 0x65, 0x64,
                0x20, 0x62, 0x6F, 0x64, 0x79, 0x20, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x96, 0xB7, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            };

        // Capture 20260824-220326 Havaris HasExtendedTextures ("self illumination body" + opacity maps).
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

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            if (string.Equals(name, "Wailing Spirit", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])WailingSpiritExtendedTextureOverrideData.Clone();
                return true;
            }

            if (string.Equals(name, "Havaris", StringComparison.OrdinalIgnoreCase))
            {
                data = (byte[])HavarisExtendedTextureOverrideData.Clone();
                return true;
            }

            data = null;
            return false;
        }

        /// <summary>
        /// Capture 20260824-220326 SCFU: Monster-side trash/boss include IsPet; spirits/weaver do not.
        /// </summary>
        internal static bool NeedsCapturedScfuIsPet(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (string.Equals(name, "Wailing Spirit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Smelly Weaver", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return NascenceDungeon1Rules.IsDungeonCorpseName(name);
        }

        internal static void SpawnForPlayfield(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || !NascenceDungeon1Rules.IsDungeonPlayfield(playfieldIdentity.Instance))
            {
                return;
            }

            int spawned = 0;
            Identity[] living;
            DateTime[] timers;
            EnsureRespawnState(playfieldIdentity.Instance, out timers, out living);
            for (int i = 0; i < CapturedSpawns.Length; i++)
            {
                try
                {
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
                        "NascenceDungeon1Spawn failed mob=" + CapturedSpawns[i].Name
                        + " ex=" + ex.GetType().Name + ": " + ex.Message);
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon1Spawn pf=" + playfieldIdentity.Instance + " spawned=" + spawned);
        }

        /// <summary>
        /// Mike: trash/treasure 10m; Havaris 20m if dungeon empty, 1h if occupied (at schedule time).
        /// </summary>
        internal static void TickRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || !NascenceDungeon1Rules.IsDungeonPlayfield(playfieldIdentity.Instance))
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
                        "NascenceDungeon1Spawn respawn failed mob=" + CapturedSpawns[i].Name
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
                    ? NascenceDungeon1Rules.HavarisRespawnWhenOccupied
                    : NascenceDungeon1Rules.HavarisRespawnWhenEmpty;
            }

            return NascenceDungeon1Rules.MobRespawnDelay;
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

            NascenceDungeon1MobSpawn def = CapturedSpawns[slotIndex];
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (candidate == null
                    || candidate.Controller is PlayerController
                    || !string.Equals(candidate.Name, def.Name, StringComparison.OrdinalIgnoreCase)
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
            NascenceDungeon1MobSpawn def,
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

            bool isSpirit = string.Equals(def.Name, "Wailing Spirit", StringComparison.OrdinalIgnoreCase);
            // Live: Rafters Breed=Monster Sex=None; Spirits Breed=Solitus Sex=Female + ExtTex.
            int breed = isSpirit ? (int)Breed.Solitus : (int)Breed.Monster;
            int sex = isSpirit ? (int)Gender.Female : (int)Gender.None;
            int appearance = def.Appearance > 0 ? def.Appearance : (isSpirit ? 1835 : 1227);

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
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.runspeed, (uint)def.RunSpeed);
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
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.life, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.health, (uint)def.Health);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healinterval, 0u);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.healdelta, 0u);
            playfield.SuspendNpcRegen(mob);

            MissionInstanceMobCombat.RegisterAggressive(mob.Identity);
            NascenceDungeon1MobCombat.RegisterAggressive(mob.Identity);
            npcController.AiProfile = NpcAiProfile.Aggressive;

            if (!combatReady)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NascenceDungeon1Spawn combat prepare failed npc=" + def.Name
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
                        playfield.ForceCharacterVisibilityToRecipient(mob, viewer);
                    }
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NascenceDungeon1Spawn ok name=" + def.Name
                + " id=" + mob.Identity.Instance
                + " md=" + def.MonsterData
                + " combatReady=" + (combatReady ? 1 : 0)
                + " appearance=" + appearance);
            spawnedIdentity = mob.Identity;
            return true;
        }
    }
}
