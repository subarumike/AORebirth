namespace ZoneEngine.Core.Missions
{
    using System;

    internal enum MissionAcgObjectiveInteraction
    {
        TargetDeath = 1,
        InfoRequest = 2,
        StaticItemPickup = 3,
        ReturnItemToTerminal = 4,
        UseComponentOnMachine = 5
    }

    internal enum MissionAcgObjectiveLifecycle
    {
        Reserved = 1,
        Exposed = 2,
        ItemPossessed = 3,
        Verified = 4,
        CompletionStarted = 5,
        Completed = 6,
        Abandoned = 7,
        Expired = 8,
        CleanupCompleted = 9,
        Invalid = 10
    }

    internal enum MissionAcgCompletionPhase
    {
        None = 0,
        ObjectiveVerified = 1,
        CompletionStarted = 2,
        RewardCalculationFrozen = 3,
        RewardClaimStarted = 4,
        CreditsGranted = 5,
        XpGranted = 6,
        ItemRewardGrantedOrNone = 7,
        MissionArtifactsRemoved = 8,
        Action59Sent = 9,
        QuestDeleteSent = 10,
        ObjectiveCleanupCompleted = 11,
        MissionCleanupCompleted = 12
    }

    internal enum MissionAcgGrantState
    {
        NotStarted = 0,
        Pending = 1,
        Granted = 2,
        ExplicitNone = 3
    }

    internal enum MissionAcgDurableClaimPhase
    {
        Uninitialized = 0,
        NotEligible = 1,
        EligibleFrozen = 2,
        ClaimReserved = 3,
        ApplicationPending = 4,
        DurablyApplied = 5,
        ClientNotificationPending = 6,
        ClientNotificationSent = 7,
        TerminalFailure = 8
    }

    internal enum MissionAcgDeliveryPhase
    {
        NotStarted = 0,
        Pending = 1,
        Sent = 2,
        TerminalFailure = 3
    }

    internal sealed class MissionAcgDurableRewardClaim
    {
        internal MissionAcgDurableRewardClaim(
            MissionAcgDurableClaimPhase phase,
            string claimId,
            long amount,
            int itemLowId,
            int itemHighId,
            int itemQuality,
            int itemCount,
            MissionAcgIdentityRecord reservedItemIdentity,
            MissionAcgIdentityRecord targetContainerIdentity,
            long preApplyValue,
            long expectedPostValue,
            string preApplyFingerprint,
            string failure)
        {
            if (!Enum.IsDefined(typeof(MissionAcgDurableClaimPhase), phase)
                || amount < 0
                || itemCount < 0)
            {
                throw new ArgumentException("Durable reward claim is invalid.");
            }

            this.Phase = phase;
            this.ClaimId = claimId ?? string.Empty;
            this.Amount = amount;
            this.ItemLowId = itemLowId;
            this.ItemHighId = itemHighId;
            this.ItemQuality = itemQuality;
            this.ItemCount = itemCount;
            this.ReservedItemIdentity = reservedItemIdentity;
            this.TargetContainerIdentity = targetContainerIdentity;
            this.PreApplyValue = preApplyValue;
            this.ExpectedPostValue = expectedPostValue;
            this.PreApplyFingerprint = preApplyFingerprint ?? string.Empty;
            this.Failure = failure ?? string.Empty;
        }

        internal MissionAcgDurableClaimPhase Phase { get; private set; }

        internal string ClaimId { get; private set; }

        internal long Amount { get; private set; }

        internal int ItemLowId { get; private set; }

        internal int ItemHighId { get; private set; }

        internal int ItemQuality { get; private set; }

        internal int ItemCount { get; private set; }

        internal MissionAcgIdentityRecord ReservedItemIdentity { get; private set; }

        internal MissionAcgIdentityRecord TargetContainerIdentity { get; private set; }

        internal long PreApplyValue { get; private set; }

        internal long ExpectedPostValue { get; private set; }

        internal string PreApplyFingerprint { get; private set; }

        internal string Failure { get; private set; }

        internal MissionAcgDurableRewardClaim Copy(
            MissionAcgDurableClaimPhase? phase = null,
            string claimId = null,
            long? amount = null,
            int? itemLowId = null,
            int? itemHighId = null,
            int? itemQuality = null,
            int? itemCount = null,
            MissionAcgIdentityRecord reservedItemIdentity = null,
            bool preserveReservedItemWhenNull = true,
            MissionAcgIdentityRecord targetContainerIdentity = null,
            bool preserveTargetContainerWhenNull = true,
            long? preApplyValue = null,
            long? expectedPostValue = null,
            string preApplyFingerprint = null,
            string failure = null)
        {
            return new MissionAcgDurableRewardClaim(
                phase ?? this.Phase,
                claimId ?? this.ClaimId,
                amount ?? this.Amount,
                itemLowId ?? this.ItemLowId,
                itemHighId ?? this.ItemHighId,
                itemQuality ?? this.ItemQuality,
                itemCount ?? this.ItemCount,
                reservedItemIdentity != null || !preserveReservedItemWhenNull
                    ? reservedItemIdentity
                    : this.ReservedItemIdentity,
                targetContainerIdentity != null || !preserveTargetContainerWhenNull
                    ? targetContainerIdentity
                    : this.TargetContainerIdentity,
                preApplyValue ?? this.PreApplyValue,
                expectedPostValue ?? this.ExpectedPostValue,
                preApplyFingerprint ?? this.PreApplyFingerprint,
                failure ?? this.Failure);
        }

        internal static MissionAcgDurableRewardClaim Empty(
            MissionAcgDurableClaimPhase phase)
        {
            return new MissionAcgDurableRewardClaim(
                phase,
                string.Empty,
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                0,
                0,
                string.Empty,
                string.Empty);
        }
    }

    /// <summary>
    /// Immutable identity relationship between one accepted generated mission and one captured
    /// objective slot. Mutable progress and reward delivery are held by
    /// <see cref="MissionAcgObjectiveState"/>.
    /// </summary>
    internal sealed class MissionAcgObjectiveBinding
    {
        internal const int CurrentFormatVersion = 2;

        internal const int LegacyFormatVersion = 1;

        internal MissionAcgObjectiveBinding(
            int formatVersion,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord ownerIdentity,
            MissionAcgIdentityRecord teamIdentity,
            bool explicitNoTeam,
            MissionRollType missionType,
            int allocatedLivePlayfield2,
            string bundleId,
            string bundlePayloadSha256,
            MissionAcgIdentityRecord buildingIdentity,
            int capturedObjectiveSlot,
            MissionAcgIdentityRecord capturedObjectiveIdentity,
            MissionAcgIdentityRecord runtimeObjectiveIdentity,
            int objectiveTemplateId,
            string objectiveName,
            MissionAcgObjectiveInteraction requiredInteraction,
            MissionAcgIdentityRecord issuingTerminalIdentity,
            int requiredMissionItemTemplateId,
            int requiredMachineTemplateId)
        {
            if ((formatVersion != CurrentFormatVersion
                 && formatVersion != LegacyFormatVersion)
                || acceptedQuestIdentity == null
                || ownerIdentity == null
                || buildingIdentity == null
                || capturedObjectiveIdentity == null
                || runtimeObjectiveIdentity == null
                || string.IsNullOrWhiteSpace(bundleId)
                || string.IsNullOrWhiteSpace(bundlePayloadSha256)
                || capturedObjectiveSlot < 0
                || allocatedLivePlayfield2 <= 0
                || !Enum.IsDefined(typeof(MissionRollType), missionType)
                || missionType == MissionRollType.Unknown
                || !Enum.IsDefined(typeof(MissionAcgObjectiveInteraction), requiredInteraction)
                || explicitNoTeam == (teamIdentity != null))
            {
                throw new ArgumentException("Objective binding identity is incomplete.");
            }

            this.FormatVersion = formatVersion;
            this.AcceptedQuestIdentity = acceptedQuestIdentity;
            this.OwnerIdentity = ownerIdentity;
            this.TeamIdentity = teamIdentity;
            this.ExplicitNoTeam = explicitNoTeam;
            this.MissionType = missionType;
            this.AllocatedLivePlayfield2 = allocatedLivePlayfield2;
            this.BundleId = bundleId.Trim();
            this.BundlePayloadSha256 = bundlePayloadSha256.Trim().ToLowerInvariant();
            this.BuildingIdentity = buildingIdentity;
            this.CapturedObjectiveSlot = capturedObjectiveSlot;
            this.CapturedObjectiveIdentity = capturedObjectiveIdentity;
            this.RuntimeObjectiveIdentity = runtimeObjectiveIdentity;
            this.ObjectiveTemplateId = objectiveTemplateId;
            this.ObjectiveName = (objectiveName ?? string.Empty).Trim();
            this.RequiredInteraction = requiredInteraction;
            this.IssuingTerminalIdentity = issuingTerminalIdentity;
            this.RequiredMissionItemTemplateId = requiredMissionItemTemplateId;
            this.RequiredMachineTemplateId = requiredMachineTemplateId;
        }

        internal int FormatVersion { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity { get; private set; }

        internal MissionAcgIdentityRecord OwnerIdentity { get; private set; }

        internal MissionAcgIdentityRecord TeamIdentity { get; private set; }

        internal bool ExplicitNoTeam { get; private set; }

        internal MissionRollType MissionType { get; private set; }

        internal int AllocatedLivePlayfield2 { get; private set; }

        internal string BundleId { get; private set; }

        internal string BundlePayloadSha256 { get; private set; }

        internal MissionAcgIdentityRecord BuildingIdentity { get; private set; }

        internal int CapturedObjectiveSlot { get; private set; }

        internal MissionAcgIdentityRecord CapturedObjectiveIdentity { get; private set; }

        internal MissionAcgIdentityRecord RuntimeObjectiveIdentity { get; private set; }

        internal int ObjectiveTemplateId { get; private set; }

        internal string ObjectiveName { get; private set; }

        internal MissionAcgObjectiveInteraction RequiredInteraction { get; private set; }

        internal MissionAcgIdentityRecord IssuingTerminalIdentity { get; private set; }

        internal int RequiredMissionItemTemplateId { get; private set; }

        internal int RequiredMachineTemplateId { get; private set; }

        internal MissionAcgObjectiveBinding WithFormatVersion(int formatVersion)
        {
            return new MissionAcgObjectiveBinding(
                formatVersion,
                this.AcceptedQuestIdentity,
                this.OwnerIdentity,
                this.TeamIdentity,
                this.ExplicitNoTeam,
                this.MissionType,
                this.AllocatedLivePlayfield2,
                this.BundleId,
                this.BundlePayloadSha256,
                this.BuildingIdentity,
                this.CapturedObjectiveSlot,
                this.CapturedObjectiveIdentity,
                this.RuntimeObjectiveIdentity,
                this.ObjectiveTemplateId,
                this.ObjectiveName,
                this.RequiredInteraction,
                this.IssuingTerminalIdentity,
                this.RequiredMissionItemTemplateId,
                this.RequiredMachineTemplateId);
        }
    }

    internal sealed class MissionAcgObjectiveState
    {
        internal MissionAcgObjectiveState(
            MissionAcgObjectiveLifecycle lifecycle,
            MissionAcgCompletionPhase phase,
            MissionAcgIdentityRecord missionItemIdentity,
            int frozenCredits,
            int frozenXp,
            int frozenItemLowId,
            int frozenItemHighId,
            int frozenItemQuality,
            int frozenItemCount,
            MissionAcgGrantState creditsState,
            MissionAcgGrantState xpState,
            MissionAcgGrantState itemState,
            string creditsClaimId,
            string xpClaimId,
            string itemClaimId,
            int grantedRewardItemInstance,
            bool artifactsRemoved,
            bool action59Sent,
            bool questDeleteSent,
            bool objectiveCleanupCompleted,
            bool missionCleanupCompleted,
            DateTime updatedUtc)
        {
            if (!Enum.IsDefined(typeof(MissionAcgObjectiveLifecycle), lifecycle)
                || !Enum.IsDefined(typeof(MissionAcgCompletionPhase), phase)
                || !Enum.IsDefined(typeof(MissionAcgGrantState), creditsState)
                || !Enum.IsDefined(typeof(MissionAcgGrantState), xpState)
                || !Enum.IsDefined(typeof(MissionAcgGrantState), itemState)
                || frozenCredits < 0
                || frozenXp < 0
                || frozenItemCount < 0
                || updatedUtc == DateTime.MinValue)
            {
                throw new ArgumentException("Objective mutable state is invalid.");
            }

            this.Lifecycle = lifecycle;
            this.Phase = phase;
            this.MissionItemIdentity = missionItemIdentity;
            this.FrozenCredits = frozenCredits;
            this.FrozenXp = frozenXp;
            this.FrozenItemLowId = frozenItemLowId;
            this.FrozenItemHighId = frozenItemHighId;
            this.FrozenItemQuality = frozenItemQuality;
            this.FrozenItemCount = frozenItemCount;
            this.CreditsState = creditsState;
            this.XpState = xpState;
            this.ItemState = itemState;
            this.CreditsClaimId = creditsClaimId ?? string.Empty;
            this.XpClaimId = xpClaimId ?? string.Empty;
            this.ItemClaimId = itemClaimId ?? string.Empty;
            this.GrantedRewardItemInstance = grantedRewardItemInstance;
            this.ArtifactsRemoved = artifactsRemoved;
            this.Action59Sent = action59Sent;
            this.QuestDeleteSent = questDeleteSent;
            this.ObjectiveCleanupCompleted = objectiveCleanupCompleted;
            this.MissionCleanupCompleted = missionCleanupCompleted;
            this.UpdatedUtc =
                updatedUtc.Kind == DateTimeKind.Utc
                    ? updatedUtc
                    : updatedUtc.ToUniversalTime();
            this.CreditsClaim = FromLegacyGrant(
                creditsState,
                this.CreditsClaimId,
                frozenCredits,
                0,
                0,
                0,
                0,
                grantedRewardItemInstance,
                "credits");
            this.XpClaim = FromLegacyGrant(
                xpState,
                this.XpClaimId,
                frozenXp,
                0,
                0,
                0,
                0,
                0,
                "xp");
            this.ItemClaim = FromLegacyGrant(
                itemState,
                this.ItemClaimId,
                0,
                frozenItemLowId,
                frozenItemHighId,
                frozenItemQuality,
                frozenItemCount,
                grantedRewardItemInstance,
                "item");
            this.TokenClaim = MissionAcgDurableRewardClaim.Empty(
                MissionAcgDurableClaimPhase.Uninitialized);
            this.RewardFeedbackDelivery = MissionAcgDeliveryPhase.NotStarted;
            this.MissionAccomplishedDelivery = MissionAcgDeliveryPhase.NotStarted;
            this.Action59Delivery =
                action59Sent
                    ? MissionAcgDeliveryPhase.Sent
                    : MissionAcgDeliveryPhase.NotStarted;
            this.QuestDeleteDelivery =
                questDeleteSent
                    ? MissionAcgDeliveryPhase.Sent
                    : MissionAcgDeliveryPhase.NotStarted;
            this.MissionListRemovalDelivery = MissionAcgDeliveryPhase.NotStarted;
            this.CleanupHandoffDelivery =
                missionCleanupCompleted
                    ? MissionAcgDeliveryPhase.Sent
                    : MissionAcgDeliveryPhase.NotStarted;
            if (phase >= MissionAcgCompletionPhase.MissionArtifactsRemoved)
            {
                this.CreditsClaim = LegacyNotificationSent(this.CreditsClaim);
                this.XpClaim = LegacyNotificationSent(this.XpClaim);
                this.ItemClaim = LegacyNotificationSent(this.ItemClaim);
                this.RewardFeedbackDelivery = MissionAcgDeliveryPhase.Sent;
                this.MissionAccomplishedDelivery = MissionAcgDeliveryPhase.Sent;
            }
        }

        internal MissionAcgObjectiveState(
            MissionAcgObjectiveLifecycle lifecycle,
            MissionAcgCompletionPhase phase,
            MissionAcgIdentityRecord missionItemIdentity,
            int frozenCredits,
            int frozenXp,
            int frozenItemLowId,
            int frozenItemHighId,
            int frozenItemQuality,
            int frozenItemCount,
            MissionAcgGrantState creditsState,
            MissionAcgGrantState xpState,
            MissionAcgGrantState itemState,
            string creditsClaimId,
            string xpClaimId,
            string itemClaimId,
            int grantedRewardItemInstance,
            bool artifactsRemoved,
            bool action59Sent,
            bool questDeleteSent,
            bool objectiveCleanupCompleted,
            bool missionCleanupCompleted,
            DateTime updatedUtc,
            MissionAcgDurableRewardClaim creditsClaim,
            MissionAcgDurableRewardClaim xpClaim,
            MissionAcgDurableRewardClaim itemClaim,
            MissionAcgDurableRewardClaim tokenClaim,
            MissionAcgDeliveryPhase rewardFeedbackDelivery,
            MissionAcgDeliveryPhase missionAccomplishedDelivery,
            MissionAcgDeliveryPhase action59Delivery,
            MissionAcgDeliveryPhase questDeleteDelivery,
            MissionAcgDeliveryPhase missionListRemovalDelivery,
            MissionAcgDeliveryPhase cleanupHandoffDelivery)
            : this(
                lifecycle,
                phase,
                missionItemIdentity,
                frozenCredits,
                frozenXp,
                frozenItemLowId,
                frozenItemHighId,
                frozenItemQuality,
                frozenItemCount,
                creditsState,
                xpState,
                itemState,
                creditsClaimId,
                xpClaimId,
                itemClaimId,
                grantedRewardItemInstance,
                artifactsRemoved,
                action59Sent,
                questDeleteSent,
                objectiveCleanupCompleted,
                missionCleanupCompleted,
                updatedUtc)
        {
            if (creditsClaim == null
                || xpClaim == null
                || itemClaim == null
                || tokenClaim == null
                || !Enum.IsDefined(typeof(MissionAcgDeliveryPhase), rewardFeedbackDelivery)
                || !Enum.IsDefined(typeof(MissionAcgDeliveryPhase), missionAccomplishedDelivery)
                || !Enum.IsDefined(typeof(MissionAcgDeliveryPhase), action59Delivery)
                || !Enum.IsDefined(typeof(MissionAcgDeliveryPhase), questDeleteDelivery)
                || !Enum.IsDefined(typeof(MissionAcgDeliveryPhase), missionListRemovalDelivery)
                || !Enum.IsDefined(typeof(MissionAcgDeliveryPhase), cleanupHandoffDelivery))
            {
                throw new ArgumentException("Durable completion recovery state is invalid.");
            }

            this.CreditsClaim = creditsClaim;
            this.XpClaim = xpClaim;
            this.ItemClaim = itemClaim;
            this.TokenClaim = tokenClaim;
            this.RewardFeedbackDelivery = rewardFeedbackDelivery;
            this.MissionAccomplishedDelivery = missionAccomplishedDelivery;
            this.Action59Delivery = action59Delivery;
            this.QuestDeleteDelivery = questDeleteDelivery;
            this.MissionListRemovalDelivery = missionListRemovalDelivery;
            this.CleanupHandoffDelivery = cleanupHandoffDelivery;
        }

        internal MissionAcgObjectiveLifecycle Lifecycle { get; private set; }

        internal MissionAcgCompletionPhase Phase { get; private set; }

        internal MissionAcgIdentityRecord MissionItemIdentity { get; private set; }

        internal int FrozenCredits { get; private set; }

        internal int FrozenXp { get; private set; }

        internal int FrozenItemLowId { get; private set; }

        internal int FrozenItemHighId { get; private set; }

        internal int FrozenItemQuality { get; private set; }

        internal int FrozenItemCount { get; private set; }

        internal MissionAcgGrantState CreditsState { get; private set; }

        internal MissionAcgGrantState XpState { get; private set; }

        internal MissionAcgGrantState ItemState { get; private set; }

        internal string CreditsClaimId { get; private set; }

        internal string XpClaimId { get; private set; }

        internal string ItemClaimId { get; private set; }

        internal int GrantedRewardItemInstance { get; private set; }

        internal bool ArtifactsRemoved { get; private set; }

        internal bool Action59Sent { get; private set; }

        internal bool QuestDeleteSent { get; private set; }

        internal bool ObjectiveCleanupCompleted { get; private set; }

        internal bool MissionCleanupCompleted { get; private set; }

        internal DateTime UpdatedUtc { get; private set; }

        internal MissionAcgDurableRewardClaim CreditsClaim { get; private set; }

        internal MissionAcgDurableRewardClaim XpClaim { get; private set; }

        internal MissionAcgDurableRewardClaim ItemClaim { get; private set; }

        internal MissionAcgDurableRewardClaim TokenClaim { get; private set; }

        internal MissionAcgDeliveryPhase RewardFeedbackDelivery { get; private set; }

        internal MissionAcgDeliveryPhase MissionAccomplishedDelivery { get; private set; }

        internal MissionAcgDeliveryPhase Action59Delivery { get; private set; }

        internal MissionAcgDeliveryPhase QuestDeleteDelivery { get; private set; }

        internal MissionAcgDeliveryPhase MissionListRemovalDelivery { get; private set; }

        internal MissionAcgDeliveryPhase CleanupHandoffDelivery { get; private set; }

        internal MissionAcgObjectiveState Copy(
            MissionAcgObjectiveLifecycle? lifecycle = null,
            MissionAcgCompletionPhase? phase = null,
            MissionAcgIdentityRecord missionItemIdentity = null,
            bool preserveMissionItemWhenNull = true,
            int? frozenCredits = null,
            int? frozenXp = null,
            int? frozenItemLowId = null,
            int? frozenItemHighId = null,
            int? frozenItemQuality = null,
            int? frozenItemCount = null,
            MissionAcgGrantState? creditsState = null,
            MissionAcgGrantState? xpState = null,
            MissionAcgGrantState? itemState = null,
            string creditsClaimId = null,
            string xpClaimId = null,
            string itemClaimId = null,
            int? grantedRewardItemInstance = null,
            bool? artifactsRemoved = null,
            bool? action59Sent = null,
            bool? questDeleteSent = null,
            bool? objectiveCleanupCompleted = null,
            bool? missionCleanupCompleted = null,
            MissionAcgDurableRewardClaim creditsClaim = null,
            MissionAcgDurableRewardClaim xpClaim = null,
            MissionAcgDurableRewardClaim itemClaim = null,
            MissionAcgDurableRewardClaim tokenClaim = null,
            MissionAcgDeliveryPhase? rewardFeedbackDelivery = null,
            MissionAcgDeliveryPhase? missionAccomplishedDelivery = null,
            MissionAcgDeliveryPhase? action59Delivery = null,
            MissionAcgDeliveryPhase? questDeleteDelivery = null,
            MissionAcgDeliveryPhase? missionListRemovalDelivery = null,
            MissionAcgDeliveryPhase? cleanupHandoffDelivery = null,
            DateTime? updatedUtc = null)
        {
            return new MissionAcgObjectiveState(
                lifecycle ?? this.Lifecycle,
                phase ?? this.Phase,
                missionItemIdentity != null || !preserveMissionItemWhenNull
                    ? missionItemIdentity
                    : this.MissionItemIdentity,
                frozenCredits ?? this.FrozenCredits,
                frozenXp ?? this.FrozenXp,
                frozenItemLowId ?? this.FrozenItemLowId,
                frozenItemHighId ?? this.FrozenItemHighId,
                frozenItemQuality ?? this.FrozenItemQuality,
                frozenItemCount ?? this.FrozenItemCount,
                creditsState ?? this.CreditsState,
                xpState ?? this.XpState,
                itemState ?? this.ItemState,
                creditsClaimId ?? this.CreditsClaimId,
                xpClaimId ?? this.XpClaimId,
                itemClaimId ?? this.ItemClaimId,
                grantedRewardItemInstance ?? this.GrantedRewardItemInstance,
                artifactsRemoved ?? this.ArtifactsRemoved,
                action59Sent ?? this.Action59Sent,
                questDeleteSent ?? this.QuestDeleteSent,
                objectiveCleanupCompleted ?? this.ObjectiveCleanupCompleted,
                missionCleanupCompleted ?? this.MissionCleanupCompleted,
                updatedUtc ?? DateTime.UtcNow,
                creditsClaim ?? this.CreditsClaim,
                xpClaim ?? this.XpClaim,
                itemClaim ?? this.ItemClaim,
                tokenClaim ?? this.TokenClaim,
                rewardFeedbackDelivery ?? this.RewardFeedbackDelivery,
                missionAccomplishedDelivery ?? this.MissionAccomplishedDelivery,
                action59Delivery ?? this.Action59Delivery,
                questDeleteDelivery ?? this.QuestDeleteDelivery,
                missionListRemovalDelivery ?? this.MissionListRemovalDelivery,
                cleanupHandoffDelivery ?? this.CleanupHandoffDelivery);
        }

        private static MissionAcgDurableRewardClaim FromLegacyGrant(
            MissionAcgGrantState state,
            string claimId,
            long amount,
            int itemLowId,
            int itemHighId,
            int itemQuality,
            int itemCount,
            int grantedItemInstance,
            string component)
        {
            MissionAcgDurableClaimPhase phase;
            string failure = string.Empty;
            switch (state)
            {
                case MissionAcgGrantState.ExplicitNone:
                    phase = MissionAcgDurableClaimPhase.NotEligible;
                    break;
                case MissionAcgGrantState.NotStarted:
                    phase = string.IsNullOrEmpty(claimId)
                                ? MissionAcgDurableClaimPhase.Uninitialized
                                : MissionAcgDurableClaimPhase.EligibleFrozen;
                    break;
                case MissionAcgGrantState.Granted:
                    phase = MissionAcgDurableClaimPhase.DurablyApplied;
                    break;
                default:
                    phase = MissionAcgDurableClaimPhase.TerminalFailure;
                    failure =
                        "Legacy " + component
                        + " application was pending and cannot be replayed safely.";
                    break;
            }

            return new MissionAcgDurableRewardClaim(
                phase,
                claimId,
                amount,
                itemLowId,
                itemHighId,
                itemQuality,
                itemCount,
                grantedItemInstance == 0
                    ? null
                    : new MissionAcgIdentityRecord(0x0000C76D, grantedItemInstance),
                null,
                0,
                0,
                string.Empty,
                failure);
        }

        private static MissionAcgDurableRewardClaim LegacyNotificationSent(
            MissionAcgDurableRewardClaim claim)
        {
            return claim.Phase == MissionAcgDurableClaimPhase.DurablyApplied
                       ? claim.Copy(
                           phase: MissionAcgDurableClaimPhase.ClientNotificationSent)
                       : claim;
        }
    }

    internal sealed class MissionAcgObjectiveRecord
    {
        internal MissionAcgObjectiveRecord(
            MissionAcgObjectiveBinding binding,
            MissionAcgObjectiveState state,
            string recordPath)
        {
            if (binding == null || state == null)
            {
                throw new ArgumentNullException(binding == null ? "binding" : "state");
            }

            this.Binding = binding;
            this.State = state;
            this.RecordPath = recordPath ?? string.Empty;
        }

        internal MissionAcgObjectiveBinding Binding { get; private set; }

        internal MissionAcgObjectiveState State { get; private set; }

        internal string RecordPath { get; private set; }

        internal MissionAcgObjectiveRecord WithState(MissionAcgObjectiveState state)
        {
            return new MissionAcgObjectiveRecord(this.Binding, state, this.RecordPath);
        }
    }
}
