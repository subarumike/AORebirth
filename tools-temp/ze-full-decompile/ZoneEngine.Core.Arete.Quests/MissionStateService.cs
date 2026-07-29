using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class MissionStateService
{
	private readonly AreteNoOpActionRecorder actionRecorder;

	private readonly QuestContentRegistry registry;

	private readonly MissionStateStore store;

	public MissionStateStore Store => store;

	public MissionStateService(QuestContentRegistry registry)
		: this(registry, new MissionStateStore(), new AreteNoOpActionRecorder())
	{
	}

	public MissionStateService(QuestContentRegistry registry, MissionStateStore store, AreteNoOpActionRecorder actionRecorder)
	{
		this.registry = registry;
		this.store = store ?? new MissionStateStore();
		this.actionRecorder = actionRecorder ?? new AreteNoOpActionRecorder();
	}

	public MissionStateResult GetMissionState(int characterId, string questId)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		ValidateCharacterId(characterId, areteValidationResult);
		QuestDefinition questDefinition = ResolveQuest(questId, areteValidationResult);
		if (!areteValidationResult.IsValid)
		{
			return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		MissionStateRecord orCreate = store.GetOrCreate(characterId, questDefinition.QuestId);
		return new MissionStateResult(orCreate, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
	}

	public MissionStateResult OfferMission(int characterId, string questId)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		ValidateCharacterId(characterId, areteValidationResult);
		QuestDefinition questDefinition = ResolveQuest(questId, areteValidationResult);
		if (!areteValidationResult.IsValid)
		{
			return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		ValidateChainPrerequisites(characterId, questDefinition.QuestId, areteValidationResult);
		if (!areteValidationResult.IsValid)
		{
			return new MissionStateResult(store.GetOrCreate(characterId, questDefinition.QuestId), Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		MissionStateRecord orCreate = store.GetOrCreate(characterId, questDefinition.QuestId);
		if (orCreate.State != 0)
		{
			areteValidationResult.AddError(questDefinition.QuestId, "mission cannot be offered from state '" + orCreate.State.ToString() + "'");
			return new MissionStateResult(orCreate, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		orCreate.State = AreteMissionState.Offered;
		orCreate.CurrentStepId = questDefinition.InitialStepId;
		orCreate.LastTransition = "offerMission";
		return CreateResult(orCreate, "offerMission", areteValidationResult);
	}

	public MissionStateResult AcceptMission(int characterId, string questId)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		ValidateCharacterId(characterId, areteValidationResult);
		QuestDefinition questDefinition = ResolveQuest(questId, areteValidationResult);
		if (!areteValidationResult.IsValid)
		{
			return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		MissionStateRecord orCreate = store.GetOrCreate(characterId, questDefinition.QuestId);
		if (orCreate.State != AreteMissionState.Offered)
		{
			areteValidationResult.AddError(questDefinition.QuestId, "mission is not offered");
			return new MissionStateResult(orCreate, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		orCreate.State = AreteMissionState.Active;
		orCreate.CurrentStepId = (string.IsNullOrWhiteSpace(orCreate.CurrentStepId) ? questDefinition.InitialStepId : orCreate.CurrentStepId);
		orCreate.LastTransition = "acceptMission";
		return CreateResult(orCreate, "acceptMission", areteValidationResult);
	}

	public MissionStateResult CompleteMission(int characterId, string questId)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		ValidateCharacterId(characterId, areteValidationResult);
		QuestDefinition questDefinition = ResolveQuest(questId, areteValidationResult);
		if (!areteValidationResult.IsValid)
		{
			return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		MissionStateRecord orCreate = store.GetOrCreate(characterId, questDefinition.QuestId);
		if (orCreate.State != AreteMissionState.Active)
		{
			areteValidationResult.AddError(questDefinition.QuestId, "mission is not active");
			return new MissionStateResult(orCreate, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		orCreate.State = AreteMissionState.Completed;
		orCreate.LastTransition = "completeMission";
		return CreateResult(orCreate, "completeMission", areteValidationResult);
	}

	public MissionStateResult FailMission(int characterId, string questId)
	{
		return SetTerminalState(characterId, questId, AreteMissionState.Failed, "failMission");
	}

	public MissionStateResult AbandonMission(int characterId, string questId)
	{
		return SetTerminalState(characterId, questId, AreteMissionState.Abandoned, "abandonMission");
	}

	private MissionStateResult SetTerminalState(int characterId, string questId, AreteMissionState terminalState, string transitionName)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		ValidateCharacterId(characterId, areteValidationResult);
		QuestDefinition questDefinition = ResolveQuest(questId, areteValidationResult);
		if (!areteValidationResult.IsValid)
		{
			return new MissionStateResult(null, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		MissionStateRecord orCreate = store.GetOrCreate(characterId, questDefinition.QuestId);
		if (orCreate.State != AreteMissionState.Offered && orCreate.State != AreteMissionState.Active)
		{
			areteValidationResult.AddError(questDefinition.QuestId, "mission is not offered or active");
			return new MissionStateResult(orCreate, Enumerable.Empty<AreteRecordedAction>(), areteValidationResult);
		}
		orCreate.State = terminalState;
		orCreate.LastTransition = transitionName;
		return CreateResult(orCreate, transitionName, areteValidationResult);
	}

	private QuestDefinition ResolveQuest(string questId, AreteValidationResult validation)
	{
		if (registry == null)
		{
			validation.AddError("missionState", "quest registry is missing");
			return null;
		}
		if (string.IsNullOrWhiteSpace(questId))
		{
			validation.AddError("missionState", "missing mission id");
			return null;
		}
		if (!registry.TryGetQuest(questId, out var quest))
		{
			validation.AddError(questId, "mission was not found");
			return null;
		}
		return quest;
	}

	private void ValidateChainPrerequisites(int characterId, string questId, AreteValidationResult validation)
	{
		foreach (QuestChainLinkMetadata item in registry.GetLinksTo(questId))
		{
			if (item != null && !string.IsNullOrWhiteSpace(item.FromQuestId))
			{
				MissionStateRecord orCreate = store.GetOrCreate(characterId, item.FromQuestId);
				if (orCreate.State != AreteMissionState.Completed)
				{
					validation.AddError(questId, "mission prerequisite is not completed: '" + item.FromQuestId + "'");
				}
			}
		}
	}

	private static void ValidateCharacterId(int characterId, AreteValidationResult validation)
	{
		if (characterId <= 0)
		{
			validation.AddError("missionState", "stable character identity must be positive");
		}
	}

	private MissionStateResult CreateResult(MissionStateRecord record, string actionType, AreteValidationResult validation)
	{
		IList<AreteRecordedAction> recordedActions = new List<AreteRecordedAction> { actionRecorder.RecordMissionStateAction(actionType, record.QuestId, record.CurrentStepId) };
		return new MissionStateResult(record, recordedActions, validation);
	}
}
