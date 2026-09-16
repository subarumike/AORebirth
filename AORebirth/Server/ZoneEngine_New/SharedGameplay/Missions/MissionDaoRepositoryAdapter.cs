namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Missions;

    /// <summary>
    /// Keeps the existing mission-domain repository contract above the neutral DAO boundary.
    /// </summary>
    public sealed class MissionDaoRepositoryAdapter : IMissionRepository
    {
        private readonly IMissionDao missionDao;

        public MissionDaoRepositoryAdapter(IMissionDao missionDao)
        {
            if (missionDao == null)
            {
                throw new ArgumentNullException("missionDao");
            }

            this.missionDao = missionDao;
        }

        public MissionStateRecord GetMission(MissionKey key)
        {
            return MissionDataMapper.ToDomain(this.missionDao.GetMission(MissionDataMapper.ToData(key)));
        }

        public IList<MissionStateRecord> GetMissions(int characterId)
        {
            return MissionDataMapper.ToDomain(this.missionDao.GetMissions(characterId));
        }

        public MissionCharacterSnapshot ReadCharacter(int characterId)
        {
            return MissionDataMapper.ToDomain(this.missionDao.ReadCharacter(characterId));
        }

        public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
        {
            return MissionDataMapper.ToDomain(this.missionDao.GetAccountFlag(accountKey, flagKey));
        }

        public IList<MissionAccountFlagRecord> GetAccountFlags(string accountKey)
        {
            return MissionDataMapper.ToDomain(this.missionDao.GetAccountFlags(accountKey));
        }

        public T Execute<T>(int characterId, Func<IMissionRepositoryTransaction, T> operation)
        {
            return this.Execute(characterId, null, operation);
        }

        public T Execute<T>(
            int characterId,
            string accountKey,
            Func<IMissionRepositoryTransaction, T> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException("operation");
            }

            return this.missionDao.Execute(
                characterId,
                accountKey,
                transaction => operation(new TransactionAdapter(transaction)));
        }

        private sealed class TransactionAdapter : IMissionRepositoryTransaction
        {
            private readonly IMissionDaoTransaction transaction;

            internal TransactionAdapter(IMissionDaoTransaction transaction)
            {
                if (transaction == null)
                {
                    throw new ArgumentNullException("transaction");
                }

                this.transaction = transaction;
            }

            public int CharacterId
            {
                get { return this.transaction.CharacterId; }
            }

            public string AccountKey
            {
                get { return this.transaction.AccountKey; }
            }

            public MissionStateRecord GetMission(MissionKey key)
            {
                return MissionDataMapper.ToDomain(this.transaction.GetMission(MissionDataMapper.ToData(key)));
            }

            public IList<MissionStateRecord> GetMissions(int characterId)
            {
                return MissionDataMapper.ToDomain(this.transaction.GetMissions(characterId));
            }

            public void SaveMission(MissionKey key, MissionStateRecord record)
            {
                MissionStateData data = MissionDataMapper.ToData(record);
                this.transaction.SaveMission(MissionDataMapper.ToData(key), data);
                MissionDataMapper.CopyBack(data, record);
            }

            public MissionObjectiveProgressRecord GetObjective(MissionObjectiveKey key)
            {
                return MissionDataMapper.ToDomain(this.transaction.GetObjective(MissionDataMapper.ToData(key)));
            }

            public void SaveObjective(MissionObjectiveKey key, MissionObjectiveProgressRecord record)
            {
                MissionObjectiveProgressData data = MissionDataMapper.ToData(record);
                this.transaction.SaveObjective(MissionDataMapper.ToData(key), data);
                MissionDataMapper.CopyBack(data, record);
            }

            public bool TryAddObservation(MissionObjectiveObservationRecord observation)
            {
                MissionObjectiveObservationData data = MissionDataMapper.ToData(observation);
                bool added = this.transaction.TryAddObservation(data);
                MissionDataMapper.CopyBack(data, observation);
                return added;
            }

            public MissionFlagRecord GetFlag(MissionKey key, string flagKey)
            {
                return MissionDataMapper.ToDomain(
                    this.transaction.GetFlag(MissionDataMapper.ToData(key), flagKey));
            }

            public void SaveFlag(MissionKey key, MissionFlagRecord flag)
            {
                MissionFlagData data = MissionDataMapper.ToData(flag);
                this.transaction.SaveFlag(MissionDataMapper.ToData(key), data);
                MissionDataMapper.CopyBack(data, flag);
            }

            public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
            {
                return MissionDataMapper.ToDomain(this.transaction.GetAccountFlag(accountKey, flagKey));
            }

            public void SaveAccountFlag(string accountKey, MissionAccountFlagRecord flag)
            {
                MissionAccountFlagData data = MissionDataMapper.ToData(flag);
                this.transaction.SaveAccountFlag(accountKey, data);
                MissionDataMapper.CopyBack(data, flag);
            }

            public MissionRewardStageRecord GetReward(MissionRewardKey key)
            {
                return MissionDataMapper.ToDomain(this.transaction.GetReward(MissionDataMapper.ToData(key)));
            }

            public MissionRewardClaimResult TryClaimReward(
                MissionRewardKey key,
                string rewardType,
                string claimToken,
                long claimedAtUtcTicks,
                long claimExpiresAtUtcTicks)
            {
                MissionRewardClaimResultData result = this.transaction.TryClaimReward(
                    MissionDataMapper.ToData(key),
                    rewardType,
                    claimToken,
                    claimedAtUtcTicks,
                    claimExpiresAtUtcTicks);
                return new MissionRewardClaimResult
                       {
                           Status = (MissionRewardClaimStatus)(int)result.Status,
                           Stage = MissionDataMapper.ToDomain(result.Stage),
                           Message = result.Message
                       };
            }

            public bool TryMarkRewardApplied(
                MissionRewardKey key,
                string claimToken,
                long expectedVersion,
                string effectReference,
                long appliedAtUtcTicks,
                out MissionRewardStageRecord stage)
            {
                MissionRewardStageData data;
                bool applied = this.transaction.TryMarkRewardApplied(
                    MissionDataMapper.ToData(key),
                    claimToken,
                    expectedVersion,
                    effectReference,
                    appliedAtUtcTicks,
                    out data);
                stage = MissionDataMapper.ToDomain(data);
                return applied;
            }

            public bool TryMarkRewardFailed(
                MissionRewardKey key,
                string claimToken,
                long expectedVersion,
                string error,
                long failedAtUtcTicks,
                out MissionRewardStageRecord stage)
            {
                MissionRewardStageData data;
                bool failed = this.transaction.TryMarkRewardFailed(
                    MissionDataMapper.ToData(key),
                    claimToken,
                    expectedVersion,
                    error,
                    failedAtUtcTicks,
                    out data);
                stage = MissionDataMapper.ToDomain(data);
                return failed;
            }

            public MissionAtomicStatRewardResult TryApplyCharacterStatReward(
                MissionRewardKey key,
                string rewardType,
                IList<MissionCharacterStatMutation> mutations,
                string effectReference,
                long appliedAtUtcTicks)
            {
                MissionAtomicStatRewardResultData result = this.transaction.TryApplyCharacterStatReward(
                    MissionDataMapper.ToData(key),
                    rewardType,
                    (mutations ?? new MissionCharacterStatMutation[0]).Select(MissionDataMapper.ToData).ToList(),
                    effectReference,
                    appliedAtUtcTicks);
                return new MissionAtomicStatRewardResult
                       {
                           Status = (MissionAtomicRewardStatus)(int)result.Status,
                           Stage = MissionDataMapper.ToDomain(result.Stage),
                           StatValues = (result.StatValues ?? new MissionStatValueData[0])
                               .Select(MissionDataMapper.ToDomain)
                               .ToList(),
                           Message = result.Message
                       };
            }
        }
    }
}
