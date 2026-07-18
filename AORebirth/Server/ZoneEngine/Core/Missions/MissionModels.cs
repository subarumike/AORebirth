namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    #endregion

    public enum MissionLifecycleState
    {
        Offered = 1,
        Active = 2,
        Completed = 3,
        Failed = 4,
        Abandoned = 5
    }

    public enum MissionRewardStatus
    {
        Pending = 1,
        InProgress = 2,
        Applied = 3,
        Failed = 4
    }

    public enum MissionRewardClaimStatus
    {
        Claimed = 1,
        AlreadyApplied = 2,
        Busy = 3,
        Rejected = 4
    }

    public enum MissionAtomicRewardStatus
    {
        Applied = 1,
        AlreadyApplied = 2,
        Rejected = 3
    }

    public enum MissionStatMutationKind
    {
        AddClamped = 1,
        Set = 2
    }

    public enum MissionOperationStatus
    {
        Applied = 1,
        AlreadyApplied = 2,
        DuplicateObservation = 3,
        Rejected = 4,
        NotFound = 5,
        Unresolved = 6
    }

    public enum MissionReloadReason
    {
        Login = 1,
        Reconnect = 2,
        Zoning = 3,
        ZoneEngineRestart = 4,
        Explicit = 5
    }

    public struct MissionKey : IEquatable<MissionKey>
    {
        public MissionKey(int characterId, string questId)
        {
            if (characterId <= 0)
            {
                throw new ArgumentOutOfRangeException("characterId", "Stable character identity must be positive.");
            }

            if (string.IsNullOrWhiteSpace(questId))
            {
                throw new ArgumentException("Quest identity is required.", "questId");
            }

            this.CharacterId = characterId;
            this.QuestId = questId.Trim();
        }

        public int CharacterId { get; private set; }

        public string QuestId { get; private set; }

        public bool Equals(MissionKey other)
        {
            return this.CharacterId == other.CharacterId
                   && string.Equals(this.QuestId, other.QuestId, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is MissionKey && this.Equals((MissionKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.CharacterId * 397)
                       ^ StringComparer.OrdinalIgnoreCase.GetHashCode(this.QuestId ?? string.Empty);
            }
        }

        public override string ToString()
        {
            return this.CharacterId + "|" + this.QuestId;
        }
    }

    public struct MissionObjectiveKey : IEquatable<MissionObjectiveKey>
    {
        public MissionObjectiveKey(MissionKey mission, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
            {
                throw new ArgumentException("Objective identity is required.", "objectiveId");
            }

            this.Mission = mission;
            this.ObjectiveId = objectiveId.Trim();
        }

        public MissionKey Mission { get; private set; }

        public string ObjectiveId { get; private set; }

        public bool Equals(MissionObjectiveKey other)
        {
            return this.Mission.Equals(other.Mission)
                   && string.Equals(this.ObjectiveId, other.ObjectiveId, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is MissionObjectiveKey && this.Equals((MissionObjectiveKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Mission.GetHashCode() * 397)
                       ^ StringComparer.OrdinalIgnoreCase.GetHashCode(this.ObjectiveId ?? string.Empty);
            }
        }

        public override string ToString()
        {
            return this.Mission + "|" + this.ObjectiveId;
        }
    }

    public struct MissionRewardKey : IEquatable<MissionRewardKey>
    {
        public MissionRewardKey(MissionKey mission, string rewardKey)
        {
            if (string.IsNullOrWhiteSpace(rewardKey))
            {
                throw new ArgumentException("Reward key is required.", "rewardKey");
            }

            this.Mission = mission;
            this.RewardKey = rewardKey.Trim();
        }

        public MissionKey Mission { get; private set; }

        public string RewardKey { get; private set; }

        public bool Equals(MissionRewardKey other)
        {
            return this.Mission.Equals(other.Mission)
                   && string.Equals(this.RewardKey, other.RewardKey, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is MissionRewardKey && this.Equals((MissionRewardKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Mission.GetHashCode() * 397)
                       ^ StringComparer.OrdinalIgnoreCase.GetHashCode(this.RewardKey ?? string.Empty);
            }
        }

        public override string ToString()
        {
            return this.Mission + "|" + this.RewardKey;
        }
    }

    public sealed class MissionDefinition
    {
        public MissionDefinition()
        {
            this.StepIds = new string[0];
            this.PrerequisiteQuestIds = new string[0];
            this.Objectives = new MissionObjectiveDefinition[0];
        }

        public string QuestId { get; set; }

        public string InitialStepId { get; set; }

        public bool IsResolved { get; set; }

        public IList<string> StepIds { get; set; }

        public IList<string> PrerequisiteQuestIds { get; set; }

        public IList<MissionObjectiveDefinition> Objectives { get; set; }
    }

    public sealed class MissionObjectiveDefinition
    {
        public string ObjectiveId { get; set; }

        public string StepId { get; set; }

        public int RequiredCount { get; set; }

        public bool IsResolved { get; set; }
    }

    public sealed class MissionStateRecord
    {
        public int CharacterId { get; set; }

        public string QuestId { get; set; }

        public MissionLifecycleState State { get; set; }

        public string CurrentStepId { get; set; }

        public long OfferedAtUtcTicks { get; set; }

        public long AcceptedAtUtcTicks { get; set; }

        public long CompletedAtUtcTicks { get; set; }

        public long FailedAtUtcTicks { get; set; }

        public long AbandonedAtUtcTicks { get; set; }

        public long CreatedAtUtcTicks { get; set; }

        public long UpdatedAtUtcTicks { get; set; }

        public long Version { get; set; }

        public MissionKey Key
        {
            get
            {
                return new MissionKey(this.CharacterId, this.QuestId);
            }
        }

        public MissionStateRecord Clone()
        {
            return (MissionStateRecord)this.MemberwiseClone();
        }
    }

    public sealed class MissionObjectiveProgressRecord
    {
        public int CharacterId { get; set; }

        public string QuestId { get; set; }

        public string ObjectiveId { get; set; }

        public int Progress { get; set; }

        public int RequiredCount { get; set; }

        public string LastObservationKey { get; set; }

        public long CreatedAtUtcTicks { get; set; }

        public long UpdatedAtUtcTicks { get; set; }

        public long Version { get; set; }

        public MissionObjectiveProgressRecord Clone()
        {
            return (MissionObjectiveProgressRecord)this.MemberwiseClone();
        }
    }

    public sealed class MissionObjectiveObservationRecord
    {
        public int CharacterId { get; set; }

        public string QuestId { get; set; }

        public string ObjectiveId { get; set; }

        public string ObservationKey { get; set; }

        public string EventType { get; set; }

        public string SourceIdentity { get; set; }

        public string TargetIdentity { get; set; }

        public long ObservedAtUtcTicks { get; set; }

        public MissionObjectiveKey ObjectiveKey
        {
            get
            {
                return new MissionObjectiveKey(
                    new MissionKey(this.CharacterId, this.QuestId),
                    this.ObjectiveId);
            }
        }

        public MissionObjectiveObservationRecord Clone()
        {
            return (MissionObjectiveObservationRecord)this.MemberwiseClone();
        }
    }

    public sealed class MissionFlagRecord
    {
        public int CharacterId { get; set; }

        public string QuestId { get; set; }

        public string FlagKey { get; set; }

        public string Value { get; set; }

        public long CreatedAtUtcTicks { get; set; }

        public long UpdatedAtUtcTicks { get; set; }

        public long Version { get; set; }

        public MissionFlagRecord Clone()
        {
            return (MissionFlagRecord)this.MemberwiseClone();
        }
    }

    public sealed class MissionAccountFlagRecord
    {
        public string AccountKey { get; set; }

        public string FlagKey { get; set; }

        public string Value { get; set; }

        public string SourceQuestId { get; set; }

        public long CreatedAtUtcTicks { get; set; }

        public long UpdatedAtUtcTicks { get; set; }

        public long Version { get; set; }

        public MissionAccountFlagRecord Clone()
        {
            return (MissionAccountFlagRecord)this.MemberwiseClone();
        }
    }

    public sealed class MissionRewardStageRecord
    {
        public int CharacterId { get; set; }

        public string QuestId { get; set; }

        public string RewardKey { get; set; }

        public string RewardType { get; set; }

        public MissionRewardStatus Status { get; set; }

        public int Attempts { get; set; }

        public string LastError { get; set; }

        public string EffectReference { get; set; }

        public string ClaimToken { get; set; }

        public long ClaimedAtUtcTicks { get; set; }

        public long ClaimExpiresAtUtcTicks { get; set; }

        public long AppliedAtUtcTicks { get; set; }

        public long CreatedAtUtcTicks { get; set; }

        public long UpdatedAtUtcTicks { get; set; }

        public long Version { get; set; }

        public MissionRewardStageRecord Clone()
        {
            return (MissionRewardStageRecord)this.MemberwiseClone();
        }
    }

    public sealed class MissionCharacterStatMutation
    {
        public int StatIdentityType { get; set; }

        public int StatId { get; set; }

        public MissionStatMutationKind Kind { get; set; }

        public long Value { get; set; }

        public long MinimumValue { get; set; }

        public long MaximumValue { get; set; }
    }

    public sealed class MissionCharacterStatValue
    {
        public int StatIdentityType { get; set; }

        public int StatId { get; set; }

        public long Value { get; set; }
    }

    public sealed class MissionCharacterSnapshot
    {
        public MissionCharacterSnapshot(
            int characterId,
            IEnumerable<MissionStateRecord> missions,
            IEnumerable<MissionObjectiveProgressRecord> objectives,
            IEnumerable<MissionFlagRecord> flags,
            IEnumerable<MissionRewardStageRecord> rewards)
        {
            this.CharacterId = characterId;
            this.Missions = (missions ?? Enumerable.Empty<MissionStateRecord>()).Select(value => value.Clone()).ToList();
            this.Objectives = (objectives ?? Enumerable.Empty<MissionObjectiveProgressRecord>()).Select(value => value.Clone()).ToList();
            this.Flags = (flags ?? Enumerable.Empty<MissionFlagRecord>()).Select(value => value.Clone()).ToList();
            this.Rewards = (rewards ?? Enumerable.Empty<MissionRewardStageRecord>()).Select(value => value.Clone()).ToList();
        }

        public int CharacterId { get; private set; }

        public IList<MissionStateRecord> Missions { get; private set; }

        public IList<MissionObjectiveProgressRecord> Objectives { get; private set; }

        public IList<MissionFlagRecord> Flags { get; private set; }

        public IList<MissionRewardStageRecord> Rewards { get; private set; }
    }

    public sealed class MissionRewardClaimResult
    {
        public MissionRewardClaimStatus Status { get; set; }

        public MissionRewardStageRecord Stage { get; set; }

        public string Message { get; set; }
    }

    public sealed class MissionAtomicStatRewardResult
    {
        public MissionAtomicRewardStatus Status { get; set; }

        public MissionRewardStageRecord Stage { get; set; }

        public IList<MissionCharacterStatValue> StatValues { get; set; }

        public string Message { get; set; }
    }

    public sealed class MissionObjectiveObservation
    {
        public int CharacterId { get; set; }

        public string QuestId { get; set; }

        public string ObjectiveId { get; set; }

        public string ObservationKey { get; set; }

        public int Amount { get; set; }

        public string EventType { get; set; }

        public string SourceIdentity { get; set; }

        public string TargetIdentity { get; set; }
    }

    public sealed class MissionOperationResult
    {
        public MissionOperationStatus Status { get; set; }

        public MissionStateRecord Mission { get; set; }

        public MissionObjectiveProgressRecord Objective { get; set; }

        public string Message { get; set; }

        public bool Succeeded
        {
            get
            {
                return this.Status == MissionOperationStatus.Applied
                       || this.Status == MissionOperationStatus.AlreadyApplied
                       || this.Status == MissionOperationStatus.DuplicateObservation;
            }
        }
    }

    public sealed class MissionReloadResult
    {
        public int CharacterId { get; set; }

        public MissionReloadReason Reason { get; set; }

        public MissionCharacterSnapshot Snapshot { get; set; }

        public bool ClientJournalReconciliationSupported { get; set; }
    }
}
