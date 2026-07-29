using System;
using System.Collections.Generic;
using System.Linq;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueMissionActionAdapter
{
	private readonly MissionStateService missionStateService;

	public DialogueMissionActionAdapter(MissionStateService missionStateService)
	{
		this.missionStateService = missionStateService;
	}

	public DialogueMissionActionAdapterResult ExecuteAction(int characterId, DialogueAction action)
	{
		return ExecuteActionsForSession(characterId, null, new DialogueAction[1] { action });
	}

	public DialogueMissionActionAdapterResult ExecuteActions(int characterId, IEnumerable<DialogueAction> actions)
	{
		return ExecuteActionsForSession(characterId, null, actions);
	}

	public DialogueMissionActionAdapterResult ExecuteActionsForSession(int characterId, DialogueSession session, IEnumerable<DialogueAction> actions)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		List<DialogueMissionActionResult> list = new List<DialogueMissionActionResult>();
		bool endedDialogue = false;
		foreach (DialogueAction item in actions ?? Enumerable.Empty<DialogueAction>())
		{
			DialogueMissionActionResult dialogueMissionActionResult = ExecuteSingleAction(characterId, session, item);
			list.Add(dialogueMissionActionResult);
			areteValidationResult.AddErrors(dialogueMissionActionResult.Validation);
			if (dialogueMissionActionResult.EndedDialogue)
			{
				endedDialogue = true;
			}
		}
		return new DialogueMissionActionAdapterResult(list, endedDialogue, areteValidationResult);
	}

	private DialogueMissionActionResult ExecuteSingleAction(int characterId, DialogueSession session, DialogueAction action)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		if (action == null)
		{
			areteValidationResult.AddError("dialogueAction", "dialogue action is missing");
			return CreateActionResult(null, null, endedDialogue: false, null, applied: false, areteValidationResult);
		}
		if (string.IsNullOrWhiteSpace(action.Type))
		{
			areteValidationResult.AddError("dialogueAction", "missing dialogue action type");
			return CreateActionResult(action.Type, action.QuestId, endedDialogue: false, null, applied: false, areteValidationResult);
		}
		if (IsEndDialogueAction(action.Type))
		{
			if (session != null)
			{
				session.IsActive = false;
			}
			return CreateActionResult(action.Type, action.QuestId, endedDialogue: true, null, applied: true, areteValidationResult);
		}
		MissionStateResult missionStateResult = ExecuteMissionAction(characterId, action, areteValidationResult);
		bool applied = missionStateResult?.IsValid ?? false;
		return CreateActionResult(action.Type, action.QuestId, endedDialogue: false, missionStateResult, applied, areteValidationResult);
	}

	private MissionStateResult ExecuteMissionAction(int characterId, DialogueAction action, AreteValidationResult validation)
	{
		if (missionStateService == null)
		{
			validation.AddError("dialogueMissionActionAdapter", "mission state service is missing");
			return null;
		}
		MissionStateResult missionStateResult;
		if (IsAction(action.Type, "OfferMission"))
		{
			missionStateResult = missionStateService.OfferMission(characterId, action.QuestId);
		}
		else if (IsAction(action.Type, "AcceptMission"))
		{
			missionStateResult = missionStateService.AcceptMission(characterId, action.QuestId);
		}
		else if (IsAction(action.Type, "CompleteMission"))
		{
			missionStateResult = missionStateService.CompleteMission(characterId, action.QuestId);
		}
		else if (IsAction(action.Type, "FailMission"))
		{
			missionStateResult = missionStateService.FailMission(characterId, action.QuestId);
		}
		else
		{
			if (!IsAction(action.Type, "AbandonMission"))
			{
				validation.AddError("dialogueAction", "unsupported dialogue action type '" + action.Type + "'");
				return null;
			}
			missionStateResult = missionStateService.AbandonMission(characterId, action.QuestId);
		}
		validation.AddErrors(missionStateResult?.Validation);
		return missionStateResult;
	}

	private DialogueMissionActionResult CreateActionResult(string actionType, string questId, bool endedDialogue, MissionStateResult missionStateResult, bool applied, AreteValidationResult validation)
	{
		AreteRecordedAction recordedAction = new AreteRecordedAction
		{
			SourceType = "dialogueMissionAdapter",
			ActionType = actionType,
			QuestId = questId,
			WasApplied = applied,
			MutatedCharacterState = false
		};
		return new DialogueMissionActionResult(actionType, questId, endedDialogue, missionStateResult, recordedAction, validation);
	}

	private static bool IsAction(string actualType, string expectedType)
	{
		return string.Equals(actualType, expectedType, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsEndDialogueAction(string actionType)
	{
		return IsAction(actionType, "EndDialogue");
	}
}
