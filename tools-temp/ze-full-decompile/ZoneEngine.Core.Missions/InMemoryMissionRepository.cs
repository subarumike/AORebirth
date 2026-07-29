using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Missions;

public sealed class InMemoryMissionRepository : IMissionRepository
{
	private sealed class InMemoryMissionRepositoryTransaction : IMissionRepositoryTransaction
	{
		private readonly Dictionary<MissionKey, MissionStateRecord> missions;

		private readonly Dictionary<MissionObjectiveKey, MissionObjectiveProgressRecord> objectives;

		private readonly Dictionary<string, MissionObjectiveObservationRecord> observations;

		private readonly Dictionary<string, MissionFlagRecord> flags;

		private readonly Dictionary<string, MissionAccountFlagRecord> accountFlags;

		private readonly Dictionary<MissionRewardKey, MissionRewardStageRecord> rewards;

		private readonly Dictionary<string, long> characterStats;

		public int CharacterId { get; private set; }

		public string AccountKey { get; private set; }

		internal InMemoryMissionRepositoryTransaction(int characterId, string accountKey, InMemoryMissionRepositoryState source)
		{
			CharacterId = characterId;
			AccountKey = accountKey;
			missions = source.Missions.ToDictionary((KeyValuePair<MissionKey, MissionStateRecord> value) => value.Key, (KeyValuePair<MissionKey, MissionStateRecord> value) => value.Value.Clone());
			objectives = source.Objectives.ToDictionary((KeyValuePair<MissionObjectiveKey, MissionObjectiveProgressRecord> value) => value.Key, (KeyValuePair<MissionObjectiveKey, MissionObjectiveProgressRecord> value) => value.Value.Clone());
			observations = source.Observations.ToDictionary((KeyValuePair<string, MissionObjectiveObservationRecord> value) => value.Key, (KeyValuePair<string, MissionObjectiveObservationRecord> value) => value.Value.Clone(), StringComparer.OrdinalIgnoreCase);
			flags = source.Flags.ToDictionary((KeyValuePair<string, MissionFlagRecord> value) => value.Key, (KeyValuePair<string, MissionFlagRecord> value) => value.Value.Clone(), StringComparer.OrdinalIgnoreCase);
			accountFlags = source.AccountFlags.ToDictionary((KeyValuePair<string, MissionAccountFlagRecord> value) => value.Key, (KeyValuePair<string, MissionAccountFlagRecord> value) => value.Value.Clone(), StringComparer.OrdinalIgnoreCase);
			rewards = source.Rewards.ToDictionary((KeyValuePair<MissionRewardKey, MissionRewardStageRecord> value) => value.Key, (KeyValuePair<MissionRewardKey, MissionRewardStageRecord> value) => value.Value.Clone());
			characterStats = new Dictionary<string, long>(source.CharacterStats, StringComparer.OrdinalIgnoreCase);
		}

		public MissionStateRecord GetMission(MissionKey key)
		{
			EnsureOwns(key);
			MissionStateRecord value;
			return missions.TryGetValue(key, out value) ? value.Clone() : null;
		}

		public IList<MissionStateRecord> GetMissions(int characterId)
		{
			EnsureOwns(characterId);
			return (from value in missions.Where((KeyValuePair<MissionKey, MissionStateRecord> value) => value.Key.CharacterId == characterId).OrderBy((KeyValuePair<MissionKey, MissionStateRecord> value) => value.Key.QuestId, StringComparer.OrdinalIgnoreCase)
				select value.Value.Clone()).ToList();
		}

		public void SaveMission(MissionKey key, MissionStateRecord record)
		{
			EnsureOwns(key);
			if (record == null || record.CharacterId != key.CharacterId || !string.Equals(record.QuestId, key.QuestId, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Mission record ownership does not match its stable key.");
			}
			if (missions.TryGetValue(key, out var value))
			{
				EnsureVersion(value.Version, record.Version, "mission");
			}
			MissionStateRecord missionStateRecord = record.Clone();
			missionStateRecord.Version = ((value == null) ? 1 : (value.Version + 1));
			record.Version = missionStateRecord.Version;
			missions[key] = missionStateRecord;
		}

		public MissionObjectiveProgressRecord GetObjective(MissionObjectiveKey key)
		{
			EnsureOwns(key.Mission);
			MissionObjectiveProgressRecord value;
			return objectives.TryGetValue(key, out value) ? value.Clone() : null;
		}

		public void SaveObjective(MissionObjectiveKey key, MissionObjectiveProgressRecord record)
		{
			EnsureOwns(key.Mission);
			if (record == null || record.CharacterId != key.Mission.CharacterId || !string.Equals(record.QuestId, key.Mission.QuestId, StringComparison.OrdinalIgnoreCase) || !string.Equals(record.ObjectiveId, key.ObjectiveId, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Objective record ownership does not match its stable key.");
			}
			if (objectives.TryGetValue(key, out var value))
			{
				EnsureVersion(value.Version, record.Version, "objective");
			}
			MissionObjectiveProgressRecord missionObjectiveProgressRecord = record.Clone();
			missionObjectiveProgressRecord.Version = ((value == null) ? 1 : (value.Version + 1));
			record.Version = missionObjectiveProgressRecord.Version;
			objectives[key] = missionObjectiveProgressRecord;
		}

		public bool TryAddObservation(MissionObjectiveObservationRecord observation)
		{
			if (observation == null || string.IsNullOrWhiteSpace(observation.ObservationKey))
			{
				throw new InvalidOperationException("A stable observation key is required.");
			}
			MissionObjectiveKey objectiveKey = observation.ObjectiveKey;
			EnsureOwns(objectiveKey.Mission);
			string key = MakeObservationKey(objectiveKey, observation.ObservationKey);
			if (observations.ContainsKey(key))
			{
				return false;
			}
			observations.Add(key, observation.Clone());
			return true;
		}

		public MissionFlagRecord GetFlag(MissionKey key, string flagKey)
		{
			EnsureOwns(key);
			MissionFlagRecord value;
			return flags.TryGetValue(MakeFlagKey(key, flagKey), out value) ? value.Clone() : null;
		}

		public void SaveFlag(MissionKey key, MissionFlagRecord flag)
		{
			EnsureOwns(key);
			if (flag == null || string.IsNullOrWhiteSpace(flag.FlagKey) || flag.CharacterId != key.CharacterId || !string.Equals(flag.QuestId, key.QuestId, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Flag record ownership does not match its stable key.");
			}
			string key2 = MakeFlagKey(key, flag.FlagKey);
			if (flags.TryGetValue(key2, out var value))
			{
				EnsureVersion(value.Version, flag.Version, "flag");
			}
			MissionFlagRecord missionFlagRecord = flag.Clone();
			missionFlagRecord.Version = ((value == null) ? 1 : (value.Version + 1));
			flag.Version = missionFlagRecord.Version;
			flags[key2] = missionFlagRecord;
		}

		public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
		{
			string accountKey2 = EnsureOwnsAccount(accountKey);
			MissionAccountFlagRecord value;
			return accountFlags.TryGetValue(MakeAccountFlagKey(accountKey2, flagKey), out value) ? value.Clone() : null;
		}

		public void SaveAccountFlag(string accountKey, MissionAccountFlagRecord flag)
		{
			string text = EnsureOwnsAccount(accountKey);
			if (flag == null || string.IsNullOrWhiteSpace(flag.FlagKey) || !string.Equals(flag.AccountKey, text, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Account flag ownership does not match its stable account key.");
			}
			string key = MakeAccountFlagKey(text, flag.FlagKey);
			if (accountFlags.TryGetValue(key, out var value))
			{
				EnsureVersion(value.Version, flag.Version, "account flag");
			}
			MissionAccountFlagRecord missionAccountFlagRecord = flag.Clone();
			missionAccountFlagRecord.AccountKey = text;
			missionAccountFlagRecord.Version = ((value == null) ? 1 : (value.Version + 1));
			flag.Version = missionAccountFlagRecord.Version;
			accountFlags[key] = missionAccountFlagRecord;
		}

		public MissionRewardStageRecord GetReward(MissionRewardKey key)
		{
			EnsureOwns(key.Mission);
			MissionRewardStageRecord value;
			return rewards.TryGetValue(key, out value) ? value.Clone() : null;
		}

		public MissionRewardClaimResult TryClaimReward(MissionRewardKey key, string rewardType, string claimToken, long claimedAtUtcTicks, long claimExpiresAtUtcTicks)
		{
			EnsureOwns(key.Mission);
			if (!missions.TryGetValue(key.Mission, out var value) || value.State != MissionLifecycleState.Completed)
			{
				return ClaimResult(MissionRewardClaimStatus.Rejected, null, "Mission must be completed before rewards can be claimed.");
			}
			if (string.IsNullOrWhiteSpace(rewardType) || string.IsNullOrWhiteSpace(claimToken) || claimedAtUtcTicks <= 0 || claimExpiresAtUtcTicks <= claimedAtUtcTicks)
			{
				return ClaimResult(MissionRewardClaimStatus.Rejected, null, "Reward claim is incomplete.");
			}
			if (rewards.TryGetValue(key, out var value2))
			{
				if (!string.Equals(value2.RewardType, rewardType, StringComparison.OrdinalIgnoreCase))
				{
					return ClaimResult(MissionRewardClaimStatus.Rejected, value2, "Reward type does not match the durable stage.");
				}
				if (value2.Status == MissionRewardStatus.Applied)
				{
					return ClaimResult(MissionRewardClaimStatus.AlreadyApplied, value2, "Reward was already applied.");
				}
				if (value2.Status == MissionRewardStatus.InProgress && value2.ClaimExpiresAtUtcTicks > claimedAtUtcTicks)
				{
					return ClaimResult(MissionRewardClaimStatus.Busy, value2, "Reward has an active durable claim.");
				}
				value2 = value2.Clone();
				value2.Version++;
			}
			else
			{
				value2 = new MissionRewardStageRecord
				{
					CharacterId = key.Mission.CharacterId,
					QuestId = key.Mission.QuestId,
					RewardKey = key.RewardKey,
					RewardType = rewardType,
					Status = MissionRewardStatus.Pending,
					CreatedAtUtcTicks = claimedAtUtcTicks,
					Version = 1L
				};
			}
			value2.Status = MissionRewardStatus.InProgress;
			value2.Attempts++;
			value2.LastError = null;
			value2.ClaimToken = claimToken;
			value2.ClaimedAtUtcTicks = claimedAtUtcTicks;
			value2.ClaimExpiresAtUtcTicks = claimExpiresAtUtcTicks;
			value2.UpdatedAtUtcTicks = claimedAtUtcTicks;
			rewards[key] = value2.Clone();
			return ClaimResult(MissionRewardClaimStatus.Claimed, value2, "Reward claim acquired.");
		}

		public bool TryMarkRewardApplied(MissionRewardKey key, string claimToken, long expectedVersion, string effectReference, long appliedAtUtcTicks, out MissionRewardStageRecord stage)
		{
			EnsureOwns(key.Mission);
			if (!rewards.TryGetValue(key, out var value) || value.Status != MissionRewardStatus.InProgress || value.Version != expectedVersion || !string.Equals(value.ClaimToken, claimToken, StringComparison.Ordinal))
			{
				stage = value?.Clone();
				return false;
			}
			value = value.Clone();
			value.Status = MissionRewardStatus.Applied;
			value.EffectReference = effectReference;
			value.LastError = null;
			value.AppliedAtUtcTicks = appliedAtUtcTicks;
			value.UpdatedAtUtcTicks = appliedAtUtcTicks;
			value.ClaimExpiresAtUtcTicks = 0L;
			value.Version++;
			rewards[key] = value.Clone();
			stage = value.Clone();
			return true;
		}

		public bool TryMarkRewardFailed(MissionRewardKey key, string claimToken, long expectedVersion, string error, long failedAtUtcTicks, out MissionRewardStageRecord stage)
		{
			EnsureOwns(key.Mission);
			if (!rewards.TryGetValue(key, out var value) || value.Status != MissionRewardStatus.InProgress || value.Version != expectedVersion || !string.Equals(value.ClaimToken, claimToken, StringComparison.Ordinal))
			{
				stage = value?.Clone();
				return false;
			}
			value = value.Clone();
			value.Status = MissionRewardStatus.Failed;
			value.LastError = error;
			value.UpdatedAtUtcTicks = failedAtUtcTicks;
			value.ClaimExpiresAtUtcTicks = 0L;
			value.Version++;
			rewards[key] = value.Clone();
			stage = value.Clone();
			return true;
		}

		public MissionAtomicStatRewardResult TryApplyCharacterStatReward(MissionRewardKey key, string rewardType, IList<MissionCharacterStatMutation> mutations, string effectReference, long appliedAtUtcTicks)
		{
			EnsureOwns(key.Mission);
			if (!missions.TryGetValue(key.Mission, out var value) || value.State != MissionLifecycleState.Completed)
			{
				return AtomicResult(MissionAtomicRewardStatus.Rejected, null, null, "Mission must be completed before rewards can be applied.");
			}
			if (string.IsNullOrWhiteSpace(rewardType) || mutations == null || mutations.Count == 0 || appliedAtUtcTicks <= 0)
			{
				return AtomicResult(MissionAtomicRewardStatus.Rejected, null, null, "Atomic stat reward is incomplete.");
			}
			if (rewards.TryGetValue(key, out var value2))
			{
				if (!string.Equals(value2.RewardType, rewardType, StringComparison.OrdinalIgnoreCase))
				{
					return AtomicResult(MissionAtomicRewardStatus.Rejected, value2, null, "Reward type does not match the durable stage.");
				}
				if (value2.Status == MissionRewardStatus.Applied)
				{
					return AtomicResult(MissionAtomicRewardStatus.AlreadyApplied, value2, null, "Reward was already applied.");
				}
			}
			List<MissionCharacterStatValue> list = new List<MissionCharacterStatValue>();
			foreach (MissionCharacterStatMutation mutation in mutations)
			{
				if (mutation == null || mutation.StatIdentityType <= 0 || mutation.StatId < 0 || mutation.MaximumValue < mutation.MinimumValue || (mutation.Kind != MissionStatMutationKind.AddClamped && mutation.Kind != MissionStatMutationKind.Set))
				{
					return AtomicResult(MissionAtomicRewardStatus.Rejected, value2, null, "Atomic stat mutation is unresolved or invalid.");
				}
				string key2 = MakeStatKey(CharacterId, mutation.StatIdentityType, mutation.StatId);
				characterStats.TryGetValue(key2, out var value3);
				decimal num = ((mutation.Kind == MissionStatMutationKind.AddClamped) ? ((decimal)value3 + (decimal)mutation.Value) : ((decimal)mutation.Value));
				long value4 = ((num < (decimal)mutation.MinimumValue) ? mutation.MinimumValue : ((num > (decimal)mutation.MaximumValue) ? mutation.MaximumValue : ((long)num)));
				list.Add(new MissionCharacterStatValue
				{
					StatIdentityType = mutation.StatIdentityType,
					StatId = mutation.StatId,
					Value = value4
				});
			}
			foreach (MissionCharacterStatValue item in list)
			{
				characterStats[MakeStatKey(CharacterId, item.StatIdentityType, item.StatId)] = item.Value;
			}
			MissionRewardStageRecord missionRewardStageRecord = ((value2 == null) ? new MissionRewardStageRecord
			{
				CharacterId = key.Mission.CharacterId,
				QuestId = key.Mission.QuestId,
				RewardKey = key.RewardKey,
				RewardType = rewardType,
				CreatedAtUtcTicks = appliedAtUtcTicks,
				Version = 1L
			} : value2.Clone());
			if (value2 != null)
			{
				missionRewardStageRecord.Version++;
			}
			missionRewardStageRecord.Status = MissionRewardStatus.Applied;
			missionRewardStageRecord.Attempts++;
			missionRewardStageRecord.EffectReference = effectReference;
			missionRewardStageRecord.LastError = null;
			missionRewardStageRecord.AppliedAtUtcTicks = appliedAtUtcTicks;
			missionRewardStageRecord.UpdatedAtUtcTicks = appliedAtUtcTicks;
			missionRewardStageRecord.ClaimExpiresAtUtcTicks = 0L;
			rewards[key] = missionRewardStageRecord.Clone();
			return AtomicResult(MissionAtomicRewardStatus.Applied, missionRewardStageRecord, list, "Atomic stat reward applied.");
		}

		internal void Commit(InMemoryMissionRepositoryState destination)
		{
			destination.Missions = missions;
			destination.Objectives = objectives;
			destination.Observations = observations;
			destination.Flags = flags;
			destination.AccountFlags = accountFlags;
			destination.Rewards = rewards;
			destination.CharacterStats = characterStats;
		}

		private static MissionRewardClaimResult ClaimResult(MissionRewardClaimStatus status, MissionRewardStageRecord stage, string message)
		{
			return new MissionRewardClaimResult
			{
				Status = status,
				Stage = stage?.Clone(),
				Message = message
			};
		}

		private static MissionAtomicStatRewardResult AtomicResult(MissionAtomicRewardStatus status, MissionRewardStageRecord stage, IEnumerable<MissionCharacterStatValue> statValues, string message)
		{
			return new MissionAtomicStatRewardResult
			{
				Status = status,
				Stage = stage?.Clone(),
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
			EnsureOwns(key.CharacterId);
		}

		private void EnsureOwns(int characterId)
		{
			if (characterId != CharacterId)
			{
				throw new InvalidOperationException("Transaction cannot mutate another character's mission state.");
			}
		}

		private string EnsureOwnsAccount(string accountKey)
		{
			string text = EnsureAccountKey(accountKey);
			if (string.IsNullOrWhiteSpace(AccountKey) || !string.Equals(AccountKey, text, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Transaction does not own the required stable account scope.");
			}
			return text;
		}

		private static string MakeObservationKey(MissionObjectiveKey key, string observationKey)
		{
			MissionObjectiveKey missionObjectiveKey = key;
			return missionObjectiveKey.ToString() + "|" + observationKey.Trim();
		}

		private static string MakeFlagKey(MissionKey key, string flagKey)
		{
			if (string.IsNullOrWhiteSpace(flagKey))
			{
				throw new InvalidOperationException("Mission flag key is required.");
			}
			MissionKey missionKey = key;
			return missionKey.ToString() + "|" + flagKey.Trim();
		}
	}

	private readonly InMemoryMissionRepositoryState state;

	public InMemoryMissionRepositoryState State => state;

	public InMemoryMissionRepository()
		: this(new InMemoryMissionRepositoryState())
	{
	}

	public InMemoryMissionRepository(InMemoryMissionRepositoryState state)
	{
		this.state = state ?? throw new ArgumentNullException("state");
	}

	public MissionStateRecord GetMission(MissionKey key)
	{
		lock (state.SyncRoot)
		{
			MissionStateRecord value;
			return state.Missions.TryGetValue(key, out value) ? value.Clone() : null;
		}
	}

	public IList<MissionStateRecord> GetMissions(int characterId)
	{
		EnsureCharacterId(characterId);
		lock (state.SyncRoot)
		{
			return (from value in state.Missions.Where((KeyValuePair<MissionKey, MissionStateRecord> value) => value.Key.CharacterId == characterId).OrderBy((KeyValuePair<MissionKey, MissionStateRecord> value) => value.Key.QuestId, StringComparer.OrdinalIgnoreCase)
				select value.Value.Clone()).ToList();
		}
	}

	public MissionCharacterSnapshot ReadCharacter(int characterId)
	{
		EnsureCharacterId(characterId);
		lock (state.SyncRoot)
		{
			return CreateSnapshot(characterId, state.Missions, state.Objectives, state.Flags, state.Rewards);
		}
	}

	public T Execute<T>(int characterId, Func<IMissionRepositoryTransaction, T> operation)
	{
		return Execute(characterId, null, operation);
	}

	public T Execute<T>(int characterId, string accountKey, Func<IMissionRepositoryTransaction, T> operation)
	{
		EnsureCharacterId(characterId);
		if (operation == null)
		{
			throw new ArgumentNullException("operation");
		}
		lock (state.SyncRoot)
		{
			InMemoryMissionRepositoryTransaction inMemoryMissionRepositoryTransaction = new InMemoryMissionRepositoryTransaction(characterId, string.IsNullOrWhiteSpace(accountKey) ? null : accountKey.Trim(), state);
			T result = operation(inMemoryMissionRepositoryTransaction);
			inMemoryMissionRepositoryTransaction.Commit(state);
			return result;
		}
	}

	public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
	{
		string accountKey2 = EnsureAccountKey(accountKey);
		string key = MakeAccountFlagKey(accountKey2, flagKey);
		lock (state.SyncRoot)
		{
			MissionAccountFlagRecord value;
			return state.AccountFlags.TryGetValue(key, out value) ? value.Clone() : null;
		}
	}

	public IList<MissionAccountFlagRecord> GetAccountFlags(string accountKey)
	{
		string normalizedAccountKey = EnsureAccountKey(accountKey);
		lock (state.SyncRoot)
		{
			return (from value in state.AccountFlags.Values.Where((MissionAccountFlagRecord value) => string.Equals(value.AccountKey, normalizedAccountKey, StringComparison.OrdinalIgnoreCase)).OrderBy((MissionAccountFlagRecord value) => value.FlagKey, StringComparer.OrdinalIgnoreCase)
				select value.Clone()).ToList();
		}
	}

	public void SeedCharacterStat(int characterId, int statIdentityType, int statId, long value)
	{
		EnsureCharacterId(characterId);
		lock (state.SyncRoot)
		{
			state.CharacterStats[MakeStatKey(characterId, statIdentityType, statId)] = value;
		}
	}

	public long GetCharacterStat(int characterId, int statIdentityType, int statId)
	{
		EnsureCharacterId(characterId);
		lock (state.SyncRoot)
		{
			long value;
			return state.CharacterStats.TryGetValue(MakeStatKey(characterId, statIdentityType, statId), out value) ? value : 0;
		}
	}

	private static MissionCharacterSnapshot CreateSnapshot(int characterId, IDictionary<MissionKey, MissionStateRecord> missions, IDictionary<MissionObjectiveKey, MissionObjectiveProgressRecord> objectives, IDictionary<string, MissionFlagRecord> flags, IDictionary<MissionRewardKey, MissionRewardStageRecord> rewards)
	{
		return new MissionCharacterSnapshot(characterId, from value in missions.Where((KeyValuePair<MissionKey, MissionStateRecord> value) => value.Key.CharacterId == characterId).OrderBy((KeyValuePair<MissionKey, MissionStateRecord> value) => value.Key.QuestId, StringComparer.OrdinalIgnoreCase)
			select value.Value, from value in objectives.Where((KeyValuePair<MissionObjectiveKey, MissionObjectiveProgressRecord> value) => value.Key.Mission.CharacterId == characterId).OrderBy((KeyValuePair<MissionObjectiveKey, MissionObjectiveProgressRecord> value) => value.Key.Mission.QuestId, StringComparer.OrdinalIgnoreCase).ThenBy((KeyValuePair<MissionObjectiveKey, MissionObjectiveProgressRecord> value) => value.Key.ObjectiveId, StringComparer.OrdinalIgnoreCase)
			select value.Value, flags.Values.Where((MissionFlagRecord value) => value.CharacterId == characterId).OrderBy((MissionFlagRecord value) => value.QuestId, StringComparer.OrdinalIgnoreCase).ThenBy((MissionFlagRecord value) => value.FlagKey, StringComparer.OrdinalIgnoreCase), from value in rewards.Where((KeyValuePair<MissionRewardKey, MissionRewardStageRecord> value) => value.Key.Mission.CharacterId == characterId).OrderBy((KeyValuePair<MissionRewardKey, MissionRewardStageRecord> value) => value.Key.Mission.QuestId, StringComparer.OrdinalIgnoreCase).ThenBy((KeyValuePair<MissionRewardKey, MissionRewardStageRecord> value) => value.Key.RewardKey, StringComparer.OrdinalIgnoreCase)
			select value.Value);
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
}
