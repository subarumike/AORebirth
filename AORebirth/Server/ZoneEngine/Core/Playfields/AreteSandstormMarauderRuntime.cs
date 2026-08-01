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
    /// Capture 20260727-204902 SANDSTORM Marauders east of Arete market (Remi Hellfyre quest).
    /// </summary>
    internal static class AreteSandstormMarauderRuntime
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const string MarauderName = "SANDSTORM Marauder";

        private const int MarauderLevel = 7;

        private const int MarauderHealth = 650;

        private const int MarauderScale = 94;

        private const int MarauderNpcFamily = 0;

        private const int MarauderRunSpeed = 24;

        private const int MarauderCharacterFlags = 268964353;

        private const int MarauderHeadMesh = 40101;

        // Capture corpse CATMesh (not MonsterData). MD-as-CATMesh crashes the client.
        private const int MarauderCorpseCatMesh = 265819;

        private sealed class ReplacementDefinition
        {
            public ReplacementDefinition(
                int monsterData,
                double delaySeconds,
                float x,
                float y,
                float z)
            {
                this.MonsterData = monsterData;
                this.DelaySeconds = delaySeconds;
                this.X = x;
                this.Y = y;
                this.Z = z;
            }

            public int MonsterData { get; private set; }

            public double DelaySeconds { get; private set; }

            public float X { get; private set; }

            public float Y { get; private set; }

            public float Z { get; private set; }
        }

        private sealed class MarauderSlot
        {
            public MarauderSlot(
                int monsterData,
                float x,
                float y,
                float z,
                ReplacementDefinition replacement)
            {
                this.MonsterData = monsterData;
                this.X = x;
                this.Y = y;
                this.Z = z;
                this.Replacement = replacement;
            }

            public int MonsterData { get; private set; }

            public float X { get; private set; }

            public float Y { get; private set; }

            public float Z { get; private set; }

            public ReplacementDefinition Replacement { get; private set; }
        }

        private sealed class SlotRuntimeState
        {
            public Identity CurrentIdentity { get; set; }

            public DateTime? RespawnDueUtc { get; set; }

            public bool ReplacementConsumed { get; set; }
        }

        private static readonly HashSet<int> LinkedPlayfields = new HashSet<int>();

        private static readonly Dictionary<int, SlotRuntimeState[]> RuntimeStatesByPlayfield =
            new Dictionary<int, SlotRuntimeState[]>();

        // Exact initial actors from 20260727-204902. Only the first two slots
        // have identity-correlated replacement observations; the other three
        // remain intentionally non-respawning.
        private static readonly MarauderSlot[] SpawnSlots =
            {
                new MarauderSlot(
                    265822,
                    4033.099f,
                    0.010f,
                    677.2908f,
                    new ReplacementDefinition(26092, 42.5370285, 4031.978f, 0.6528038f, 677.3542f)),
                new MarauderSlot(
                    287217,
                    4032.111f,
                    0.010f,
                    667.5142f,
                    new ReplacementDefinition(26092, 42.5948143, 4032.878f, 0.010f, 667.3873f)),
                new MarauderSlot(265822, 4039.592f, 0.6754054f, 696.7009f, null),
                new MarauderSlot(287217, 4038.502f, 0.010f, 688.2748f, null),
                new MarauderSlot(287217, 4054.383f, 1.537878f, 651.4177f, null)
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
                    Character marauder = SpawnSlot(
                        playfield,
                        playfieldIdentity,
                        activateNpc,
                        SpawnSlots[i].MonsterData,
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

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "AreteSandstormMarauderRuntime spawned="
                + spawned
                + "/"
                + SpawnSlots.Length
                + " pf="
                + playfieldIdentity.Instance
                + " source=20260727-204902");
            if (spawned == 0)
            {
                LinkedPlayfields.Remove(playfieldIdentity.Instance);
                RuntimeStatesByPlayfield.Remove(playfieldIdentity.Instance);
            }
        }

        public static void ClearPlayfield(int playfieldInstance)
        {
            LinkedPlayfields.Remove(playfieldInstance);
            RuntimeStatesByPlayfield.Remove(playfieldInstance);
        }

        internal static bool IsRegisteredMarauder(ICharacter target)
        {
            if (target == null
                || target.Playfield == null
                || target.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !string.Equals(target.Name, MarauderName, StringComparison.OrdinalIgnoreCase)
                || target.Stats[StatIds.level].Value != MarauderLevel
                || target.Stats[StatIds.npcfamily].Value != MarauderNpcFamily)
            {
                return false;
            }

            SlotRuntimeState[] states;
            if (!RuntimeStatesByPlayfield.TryGetValue(AreteLandingPlayfieldId, out states)
                || states == null)
            {
                return false;
            }

            for (int i = 0; i < states.Length; i++)
            {
                SlotRuntimeState state = states[i];
                if (state != null
                    && state.CurrentIdentity != null
                    && state.CurrentIdentity.Type == target.Identity.Type
                    && state.CurrentIdentity.Instance == target.Identity.Instance)
                {
                    return true;
                }
            }

            return false;
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

            SlotRuntimeState[] states;
            if (!RuntimeStatesByPlayfield.TryGetValue(playfieldIdentity.Instance, out states)
                || states == null
                || states.Length != SpawnSlots.Length)
            {
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            for (int i = 0; i < SpawnSlots.Length; i++)
            {
                MarauderSlot slot = SpawnSlots[i];
                SlotRuntimeState state = states[i];
                if (slot.Replacement == null
                    || state == null
                    || state.ReplacementConsumed)
                {
                    continue;
                }

                ICharacter current = Pool.Instance.GetObject<ICharacter>(state.CurrentIdentity);
                if (current != null
                    && current.Playfield == playfield
                    && current.Stats[StatIds.health].Value > 0)
                {
                    state.RespawnDueUtc = null;
                    continue;
                }

                if (!state.RespawnDueUtc.HasValue)
                {
                    state.RespawnDueUtc = utcNow + TimeSpan.FromSeconds(slot.Replacement.DelaySeconds);
                    continue;
                }

                if (state.RespawnDueUtc.Value > utcNow)
                {
                    continue;
                }

                try
                {
                    ReplacementDefinition replacement = slot.Replacement;
                    Character marauder = SpawnSlot(
                        playfield,
                        playfieldIdentity,
                        activateNpc,
                        replacement.MonsterData,
                        replacement.X,
                        replacement.Y,
                        replacement.Z);
                    if (marauder != null)
                    {
                        state.CurrentIdentity = marauder.Identity;
                        state.RespawnDueUtc = null;
                        state.ReplacementConsumed = true;
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private static Character SpawnSlot(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            int monsterData,
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
            ApplyCaptureStats(marauder, monsterData);
            marauder.Name = MarauderName;
            CapturedEnemyCombatContract contract = CapturedEnemyCombatContract.FixedAttackOnSight(
                "arete-sandstorm-20260727-204902",
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
            controller.AiProfile = NpcAiProfile.Aggressive;
            marauder.Coordinates(new Coordinate { x = x, y = y, z = z });
            marauder.DoNotDoTimers = false;
            activateNpc(marauder);
            playfield.AnnounceSpawnedCharacterVisibility(marauder, Identity.None);
            return marauder;
        }

        private static void ApplyCaptureStats(Character marauder, int monsterData)
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
            SetStat(marauder, StatIds.headmesh, MarauderHeadMesh);
            // Usable corpse CATMesh for CorpseCatMeshFor(); living body still uses MonsterData.
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
