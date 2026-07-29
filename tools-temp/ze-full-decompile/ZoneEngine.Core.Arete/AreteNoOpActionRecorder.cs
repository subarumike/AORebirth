using System.Collections.Generic;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete;

public sealed class AreteNoOpActionRecorder
{
	public IList<AreteRecordedAction> RecordDialogueActions(IEnumerable<DialogueAction> actions)
	{
		List<AreteRecordedAction> list = new List<AreteRecordedAction>();
		foreach (DialogueAction item in actions ?? new DialogueAction[0])
		{
			if (item != null)
			{
				list.Add(Record(item));
			}
		}
		return list;
	}

	public IList<AreteRecordedAction> RecordQuestActions(IEnumerable<QuestAction> actions)
	{
		List<AreteRecordedAction> list = new List<AreteRecordedAction>();
		foreach (QuestAction item in actions ?? new QuestAction[0])
		{
			if (item != null)
			{
				list.Add(Record(item));
			}
		}
		return list;
	}

	public AreteRecordedAction Record(DialogueAction action)
	{
		return new AreteRecordedAction
		{
			SourceType = "dialogue",
			ActionType = action?.Type,
			QuestId = action?.QuestId,
			TargetNodeId = action?.TargetNodeId,
			Text = action?.Text,
			WasApplied = false,
			MutatedCharacterState = false
		};
	}

	public AreteRecordedAction Record(QuestAction action)
	{
		return new AreteRecordedAction
		{
			SourceType = "quest",
			ActionType = action?.Type,
			QuestId = action?.QuestId,
			StepId = action?.StepId,
			TargetIdentity = action?.TargetIdentity,
			WasApplied = false,
			MutatedCharacterState = false
		};
	}

	public AreteRecordedAction RecordMissionStateAction(string actionType, string questId, string stepId)
	{
		return new AreteRecordedAction
		{
			SourceType = "missionState",
			ActionType = actionType,
			QuestId = questId,
			StepId = stepId,
			WasApplied = false,
			MutatedCharacterState = false
		};
	}
}
