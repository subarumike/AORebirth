namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    #endregion

    /// <summary>
    /// Shared deterministic state used to model durable storage across repository/service reconstruction in tests.
    /// Production runtime ownership belongs to the database-backed repository.
    /// </summary>
    public sealed class InMemoryMissionRepositoryState
    {
        internal readonly object SyncRoot = new object();

        internal Dictionary<MissionKey, MissionStateRecord> Missions =
            new Dictionary<MissionKey, MissionStateRecord>();

        internal Dictionary<MissionObjectiveKey, MissionObjectiveProgressRecord> Objectives =
            new Dictionary<MissionObjectiveKey, MissionObjectiveProgressRecord>();

        internal Dictionary<string, MissionObjectiveObservationRecord> Observations =
            new Dictionary<string, MissionObjectiveObservationRecord>(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, MissionFlagRecord> Flags =
            new Dictionary<string, MissionFlagRecord>(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<string, MissionAccountFlagRecord> AccountFlags =
            new Dictionary<string, MissionAccountFlagRecord>(StringComparer.OrdinalIgnoreCase);

        internal Dictionary<MissionRewardKey, MissionRewardStageRecord> Rewards =
            new Dictionary<MissionRewardKey, MissionRewardStageRecord>();

        internal Dictionary<string, long> CharacterStats =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class InMemoryMissionRepository : IMissionRepository
    {
        private readonly InMemoryMissionRepositoryState state;

        public InMemoryMissionRepository()
            : this(new InMemoryMissionRepositoryState())
        {
        }

        public InMemoryMissionRepository(InMemoryMissionRepositoryState state)
        {
            this.state = state ?? throw new ArgumentNullException("state");
        }

        public InMemoryMissionRepositoryState State
        {
            get
            {
                return this.state;
            }
        }

        public MissionStateRecord GetMission(MissionKey key)
        {
            lock (this.state.SyncRoot)
            {
                MissionStateRecord record;
                return this.state.Missions.TryGetValue(key, out record) ? record.Clone() : null;
            }
        }

        public IList<MissionStateRecord> GetMissions(int characterId)
        {
            EnsureCharacterId(characterId);
            lock (this.state.SyncRoot)
            {
                return this.state.Missions
                    .Where(value => value.Key.CharacterId == characterId)
                    .OrderBy(value => value.Key.QuestId, StringComparer.OrdinalIgnoreCase)
                    .Select(value => value.Value.Clone())
                    .ToList();
            }
        }

        public MissionCharacterSnapshot ReadCharacter(int characterId)
        {
            EnsureCharacterId(characterId);
            lock (this.state.SyncRoot)
            {
                return CreateSnapshot(
                    characterId,
                    this.state.Missions,
                    this.state.Objectives,
                    this.state.Flags,
                    this.state.Rewards);
            }
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
            EnsureCharacterId(characterId);
            if (operation == null)
            {
                throw new ArgumentNullException("operation");
            }

            lock (this.state.SyncRoot)
            {
                var transaction = new InMemoryMissionRepositoryTransaction(
                    characterId,
                    string.IsNullOrWhiteSpace(accountKey) ? null : accountKey.Trim(),
                    this.state);
                T result = operation(transaction);
                transaction.Commit(this.state);
                return result;
            }
        }

        public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
        {
            string normalizedAccountKey = EnsureAccountKey(accountKey);
            string storageKey = MakeAccountFlagKey(normalizedAccountKey, flagKey);
            lock (this.state.SyncRoot)
            {
                MissionAccountFlagRecord flag;
                return this.state.AccountFlags.TryGetValue(storageKey, out flag) ? flag.Clone() : null;
            }
        }

        public IList<MissionAccountFlagRecord> GetAccountFlags(string accountKey)
        {
            string normalizedAccountKey = EnsureAccountKey(accountKey);
            lock (this.state.SyncRoot)
            {
                return this.state.AccountFlags.Values
                    .Where(value => string.Equals(value.AccountKey, normalizedAccountKey, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(value => value.FlagKey, StringComparer.OrdinalIgnoreCase)
                    .Select(value => value.Clone())
                    .ToList();
            }
        }

        public void SeedCharacterStat(int characterId, int statIdentityType, int statId, long value)
        {
            EnsureCharacterId(characterId);
            lock (this.state.SyncRoot)
            {
                this.state.CharacterStats[MakeStatKey(characterId, statIdentityType, statId)] = value;
            }
        }

        public long GetCharacterStat(int characterId, int statIdentityType, int statId)
        {
            EnsureCharacterId(characterId);
            lock (this.state.SyncRoot)
            {
                long value;
                return this.state.CharacterStats.TryGetValue(
                    MakeStatKey(characterId, statIdentityType, statId),
                    out value)
                           ? value
                           : 0;
            }
        }

        private static MissionCharacterSnapshot CreateSnapshot(
            int characterId,
            IDictionary<MissionKey, MissionStateRecord> missions,
            IDictionary<MissionObjectiveKey, MissionObjectiveProgressRecord> objectives,
            IDictionary<string, MissionFlagRecord> flags,
            IDictionary<MissionRewardKey, MissionRewardStageRecord> rewards)
        {
            return new MissionCharacterSnapshot(
                characterId,
                missions.Where(value => value.Key.CharacterId == characterId)
                    .OrderBy(value => value.Key.QuestId, StringComparer.OrdinalIgnoreCase)
                    .Select(value => value.Value),
                objectives.Where(value => value.Key.Mission.CharacterId == characterId)
                    .OrderBy(value => value.Key.Mission.QuestId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(value => value.Key.ObjectiveId, StringComparer.OrdinalIgnoreCase)
                    .Select(value => value.Value),
                flags.Values.Where(value => value.CharacterId == characterId)
                    .OrderBy(value => value.QuestId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(value => value.FlagKey, StringComparer.OrdinalIgnoreCase),
                rewards.Where(value => value.Key.Mission.CharacterId == characterId)
                    .OrderBy(value => value.Key.Mission.QuestId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(value => value.Key.RewardKey, StringComparer.OrdinalIgnoreCase)
                    .Select(value => value.Value));
        }

        private static string MakeStatKey(int characterId, int statIdentityType, int statId)
        {
            return characterId + "|" + statIdentityType + "|" + statId;
        }

        private static string MakeAccountFlagKey(string accountKey, string flagKey)
        {
            if (string.IsNullOrWhiteSpace(flagKey))
            {
                throw new ArgumentException("Account flag key is required.", "flagKey");
            }

            return accountKey + "|" + flagKey.Trim();
        }

        private static string EnsureAccountKey(string accountKey)
        {
            if (string.IsNullOrWhiteSpace(accountKey))
            {
                throw new ArgumentException("Stable account key is required.", "accountKey");
            }

            return accountKey.Trim();
        }

        private static void EnsureCharacterId(int characterId)
        {
            if (characterId <= 0)
            {
                throw new ArgumentOutOfRangeException("characterId", "Stable character identity must be positive.");
            }
        }

        private sealed class InMemoryMissionRepositoryTransaction : IMissionRepositoryTransaction
        {
            private readonly Dictionary<MissionKey, MissionStateRecord> missions;
            private readonly Dictionary<MissionObjectiveKey, MissionObjectiveProgressRecord> objectives;
            private readonly Dictionary<string, MissionObjectiveObservationRecord> observations;
            private readonly Dictionary<string, MissionFlagRecord> flags;
            private readonly Dictionary<string, MissionAccountFlagRecord> accountFlags;
            private readonly Dictionary<MissionRewardKey, MissionRewardStageRecord> rewards;
            private readonly Dictionary<string, long> characterStats;

            internal InMemoryMissionRepositoryTransaction(
                int characterId,
                string accountKey,
                InMemoryMissionRepositoryState source)
            {
                this.CharacterId = characterId;
                this.AccountKey = accountKey;
                this.missions = source.Missions.ToDictionary(value => value.Key, value => value.Value.Clone());
                this.objectives = source.Objectives.ToDictionary(value => value.Key, value => value.Value.Clone());
                this.observations = source.Observations.ToDictionary(
                    value => value.Key,
                    value => value.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase);
                this.flags = source.Flags.ToDictionary(
                    value => value.Key,
                    value => value.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase);
                this.accountFlags = source.AccountFlags.ToDictionary(
                    value => value.Key,
                    value => value.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase);
                this.rewards = source.Rewards.ToDictionary(value => value.Key, value => value.Value.Clone());
                this.characterStats = new Dictionary<string, long>(source.CharacterStats, StringComparer.OrdinalIgnoreCase);
            }

            public int CharacterId { get; private set; }

            public string AccountKey { get; private set; }

            public MissionStateRecord GetMission(MissionKey key)
            {
                this.EnsureOwns(key);
                MissionStateRecord record;
                return this.missions.TryGetValue(key, out record) ? record.Clone() : null;
            }

            public IList<MissionStateRecord> GetMissions(int characterId)
            {
                this.EnsureOwns(characterId);
                return this.missions
                    .Where(value => value.Key.CharacterId == characterId)
                    .OrderBy(value => value.Key.QuestId, StringComparer.OrdinalIgnoreCase)
                    .Select(value => value.Value.Clone())
                    .ToList();
            }

            public void SaveMission(MissionKey key, MissionStateRecord record)
            {
                this.EnsureOwns(key);
                if (record == null || record.CharacterId != key.CharacterId
                    || !string.Equals(record.QuestId, key.QuestId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Mission record ownership does not match its stable key.");
                }

                MissionStateRecord existing;
                if (this.missions.TryGetValue(key, out existing))
                {
                    EnsureVersion(existing.Version, record.Version, "mission");
                }

                MissionStateRecord stored = record.Clone();
                stored.Version = existing == null ? 1 : existing.Version + 1;
                record.Version = stored.Version;
                this.missions[key] = stored;
            }

            public MissionObjectiveProgressRecord GetObjective(MissionObjectiveKey key)
            {
                this.EnsureOwns(key.Mission);
                MissionObjectiveProgressRecord record;
                return this.objectives.TryGetValue(key, out record) ? record.Clone() : null;
            }

            public void SaveObjective(MissionObjectiveKey key, MissionObjectiveProgressRecord record)
            {
                this.EnsureOwns(key.Mission);
                if (record == null || record.CharacterId != key.Mission.CharacterId
                    || !string.Equals(record.QuestId, key.Mission.QuestId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(record.ObjectiveId, key.ObjectiveId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Objective record ownership does not match its stable key.");
                }

                MissionObjectiveProgressRecord existing;
                if (this.objectives.TryGetValue(key, out existing))
                {
                    EnsureVersion(existing.Version, record.Version, "objective");
                }

                MissionObjectiveProgressRecord stored = record.Clone();
                stored.Version = existing == null ? 1 : existing.Version + 1;
                record.Version = stored.Version;
                this.objectives[key] = stored;
            }

            public bool TryAddObservation(MissionObjectiveObservationRecord observation)
            {
                if (observation == null || string.IsNullOrWhiteSpace(observation.ObservationKey))
                {
                    throw new InvalidOperationException("A stable observation key is required.");
                }

                MissionObjectiveKey objectiveKey = observation.ObjectiveKey;
                this.EnsureOwns(objectiveKey.Mission);
                string key = MakeObservationKey(objectiveKey, observation.ObservationKey);
                if (this.observations.ContainsKey(key))
                {
                    return false;
                }

                this.observations.Add(key, observation.Clone());
                return true;
            }

            public MissionFlagRecord GetFlag(MissionKey key, string flagKey)
            {
                this.EnsureOwns(key);
                MissionFlagRecord flag;
                return this.flags.TryGetValue(MakeFlagKey(key, flagKey), out flag) ? flag.Clone() : null;
            }

            public void SaveFlag(MissionKey key, MissionFlagRecord flag)
            {
                this.EnsureOwns(key);
                if (flag == null || string.IsNullOrWhiteSpace(flag.FlagKey)
                    || flag.CharacterId != key.CharacterId
                    || !string.Equals(flag.QuestId, key.QuestId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Flag record ownership does not match its stable key.");
                }

                string flagStorageKey = MakeFlagKey(key, flag.FlagKey);
                MissionFlagRecord existing;
                if (this.flags.TryGetValue(flagStorageKey, out existing))
                {
                    EnsureVersion(existing.Version, flag.Version, "flag");
                }

                MissionFlagRecord stored = flag.Clone();
                stored.Version = existing == null ? 1 : existing.Version + 1;
                flag.Version = stored.Version;
                this.flags[flagStorageKey] = stored;
            }

            public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
            {
                string normalizedAccountKey = this.EnsureOwnsAccount(accountKey);
                MissionAccountFlagRecord flag;
                return this.accountFlags.TryGetValue(MakeAccountFlagKey(normalizedAccountKey, flagKey), out flag)
                           ? flag.Clone()
                           : null;
            }

            public void SaveAccountFlag(string accountKey, MissionAccountFlagRecord flag)
            {
                string normalizedAccountKey = this.EnsureOwnsAccount(accountKey);
                if (flag == null || string.IsNullOrWhiteSpace(flag.FlagKey)
                    || !string.Equals(flag.AccountKey, normalizedAccountKey, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Account flag ownership does not match its stable account key.");
                }

                string storageKey = MakeAccountFlagKey(normalizedAccountKey, flag.FlagKey);
                MissionAccountFlagRecord existing;
                if (this.accountFlags.TryGetValue(storageKey, out existing))
                {
                    EnsureVersion(existing.Version, flag.Version, "account flag");
                }

                MissionAccountFlagRecord stored = flag.Clone();
                stored.AccountKey = normalizedAccountKey;
                stored.Version = existing == null ? 1 : existing.Version + 1;
                flag.Version = stored.Version;
                this.accountFlags[storageKey] = stored;
            }

            public MissionRewardStageRecord GetReward(MissionRewardKey key)
            {
                this.EnsureOwns(key.Mission);
                MissionRewardStageRecord stage;
                return this.rewards.TryGetValue(key, out stage) ? stage.Clone() : null;
            }

            public MissionRewardClaimResult TryClaimReward(
                MissionRewardKey key,
                string rewardType,
                string claimToken,
                long claimedAtUtcTicks,
                long claimExpiresAtUtcTicks)
            {
                this.EnsureOwns(key.Mission);
                MissionStateRecord mission;
                if (!this.missions.TryGetValue(key.Mission, out mission)
                    || mission.State != MissionLifecycleState.Completed)
                {
                    return ClaimResult(MissionRewardClaimStatus.Rejected, null, "Mission must be completed before rewards can be claimed.");
                }

                if (string.IsNullOrWhiteSpace(rewardType) || string.IsNullOrWhiteSpace(claimToken)
                    || claimedAtUtcTicks <= 0 || claimExpiresAtUtcTicks <= claimedAtUtcTicks)
                {
                    return ClaimResult(MissionRewardClaimStatus.Rejected, null, "Reward claim is incomplete.");
                }

                MissionRewardStageRecord stage;
                if (this.rewards.TryGetValue(key, out stage))
                {
                    if (!string.Equals(stage.RewardType, rewardType, StringComparison.OrdinalIgnoreCase))
                    {
                        return ClaimResult(MissionRewardClaimStatus.Rejected, stage, "Reward type does not match the durable stage.");
                    }

                    if (stage.Status == MissionRewardStatus.Applied)
                    {
                        return ClaimResult(MissionRewardClaimStatus.AlreadyApplied, stage, "Reward was already applied.");
                    }

                    if (stage.Status == MissionRewardStatus.InProgress
                        && stage.ClaimExpiresAtUtcTicks > claimedAtUtcTicks)
                    {
                        return ClaimResult(MissionRewardClaimStatus.Busy, stage, "Reward has an active durable claim.");
                    }

                    stage = stage.Clone();
                    stage.Version++;
                }
                else
                {
                    stage = new MissionRewardStageRecord
                            {
                                CharacterId = key.Mission.CharacterId,
                                QuestId = key.Mission.QuestId,
                                RewardKey = key.RewardKey,
                                RewardType = rewardType,
                                Status = MissionRewardStatus.Pending,
                                CreatedAtUtcTicks = claimedAtUtcTicks,
                                Version = 1
                            };
                }

                stage.Status = MissionRewardStatus.InProgress;
                stage.Attempts++;
                stage.LastError = null;
                stage.ClaimToken = claimToken;
                stage.ClaimedAtUtcTicks = claimedAtUtcTicks;
                stage.ClaimExpiresAtUtcTicks = claimExpiresAtUtcTicks;
                stage.UpdatedAtUtcTicks = claimedAtUtcTicks;
                this.rewards[key] = stage.Clone();
                return ClaimResult(MissionRewardClaimStatus.Claimed, stage, "Reward claim acquired.");
            }

            public bool TryMarkRewardApplied(
                MissionRewardKey key,
                string claimToken,
                long expectedVersion,
                string effectReference,
                long appliedAtUtcTicks,
                out MissionRewardStageRecord stage)
            {
                this.EnsureOwns(key.Mission);
                MissionRewardStageRecord current;
                if (!this.rewards.TryGetValue(key, out current)
                    || current.Status != MissionRewardStatus.InProgress
                    || current.Version != expectedVersion
                    || !string.Equals(current.ClaimToken, claimToken, StringComparison.Ordinal))
                {
                    stage = current == null ? null : current.Clone();
                    return false;
                }

                current = current.Clone();
                current.Status = MissionRewardStatus.Applied;
                current.EffectReference = effectReference;
                current.LastError = null;
                current.AppliedAtUtcTicks = appliedAtUtcTicks;
                current.UpdatedAtUtcTicks = appliedAtUtcTicks;
                current.ClaimExpiresAtUtcTicks = 0;
                current.Version++;
                this.rewards[key] = current.Clone();
                stage = current.Clone();
                return true;
            }

            public bool TryMarkRewardFailed(
                MissionRewardKey key,
                string claimToken,
                long expectedVersion,
                string error,
                long failedAtUtcTicks,
                out MissionRewardStageRecord stage)
            {
                this.EnsureOwns(key.Mission);
                MissionRewardStageRecord current;
                if (!this.rewards.TryGetValue(key, out current)
                    || current.Status != MissionRewardStatus.InProgress
                    || current.Version != expectedVersion
                    || !string.Equals(current.ClaimToken, claimToken, StringComparison.Ordinal))
                {
                    stage = current == null ? null : current.Clone();
                    return false;
                }

                current = current.Clone();
                current.Status = MissionRewardStatus.Failed;
                current.LastError = error;
                current.UpdatedAtUtcTicks = failedAtUtcTicks;
                current.ClaimExpiresAtUtcTicks = 0;
                current.Version++;
                this.rewards[key] = current.Clone();
                stage = current.Clone();
                return true;
            }

            public MissionAtomicStatRewardResult TryApplyCharacterStatReward(
                MissionRewardKey key,
                string rewardType,
                IList<MissionCharacterStatMutation> mutations,
                string effectReference,
                long appliedAtUtcTicks)
            {
                this.EnsureOwns(key.Mission);
                MissionStateRecord mission;
                if (!this.missions.TryGetValue(key.Mission, out mission)
                    || mission.State != MissionLifecycleState.Completed)
                {
                    return AtomicResult(MissionAtomicRewardStatus.Rejected, null, null, "Mission must be completed before rewards can be applied.");
                }

                if (string.IsNullOrWhiteSpace(rewardType) || mutations == null || mutations.Count == 0
                    || appliedAtUtcTicks <= 0)
                {
                    return AtomicResult(MissionAtomicRewardStatus.Rejected, null, null, "Atomic stat reward is incomplete.");
                }

                MissionRewardStageRecord existing;
                if (this.rewards.TryGetValue(key, out existing))
                {
                    if (!string.Equals(existing.RewardType, rewardType, StringComparison.OrdinalIgnoreCase))
                    {
                        return AtomicResult(MissionAtomicRewardStatus.Rejected, existing, null, "Reward type does not match the durable stage.");
                    }

                    if (existing.Status == MissionRewardStatus.Applied)
                    {
                        return AtomicResult(MissionAtomicRewardStatus.AlreadyApplied, existing, null, "Reward was already applied.");
                    }
                }

                var pendingValues = new List<MissionCharacterStatValue>();
                foreach (MissionCharacterStatMutation mutation in mutations)
                {
                    if (mutation == null || mutation.StatIdentityType <= 0 || mutation.StatId < 0
                        || mutation.MaximumValue < mutation.MinimumValue
                        || (mutation.Kind != MissionStatMutationKind.AddClamped
                            && mutation.Kind != MissionStatMutationKind.Set))
                    {
                        return AtomicResult(MissionAtomicRewardStatus.Rejected, existing, null, "Atomic stat mutation is unresolved or invalid.");
                    }

                    string statKey = MakeStatKey(this.CharacterId, mutation.StatIdentityType, mutation.StatId);
                    long currentValue;
                    this.characterStats.TryGetValue(statKey, out currentValue);
                    decimal requested = mutation.Kind == MissionStatMutationKind.AddClamped
                                            ? (decimal)currentValue + mutation.Value
                                            : mutation.Value;
                    long next = requested < mutation.MinimumValue
                                    ? mutation.MinimumValue
                                    : requested > mutation.MaximumValue
                                        ? mutation.MaximumValue
                                        : (long)requested;
                    pendingValues.Add(
                        new MissionCharacterStatValue
                        {
                            StatIdentityType = mutation.StatIdentityType,
                            StatId = mutation.StatId,
                            Value = next
                        });
                }

                foreach (MissionCharacterStatValue value in pendingValues)
                {
                    this.characterStats[MakeStatKey(this.CharacterId, value.StatIdentityType, value.StatId)] = value.Value;
                }

                MissionRewardStageRecord stage = existing == null
                                                     ? new MissionRewardStageRecord
                                                       {
                                                           CharacterId = key.Mission.CharacterId,
                                                           QuestId = key.Mission.QuestId,
                                                           RewardKey = key.RewardKey,
                                                           RewardType = rewardType,
                                                           CreatedAtUtcTicks = appliedAtUtcTicks,
                                                           Version = 1
                                                       }
                                                     : existing.Clone();
                if (existing != null)
                {
                    stage.Version++;
                }

                stage.Status = MissionRewardStatus.Applied;
                stage.Attempts++;
                stage.EffectReference = effectReference;
                stage.LastError = null;
                stage.AppliedAtUtcTicks = appliedAtUtcTicks;
                stage.UpdatedAtUtcTicks = appliedAtUtcTicks;
                stage.ClaimExpiresAtUtcTicks = 0;
                this.rewards[key] = stage.Clone();
                return AtomicResult(MissionAtomicRewardStatus.Applied, stage, pendingValues, "Atomic stat reward applied.");
            }

            internal void Commit(InMemoryMissionRepositoryState destination)
            {
                destination.Missions = this.missions;
                destination.Objectives = this.objectives;
                destination.Observations = this.observations;
                destination.Flags = this.flags;
                destination.AccountFlags = this.accountFlags;
                destination.Rewards = this.rewards;
                destination.CharacterStats = this.characterStats;
            }

            private static MissionRewardClaimResult ClaimResult(
                MissionRewardClaimStatus status,
                MissionRewardStageRecord stage,
                string message)
            {
                return new MissionRewardClaimResult
                       {
                           Status = status,
                           Stage = stage == null ? null : stage.Clone(),
                           Message = message
                       };
            }

            private static MissionAtomicStatRewardResult AtomicResult(
                MissionAtomicRewardStatus status,
                MissionRewardStageRecord stage,
                IEnumerable<MissionCharacterStatValue> statValues,
                string message)
            {
                return new MissionAtomicStatRewardResult
                       {
                           Status = status,
                           Stage = stage == null ? null : stage.Clone(),
                           StatValues = (statValues ?? Enumerable.Empty<MissionCharacterStatValue>()).ToList(),
                           Message = message
                       };
            }

            private static void EnsureVersion(long stored, long supplied, string entity)
            {
                if (stored != supplied)
                {
                    throw new InvalidOperationException("Stale " + entity + " version.");
                }
            }

            private void EnsureOwns(MissionKey key)
            {
                this.EnsureOwns(key.CharacterId);
            }

            private void EnsureOwns(int characterId)
            {
                if (characterId != this.CharacterId)
                {
                    throw new InvalidOperationException("Transaction cannot mutate another character's mission state.");
                }
            }

            private string EnsureOwnsAccount(string accountKey)
            {
                string normalizedAccountKey = EnsureAccountKey(accountKey);
                if (string.IsNullOrWhiteSpace(this.AccountKey)
                    || !string.Equals(this.AccountKey, normalizedAccountKey, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Transaction does not own the required stable account scope.");
                }

                return normalizedAccountKey;
            }

            private static string MakeObservationKey(MissionObjectiveKey key, string observationKey)
            {
                return key + "|" + observationKey.Trim();
            }

            private static string MakeFlagKey(MissionKey key, string flagKey)
            {
                if (string.IsNullOrWhiteSpace(flagKey))
                {
                    throw new InvalidOperationException("Mission flag key is required.");
                }

                return key + "|" + flagKey.Trim();
            }
        }
    }
}
