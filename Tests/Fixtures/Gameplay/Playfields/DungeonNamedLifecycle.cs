namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal enum DungeonNamedDomainKind
    {
        Initial,
        Successor,
        OwnedAdd,
        OrdinaryPatrol
    }

    internal enum DungeonNamedRespawnClassification
    {
        ExactRuleAlreadyImplemented,
        ExactRuleImplementedInThisTask,
        ProvenSharedNamedRespawnRule,
        ExplicitlyNoIndependentRespawn,
        UnresolvedFailClosed
    }

    internal enum DungeonNamedRespawnTrigger
    {
        Death,
        NpcDespawn,
        PredecessorDeath,
        OwnerAction,
        OwnerLifecycle
    }

    internal enum DungeonNamedLootOwnership
    {
        GlobalAtomicCapturedDefinition,
        GlobalAtomicExistingDefinition,
        GlobalAtomicFailClosed
    }

    internal sealed class DungeonNamedLifecycleDefinition
    {
        internal int PlayfieldId { get; set; }
        internal string ProfileKey { get; set; }
        internal string DisplayName { get; set; }
        internal DungeonNamedDomainKind Kind { get; set; }
        internal bool IsRecreated { get; set; }
        internal string RespawnOwnerKey { get; set; }
        internal DungeonNamedRespawnTrigger Trigger { get; set; }
        internal string DelayRule { get; set; }
        internal bool CorpseBlocksRecreation { get; set; }
        internal DungeonNamedLootOwnership LootOwnership { get; set; }
        internal bool LootBlocksRecreation { get; set; }
        internal bool PlayerPresenceRequired { get; set; }
        internal bool OwnerRequired { get; set; }
        internal string LiveRuntimeBehavior { get; set; }
        internal string RuntimeDisposalBehavior { get; set; }
        internal string ReentryBehavior { get; set; }
        internal DungeonNamedRespawnClassification Classification { get; set; }
    }

    internal static class DungeonNamedLifecycleCatalog
    {
        internal const string AbmouthProfileKey = "subway.127.boss.abmouth-supremus";
        internal const string VergilProfileKey = "subway.127.boss.vergil-aeneid";
        internal const string EumenidesProfileKey = "subway.127.named.eumenides";
        internal const string InfectorProfileKey = "subway.127.encounter.abmouth-infector";
        internal const string StrikeForemanProfileKey = "subway.127.named.strike-foreman";
        internal const string DefenderProfileKey = "totw.647.boss.defender-of-the-three";
        internal const string YatilaProfileKey = "totw.647.named.windcaller-yatila";
        internal const string GulardProfileKey = "totw.647.named.reverend-gulard";
        internal const string ReAnimatorProfileKey = "totw.647.boss.the-re-animator";
        internal const string ReanimatedCorpseProfileKey =
            "totw.647.encounter.re-animator.reanimated-corpse";
        internal const string BetanyProfileKey = "totw.647.named.acolyte-betany";
        internal const string CuratorProfileKey = "totw.647.boss.the-curator";
        internal const string NematetProfileKey =
            "totw.647.boss.nematet-the-custodian-of-time";
        internal const string GuardianProfileKey = "totw.1931.boss.guardian-of-tomorrow";
        internal const string GartuaProfileKey = "totw.1931.boss.gartua-the-doorkeeper";
        internal const string UkleshProfileKey = "totw.1931.boss.uklesh-the-frozen";
        internal const string KhalumProfileKey = "totw.1931.boss.khalum";
        internal const string AzturProfileKey = "totw.1931.boss.aztur-the-immortal";
        internal const string MurialProfileKey =
            "totw.ordinary.main-room.murial-the-faithful.26090";

        private static readonly DungeonNamedLifecycleDefinition[] Definitions =
        {
            Initial(127, AbmouthProfileKey, "Abmouth Supremus",
                DungeonNamedRespawnTrigger.Death, "600 seconds after death",
                DungeonNamedRespawnClassification.ExactRuleAlreadyImplemented,
                DungeonNamedLootOwnership.GlobalAtomicCapturedDefinition),
            Initial(127, VergilProfileKey, "Vergil Aeneid",
                DungeonNamedRespawnTrigger.Death, "600 seconds after death",
                DungeonNamedRespawnClassification.ExactRuleAlreadyImplemented,
                DungeonNamedLootOwnership.GlobalAtomicCapturedDefinition),
            Initial(127, EumenidesProfileKey, "Eumenides",
                DungeonNamedRespawnTrigger.Death, "600 seconds after death",
                DungeonNamedRespawnClassification.ExactRuleAlreadyImplemented,
                DungeonNamedLootOwnership.GlobalAtomicCapturedDefinition),
            OwnedAdd(127, InfectorProfileKey, "Abmouth-owned Infector adds",
                AbmouthProfileKey,
                "initial slots at 1.212281/2.326367 seconds; captured owner refill cycle 0.830/0.380/3.322/3.490 seconds",
                DungeonNamedLootOwnership.GlobalAtomicExistingDefinition),
            Initial(127, StrikeForemanProfileKey, "Strike Foreman",
                DungeonNamedRespawnTrigger.Death, "600 seconds after death",
                DungeonNamedRespawnClassification.ExactRuleAlreadyImplemented,
                DungeonNamedLootOwnership.GlobalAtomicCapturedDefinition),

            Initial(1931, DefenderProfileKey, "Defender of the Three",
                DungeonNamedRespawnTrigger.NpcDespawn, "600 seconds after NPC despawn",
                DungeonNamedRespawnClassification.ExactRuleAlreadyImplemented,
                DungeonNamedLootOwnership.GlobalAtomicCapturedDefinition),
            SharedTempleInitial(YatilaProfileKey, "Windcaller Yatila"),
            SharedTempleInitial(GulardProfileKey, "Reverend Gulard"),
            SharedTempleInitial(ReAnimatorProfileKey, "The Re-Animator"),
            OwnedAdd(1931, ReanimatedCorpseProfileKey, "Reanimated Corpse adds",
                ReAnimatorProfileKey,
                "1.578 seconds after proven owner cast; 1.123 seconds after requested add despawn; 1 second owner-reset refill",
                DungeonNamedLootOwnership.GlobalAtomicExistingDefinition),
            SharedTempleInitial(BetanyProfileKey, "Acolyte Betany"),
            SharedTempleInitial(CuratorProfileKey, "The Curator"),
            SharedTempleInitial(NematetProfileKey, "Nematet"),
            Initial(1931, GuardianProfileKey, "Guardian of Tomorrow",
                DungeonNamedRespawnTrigger.NpcDespawn, "600 seconds after NPC despawn",
                DungeonNamedRespawnClassification.ExactRuleAlreadyImplemented,
                DungeonNamedLootOwnership.GlobalAtomicCapturedDefinition),
            Initial(1931, GartuaProfileKey, "Gartua",
                DungeonNamedRespawnTrigger.NpcDespawn, "600 seconds after NPC despawn",
                DungeonNamedRespawnClassification.ExactRuleAlreadyImplemented,
                DungeonNamedLootOwnership.GlobalAtomicCapturedDefinition),
            ChainInitial(),
            Successor(KhalumProfileKey, "Khalum", UkleshProfileKey, "0.6822027 seconds"),
            Successor(AzturProfileKey, "Aztur", KhalumProfileKey, "0.211 seconds"),
            Murial()
        };

        internal static DungeonNamedLifecycleDefinition[] All()
        {
            return Definitions.ToArray();
        }

        internal static DungeonNamedLifecycleDefinition Get(string profileKey)
        {
            return Definitions.Single(value => string.Equals(
                value.ProfileKey,
                profileKey,
                StringComparison.Ordinal));
        }

        private static DungeonNamedLifecycleDefinition Initial(
            int playfieldId,
            string profileKey,
            string displayName,
            DungeonNamedRespawnTrigger trigger,
            string delayRule,
            DungeonNamedRespawnClassification classification,
            DungeonNamedLootOwnership lootOwnership)
        {
            return Definition(
                playfieldId,
                profileKey,
                displayName,
                DungeonNamedDomainKind.Initial,
                true,
                profileKey,
                trigger,
                delayRule,
                false,
                lootOwnership,
                false,
                false,
                false,
                classification);
        }

        private static DungeonNamedLifecycleDefinition SharedTempleInitial(
            string profileKey,
            string displayName)
        {
            return Initial(
                1931,
                profileKey,
                displayName,
                DungeonNamedRespawnTrigger.NpcDespawn,
                "600 seconds after NPC despawn under the shared Temple named policy",
                DungeonNamedRespawnClassification.ProvenSharedNamedRespawnRule,
                DungeonNamedLootOwnership.GlobalAtomicFailClosed);
        }

        private static DungeonNamedLifecycleDefinition ChainInitial()
        {
            return Definition(
                1931,
                UkleshProfileKey,
                "Uklesh",
                DungeonNamedDomainKind.Initial,
                true,
                AzturProfileKey,
                DungeonNamedRespawnTrigger.OwnerLifecycle,
                "600 seconds after Aztur NPC despawn under the shared Temple named policy",
                false,
                DungeonNamedLootOwnership.GlobalAtomicFailClosed,
                false,
                false,
                true,
                DungeonNamedRespawnClassification.ExactRuleImplementedInThisTask);
        }

        private static DungeonNamedLifecycleDefinition Successor(
            string profileKey,
            string displayName,
            string predecessorProfileKey,
            string delayRule)
        {
            return Definition(
                1931,
                profileKey,
                displayName,
                DungeonNamedDomainKind.Successor,
                true,
                predecessorProfileKey,
                DungeonNamedRespawnTrigger.PredecessorDeath,
                delayRule,
                false,
                DungeonNamedLootOwnership.GlobalAtomicFailClosed,
                false,
                false,
                true,
                DungeonNamedRespawnClassification.ExplicitlyNoIndependentRespawn);
        }

        private static DungeonNamedLifecycleDefinition OwnedAdd(
            int playfieldId,
            string profileKey,
            string displayName,
            string ownerProfileKey,
            string delayRule,
            DungeonNamedLootOwnership lootOwnership)
        {
            return Definition(
                playfieldId,
                profileKey,
                displayName,
                DungeonNamedDomainKind.OwnedAdd,
                true,
                ownerProfileKey,
                DungeonNamedRespawnTrigger.OwnerAction,
                delayRule,
                false,
                lootOwnership,
                false,
                false,
                true,
                DungeonNamedRespawnClassification.ExplicitlyNoIndependentRespawn);
        }

        private static DungeonNamedLifecycleDefinition Murial()
        {
            return Definition(
                1931,
                MurialProfileKey,
                "Murial",
                DungeonNamedDomainKind.OrdinaryPatrol,
                true,
                MurialProfileKey,
                DungeonNamedRespawnTrigger.NpcDespawn,
                "300 seconds after NPC despawn under the explicit shared ordinary policy",
                false,
                DungeonNamedLootOwnership.GlobalAtomicFailClosed,
                false,
                false,
                false,
                DungeonNamedRespawnClassification.ProvenSharedNamedRespawnRule);
        }

        private static DungeonNamedLifecycleDefinition Definition(
            int playfieldId,
            string profileKey,
            string displayName,
            DungeonNamedDomainKind kind,
            bool isRecreated,
            string ownerKey,
            DungeonNamedRespawnTrigger trigger,
            string delayRule,
            bool corpseBlocksRecreation,
            DungeonNamedLootOwnership lootOwnership,
            bool lootBlocksRecreation,
            bool playerPresenceRequired,
            bool ownerRequired,
            DungeonNamedRespawnClassification classification)
        {
            return new DungeonNamedLifecycleDefinition
            {
                PlayfieldId = playfieldId,
                ProfileKey = profileKey,
                DisplayName = displayName,
                Kind = kind,
                IsRecreated = isRecreated,
                RespawnOwnerKey = ownerKey,
                Trigger = trigger,
                DelayRule = delayRule,
                CorpseBlocksRecreation = corpseBlocksRecreation,
                LootOwnership = lootOwnership,
                LootBlocksRecreation = lootBlocksRecreation,
                PlayerPresenceRequired = playerPresenceRequired,
                OwnerRequired = ownerRequired,
                LiveRuntimeBehavior = "one active identity and at most one due schedule",
                RuntimeDisposalBehavior =
                    "cancel schedule and retire actor, combat, movement, corpse, loot, and visibility ownership",
                ReentryBehavior =
                    "reuse live state without materialization; replacement runtime starts from clean initial ownership",
                Classification = classification
            };
        }
    }

    internal sealed class DungeonNamedRespawnScheduler
    {
        private readonly WorldRespawnScheduler scheduler = new WorldRespawnScheduler();
        private readonly Dictionary<string, int> generations =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> dueAtUtc =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> playfieldByProfile =
            new Dictionary<string, int>(StringComparer.Ordinal);

        internal int Count { get { return this.scheduler.Count; } }

        internal bool Schedule(
            int playfieldId,
            string profileKey,
            string ownerKey,
            DateTime dueAt)
        {
            if (playfieldId <= 0
                || string.IsNullOrWhiteSpace(profileKey)
                || dueAt == default(DateTime)
                || this.scheduler.Contains(profileKey))
            {
                return false;
            }

            int priorGeneration;
            this.generations.TryGetValue(profileKey, out priorGeneration);
            int generation = priorGeneration + 1;
            bool scheduled = this.scheduler.Schedule(
                new WorldRespawnSchedule
                {
                    SpawnKey = profileKey,
                    GroupKey = ownerKey,
                    PlayfieldId = playfieldId,
                    DueAtUtc = dueAt,
                    Generation = generation
                });
            if (!scheduled)
            {
                return false;
            }

            this.generations[profileKey] = generation;
            this.dueAtUtc[profileKey] = dueAt;
            this.playfieldByProfile[profileKey] = playfieldId;
            return true;
        }

        internal bool Contains(string profileKey)
        {
            return this.scheduler.Contains(profileKey);
        }

        internal bool TryGetDueAtUtc(string profileKey, out DateTime dueAt)
        {
            return this.dueAtUtc.TryGetValue(profileKey, out dueAt);
        }

        internal bool IsDue(string profileKey, DateTime utcNow)
        {
            DateTime dueAt;
            return this.dueAtUtc.TryGetValue(profileKey, out dueAt)
                   && dueAt <= utcNow;
        }

        internal bool Cancel(string profileKey)
        {
            this.dueAtUtc.Remove(profileKey);
            this.playfieldByProfile.Remove(profileKey);
            return this.scheduler.Cancel(profileKey);
        }

        internal WorldRespawnSchedule[] TakeDue(DateTime utcNow, int maximumWork)
        {
            WorldRespawnSchedule[] due = this.scheduler.TakeDue(utcNow, maximumWork);
            foreach (WorldRespawnSchedule value in due)
            {
                this.dueAtUtc.Remove(value.SpawnKey);
                this.playfieldByProfile.Remove(value.SpawnKey);
            }

            return due;
        }

        internal void CancelPlayfield(int playfieldId)
        {
            this.scheduler.CancelPlayfield(playfieldId);
            foreach (string profileKey in this.playfieldByProfile
                .Where(value => value.Value == playfieldId)
                .Select(value => value.Key)
                .ToArray())
            {
                this.dueAtUtc.Remove(profileKey);
                this.playfieldByProfile.Remove(profileKey);
                this.generations.Remove(profileKey);
            }
        }
    }

    internal enum CapturedTempleMainRoomPhase
    {
        InitialUkleshReady,
        UkleshActive,
        KhalumPending,
        KhalumActive,
        AzturPending,
        AzturActive,
        AzturDeadAwaitingDespawn,
        ResetPending,
        Disposed
    }

    internal sealed class CapturedTempleMainRoomLifecycle
    {
        internal CapturedTempleMainRoomPhase Phase { get; private set; }

        internal CapturedTempleMainRoomLifecycle()
        {
            this.Phase = CapturedTempleMainRoomPhase.InitialUkleshReady;
        }

        internal bool TryMarkSpawned(string profileKey)
        {
            if (string.Equals(
                    profileKey,
                    DungeonNamedLifecycleCatalog.UkleshProfileKey,
                    StringComparison.Ordinal)
                && (this.Phase == CapturedTempleMainRoomPhase.InitialUkleshReady
                    || this.Phase == CapturedTempleMainRoomPhase.ResetPending))
            {
                this.Phase = CapturedTempleMainRoomPhase.UkleshActive;
                return true;
            }

            if (string.Equals(
                    profileKey,
                    DungeonNamedLifecycleCatalog.KhalumProfileKey,
                    StringComparison.Ordinal)
                && this.Phase == CapturedTempleMainRoomPhase.KhalumPending)
            {
                this.Phase = CapturedTempleMainRoomPhase.KhalumActive;
                return true;
            }

            if (string.Equals(
                    profileKey,
                    DungeonNamedLifecycleCatalog.AzturProfileKey,
                    StringComparison.Ordinal)
                && this.Phase == CapturedTempleMainRoomPhase.AzturPending)
            {
                this.Phase = CapturedTempleMainRoomPhase.AzturActive;
                return true;
            }

            return false;
        }

        internal bool CanSpawn(string profileKey)
        {
            if (string.Equals(
                profileKey,
                DungeonNamedLifecycleCatalog.UkleshProfileKey,
                StringComparison.Ordinal))
            {
                return this.Phase == CapturedTempleMainRoomPhase.InitialUkleshReady
                       || this.Phase == CapturedTempleMainRoomPhase.ResetPending;
            }

            if (string.Equals(
                profileKey,
                DungeonNamedLifecycleCatalog.KhalumProfileKey,
                StringComparison.Ordinal))
            {
                return this.Phase == CapturedTempleMainRoomPhase.KhalumPending;
            }

            if (string.Equals(
                profileKey,
                DungeonNamedLifecycleCatalog.AzturProfileKey,
                StringComparison.Ordinal))
            {
                return this.Phase == CapturedTempleMainRoomPhase.AzturPending;
            }

            return false;
        }

        internal bool TryMarkDeath(string profileKey)
        {
            if (string.Equals(
                    profileKey,
                    DungeonNamedLifecycleCatalog.UkleshProfileKey,
                    StringComparison.Ordinal)
                && this.Phase == CapturedTempleMainRoomPhase.UkleshActive)
            {
                this.Phase = CapturedTempleMainRoomPhase.KhalumPending;
                return true;
            }

            if (string.Equals(
                    profileKey,
                    DungeonNamedLifecycleCatalog.KhalumProfileKey,
                    StringComparison.Ordinal)
                && this.Phase == CapturedTempleMainRoomPhase.KhalumActive)
            {
                this.Phase = CapturedTempleMainRoomPhase.AzturPending;
                return true;
            }

            if (string.Equals(
                    profileKey,
                    DungeonNamedLifecycleCatalog.AzturProfileKey,
                    StringComparison.Ordinal)
                && this.Phase == CapturedTempleMainRoomPhase.AzturActive)
            {
                this.Phase = CapturedTempleMainRoomPhase.AzturDeadAwaitingDespawn;
                return true;
            }

            return false;
        }

        internal bool TryScheduleReset()
        {
            if (this.Phase != CapturedTempleMainRoomPhase.AzturDeadAwaitingDespawn)
            {
                return false;
            }

            this.Phase = CapturedTempleMainRoomPhase.ResetPending;
            return true;
        }

        internal void Dispose()
        {
            this.Phase = CapturedTempleMainRoomPhase.Disposed;
        }
    }
}
