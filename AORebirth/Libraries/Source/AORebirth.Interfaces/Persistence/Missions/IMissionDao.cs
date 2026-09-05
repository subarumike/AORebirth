namespace AORebirth.Interfaces.Persistence.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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

    public enum MissionRollFeeStatus
    {
        Applied = 1,
        AlreadyApplied = 2,
        InsufficientCredits = 3,
        Conflict = 4
    }

    public struct MissionKeyData : IEquatable<MissionKeyData>
    {
        public MissionKeyData(int characterId, string questId)
        {
            this.CharacterId = characterId;
            this.QuestId = questId;
        }

        public int CharacterId { get; private set; }

        public string QuestId { get; private set; }

        public bool Equals(MissionKeyData other)
        {
            return this.CharacterId == other.CharacterId
                   && string.Equals(this.QuestId, other.QuestId, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is MissionKeyData && this.Equals((MissionKeyData)obj);
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

    public struct MissionObjectiveKeyData : IEquatable<MissionObjectiveKeyData>
    {
        public MissionObjectiveKeyData(MissionKeyData mission, string objectiveId)
        {
            this.Mission = mission;
            this.ObjectiveId = objectiveId;
        }

        public MissionKeyData Mission { get; private set; }

        public string ObjectiveId { get; private set; }

        public bool Equals(MissionObjectiveKeyData other)
        {
            return this.Mission.Equals(other.Mission)
                   && string.Equals(this.ObjectiveId, other.ObjectiveId, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is MissionObjectiveKeyData && this.Equals((MissionObjectiveKeyData)obj);
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

    public struct MissionRewardKeyData : IEquatable<MissionRewardKeyData>
    {
        public MissionRewardKeyData(MissionKeyData mission, string rewardKey)
        {
            this.Mission = mission;
            this.RewardKey = rewardKey;
        }

        public MissionKeyData Mission { get; private set; }

        public string RewardKey { get; private set; }

        public bool Equals(MissionRewardKeyData other)
        {
            return this.Mission.Equals(other.Mission)
                   && string.Equals(this.RewardKey, other.RewardKey, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is MissionRewardKeyData && this.Equals((MissionRewardKeyData)obj);
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

    public sealed class MissionStateData
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

        public MissionStateData Clone()
        {
            return (MissionStateData)this.MemberwiseClone();
        }
    }

    public sealed class MissionObjectiveProgressData
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

        public MissionObjectiveProgressData Clone()
        {
            return (MissionObjectiveProgressData)this.MemberwiseClone();
        }
    }

    public sealed class MissionObjectiveObservationData
    {
        public int CharacterId { get; set; }
        public string QuestId { get; set; }
        public string ObjectiveId { get; set; }
        public string ObservationKey { get; set; }
        public string EventType { get; set; }
        public string SourceIdentity { get; set; }
        public string TargetIdentity { get; set; }
        public long ObservedAtUtcTicks { get; set; }

        public MissionObjectiveKeyData ObjectiveKey
        {
            get
            {
                return new MissionObjectiveKeyData(
                    new MissionKeyData(this.CharacterId, this.QuestId),
                    this.ObjectiveId);
            }
        }
    }

    public sealed class MissionFlagData
    {
        public int CharacterId { get; set; }
        public string QuestId { get; set; }
        public string FlagKey { get; set; }
        public string Value { get; set; }
        public long CreatedAtUtcTicks { get; set; }
        public long UpdatedAtUtcTicks { get; set; }
        public long Version { get; set; }

        public MissionFlagData Clone()
        {
            return (MissionFlagData)this.MemberwiseClone();
        }
    }

    public sealed class MissionAccountFlagData
    {
        public string AccountKey { get; set; }
        public string FlagKey { get; set; }
        public string Value { get; set; }
        public string SourceQuestId { get; set; }
        public long CreatedAtUtcTicks { get; set; }
        public long UpdatedAtUtcTicks { get; set; }
        public long Version { get; set; }

        public MissionAccountFlagData Clone()
        {
            return (MissionAccountFlagData)this.MemberwiseClone();
        }
    }

    public sealed class MissionRewardStageData
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

        public MissionRewardStageData Clone()
        {
            return (MissionRewardStageData)this.MemberwiseClone();
        }
    }

    public sealed class MissionStatMutationData
    {
        public int StatIdentityType { get; set; }
        public int StatId { get; set; }
        public MissionStatMutationKind Kind { get; set; }
        public long Value { get; set; }
        public long MinimumValue { get; set; }
        public long MaximumValue { get; set; }
    }

    public sealed class MissionStatValueData
    {
        public int StatIdentityType { get; set; }
        public int StatId { get; set; }
        public long Value { get; set; }
    }

    public sealed class MissionCharacterSnapshotData
    {
        public MissionCharacterSnapshotData(
            int characterId,
            IEnumerable<MissionStateData> missions,
            IEnumerable<MissionObjectiveProgressData> objectives,
            IEnumerable<MissionFlagData> flags,
            IEnumerable<MissionRewardStageData> rewards)
        {
            this.CharacterId = characterId;
            this.Missions = (missions ?? Enumerable.Empty<MissionStateData>()).Select(value => value.Clone()).ToList();
            this.Objectives = (objectives ?? Enumerable.Empty<MissionObjectiveProgressData>()).Select(value => value.Clone()).ToList();
            this.Flags = (flags ?? Enumerable.Empty<MissionFlagData>()).Select(value => value.Clone()).ToList();
            this.Rewards = (rewards ?? Enumerable.Empty<MissionRewardStageData>()).Select(value => value.Clone()).ToList();
        }

        public int CharacterId { get; private set; }
        public IList<MissionStateData> Missions { get; private set; }
        public IList<MissionObjectiveProgressData> Objectives { get; private set; }
        public IList<MissionFlagData> Flags { get; private set; }
        public IList<MissionRewardStageData> Rewards { get; private set; }
    }

    public sealed class MissionRewardClaimResultData
    {
        public MissionRewardClaimStatus Status { get; set; }
        public MissionRewardStageData Stage { get; set; }
        public string Message { get; set; }
    }

    public sealed class MissionAtomicStatRewardResultData
    {
        public MissionAtomicRewardStatus Status { get; set; }
        public MissionRewardStageData Stage { get; set; }
        public IList<MissionStatValueData> StatValues { get; set; }
        public string Message { get; set; }
    }

    public sealed class MissionRollFeeRequest
    {
        public int CharacterType { get; set; }
        public int CharacterId { get; set; }
        public string BatchIdentity { get; set; }
        public int Fee { get; set; }
        public long AppliedAtUtcTicks { get; set; }
    }

    public sealed class MissionRollFeeResult
    {
        public MissionRollFeeStatus Status { get; set; }
        public int CashBefore { get; set; }
        public int CashAfter { get; set; }
        public string Failure { get; set; }
    }

    public static class MissionStartAreaSelectionStates
    {
        public const string Pending = "pending";
        public const string Arete = "arete";
        public const string IccShuttleport = "icc_shuttleport";
    }

    /// <summary>
    /// Buffered mission persistence, independent of engine/session/packet objects.
    /// Mutations belong to Execute; the implementation owns connection and commit.
    /// Not-found reads return null; list reads return an empty list when absent.
    /// Database failures propagate, except the legacy start-area convenience methods
    /// which return false/null on failure. They must not be used to infer absence
    /// after a database outage. No method creates or migrates schema.
    /// </summary>
    public interface IMissionDao
    {
        MissionStateData GetMission(MissionKeyData key);
        IList<MissionStateData> GetMissions(int characterId);
        MissionCharacterSnapshotData ReadCharacter(int characterId);
        string ResolveCharacterAccountKey(int characterId);
        MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey);
        IList<MissionAccountFlagData> GetAccountFlags(string accountKey);
        MissionRollFeeResult TryChargeRollFee(MissionRollFeeRequest request);
        bool MarkStartAreaSelectionPending(int characterId);
        string GetStartAreaSelectionState(int characterId);
        bool TryCompleteStartAreaSelection(int characterId, string selectedState);
        /// <summary>
        /// Runs one synchronous, character-scoped transaction. Keep effects outside
        /// persistence (packets, inventory items, playfields) outside the callback.
        /// Do not return a Task or retain/share the transaction. Exceptions should
        /// escape the callback; failed writes prevent commit even if caught there.
        /// The DAO does not retry callbacks or perform nested transaction enlistment.
        /// If rollback also fails, the original exception is rethrown with that
        /// exception in Data["MissionDao.RollbackFailure"]. Reconcile before retrying.
        /// </summary>
        T Execute<T>(int characterId, Func<IMissionDaoTransaction, T> operation);
        /// <summary>As above, with a locked character/account ownership check for account flags.</summary>
        T Execute<T>(int characterId, string accountKey, Func<IMissionDaoTransaction, T> operation);
    }

    /// <summary>
    /// Valid only during the enclosing Execute callback, on one thread. Save methods
    /// update DTO Version for subsequent writes in that callback; rollback restores
    /// those version values. Discard/reload other returned data after any failure.
    /// After a connection or commit failure the durable outcome may be unknown:
    /// reconcile from the database before retrying, using the same durable keys.
    /// An insert uses Version &lt;= 0; an update requires the current positive Version.
    /// </summary>
    public interface IMissionDaoTransaction
    {
        int CharacterId { get; }
        string AccountKey { get; }
        MissionStateData GetMission(MissionKeyData key);
        IList<MissionStateData> GetMissions(int characterId);
        void SaveMission(MissionKeyData key, MissionStateData record);
        MissionObjectiveProgressData GetObjective(MissionObjectiveKeyData key);
        void SaveObjective(MissionObjectiveKeyData key, MissionObjectiveProgressData record);
        bool TryAddObservation(MissionObjectiveObservationData observation);
        MissionFlagData GetFlag(MissionKeyData key, string flagKey);
        void SaveFlag(MissionKeyData key, MissionFlagData flag);
        MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey);
        void SaveAccountFlag(string accountKey, MissionAccountFlagData flag);
        MissionRewardStageData GetReward(MissionRewardKeyData key);
        MissionRewardClaimResultData TryClaimReward(
            MissionRewardKeyData key,
            string rewardType,
            string claimToken,
            long claimedAtUtcTicks,
            long claimExpiresAtUtcTicks);
        bool TryMarkRewardApplied(
            MissionRewardKeyData key,
            string claimToken,
            long expectedVersion,
            string effectReference,
            long appliedAtUtcTicks,
            out MissionRewardStageData stage);
        bool TryMarkRewardFailed(
            MissionRewardKeyData key,
            string claimToken,
            long expectedVersion,
            string error,
            long failedAtUtcTicks,
            out MissionRewardStageData stage);
        MissionAtomicStatRewardResultData TryApplyCharacterStatReward(
            MissionRewardKeyData key,
            string rewardType,
            IList<MissionStatMutationData> mutations,
            string effectReference,
            long appliedAtUtcTicks);
    }
}
