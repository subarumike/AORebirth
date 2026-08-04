namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;
    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;

    #endregion

    /// <summary>
    /// Capture 20260801-SANDSTORM SANDSTORM Marauders + Control Tower on Remi Hellfyre path.
    /// </summary>
    internal static class AreteSandstormMarauderRuntime
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const string MarauderName = "SANDSTORM Marauder";

        private const string ControlTowerName = "SANDSTORM Control Tower";

        private const int MarauderLevel = 7;

        private const int MarauderHealth = 650;

        private const int MarauderScale = 94;

        private const int MarauderNpcFamily = 0;

        // Capture SCFU RunSpeedBase=500.
        private const int MarauderRunSpeed = 500;

        private const int MarauderCharacterFlags = 268964353;

        private const int DefaultMarauderHeadMesh = 40101;

        // Capture corpse CATMesh (not MonsterData). MD-as-CATMesh crashes the client.
        private const int MarauderCorpseCatMesh = 265819;

        private const int ControlTowerMonsterData = 200894;

        private const int ControlTowerLevel = 13;

        private const int ControlTowerHealth = 327;

        private const int ControlTowerScale = 96;

        private const int ControlTowerRunSpeed = 45;

        private const int ControlTowerCharacterFlags = 269095425;

        // Mike: SANDSTORM Marauders respawn 30s after death at their spawn slot.
        private const double MarauderRespawnSeconds = 30.0;

        private sealed class MarauderSlot
        {
            public MarauderSlot(
                int monsterData,
                int headMesh,
                float x,
                float y,
                float z)
            {
                this.MonsterData = monsterData;
                this.HeadMesh = headMesh;
                this.X = x;
                this.Y = y;
                this.Z = z;
            }

            public int MonsterData { get; private set; }

            public int HeadMesh { get; private set; }

            public float X { get; private set; }

            public float Y { get; private set; }

            public float Z { get; private set; }
        }

        private sealed class SlotRuntimeState
        {
            public Identity CurrentIdentity { get; set; }

            public DateTime? RespawnDueUtc { get; set; }
        }

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, SlotRuntimeState[]> RuntimeStatesByPlayfield =
            new Dictionary<int, SlotRuntimeState[]>();

        private static readonly Dictionary<int, Identity> ControlTowerByPlayfield =
            new Dictionary<int, Identity>();

        // Capture 20260801-SANDSTORM first-seen path actors; Mike: all slots respawn after 30s.
        private static readonly MarauderSlot[] SpawnSlots =
            {
                new MarauderSlot(265822, DefaultMarauderHeadMesh, 4033.377f, 0.010f, 667.7479f),
                new MarauderSlot(265822, DefaultMarauderHeadMesh, 4033.406f, 0.010f, 676.7122f),
                new MarauderSlot(287217, 0, 4039.895f, 0.6299585f, 696.3529f),
                new MarauderSlot(287217, 0, 4058.394f, 0.610f, 678.1385f),
                new MarauderSlot(265822, DefaultMarauderHeadMesh, 4055.279f, 2.131286f, 650.3979f)
            };

        public static void StartForPlayfield(Playfield playfield, Identity playfieldIdentity, Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId
                || !LinkedPlayfields.Add(playfieldIdentity.Instance))
            {
                return;
            }

            var states = new SlotRuntimeState[SpawnSlots.Length];
            RuntimeStatesByPlayfield[playfieldIdentity.Instance] = states;
            int spawned = 0;
            for (int i = 0; i < SpawnSlots.Length; i++)
            {
                try
                {
                    Character marauder = SpawnMarauder(
                        playfield,
                        playfieldIdentity,
                        activateNpc,
                        SpawnSlots[i].MonsterData,
                        SpawnSlots[i].HeadMesh,
                        SpawnSlots[i].X,
                        SpawnSlots[i].Y,
                        SpawnSlots[i].Z);
                    if (marauder != null)
                    {
                        states[i] = new SlotRuntimeState { CurrentIdentity = marauder.Identity };
                        spawned++;
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "AreteSandstormMarauderRuntime spawn slot=" + i + " failed: "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }

            try
            {
                Character tower = SpawnControlTower(playfield, playfieldIdentity, activateNpc);
                if (tower != null)
                {
                    ControlTowerByPlayfield[playfieldIdentity.Instance] = tower.Identity;
                    spawned++;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "AreteSandstormMarauderRuntime control-tower spawn failed: "
                    + ex.GetType().Name + ": " + ex.Message);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteSandstormMarauderRuntime spawned="
                + spawned
                + "/"
                + (SpawnSlots.Length + 1)
                + " pf="
                + playfieldIdentity.Instance
                + " source=20260801-SANDSTORM");
            if (spawned == 0)
            {
                LinkedPlayfields.Remove(playfieldIdentity.Instance);
                RuntimeStatesByPlayfield.Remove(playfieldIdentity.Instance);
                ControlTowerByPlayfield.Remove(playfieldIdentity.Instance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            RuntimeStatesByPlayfield.Remove(playfieldInstance);
            ControlTowerByPlayfield.Remove(playfieldInstance);
        }

        internal static bool IsRegisteredMarauder(ICharacter target)
        {
            if (target == null
                || target.Playfield == null
                || target.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !string.Equals(target.Name, MarauderName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            SlotRuntimeState[] states;
            if (!RuntimeStatesByPlayfield.TryGetValue(AreteLandingPlayfieldId, out states)
                || states == null)
            {
                // Name match on Arete is enough when slot table was cleared mid-session.
                return target.Stats[StatIds.level].Value == MarauderLevel
                       || target.Stats[StatIds.level].Value == 0;
            }

            for (int i = 0; i < states.Length; i++)
            {
                SlotRuntimeState state = states[i];
                if (state != null
                    && state.CurrentIdentity.Type == target.Identity.Type
                    && state.CurrentIdentity.Instance == target.Identity.Instance)
                {
                    return true;
                }
            }

            return target.Stats[StatIds.level].Value == MarauderLevel
                   || string.Equals(target.Name, MarauderName, StringComparison.OrdinalIgnoreCase);
        }

        public static void TickRespawn(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            if (playfield == null
                || activateNpc == null
                || playfieldIdentity.Instance != AreteLandingPlayfieldId)
            {
                return;
            }

            // Survive ClearPlayfield / late join: re-arm like Alex fleas.
            LinkedPlayfields.Add(playfieldIdentity.Instance);
            SlotRuntimeState[] states;
            if (!RuntimeStatesByPlayfield.TryGetValue(playfieldIdentity.Instance, out states)
                || states == null
                || states.Length != SpawnSlots.Length)
            {
                states = new SlotRuntimeState[SpawnSlots.Length];
                RuntimeStatesByPlayfield[playfieldIdentity.Instance] = states;
            }

            DateTime utcNow = DateTime.UtcNow;
            for (int i = 0; i < SpawnSlots.Length; i++)
            {
                MarauderSlot slot = SpawnSlots[i];
                SlotRuntimeState state = states[i];
                if (state == null)
                {
                    state = new SlotRuntimeState();
                    states[i] = state;
                }

                Identity livingId;
                if (TryResolveLivingMarauder(playfield, state, slot, out livingId))
                {
                    state.CurrentIdentity = livingId;
                    state.RespawnDueUtc = null;
                    continue;
                }

                if (!state.RespawnDueUtc.HasValue)
                {
                    state.RespawnDueUtc = utcNow + TimeSpan.FromSeconds(MarauderRespawnSeconds);
                    continue;
                }

                if (state.RespawnDueUtc.Value > utcNow)
                {
                    continue;
                }

                try
                {
                    Character marauder = SpawnMarauder(
                        playfield,
                        playfieldIdentity,
                        activateNpc,
                        slot.MonsterData,
                        slot.HeadMesh,
                        slot.X,
                        slot.Y,
                        slot.Z);
                    if (marauder != null)
                    {
                        state.CurrentIdentity = marauder.Identity;
                        state.RespawnDueUtc = null;
                        LogUtil.Debug(
                            DebugInfoDetail.Engine,
                            "AreteSandstormMarauderRuntime respawned slot=" + i
                            + " id=" + marauder.Identity.ToString(true));
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "AreteSandstormMarauderRuntime respawn slot=" + i + " failed: "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private static bool TryResolveLivingMarauder(
            Playfield playfield,
            SlotRuntimeState state,
            MarauderSlot slot,
            out Identity livingId)
        {
            livingId = Identity.None;
            if (playfield == null || state == null || slot == null)
            {
                return false;
            }

            if (state.CurrentIdentity.Type != IdentityType.None
                && state.CurrentIdentity.Instance != 0)
            {
                try
                {
                    ICharacter current = Pool.Instance.GetObject<ICharacter>(state.CurrentIdentity);
                    if (IsLivingMarauderOnPlayfield(current, playfield))
                    {
                        livingId = current.Identity;
                        return true;
                    }
                }
                catch (Exception)
                {
                }
            }

            const float radiusSq = 64.0f; // 8m
            foreach (ICharacter candidate in Pool.Instance.GetAll<ICharacter>(playfield.Identity))
            {
                if (!IsLivingMarauderOnPlayfield(candidate, playfield))
                {
                    continue;
                }

                float dx = candidate.Coordinates().x - slot.X;
                float dz = candidate.Coordinates().z - slot.Z;
                if ((dx * dx) + (dz * dz) <= radiusSq)
                {
                    livingId = candidate.Identity;
                    return true;
                }
            }

            return false;
        }

        private static bool IsLivingMarauderOnPlayfield(ICharacter candidate, Playfield playfield)
        {
            return candidate != null
                   && candidate.Playfield == playfield
                   && candidate.Controller is NPCController
                   && candidate.Stats[StatIds.health].Value > 0
                   && candidate.Stats[StatIds.deadtimer].Value == 0
                   && string.Equals(candidate.Name, MarauderName, StringComparison.OrdinalIgnoreCase);
        }

        private static Character SpawnMarauder(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int monsterData,
            int headMesh,
            float x,
            float y,
            float z)
        {
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Aggressive };
            Character marauder = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                "A004",
                playfieldIdentity,
                new Coordinate { x = x, y = y, z = z },
                new Quaternion(0.0, 0.0, 0.0, 1.0),
                controller,
                MarauderLevel);
            if (marauder == null)
            {
                return null;
            }

            marauder.Name = MarauderName;
            marauder.Playfield = playfield;
            ApplyMarauderStats(marauder, monsterData, headMesh);
            marauder.Name = MarauderName;
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "arete-sandstorm-20260801-SANDSTORM",
                8,
                18,
                2.0,
                1,
                0,
                1279612721,
                0,
                0,
                0,
                0,
                0,
                0);
            string unused;
            CapturedEnemyCombatRuntime.Prepare(marauder, controller, contract, out unused);
            // Prepare can overwrite identity stats — restore capture marauder profile.
            ApplyMarauderStats(marauder, monsterData, headMesh);
            marauder.Name = MarauderName;
            controller.AiProfile = NpcAiProfile.Aggressive;
            marauder.Coordinates(new Coordinate { x = x, y = y, z = z });
            marauder.DoNotDoTimers = false;
            activateNpc(marauder);
            playfield.AnnounceSpawnedCharacterVisibility(marauder, Identity.None);
            return marauder;
        }

        private static Character SpawnControlTower(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc)
        {
            NPCController controller = new NPCController { AiProfile = NpcAiProfile.Passive };
            Character tower = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                "A004",
                playfieldIdentity,
                new Coordinate { x = 4066.855f, y = 0.61f, z = 666.7697f },
                new Quaternion(0.0, 0.0, 0.0, 1.0),
                controller,
                ControlTowerLevel);
            if (tower == null)
            {
                return null;
            }

            tower.Name = ControlTowerName;
            tower.Playfield = playfield;
            SetStat(tower, StatIds.monsterdata, ControlTowerMonsterData);
            SetStat(tower, StatIds.life, ControlTowerHealth);
            SetStat(tower, StatIds.health, ControlTowerHealth);
            SetStat(tower, StatIds.level, ControlTowerLevel);
            SetStat(tower, StatIds.npcfamily, MarauderNpcFamily);
            SetStat(tower, StatIds.monsterscale, ControlTowerScale);
            SetStat(tower, StatIds.runspeed, ControlTowerRunSpeed);
            SetStat(tower, StatIds.flags, ControlTowerCharacterFlags);
            SetStat(tower, StatIds.visualflags, 31);
            SetStat(tower, StatIds.side, 3);
            SetStat(tower, StatIds.breed, 1);
            SetStat(tower, StatIds.sex, 2);
            SetStat(tower, StatIds.race, 1);
            SetStat(tower, StatIds.fatness, 1);
            SetStat(tower, StatIds.catmesh, MarauderCorpseCatMesh);
            SetStat(tower, StatIds.displaycatmesh, MarauderCorpseCatMesh);
            tower.Name = ControlTowerName;
            controller.AiProfile = NpcAiProfile.Passive;
            tower.Coordinates(new Coordinate { x = 4066.855f, y = 0.61f, z = 666.7697f });
            tower.DoNotDoTimers = false;
            activateNpc(tower);
            playfield.AnnounceSpawnedCharacterVisibility(tower, Identity.None);
            return tower;
        }

        private static void ApplyMarauderStats(Character marauder, int monsterData, int headMesh)
        {
            SetStat(marauder, StatIds.monsterdata, monsterData);
            SetStat(marauder, StatIds.life, MarauderHealth);
            SetStat(marauder, StatIds.health, MarauderHealth);
            SetStat(marauder, StatIds.level, MarauderLevel);
            SetStat(marauder, StatIds.npcfamily, MarauderNpcFamily);
            SetStat(marauder, StatIds.monsterscale, MarauderScale);
            SetStat(marauder, StatIds.runspeed, MarauderRunSpeed);
            SetStat(marauder, StatIds.flags, MarauderCharacterFlags);
            SetStat(marauder, StatIds.visualflags, 31);
            SetStat(marauder, StatIds.side, 3);
            SetStat(marauder, StatIds.breed, 1);
            SetStat(marauder, StatIds.sex, 2);
            SetStat(marauder, StatIds.race, 1);
            SetStat(marauder, StatIds.fatness, 1);
            if (headMesh > 0)
            {
                SetStat(marauder, StatIds.headmesh, headMesh);
            }

            SetStat(marauder, StatIds.catmesh, MarauderCorpseCatMesh);
            SetStat(marauder, StatIds.displaycatmesh, MarauderCorpseCatMesh);
        }

        private static void SetStat(ICharacter mob, StatIds stat, int value)
        {
            mob.Stats[stat].Value = value;
            mob.Stats[stat].BaseValue = (uint)value;
        }
    }
}
