using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Missions;

public sealed class PersistentMissionService
{
	private readonly Dictionary<string, MissionDefinition> definitions;

	private readonly IMissionRepository repository;

	private readonly Func<long> utcNowTicks;

	public PersistentMissionService(IMissionRepository repository, IEnumerable<MissionDefinition> definitions)
		: this(repository, definitions, () => DateTime.UtcNow.Ticks)
	{
	}

	public PersistentMissionService(IMissionRepository repository, IEnumerable<MissionDefinition> definitions, Func<long> utcNowTicks)
	{
		this.repository = repository ?? throw new ArgumentNullException("repository");
		this.utcNowTicks = utcNowTicks ?? throw new ArgumentNullException("utcNowTicks");
		this.definitions = ValidateAndIndexDefinitions(definitions);
	}

	public MissionStateRecord GetMission(int characterId, string questId)
	{
		MissionKey key;
		return TryCreateKey(characterId, questId, out key) ? repository.GetMission(key) : null;
	}

	public IList<MissionStateRecord> GetMissions(int characterId)
	{
		IList<MissionStateRecord> result;
		if (characterId <= 0)
		{
			IList<MissionStateRecord> list = new List<MissionStateRecord>();
			result = list;
		}
		else
		{
			result = repository.GetMissions(characterId);
		}
		return result;
	}

	public MissionObjectiveProgressRecord GetObjective(int characterId, string questId, string objectiveId)
	{
		if (characterId <= 0 || string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId))
		{
			return null;
		}
		return repository.ReadCharacter(characterId)?.Objectives.FirstOrDefault((MissionObjectiveProgressRecord objective) => string.Equals(objective.QuestId, questId.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(objective.ObjectiveId, objectiveId.Trim(), StringComparison.OrdinalIgnoreCase));
	}

	public bool TryGetDefinition(string questId, out MissionDefinition definition)
	{
		if (string.IsNullOrWhiteSpace(questId))
		{
			definition = null;
			return false;
		}
		return definitions.TryGetValue(questId.Trim(), out definition);
	}

	public MissionOperationResult OfferMission(int characterId, string questId)
	{
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, questId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		long now = Now();
		return repository.Execute(characterId, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionStateRecord mission = transaction.GetMission(key);
			if (mission != null)
			{
				if (mission.State == MissionLifecycleState.Offered || mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Completed)
				{
					return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Mission was already offered.");
				}
				return Result(MissionOperationStatus.Rejected, mission, null, "Terminal missions are not repeatable.");
			}
			foreach (string prerequisiteQuestId in definition.PrerequisiteQuestIds)
			{
				MissionStateRecord mission2 = transaction.GetMission(new MissionKey(characterId, prerequisiteQuestId));
				if (mission2 == null || mission2.State != MissionLifecycleState.Completed)
				{
					return Result(MissionOperationStatus.Rejected, null, null, "Mission prerequisite is not completed for this character: " + prerequisiteQuestId);
				}
			}
			MissionStateRecord missionStateRecord = new MissionStateRecord
			{
				CharacterId = characterId,
				QuestId = definition.QuestId,
				State = MissionLifecycleState.Offered,
				CurrentStepId = definition.InitialStepId,
				OfferedAtUtcTicks = now,
				CreatedAtUtcTicks = now,
				UpdatedAtUtcTicks = now
			};
			transaction.SaveMission(key, missionStateRecord);
			foreach (MissionObjectiveDefinition item in definition.Objectives.Where((MissionObjectiveDefinition value) => value.IsResolved))
			{
				MissionObjectiveKey key2 = new MissionObjectiveKey(key, item.ObjectiveId);
				transaction.SaveObjective(key2, new MissionObjectiveProgressRecord
				{
					CharacterId = characterId,
					QuestId = definition.QuestId,
					ObjectiveId = item.ObjectiveId,
					Progress = 0,
					RequiredCount = item.RequiredCount,
					CreatedAtUtcTicks = now,
					UpdatedAtUtcTicks = now
				});
			}
			return Result(MissionOperationStatus.Applied, missionStateRecord, null, "Mission offered.");
		});
	}

	public MissionOperationResult AcceptMission(int characterId, string questId)
	{
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, questId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		long now = Now();
		return repository.Execute(characterId, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionStateRecord mission = transaction.GetMission(key);
			if (mission == null)
			{
				return Result(MissionOperationStatus.NotFound, null, null, "Mission has not been offered.");
			}
			if (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Completed)
			{
				return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Mission was already accepted.");
			}
			if (mission.State != MissionLifecycleState.Offered)
			{
				return Result(MissionOperationStatus.Rejected, mission, null, "Mission cannot be accepted from its current state.");
			}
			mission.State = MissionLifecycleState.Active;
			mission.AcceptedAtUtcTicks = now;
			mission.UpdatedAtUtcTicks = now;
			transaction.SaveMission(key, mission);
			return Result(MissionOperationStatus.Applied, mission, null, "Mission accepted.");
		});
	}

	public MissionOperationResult ChangeStep(int characterId, string questId, string stepId)
	{
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, questId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		if (string.IsNullOrWhiteSpace(stepId) || !definition.StepIds.Contains(stepId.Trim(), StringComparer.OrdinalIgnoreCase))
		{
			return Result(MissionOperationStatus.Unresolved, null, null, "Mission step is not defined.");
		}
		string normalizedStepId = stepId.Trim();
		long now = Now();
		return repository.Execute(characterId, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionStateRecord mission = transaction.GetMission(key);
			if (mission == null)
			{
				return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
			}
			if (mission.State != MissionLifecycleState.Active)
			{
				return Result(MissionOperationStatus.Rejected, mission, null, "Only active missions can change step.");
			}
			if (string.Equals(mission.CurrentStepId, normalizedStepId, StringComparison.OrdinalIgnoreCase))
			{
				return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Mission is already on that step.");
			}
			mission.CurrentStepId = normalizedStepId;
			mission.UpdatedAtUtcTicks = now;
			transaction.SaveMission(key, mission);
			return Result(MissionOperationStatus.Applied, mission, null, "Mission step changed.");
		});
	}

	public MissionOperationResult ObserveObjective(MissionObjectiveObservation observation)
	{
		if (observation == null || observation.Amount <= 0 || string.IsNullOrWhiteSpace(observation.ObjectiveId) || string.IsNullOrWhiteSpace(observation.ObservationKey) || string.IsNullOrWhiteSpace(observation.EventType))
		{
			return Result(MissionOperationStatus.Unresolved, null, null, "Objective observation is incomplete.");
		}
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(observation.CharacterId, observation.QuestId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		MissionObjectiveDefinition objectiveDefinition = definition.Objectives.FirstOrDefault((MissionObjectiveDefinition value) => string.Equals(value.ObjectiveId, observation.ObjectiveId, StringComparison.OrdinalIgnoreCase));
		if (objectiveDefinition == null || !objectiveDefinition.IsResolved || objectiveDefinition.RequiredCount <= 0)
		{
			return Result(MissionOperationStatus.Unresolved, null, null, "Objective behavior is unresolved.");
		}
		long now = Now();
		return repository.Execute(observation.CharacterId, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionStateRecord mission = transaction.GetMission(key);
			if (mission == null)
			{
				return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
			}
			if (mission.State != MissionLifecycleState.Active)
			{
				return Result(MissionOperationStatus.Rejected, mission, null, "Only active missions accept observations.");
			}
			if (!string.Equals(mission.CurrentStepId, objectiveDefinition.StepId, StringComparison.OrdinalIgnoreCase))
			{
				return Result(MissionOperationStatus.Rejected, mission, null, "Objective is not part of the current step.");
			}
			MissionObjectiveKey key2 = new MissionObjectiveKey(key, objectiveDefinition.ObjectiveId);
			MissionObjectiveProgressRecord objective = transaction.GetObjective(key2);
			if (objective == null)
			{
				return Result(MissionOperationStatus.Unresolved, mission, null, "Objective progress was not initialized.");
			}
			if (objective.Progress >= objective.RequiredCount)
			{
				return Result(MissionOperationStatus.AlreadyApplied, mission, objective, "Objective was already completed.");
			}
			if (!transaction.TryAddObservation(new MissionObjectiveObservationRecord
			{
				CharacterId = observation.CharacterId,
				QuestId = key.QuestId,
				ObjectiveId = objectiveDefinition.ObjectiveId,
				ObservationKey = observation.ObservationKey.Trim(),
				EventType = observation.EventType.Trim(),
				SourceIdentity = observation.SourceIdentity,
				TargetIdentity = observation.TargetIdentity,
				ObservedAtUtcTicks = now
			}))
			{
				return Result(MissionOperationStatus.DuplicateObservation, mission, objective, "Duplicate objective observation was ignored.");
			}
			objective.Progress = Math.Min(objective.RequiredCount, objective.Progress + observation.Amount);
			objective.LastObservationKey = observation.ObservationKey.Trim();
			objective.UpdatedAtUtcTicks = now;
			transaction.SaveObjective(key2, objective);
			return Result(MissionOperationStatus.Applied, mission, objective, "Objective progress updated.");
		});
	}

	public MissionOperationResult CompleteMission(int characterId, string questId)
	{
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, questId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		long now = Now();
		return repository.Execute(characterId, (IMissionRepositoryTransaction transaction) => CompleteWithinTransaction(transaction, key, definition, now));
	}

	public MissionOperationResult CompleteAndActivateNextMission(int characterId, string questId, string nextQuestId)
	{
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, questId, out var key, out var definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		missionOperationResult = ResolveMutation(characterId, nextQuestId, out var nextKey, out var nextDefinition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		if (key.Equals(nextKey))
		{
			return Result(MissionOperationStatus.Unresolved, null, null, "Mission completion cannot activate the same mission.");
		}
		long now = Now();
		return repository.Execute(characterId, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionStateRecord mission = transaction.GetMission(key);
			if (mission == null)
			{
				return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
			}
			if (mission.State != MissionLifecycleState.Active && mission.State != MissionLifecycleState.Completed)
			{
				return Result(MissionOperationStatus.Rejected, mission, null, "Only an active or completed mission can activate its handoff.");
			}
			if (mission.State == MissionLifecycleState.Active)
			{
				foreach (MissionObjectiveDefinition objective2 in definition.Objectives)
				{
					if (!objective2.IsResolved || objective2.RequiredCount <= 0)
					{
						return Result(MissionOperationStatus.Unresolved, mission, null, "Mission has unresolved objective behavior.");
					}
					MissionObjectiveProgressRecord objective = transaction.GetObjective(new MissionObjectiveKey(key, objective2.ObjectiveId));
					if (objective == null || objective.Progress < objective.RequiredCount)
					{
						return Result(MissionOperationStatus.Rejected, mission, objective, "Mission objectives are incomplete.");
					}
				}
			}
			MissionStateRecord mission2 = transaction.GetMission(nextKey);
			if (mission2 != null && mission2.State != MissionLifecycleState.Offered && mission2.State != MissionLifecycleState.Active && mission2.State != MissionLifecycleState.Completed)
			{
				return Result(MissionOperationStatus.Rejected, mission, null, "The next mission is in a non-repeatable terminal state.");
			}
			foreach (string prerequisiteQuestId in nextDefinition.PrerequisiteQuestIds)
			{
				if (!string.Equals(prerequisiteQuestId, key.QuestId, StringComparison.OrdinalIgnoreCase))
				{
					MissionStateRecord mission3 = transaction.GetMission(new MissionKey(characterId, prerequisiteQuestId));
					if (mission3 == null || mission3.State != MissionLifecycleState.Completed)
					{
						return Result(MissionOperationStatus.Rejected, mission, null, "Next-mission prerequisite is not completed for this character: " + prerequisiteQuestId);
					}
				}
			}
			bool flag = false;
			if (mission.State == MissionLifecycleState.Active)
			{
				mission.State = MissionLifecycleState.Completed;
				mission.CompletedAtUtcTicks = now;
				mission.UpdatedAtUtcTicks = now;
				transaction.SaveMission(key, mission);
				flag = true;
			}
			if (mission2 == null)
			{
				mission2 = new MissionStateRecord
				{
					CharacterId = characterId,
					QuestId = nextDefinition.QuestId,
					State = MissionLifecycleState.Active,
					CurrentStepId = nextDefinition.InitialStepId,
					OfferedAtUtcTicks = now,
					AcceptedAtUtcTicks = now,
					CreatedAtUtcTicks = now,
					UpdatedAtUtcTicks = now
				};
				transaction.SaveMission(nextKey, mission2);
				foreach (MissionObjectiveDefinition item in nextDefinition.Objectives.Where((MissionObjectiveDefinition value) => value.IsResolved))
				{
					transaction.SaveObjective(new MissionObjectiveKey(nextKey, item.ObjectiveId), new MissionObjectiveProgressRecord
					{
						CharacterId = characterId,
						QuestId = nextDefinition.QuestId,
						ObjectiveId = item.ObjectiveId,
						Progress = 0,
						RequiredCount = item.RequiredCount,
						CreatedAtUtcTicks = now,
						UpdatedAtUtcTicks = now
					});
				}
				flag = true;
			}
			else if (mission2.State == MissionLifecycleState.Offered)
			{
				mission2.State = MissionLifecycleState.Active;
				mission2.AcceptedAtUtcTicks = now;
				mission2.UpdatedAtUtcTicks = now;
				transaction.SaveMission(nextKey, mission2);
				flag = true;
			}
			return Result(flag ? MissionOperationStatus.Applied : MissionOperationStatus.AlreadyApplied, mission, null, flag ? "Mission completed and next mission activated in one repository transaction." : "Mission completion and next-mission activation were already durable.");
		});
	}

	public MissionOperationResult CompleteMissionWithAccountFlag(int characterId, string accountKey, string questId, string accountFlagKey, string value)
	{
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, questId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		if (string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(accountFlagKey))
		{
			return Result(MissionOperationStatus.Unresolved, null, null, "Stable account key and account flag key are required.");
		}
		string normalizedAccountKey = accountKey.Trim();
		string normalizedFlagKey = accountFlagKey.Trim();
		long now = Now();
		return repository.Execute(characterId, normalizedAccountKey, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionAccountFlagRecord accountFlag = transaction.GetAccountFlag(normalizedAccountKey, normalizedFlagKey);
			if (accountFlag != null && (!string.Equals(accountFlag.SourceQuestId, key.QuestId, StringComparison.OrdinalIgnoreCase) || !string.Equals(accountFlag.Value, value, StringComparison.Ordinal)))
			{
				return Result(MissionOperationStatus.Rejected, transaction.GetMission(key), null, "Account flag conflicts with an existing durable value.");
			}
			MissionOperationResult missionOperationResult2 = CompleteWithinTransaction(transaction, key, definition, now);
			if (!missionOperationResult2.Succeeded)
			{
				return missionOperationResult2;
			}
			if (accountFlag != null)
			{
				return Result((missionOperationResult2.Status == MissionOperationStatus.Applied) ? MissionOperationStatus.Applied : MissionOperationStatus.AlreadyApplied, missionOperationResult2.Mission, null, "Mission completion and account flag were already durable.");
			}
			transaction.SaveAccountFlag(normalizedAccountKey, new MissionAccountFlagRecord
			{
				AccountKey = normalizedAccountKey,
				FlagKey = normalizedFlagKey,
				Value = value,
				SourceQuestId = key.QuestId,
				CreatedAtUtcTicks = now,
				UpdatedAtUtcTicks = now
			});
			return Result(MissionOperationStatus.Applied, missionOperationResult2.Mission, null, "Mission completed and account access flag persisted.");
		});
	}

	public MissionOperationResult SetAccountFlag(int characterId, string accountKey, string sourceQuestId, string accountFlagKey, string value)
	{
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, sourceQuestId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		if (string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(accountFlagKey))
		{
			return Result(MissionOperationStatus.Unresolved, null, null, "Stable account key and account flag key are required.");
		}
		string normalizedAccountKey = accountKey.Trim();
		string normalizedFlagKey = accountFlagKey.Trim();
		long now = Now();
		return repository.Execute(characterId, normalizedAccountKey, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionStateRecord mission = transaction.GetMission(key);
			if (mission == null || mission.State != MissionLifecycleState.Completed)
			{
				return Result(MissionOperationStatus.Rejected, mission, null, "Source mission must be completed before granting account access.");
			}
			MissionAccountFlagRecord accountFlag = transaction.GetAccountFlag(normalizedAccountKey, normalizedFlagKey);
			if (accountFlag != null)
			{
				if (string.Equals(accountFlag.SourceQuestId, key.QuestId, StringComparison.OrdinalIgnoreCase) && string.Equals(accountFlag.Value, value, StringComparison.Ordinal))
				{
					return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Account flag already exists.");
				}
				return Result(MissionOperationStatus.Rejected, mission, null, "Account flag conflicts with an existing value.");
			}
			transaction.SaveAccountFlag(normalizedAccountKey, new MissionAccountFlagRecord
			{
				AccountKey = normalizedAccountKey,
				FlagKey = normalizedFlagKey,
				Value = value,
				SourceQuestId = key.QuestId,
				CreatedAtUtcTicks = now,
				UpdatedAtUtcTicks = now
			});
			return Result(MissionOperationStatus.Applied, mission, null, "Account flag persisted.");
		});
	}

	public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
	{
		if (string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(flagKey))
		{
			return null;
		}
		return repository.GetAccountFlag(accountKey.Trim(), flagKey.Trim());
	}

	public MissionOperationResult SetFlag(int characterId, string questId, string flagKey, string value)
	{
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, questId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		if (string.IsNullOrWhiteSpace(flagKey))
		{
			return Result(MissionOperationStatus.Unresolved, null, null, "Mission flag key is required.");
		}
		string normalizedFlagKey = flagKey.Trim();
		long now = Now();
		return repository.Execute(characterId, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionStateRecord mission = transaction.GetMission(key);
			if (mission == null)
			{
				return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
			}
			MissionFlagRecord missionFlagRecord = transaction.GetFlag(key, normalizedFlagKey);
			if (missionFlagRecord != null && string.Equals(missionFlagRecord.Value, value, StringComparison.Ordinal))
			{
				return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Mission flag already has that value.");
			}
			if (missionFlagRecord == null)
			{
				missionFlagRecord = new MissionFlagRecord
				{
					CharacterId = characterId,
					QuestId = key.QuestId,
					FlagKey = normalizedFlagKey,
					CreatedAtUtcTicks = now
				};
			}
			missionFlagRecord.Value = value;
			missionFlagRecord.UpdatedAtUtcTicks = now;
			transaction.SaveFlag(key, missionFlagRecord);
			return Result(MissionOperationStatus.Applied, mission, null, "Mission flag persisted.");
		});
	}

	public MissionFlagRecord GetFlag(int characterId, string questId, string flagKey)
	{
		if (!TryCreateKey(characterId, questId, out var key) || string.IsNullOrWhiteSpace(flagKey))
		{
			return null;
		}
		return repository.Execute(characterId, (IMissionRepositoryTransaction transaction) => transaction.GetFlag(key, flagKey.Trim()));
	}

	public MissionOperationResult FailMission(int characterId, string questId)
	{
		return SetTerminalState(characterId, questId, MissionLifecycleState.Failed);
	}

	public MissionOperationResult AbandonMission(int characterId, string questId)
	{
		return SetTerminalState(characterId, questId, MissionLifecycleState.Abandoned);
	}

	public MissionReloadResult ReloadForLogin(int characterId)
	{
		return Reload(characterId, MissionReloadReason.Login);
	}

	public MissionReloadResult ReloadForReconnect(int characterId)
	{
		return Reload(characterId, MissionReloadReason.Reconnect);
	}

	public MissionReloadResult ReloadForZoning(int characterId)
	{
		return Reload(characterId, MissionReloadReason.Zoning);
	}

	public MissionReloadResult ReloadAfterZoneEngineRestart(int characterId)
	{
		return Reload(characterId, MissionReloadReason.ZoneEngineRestart);
	}

	public MissionReloadResult Reload(int characterId, MissionReloadReason reason)
	{
		if (characterId <= 0)
		{
			return new MissionReloadResult
			{
				CharacterId = characterId,
				Reason = reason,
				Snapshot = new MissionCharacterSnapshot(characterId, null, null, null, null),
				ClientJournalReconciliationSupported = false
			};
		}
		return new MissionReloadResult
		{
			CharacterId = characterId,
			Reason = reason,
			Snapshot = repository.ReadCharacter(characterId),
			ClientJournalReconciliationSupported = false
		};
	}

	private MissionOperationResult SetTerminalState(int characterId, string questId, MissionLifecycleState terminalState)
	{
		MissionKey key;
		MissionDefinition definition;
		MissionOperationResult missionOperationResult = ResolveMutation(characterId, questId, out key, out definition);
		if (missionOperationResult != null)
		{
			return missionOperationResult;
		}
		long now = Now();
		return repository.Execute(characterId, delegate(IMissionRepositoryTransaction transaction)
		{
			MissionStateRecord mission = transaction.GetMission(key);
			if (mission == null)
			{
				return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
			}
			if (mission.State == terminalState)
			{
				return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Mission is already in that terminal state.");
			}
			bool num;
			if (terminalState != MissionLifecycleState.Failed)
			{
				if (mission.State == MissionLifecycleState.Offered)
				{
					goto IL_0097;
				}
				num = mission.State == MissionLifecycleState.Active;
			}
			else
			{
				num = mission.State == MissionLifecycleState.Active;
			}
			if (!num)
			{
				return Result(MissionOperationStatus.Rejected, mission, null, "Invalid terminal mission transition.");
			}
			goto IL_0097;
			IL_0097:
			mission.State = terminalState;
			mission.UpdatedAtUtcTicks = now;
			if (terminalState == MissionLifecycleState.Failed)
			{
				mission.FailedAtUtcTicks = now;
			}
			else
			{
				mission.AbandonedAtUtcTicks = now;
			}
			transaction.SaveMission(key, mission);
			return Result(MissionOperationStatus.Applied, mission, null, "Mission entered terminal state.");
		});
	}

	private MissionOperationResult CompleteWithinTransaction(IMissionRepositoryTransaction transaction, MissionKey key, MissionDefinition definition, long now)
	{
		MissionStateRecord mission = transaction.GetMission(key);
		if (mission == null)
		{
			return Result(MissionOperationStatus.NotFound, null, null, "Mission does not exist.");
		}
		if (mission.State == MissionLifecycleState.Completed)
		{
			return Result(MissionOperationStatus.AlreadyApplied, mission, null, "Mission was already completed.");
		}
		if (mission.State != MissionLifecycleState.Active)
		{
			return Result(MissionOperationStatus.Rejected, mission, null, "Only active missions can complete.");
		}
		foreach (MissionObjectiveDefinition objective2 in definition.Objectives)
		{
			if (!objective2.IsResolved || objective2.RequiredCount <= 0)
			{
				return Result(MissionOperationStatus.Unresolved, mission, null, "Mission has unresolved objective behavior.");
			}
			MissionObjectiveProgressRecord objective = transaction.GetObjective(new MissionObjectiveKey(key, objective2.ObjectiveId));
			if (objective == null || objective.Progress < objective.RequiredCount)
			{
				return Result(MissionOperationStatus.Rejected, mission, objective, "Mission objectives are incomplete.");
			}
		}
		mission.State = MissionLifecycleState.Completed;
		mission.CompletedAtUtcTicks = now;
		mission.UpdatedAtUtcTicks = now;
		transaction.SaveMission(key, mission);
		return Result(MissionOperationStatus.Applied, mission, null, "Mission completed.");
	}

	private MissionOperationResult ResolveMutation(int characterId, string questId, out MissionKey key, out MissionDefinition definition)
	{
		if (!TryCreateKey(characterId, questId, out key))
		{
			definition = null;
			return Result(MissionOperationStatus.Unresolved, null, null, "Stable character and quest identities are required.");
		}
		if (!definitions.TryGetValue(key.QuestId, out definition) || definition == null || !definition.IsResolved)
		{
			return Result(MissionOperationStatus.Unresolved, null, null, "Mission definition is unresolved.");
		}
		return null;
	}

	private long Now()
	{
		long num = utcNowTicks();
		if (num <= 0)
		{
			throw new InvalidOperationException("Mission clock returned an invalid UTC tick value.");
		}
		return num;
	}

	private static bool TryCreateKey(int characterId, string questId, out MissionKey key)
	{
		if (characterId <= 0 || string.IsNullOrWhiteSpace(questId))
		{
			key = default(MissionKey);
			return false;
		}
		key = new MissionKey(characterId, questId);
		return true;
	}

	private static MissionOperationResult Result(MissionOperationStatus status, MissionStateRecord mission, MissionObjectiveProgressRecord objective, string message)
	{
		return new MissionOperationResult
		{
			Status = status,
			Mission = mission?.Clone(),
			Objective = objective?.Clone(),
			Message = message
		};
	}

	private static Dictionary<string, MissionDefinition> ValidateAndIndexDefinitions(IEnumerable<MissionDefinition> definitions)
	{
		Dictionary<string, MissionDefinition> dictionary = new Dictionary<string, MissionDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (MissionDefinition item in definitions ?? Enumerable.Empty<MissionDefinition>())
		{
			if (item == null || string.IsNullOrWhiteSpace(item.QuestId))
			{
				throw new InvalidOperationException("Mission definitions require a quest identity.");
			}
			item.QuestId = item.QuestId.Trim();
			if (dictionary.ContainsKey(item.QuestId))
			{
				throw new InvalidOperationException("Duplicate mission definition: " + item.QuestId);
			}
			item.StepIds = (from value in item.StepIds ?? new string[0]
				where !string.IsNullOrWhiteSpace(value)
				select value.Trim()).ToList();
			if (item.StepIds.Count != item.StepIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
			{
				throw new InvalidOperationException("Duplicate mission step identity: " + item.QuestId);
			}
			item.PrerequisiteQuestIds = (from value in item.PrerequisiteQuestIds ?? new string[0]
				where !string.IsNullOrWhiteSpace(value)
				select value.Trim()).ToList();
			if (item.PrerequisiteQuestIds.Count != item.PrerequisiteQuestIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
			{
				throw new InvalidOperationException("Duplicate mission prerequisite: " + item.QuestId);
			}
			item.Objectives = (item.Objectives ?? new MissionObjectiveDefinition[0]).ToList();
			if (item.IsResolved)
			{
				if (string.IsNullOrWhiteSpace(item.InitialStepId) || !item.StepIds.Contains(item.InitialStepId.Trim(), StringComparer.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException("Resolved mission initial step is invalid: " + item.QuestId);
				}
				item.InitialStepId = item.InitialStepId.Trim();
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (MissionObjectiveDefinition objective in item.Objectives)
			{
				if (objective == null || string.IsNullOrWhiteSpace(objective.ObjectiveId) || !hashSet.Add(objective.ObjectiveId.Trim()))
				{
					throw new InvalidOperationException("Missing or duplicate mission objective: " + item.QuestId);
				}
				objective.ObjectiveId = objective.ObjectiveId.Trim();
				if (objective.IsResolved && (objective.RequiredCount <= 0 || string.IsNullOrWhiteSpace(objective.StepId) || !item.StepIds.Contains(objective.StepId.Trim(), StringComparer.OrdinalIgnoreCase)))
				{
					throw new InvalidOperationException("Resolved mission objective is invalid: " + objective.ObjectiveId);
				}
				if (!string.IsNullOrWhiteSpace(objective.StepId))
				{
					objective.StepId = objective.StepId.Trim();
				}
			}
			dictionary.Add(item.QuestId, item);
		}
		foreach (MissionDefinition value in dictionary.Values)
		{
			foreach (string prerequisiteQuestId in value.PrerequisiteQuestIds)
			{
				if (string.Equals(prerequisiteQuestId, value.QuestId, StringComparison.OrdinalIgnoreCase) || !dictionary.ContainsKey(prerequisiteQuestId))
				{
					throw new InvalidOperationException("Mission prerequisite is missing or self-referential: " + value.QuestId);
				}
			}
		}
		return dictionary;
	}
}
