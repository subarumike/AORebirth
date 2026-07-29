using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueMissionActionResult
{
	public string ActionType { get; private set; }

	public string QuestId { get; private set; }

	public bool EndedDialogue { get; private set; }

	public MissionStateResult MissionStateResult { get; private set; }

	public AreteRecordedAction RecordedAction { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public DialogueMissionActionResult(string actionType, string questId, bool endedDialogue, MissionStateResult missionStateResult, AreteRecordedAction recordedAction, AreteValidationResult validation)
	{
		ActionType = actionType;
		QuestId = questId;
		EndedDialogue = endedDialogue;
		MissionStateResult = missionStateResult;
		RecordedAction = recordedAction;
		Validation = validation ?? new AreteValidationResult();
	}
}
