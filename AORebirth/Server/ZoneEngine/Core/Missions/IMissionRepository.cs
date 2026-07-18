namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    public interface IMissionRepository
    {
        MissionStateRecord GetMission(MissionKey key);

        IList<MissionStateRecord> GetMissions(int characterId);

        MissionCharacterSnapshot ReadCharacter(int characterId);

        MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey);

        IList<MissionAccountFlagRecord> GetAccountFlags(string accountKey);

        T Execute<T>(int characterId, Func<IMissionRepositoryTransaction, T> operation);

        T Execute<T>(
            int characterId,
            string accountKey,
            Func<IMissionRepositoryTransaction, T> operation);
    }

    public interface IMissionRepositoryTransaction
    {
        int CharacterId { get; }

        string AccountKey { get; }

        MissionStateRecord GetMission(MissionKey key);

        IList<MissionStateRecord> GetMissions(int characterId);

        void SaveMission(MissionKey key, MissionStateRecord record);

        MissionObjectiveProgressRecord GetObjective(MissionObjectiveKey key);

        void SaveObjective(MissionObjectiveKey key, MissionObjectiveProgressRecord record);

        bool TryAddObservation(MissionObjectiveObservationRecord observation);

        MissionFlagRecord GetFlag(MissionKey key, string flagKey);

        void SaveFlag(MissionKey key, MissionFlagRecord flag);

        MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey);

        void SaveAccountFlag(string accountKey, MissionAccountFlagRecord flag);

        MissionRewardStageRecord GetReward(MissionRewardKey key);

        MissionRewardClaimResult TryClaimReward(
            MissionRewardKey key,
            string rewardType,
            string claimToken,
            long claimedAtUtcTicks,
            long claimExpiresAtUtcTicks);

        bool TryMarkRewardApplied(
            MissionRewardKey key,
            string claimToken,
            long expectedVersion,
            string effectReference,
            long appliedAtUtcTicks,
            out MissionRewardStageRecord stage);

        bool TryMarkRewardFailed(
            MissionRewardKey key,
            string claimToken,
            long expectedVersion,
            string error,
            long failedAtUtcTicks,
            out MissionRewardStageRecord stage);

        MissionAtomicStatRewardResult TryApplyCharacterStatReward(
            MissionRewardKey key,
            string rewardType,
            IList<MissionCharacterStatMutation> mutations,
            string effectReference,
            long appliedAtUtcTicks);
    }
}
