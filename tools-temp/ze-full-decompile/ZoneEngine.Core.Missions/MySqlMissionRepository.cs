using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AORebirth.Database;
using Dapper;

namespace ZoneEngine.Core.Missions;

public sealed class MySqlMissionRepository : IMissionRepository
{
	private sealed class MySqlMissionRepositoryTransaction : IMissionRepositoryTransaction
	{
		private readonly IDbConnection connection;

		private readonly IDbTransaction transaction;

		public int CharacterId { get; private set; }

		public string AccountKey { get; private set; }

		public MySqlMissionRepositoryTransaction(int characterId, string accountKey, IDbConnection connection, IDbTransaction transaction)
		{
			CharacterId = characterId;
			AccountKey = accountKey;
			this.connection = connection;
			this.transaction = transaction;
			if (accountKey != null)
			{
				string text = SqlMapper.Query<string>(connection, "SELECT Username FROM characters WHERE Id=@CharacterId FOR UPDATE", (object)new
				{
					CharacterId = characterId
				}, transaction, true, (int?)null, (CommandType?)null).SingleOrDefault();
				if (string.IsNullOrWhiteSpace(text) || !string.Equals(text.Trim(), accountKey, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException("Mission account scope does not own character " + characterId + ".");
				}
			}
		}

		public MissionStateRecord GetMission(MissionKey key)
		{
			ValidateCharacterScope(key.CharacterId);
			ValidateMissionKey(key);
			return QueryMission(connection, transaction, key, forUpdate: true);
		}

		public IList<MissionStateRecord> GetMissions(int characterId)
		{
			ValidateCharacterScope(characterId);
			return QueryMissions(connection, transaction, characterId);
		}

		public void SaveMission(MissionKey key, MissionStateRecord record)
		{
			ValidateCharacterScope(key.CharacterId);
			ValidateMissionKey(key);
			if (record == null)
			{
				throw new ArgumentNullException("record");
			}
			ValidateRecordMissionKey(key, record.CharacterId, record.QuestId);
			ValidateText(record.CurrentStepId, "CurrentStepId", 128, allowNull: true);
			if (record.Version <= 0)
			{
				int rows = SqlMapper.Execute(connection, "INSERT INTO missionstates (CharacterId, QuestId, State, CurrentStepId, OfferedAtUtcTicks, AcceptedAtUtcTicks, CompletedAtUtcTicks, FailedAtUtcTicks, AbandonedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES (@CharacterId, @QuestId, @State, @CurrentStepId, @OfferedAtUtcTicks, @AcceptedAtUtcTicks, @CompletedAtUtcTicks, @FailedAtUtcTicks, @AbandonedAtUtcTicks, @CreatedAtUtcTicks, @UpdatedAtUtcTicks, 1)", (object)record, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows, "mission insert", key.ToString(), record.Version);
				record.Version = 1L;
			}
			else
			{
				long version = record.Version;
				int rows2 = SqlMapper.Execute(connection, "UPDATE missionstates SET State=@State, CurrentStepId=@CurrentStepId, OfferedAtUtcTicks=@OfferedAtUtcTicks, AcceptedAtUtcTicks=@AcceptedAtUtcTicks, CompletedAtUtcTicks=@CompletedAtUtcTicks, FailedAtUtcTicks=@FailedAtUtcTicks, AbandonedAtUtcTicks=@AbandonedAtUtcTicks, UpdatedAtUtcTicks=@UpdatedAtUtcTicks, Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND Version=@ExpectedVersion", (object)new
				{
					State = record.State,
					CurrentStepId = record.CurrentStepId,
					OfferedAtUtcTicks = record.OfferedAtUtcTicks,
					AcceptedAtUtcTicks = record.AcceptedAtUtcTicks,
					CompletedAtUtcTicks = record.CompletedAtUtcTicks,
					FailedAtUtcTicks = record.FailedAtUtcTicks,
					AbandonedAtUtcTicks = record.AbandonedAtUtcTicks,
					UpdatedAtUtcTicks = record.UpdatedAtUtcTicks,
					CharacterId = key.CharacterId,
					QuestId = key.QuestId,
					ExpectedVersion = version
				}, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows2, "mission update", key.ToString(), version);
				record.Version = version + 1;
			}
		}

		public MissionObjectiveProgressRecord GetObjective(MissionObjectiveKey key)
		{
			ValidateCharacterScope(key.Mission.CharacterId);
			ValidateObjectiveKey(key);
			return QueryObjective(connection, transaction, key, forUpdate: true);
		}

		public void SaveObjective(MissionObjectiveKey key, MissionObjectiveProgressRecord record)
		{
			ValidateCharacterScope(key.Mission.CharacterId);
			ValidateObjectiveKey(key);
			if (record == null)
			{
				throw new ArgumentNullException("record");
			}
			ValidateRecordMissionKey(key.Mission, record.CharacterId, record.QuestId);
			if (!string.Equals(key.ObjectiveId, record.ObjectiveId, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Objective record does not match the requested objective key.");
			}
			ValidateText(record.ObjectiveId, "ObjectiveId", 128, allowNull: false);
			ValidateText(record.LastObservationKey, "LastObservationKey", 191, allowNull: true);
			if (record.Progress < 0 || record.RequiredCount < 0)
			{
				throw new ArgumentOutOfRangeException("record", "Objective progress and required count cannot be negative.");
			}
			if (record.Version <= 0)
			{
				int rows = SqlMapper.Execute(connection, "INSERT INTO missionobjectiveprogress (CharacterId, QuestId, ObjectiveId, Progress, RequiredCount, LastObservationKey, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES (@CharacterId, @QuestId, @ObjectiveId, @Progress, @RequiredCount, @LastObservationKey, @CreatedAtUtcTicks, @UpdatedAtUtcTicks, 1)", (object)record, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows, "objective insert", key.ToString(), record.Version);
				record.Version = 1L;
			}
			else
			{
				long version = record.Version;
				int rows2 = SqlMapper.Execute(connection, "UPDATE missionobjectiveprogress SET Progress=@Progress, RequiredCount=@RequiredCount, LastObservationKey=@LastObservationKey, UpdatedAtUtcTicks=@UpdatedAtUtcTicks, Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND ObjectiveId=@ObjectiveId AND Version=@ExpectedVersion", (object)new
				{
					Progress = record.Progress,
					RequiredCount = record.RequiredCount,
					LastObservationKey = record.LastObservationKey,
					UpdatedAtUtcTicks = record.UpdatedAtUtcTicks,
					CharacterId = key.Mission.CharacterId,
					QuestId = key.Mission.QuestId,
					ObjectiveId = key.ObjectiveId,
					ExpectedVersion = version
				}, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows2, "objective update", key.ToString(), version);
				record.Version = version + 1;
			}
		}

		public bool TryAddObservation(MissionObjectiveObservationRecord observation)
		{
			if (observation == null)
			{
				throw new ArgumentNullException("observation");
			}
			MissionObjectiveKey objectiveKey = observation.ObjectiveKey;
			ValidateCharacterScope(objectiveKey.Mission.CharacterId);
			ValidateObjectiveKey(objectiveKey);
			ValidateText(observation.ObservationKey, "ObservationKey", 191, allowNull: false);
			ValidateText(observation.EventType, "EventType", 64, allowNull: false);
			ValidateText(observation.SourceIdentity, "SourceIdentity", 64, allowNull: true);
			ValidateText(observation.TargetIdentity, "TargetIdentity", 64, allowNull: true);
			observation.QuestId = objectiveKey.Mission.QuestId;
			observation.ObjectiveId = objectiveKey.ObjectiveId;
			observation.ObservationKey = observation.ObservationKey.Trim();
			return SqlMapper.Execute(connection, "INSERT IGNORE INTO missionobjectiveobservations (CharacterId, QuestId, ObjectiveId, ObservationKey, EventType, SourceIdentity, TargetIdentity, ObservedAtUtcTicks) VALUES (@CharacterId, @QuestId, @ObjectiveId, @ObservationKey, @EventType, @SourceIdentity, @TargetIdentity, @ObservedAtUtcTicks)", (object)observation, transaction, (int?)null, (CommandType?)null) == 1;
		}

		public MissionFlagRecord GetFlag(MissionKey key, string flagKey)
		{
			ValidateCharacterScope(key.CharacterId);
			ValidateMissionKey(key);
			ValidateText(flagKey, "flagKey", 128, allowNull: false);
			flagKey = flagKey.Trim();
			return QueryFlag(connection, transaction, key, flagKey, forUpdate: true);
		}

		public void SaveFlag(MissionKey key, MissionFlagRecord flag)
		{
			ValidateCharacterScope(key.CharacterId);
			ValidateMissionKey(key);
			if (flag == null)
			{
				throw new ArgumentNullException("flag");
			}
			ValidateRecordMissionKey(key, flag.CharacterId, flag.QuestId);
			ValidateText(flag.FlagKey, "FlagKey", 128, allowNull: false);
			ValidateText(flag.Value, "Value", 1024, allowNull: true);
			flag.FlagKey = flag.FlagKey.Trim();
			if (flag.Version <= 0)
			{
				int rows = SqlMapper.Execute(connection, "INSERT INTO missionflags (CharacterId, QuestId, FlagKey, `Value`, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES (@CharacterId, @QuestId, @FlagKey, @Value, @CreatedAtUtcTicks, @UpdatedAtUtcTicks, 1)", (object)flag, transaction, (int?)null, (CommandType?)null);
				MissionKey missionKey = key;
				RequireSingleWrite(rows, "mission flag insert", missionKey.ToString() + "|" + flag.FlagKey, flag.Version);
				flag.Version = 1L;
			}
			else
			{
				long version = flag.Version;
				int rows2 = SqlMapper.Execute(connection, "UPDATE missionflags SET `Value`=@Value, UpdatedAtUtcTicks=@UpdatedAtUtcTicks, Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND FlagKey=@FlagKey AND Version=@ExpectedVersion", (object)new
				{
					Value = flag.Value,
					UpdatedAtUtcTicks = flag.UpdatedAtUtcTicks,
					CharacterId = key.CharacterId,
					QuestId = key.QuestId,
					FlagKey = flag.FlagKey,
					ExpectedVersion = version
				}, transaction, (int?)null, (CommandType?)null);
				MissionKey missionKey = key;
				RequireSingleWrite(rows2, "mission flag update", missionKey.ToString() + "|" + flag.FlagKey, version);
				flag.Version = version + 1;
			}
		}

		public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
		{
			accountKey = NormalizeAccountKey(accountKey);
			ValidateAccountScope(accountKey);
			ValidateText(flagKey, "flagKey", 128, allowNull: false);
			accountKey = accountKey.Trim();
			flagKey = flagKey.Trim();
			return QueryAccountFlag(connection, transaction, accountKey, flagKey, forUpdate: true);
		}

		public void SaveAccountFlag(string accountKey, MissionAccountFlagRecord flag)
		{
			accountKey = NormalizeAccountKey(accountKey);
			ValidateAccountScope(accountKey);
			if (flag == null)
			{
				throw new ArgumentNullException("flag");
			}
			if (!string.Equals(accountKey, flag.AccountKey, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Account flag does not match the transaction account scope.");
			}
			ValidateText(flag.FlagKey, "FlagKey", 128, allowNull: false);
			ValidateText(flag.Value, "Value", 1024, allowNull: true);
			ValidateText(flag.SourceQuestId, "SourceQuestId", 128, allowNull: true);
			accountKey = accountKey.Trim();
			flag.AccountKey = accountKey;
			flag.FlagKey = flag.FlagKey.Trim();
			if (flag.Version <= 0)
			{
				int rows = SqlMapper.Execute(connection, "INSERT INTO missionaccountflags (AccountKey, FlagKey, `Value`, SourceQuestId, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES (@AccountKey, @FlagKey, @Value, @SourceQuestId, @CreatedAtUtcTicks, @UpdatedAtUtcTicks, 1)", (object)flag, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows, "account flag insert", accountKey + "|" + flag.FlagKey, flag.Version);
				flag.Version = 1L;
			}
			else
			{
				long version = flag.Version;
				int rows2 = SqlMapper.Execute(connection, "UPDATE missionaccountflags SET `Value`=@Value, SourceQuestId=@SourceQuestId, UpdatedAtUtcTicks=@UpdatedAtUtcTicks, Version=Version+1 WHERE AccountKey=@AccountKey AND FlagKey=@FlagKey AND Version=@ExpectedVersion", (object)new
				{
					Value = flag.Value,
					SourceQuestId = flag.SourceQuestId,
					UpdatedAtUtcTicks = flag.UpdatedAtUtcTicks,
					AccountKey = accountKey,
					FlagKey = flag.FlagKey,
					ExpectedVersion = version
				}, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows2, "account flag update", accountKey + "|" + flag.FlagKey, version);
				flag.Version = version + 1;
			}
		}

		public MissionRewardStageRecord GetReward(MissionRewardKey key)
		{
			ValidateCharacterScope(key.Mission.CharacterId);
			ValidateRewardKey(key);
			return QueryReward(connection, transaction, key, forUpdate: true);
		}

		public MissionRewardClaimResult TryClaimReward(MissionRewardKey key, string rewardType, string claimToken, long claimedAtUtcTicks, long claimExpiresAtUtcTicks)
		{
			ValidateCharacterScope(key.Mission.CharacterId);
			ValidateRewardKey(key);
			ValidateText(rewardType, "rewardType", 64, allowNull: false);
			ValidateText(claimToken, "claimToken", 64, allowNull: false);
			MissionStateRecord missionStateRecord = QueryMission(connection, transaction, key.Mission, forUpdate: true);
			if (missionStateRecord == null || missionStateRecord.State != MissionLifecycleState.Completed)
			{
				return CreateClaimResult(MissionRewardClaimStatus.Rejected, null, "Reward claims require a completed authoritative mission.");
			}
			if (claimedAtUtcTicks <= 0 || claimExpiresAtUtcTicks <= claimedAtUtcTicks)
			{
				return CreateClaimResult(MissionRewardClaimStatus.Rejected, null, "Reward claim expiry must be later than the claim time.");
			}
			MissionRewardStageRecord missionRewardStageRecord = QueryReward(connection, transaction, key, forUpdate: true);
			if (missionRewardStageRecord == null)
			{
				int rows = SqlMapper.Execute(connection, "INSERT INTO missionrewardledger (CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, AppliedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES (@CharacterId, @QuestId, @RewardKey, @RewardType, @Status, 1, NULL, NULL, @ClaimToken, @ClaimedAtUtcTicks, @ClaimExpiresAtUtcTicks, 0, @ClaimedAtUtcTicks, @ClaimedAtUtcTicks, 1)", (object)new
				{
					CharacterId = key.Mission.CharacterId,
					QuestId = key.Mission.QuestId,
					RewardKey = key.RewardKey,
					RewardType = rewardType,
					Status = MissionRewardStatus.InProgress,
					ClaimToken = claimToken,
					ClaimedAtUtcTicks = claimedAtUtcTicks,
					ClaimExpiresAtUtcTicks = claimExpiresAtUtcTicks
				}, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows, "reward claim insert", key.ToString(), 0L);
				return CreateClaimResult(MissionRewardClaimStatus.Claimed, QueryReward(connection, transaction, key, forUpdate: true), "Reward stage claimed.");
			}
			if (!string.Equals(missionRewardStageRecord.RewardType, rewardType, StringComparison.OrdinalIgnoreCase))
			{
				return CreateClaimResult(MissionRewardClaimStatus.Rejected, missionRewardStageRecord, "Reward type does not match the durable reward stage.");
			}
			if (missionRewardStageRecord.Status == MissionRewardStatus.Applied)
			{
				return CreateClaimResult(MissionRewardClaimStatus.AlreadyApplied, missionRewardStageRecord, "Reward stage is already applied.");
			}
			if (missionRewardStageRecord.Status == MissionRewardStatus.InProgress && missionRewardStageRecord.ClaimExpiresAtUtcTicks > claimedAtUtcTicks)
			{
				return CreateClaimResult(MissionRewardClaimStatus.Busy, missionRewardStageRecord, "Reward stage has an active claim.");
			}
			int num = SqlMapper.Execute(connection, "UPDATE missionrewardledger SET Status=@Status, Attempts=Attempts+1, LastError=NULL, ClaimToken=@ClaimToken, ClaimedAtUtcTicks=@ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks=@ClaimExpiresAtUtcTicks, UpdatedAtUtcTicks=@ClaimedAtUtcTicks, Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey AND Version=@ExpectedVersion", (object)new
			{
				Status = MissionRewardStatus.InProgress,
				ClaimToken = claimToken,
				ClaimedAtUtcTicks = claimedAtUtcTicks,
				ClaimExpiresAtUtcTicks = claimExpiresAtUtcTicks,
				CharacterId = key.Mission.CharacterId,
				QuestId = key.Mission.QuestId,
				RewardKey = key.RewardKey,
				ExpectedVersion = missionRewardStageRecord.Version
			}, transaction, (int?)null, (CommandType?)null);
			if (num != 1)
			{
				return CreateClaimResult(MissionRewardClaimStatus.Rejected, QueryReward(connection, transaction, key, forUpdate: true), "Reward claim lost an optimistic concurrency race.");
			}
			return CreateClaimResult(MissionRewardClaimStatus.Claimed, QueryReward(connection, transaction, key, forUpdate: true), "Reward stage claimed for retry.");
		}

		public bool TryMarkRewardApplied(MissionRewardKey key, string claimToken, long expectedVersion, string effectReference, long appliedAtUtcTicks, out MissionRewardStageRecord stage)
		{
			ValidateCharacterScope(key.Mission.CharacterId);
			ValidateRewardKey(key);
			ValidateText(claimToken, "claimToken", 64, allowNull: false);
			ValidateText(effectReference, "effectReference", 255, allowNull: true);
			if (appliedAtUtcTicks <= 0)
			{
				stage = QueryReward(connection, transaction, key, forUpdate: true);
				return false;
			}
			int num = SqlMapper.Execute(connection, "UPDATE missionrewardledger SET Status=@Status, EffectReference=@EffectReference, LastError=NULL, AppliedAtUtcTicks=@AppliedAtUtcTicks, ClaimExpiresAtUtcTicks=0, UpdatedAtUtcTicks=@AppliedAtUtcTicks, Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey AND Status=@ExpectedStatus AND ClaimToken=@ClaimToken AND Version=@ExpectedVersion", (object)new
			{
				Status = MissionRewardStatus.Applied,
				EffectReference = effectReference,
				AppliedAtUtcTicks = appliedAtUtcTicks,
				CharacterId = key.Mission.CharacterId,
				QuestId = key.Mission.QuestId,
				RewardKey = key.RewardKey,
				ExpectedStatus = MissionRewardStatus.InProgress,
				ClaimToken = claimToken,
				ExpectedVersion = expectedVersion
			}, transaction, (int?)null, (CommandType?)null);
			stage = QueryReward(connection, transaction, key, forUpdate: true);
			return num == 1;
		}

		public bool TryMarkRewardFailed(MissionRewardKey key, string claimToken, long expectedVersion, string error, long failedAtUtcTicks, out MissionRewardStageRecord stage)
		{
			ValidateCharacterScope(key.Mission.CharacterId);
			ValidateRewardKey(key);
			ValidateText(claimToken, "claimToken", 64, allowNull: false);
			ValidateText(error, "error", 1024, allowNull: true);
			if (failedAtUtcTicks <= 0)
			{
				stage = QueryReward(connection, transaction, key, forUpdate: true);
				return false;
			}
			int num = SqlMapper.Execute(connection, "UPDATE missionrewardledger SET Status=@Status, LastError=@LastError, ClaimExpiresAtUtcTicks=0, UpdatedAtUtcTicks=@FailedAtUtcTicks, Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey AND Status=@ExpectedStatus AND ClaimToken=@ClaimToken AND Version=@ExpectedVersion", (object)new
			{
				Status = MissionRewardStatus.Failed,
				LastError = error,
				FailedAtUtcTicks = failedAtUtcTicks,
				CharacterId = key.Mission.CharacterId,
				QuestId = key.Mission.QuestId,
				RewardKey = key.RewardKey,
				ExpectedStatus = MissionRewardStatus.InProgress,
				ClaimToken = claimToken,
				ExpectedVersion = expectedVersion
			}, transaction, (int?)null, (CommandType?)null);
			stage = QueryReward(connection, transaction, key, forUpdate: true);
			return num == 1;
		}

		public MissionAtomicStatRewardResult TryApplyCharacterStatReward(MissionRewardKey key, string rewardType, IList<MissionCharacterStatMutation> mutations, string effectReference, long appliedAtUtcTicks)
		{
			ValidateCharacterScope(key.Mission.CharacterId);
			ValidateRewardKey(key);
			ValidateText(rewardType, "rewardType", 64, allowNull: false);
			ValidateText(effectReference, "effectReference", 255, allowNull: true);
			MissionStateRecord missionStateRecord = QueryMission(connection, transaction, key.Mission, forUpdate: true);
			if (missionStateRecord == null || missionStateRecord.State != MissionLifecycleState.Completed)
			{
				return CreateAtomicResult(MissionAtomicRewardStatus.Rejected, null, new MissionCharacterStatValue[0], "Character stat rewards require a completed authoritative mission.");
			}
			if (mutations == null || mutations.Count == 0 || appliedAtUtcTicks <= 0)
			{
				return CreateAtomicResult(MissionAtomicRewardStatus.Rejected, null, new MissionCharacterStatValue[0], "At least one character stat mutation is required.");
			}
			MissionRewardStageRecord missionRewardStageRecord = QueryReward(connection, transaction, key, forUpdate: true);
			if (missionRewardStageRecord != null && !string.Equals(missionRewardStageRecord.RewardType, rewardType, StringComparison.OrdinalIgnoreCase))
			{
				return CreateAtomicResult(MissionAtomicRewardStatus.Rejected, missionRewardStageRecord, new MissionCharacterStatValue[0], "Reward type does not match the durable reward stage.");
			}
			if (missionRewardStageRecord != null && missionRewardStageRecord.Status == MissionRewardStatus.Applied)
			{
				return CreateAtomicResult(MissionAtomicRewardStatus.AlreadyApplied, missionRewardStageRecord, ReadStatValues(mutations), "Character stat reward is already applied.");
			}
			if (missionRewardStageRecord != null && missionRewardStageRecord.Status == MissionRewardStatus.InProgress && missionRewardStageRecord.ClaimExpiresAtUtcTicks > appliedAtUtcTicks)
			{
				return CreateAtomicResult(MissionAtomicRewardStatus.Rejected, missionRewardStageRecord, new MissionCharacterStatValue[0], "Reward stage has an active non-stat claim.");
			}
			IList<MissionCharacterStatValue> values = ApplyStatMutations(mutations);
			if (missionRewardStageRecord == null)
			{
				int rows = SqlMapper.Execute(connection, "INSERT INTO missionrewardledger (CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, AppliedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES (@CharacterId, @QuestId, @RewardKey, @RewardType, @Status, 1, NULL, @EffectReference, NULL, @AppliedAtUtcTicks, 0, @AppliedAtUtcTicks, @AppliedAtUtcTicks, @AppliedAtUtcTicks, 1)", (object)new
				{
					CharacterId = key.Mission.CharacterId,
					QuestId = key.Mission.QuestId,
					RewardKey = key.RewardKey,
					RewardType = rewardType,
					Status = MissionRewardStatus.Applied,
					EffectReference = effectReference,
					AppliedAtUtcTicks = appliedAtUtcTicks
				}, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows, "atomic reward insert", key.ToString(), 0L);
			}
			else
			{
				int rows2 = SqlMapper.Execute(connection, "UPDATE missionrewardledger SET Status=@Status, Attempts=Attempts+1, LastError=NULL, EffectReference=@EffectReference, ClaimToken=NULL, ClaimedAtUtcTicks=@AppliedAtUtcTicks, ClaimExpiresAtUtcTicks=0, AppliedAtUtcTicks=@AppliedAtUtcTicks, UpdatedAtUtcTicks=@AppliedAtUtcTicks, Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey AND Version=@ExpectedVersion", (object)new
				{
					Status = MissionRewardStatus.Applied,
					EffectReference = effectReference,
					AppliedAtUtcTicks = appliedAtUtcTicks,
					CharacterId = key.Mission.CharacterId,
					QuestId = key.Mission.QuestId,
					RewardKey = key.RewardKey,
					ExpectedVersion = missionRewardStageRecord.Version
				}, transaction, (int?)null, (CommandType?)null);
				RequireSingleWrite(rows2, "atomic stat reward", key.ToString(), missionRewardStageRecord.Version);
			}
			return CreateAtomicResult(MissionAtomicRewardStatus.Applied, QueryReward(connection, transaction, key, forUpdate: true), values, "Character stat reward and reward ledger were applied in one database transaction.");
		}

		public MissionCharacterSnapshot ReadCharacter()
		{
			return new MissionCharacterSnapshot(CharacterId, QueryMissions(connection, transaction, CharacterId), QueryObjectives(connection, transaction, CharacterId), QueryFlags(connection, transaction, CharacterId), QueryRewards(connection, transaction, CharacterId));
		}

		private IList<MissionCharacterStatValue> ApplyStatMutations(IList<MissionCharacterStatMutation> mutations)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			List<MissionCharacterStatValue> list = new List<MissionCharacterStatValue>();
			foreach (MissionCharacterStatMutation mutation in mutations)
			{
				ValidateStatMutation(mutation);
				string text = mutation.StatIdentityType + "|" + mutation.StatId;
				if (!hashSet.Add(text))
				{
					throw new InvalidOperationException("Duplicate character stat mutation: " + text);
				}
				long current2 = ReadStatValue(mutation.StatIdentityType, mutation.StatId);
				long num = ((mutation.Kind == MissionStatMutationKind.Set) ? Clamp(mutation.Value, mutation.MinimumValue, mutation.MaximumValue) : AddClamped(current2, mutation.Value, mutation.MinimumValue, mutation.MaximumValue));
				SqlMapper.Execute(connection, "INSERT INTO stats (Instance, Type, StatId, StatValue) VALUES (@Instance, @Type, @StatId, @StatValue) ON DUPLICATE KEY UPDATE StatValue=@StatValue", (object)new
				{
					Instance = CharacterId,
					Type = mutation.StatIdentityType,
					StatId = mutation.StatId,
					StatValue = (int)num
				}, transaction, (int?)null, (CommandType?)null);
				list.Add(new MissionCharacterStatValue
				{
					StatIdentityType = mutation.StatIdentityType,
					StatId = mutation.StatId,
					Value = num
				});
			}
			return list;
		}

		private IList<MissionCharacterStatValue> ReadStatValues(IList<MissionCharacterStatMutation> mutations)
		{
			List<MissionCharacterStatValue> list = new List<MissionCharacterStatValue>();
			foreach (MissionCharacterStatMutation mutation in mutations)
			{
				ValidateStatMutation(mutation);
				list.Add(new MissionCharacterStatValue
				{
					StatIdentityType = mutation.StatIdentityType,
					StatId = mutation.StatId,
					Value = ReadStatValue(mutation.StatIdentityType, mutation.StatId)
				});
			}
			return list;
		}

		private long ReadStatValue(int statIdentityType, int statId)
		{
			int? num = SqlMapper.Query<int?>(connection, "SELECT StatValue FROM stats WHERE Instance=@Instance AND Type=@Type AND StatId=@StatId FOR UPDATE", (object)new
			{
				Instance = CharacterId,
				Type = statIdentityType,
				StatId = statId
			}, transaction, true, (int?)null, (CommandType?)null).SingleOrDefault();
			return num.HasValue ? num.Value : 0;
		}

		private void ValidateCharacterScope(int characterId)
		{
			ValidateCharacterId(characterId);
			if (characterId != CharacterId)
			{
				throw new InvalidOperationException("Mission transaction cannot access character " + characterId + " while scoped to character " + CharacterId + ".");
			}
		}

		private void ValidateAccountScope(string accountKey)
		{
			accountKey = NormalizeAccountKey(accountKey);
			if (AccountKey == null)
			{
				throw new InvalidOperationException("Account-scoped mission flags require an explicitly account-scoped transaction.");
			}
			if (!string.Equals(AccountKey, accountKey, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Mission transaction cannot access account '" + accountKey + "' while scoped to account '" + AccountKey + "'.");
			}
		}

		private void ValidateRecordMissionKey(MissionKey key, int characterId, string questId)
		{
			if (key.CharacterId != characterId || !string.Equals(key.QuestId, questId, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Mission record does not match the requested mission key.");
			}
		}

		private void RequireSingleWrite(int rows, string operation, string key, long expectedVersion)
		{
			if (rows != 1)
			{
				throw new InvalidOperationException("Persistent " + operation + " failed optimistic concurrency for '" + key + "' at version " + expectedVersion + ".");
			}
		}

		private static void ValidateStatMutation(MissionCharacterStatMutation mutation)
		{
			if (mutation == null)
			{
				throw new ArgumentNullException("mutation");
			}
			if (mutation.StatIdentityType <= 0 || mutation.StatId < 0)
			{
				throw new ArgumentOutOfRangeException("mutation", "Stat identity type and stat id are invalid.");
			}
			if (mutation.MinimumValue > mutation.MaximumValue || mutation.MinimumValue < int.MinValue || mutation.MaximumValue > int.MaxValue)
			{
				throw new ArgumentOutOfRangeException("mutation", "Stat mutation bounds must fit the persisted INT StatValue range.");
			}
			if (mutation.Kind != MissionStatMutationKind.AddClamped && mutation.Kind != MissionStatMutationKind.Set)
			{
				throw new ArgumentOutOfRangeException("mutation", "Unsupported stat mutation kind.");
			}
		}

		private static long AddClamped(long current, long delta, long minimum, long maximum)
		{
			if (delta > 0 && current > long.MaxValue - delta)
			{
				return maximum;
			}
			if (delta < 0 && current < long.MinValue - delta)
			{
				return minimum;
			}
			return Clamp(current + delta, minimum, maximum);
		}

		private static long Clamp(long value, long minimum, long maximum)
		{
			return (value < minimum) ? minimum : ((value > maximum) ? maximum : value);
		}

		private static MissionRewardClaimResult CreateClaimResult(MissionRewardClaimStatus status, MissionRewardStageRecord stage, string message)
		{
			return new MissionRewardClaimResult
			{
				Status = status,
				Stage = stage?.Clone(),
				Message = message
			};
		}

		private static MissionAtomicStatRewardResult CreateAtomicResult(MissionAtomicRewardStatus status, MissionRewardStageRecord stage, IList<MissionCharacterStatValue> values, string message)
		{
			MissionAtomicStatRewardResult missionAtomicStatRewardResult = new MissionAtomicStatRewardResult();
			missionAtomicStatRewardResult.Status = status;
			missionAtomicStatRewardResult.Stage = stage?.Clone();
			missionAtomicStatRewardResult.StatValues = values ?? new MissionCharacterStatValue[0];
			missionAtomicStatRewardResult.Message = message;
			return missionAtomicStatRewardResult;
		}
	}

	public MissionStateRecord GetMission(MissionKey key)
	{
		ValidateMissionKey(key);
		using IDbConnection connection = Connector.GetConnection();
		return QueryMission(connection, null, key, forUpdate: false);
	}

	public IList<MissionStateRecord> GetMissions(int characterId)
	{
		ValidateCharacterId(characterId);
		using IDbConnection connection = Connector.GetConnection();
		return QueryMissions(connection, null, characterId);
	}

	public MissionCharacterSnapshot ReadCharacter(int characterId)
	{
		return Execute(characterId, (IMissionRepositoryTransaction transaction) => ((MySqlMissionRepositoryTransaction)transaction).ReadCharacter());
	}

	public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
	{
		accountKey = NormalizeAccountKey(accountKey);
		ValidateText(flagKey, "flagKey", 128, allowNull: false);
		flagKey = flagKey.Trim();
		using IDbConnection connection = Connector.GetConnection();
		return QueryAccountFlag(connection, null, accountKey, flagKey, forUpdate: false);
	}

	public IList<MissionAccountFlagRecord> GetAccountFlags(string accountKey)
	{
		accountKey = NormalizeAccountKey(accountKey);
		using IDbConnection connection = Connector.GetConnection();
		return QueryAccountFlags(connection, null, accountKey);
	}

	public T Execute<T>(int characterId, Func<IMissionRepositoryTransaction, T> operation)
	{
		return Execute(characterId, null, operation);
	}

	public T Execute<T>(int characterId, string accountKey, Func<IMissionRepositoryTransaction, T> operation)
	{
		ValidateCharacterId(characterId);
		if (accountKey != null)
		{
			accountKey = NormalizeAccountKey(accountKey);
		}
		if (operation == null)
		{
			throw new ArgumentNullException("operation");
		}
		using IDbConnection dbConnection = Connector.GetConnection();
		using IDbTransaction dbTransaction = dbConnection.BeginTransaction();
		try
		{
			T result = operation(new MySqlMissionRepositoryTransaction(characterId, accountKey, dbConnection, dbTransaction));
			dbTransaction.Commit();
			return result;
		}
		catch
		{
			dbTransaction.Rollback();
			throw;
		}
	}

	private static MissionStateRecord QueryMission(IDbConnection connection, IDbTransaction transaction, MissionKey key, bool forUpdate)
	{
		return SqlMapper.Query<MissionStateRecord>(connection, "SELECT CharacterId, QuestId, State, CurrentStepId, OfferedAtUtcTicks, AcceptedAtUtcTicks, CompletedAtUtcTicks, FailedAtUtcTicks, AbandonedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionstates WHERE CharacterId=@CharacterId AND QuestId=@QuestId" + (forUpdate ? " FOR UPDATE" : string.Empty), (object)new { key.CharacterId, key.QuestId }, transaction, true, (int?)null, (CommandType?)null).SingleOrDefault();
	}

	private static IList<MissionStateRecord> QueryMissions(IDbConnection connection, IDbTransaction transaction, int characterId)
	{
		return SqlMapper.Query<MissionStateRecord>(connection, "SELECT CharacterId, QuestId, State, CurrentStepId, OfferedAtUtcTicks, AcceptedAtUtcTicks, CompletedAtUtcTicks, FailedAtUtcTicks, AbandonedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionstates WHERE CharacterId=@CharacterId ORDER BY QuestId", (object)new
		{
			CharacterId = characterId
		}, transaction, true, (int?)null, (CommandType?)null).ToList();
	}

	private static MissionObjectiveProgressRecord QueryObjective(IDbConnection connection, IDbTransaction transaction, MissionObjectiveKey key, bool forUpdate)
	{
		return SqlMapper.Query<MissionObjectiveProgressRecord>(connection, "SELECT CharacterId, QuestId, ObjectiveId, Progress, RequiredCount, LastObservationKey, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionobjectiveprogress WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND ObjectiveId=@ObjectiveId" + (forUpdate ? " FOR UPDATE" : string.Empty), (object)new
		{
			key.Mission.CharacterId,
			key.Mission.QuestId,
			key.ObjectiveId
		}, transaction, true, (int?)null, (CommandType?)null).SingleOrDefault();
	}

	private static IList<MissionObjectiveProgressRecord> QueryObjectives(IDbConnection connection, IDbTransaction transaction, int characterId)
	{
		return SqlMapper.Query<MissionObjectiveProgressRecord>(connection, "SELECT CharacterId, QuestId, ObjectiveId, Progress, RequiredCount, LastObservationKey, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionobjectiveprogress WHERE CharacterId=@CharacterId ORDER BY QuestId, ObjectiveId", (object)new
		{
			CharacterId = characterId
		}, transaction, true, (int?)null, (CommandType?)null).ToList();
	}

	private static MissionFlagRecord QueryFlag(IDbConnection connection, IDbTransaction transaction, MissionKey key, string flagKey, bool forUpdate)
	{
		return SqlMapper.Query<MissionFlagRecord>(connection, "SELECT CharacterId, QuestId, FlagKey, `Value`, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionflags WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND FlagKey=@FlagKey" + (forUpdate ? " FOR UPDATE" : string.Empty), (object)new
		{
			CharacterId = key.CharacterId,
			QuestId = key.QuestId,
			FlagKey = flagKey
		}, transaction, true, (int?)null, (CommandType?)null).SingleOrDefault();
	}

	private static IList<MissionFlagRecord> QueryFlags(IDbConnection connection, IDbTransaction transaction, int characterId)
	{
		return SqlMapper.Query<MissionFlagRecord>(connection, "SELECT CharacterId, QuestId, FlagKey, `Value`, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionflags WHERE CharacterId=@CharacterId ORDER BY QuestId, FlagKey", (object)new
		{
			CharacterId = characterId
		}, transaction, true, (int?)null, (CommandType?)null).ToList();
	}

	private static MissionAccountFlagRecord QueryAccountFlag(IDbConnection connection, IDbTransaction transaction, string accountKey, string flagKey, bool forUpdate)
	{
		return SqlMapper.Query<MissionAccountFlagRecord>(connection, "SELECT AccountKey, FlagKey, `Value`, SourceQuestId, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionaccountflags WHERE AccountKey=@AccountKey AND FlagKey=@FlagKey" + (forUpdate ? " FOR UPDATE" : string.Empty), (object)new
		{
			AccountKey = accountKey,
			FlagKey = flagKey
		}, transaction, true, (int?)null, (CommandType?)null).SingleOrDefault();
	}

	private static IList<MissionAccountFlagRecord> QueryAccountFlags(IDbConnection connection, IDbTransaction transaction, string accountKey)
	{
		return SqlMapper.Query<MissionAccountFlagRecord>(connection, "SELECT AccountKey, FlagKey, `Value`, SourceQuestId, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionaccountflags WHERE AccountKey=@AccountKey ORDER BY FlagKey", (object)new
		{
			AccountKey = accountKey
		}, transaction, true, (int?)null, (CommandType?)null).ToList();
	}

	private static MissionRewardStageRecord QueryReward(IDbConnection connection, IDbTransaction transaction, MissionRewardKey key, bool forUpdate)
	{
		return SqlMapper.Query<MissionRewardStageRecord>(connection, "SELECT CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, AppliedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionrewardledger WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey" + (forUpdate ? " FOR UPDATE" : string.Empty), (object)new
		{
			key.Mission.CharacterId,
			key.Mission.QuestId,
			key.RewardKey
		}, transaction, true, (int?)null, (CommandType?)null).SingleOrDefault();
	}

	private static IList<MissionRewardStageRecord> QueryRewards(IDbConnection connection, IDbTransaction transaction, int characterId)
	{
		return SqlMapper.Query<MissionRewardStageRecord>(connection, "SELECT CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, AppliedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionrewardledger WHERE CharacterId=@CharacterId ORDER BY QuestId, RewardKey", (object)new
		{
			CharacterId = characterId
		}, transaction, true, (int?)null, (CommandType?)null).ToList();
	}

	private static void ValidateCharacterId(int characterId)
	{
		if (characterId <= 0)
		{
			throw new ArgumentOutOfRangeException("characterId", "Stable character identity must be positive.");
		}
	}

	private static void ValidateMissionKey(MissionKey key)
	{
		ValidateCharacterId(key.CharacterId);
		ValidateText(key.QuestId, "questId", 128, allowNull: false);
	}

	private static void ValidateObjectiveKey(MissionObjectiveKey key)
	{
		ValidateMissionKey(key.Mission);
		ValidateText(key.ObjectiveId, "objectiveId", 128, allowNull: false);
	}

	private static void ValidateRewardKey(MissionRewardKey key)
	{
		ValidateMissionKey(key.Mission);
		ValidateText(key.RewardKey, "rewardKey", 191, allowNull: false);
	}

	private static string NormalizeAccountKey(string accountKey)
	{
		ValidateText(accountKey, "accountKey", 32, allowNull: false);
		string text = accountKey.Trim();
		ValidateText(text, "accountKey", 32, allowNull: false);
		return text;
	}

	private static void ValidateText(string value, string parameterName, int maximumLength, bool allowNull)
	{
		if (value == null)
		{
			if (!allowNull)
			{
				throw new ArgumentNullException(parameterName);
			}
			return;
		}
		if (!allowNull && string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException(parameterName + " is required.", parameterName);
		}
		if (value.Length > maximumLength)
		{
			throw new ArgumentOutOfRangeException(parameterName, parameterName + " exceeds the persisted maximum length of " + maximumLength + ".");
		}
	}
}
