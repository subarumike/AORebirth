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

    /// <summary>
    /// Immutable identity relationship between one accepted generated mission and one captured
    /// objective slot. Mutable progress and reward delivery are held by
    /// <see cref="MissionAcgObjectiveState"/>.
    /// </summary>
    internal sealed class MissionAcgObjectiveBinding
    {
        internal const int CurrentFormatVersion = 1;

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
            if (formatVersion != CurrentFormatVersion
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
                updatedUtc ?? DateTime.UtcNow);
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
