using System;
using System.Collections.Generic;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete;

public sealed class AreteNoOpConditionEvaluator
{
	public bool AreDialogueConditionsSatisfied(IEnumerable<DialogueCondition> conditions)
	{
		foreach (DialogueCondition item in conditions ?? new DialogueCondition[0])
		{
			if (!Evaluate(item))
			{
				return false;
			}
		}
		return true;
	}

	public bool AreQuestConditionsSatisfied(IEnumerable<QuestCondition> conditions)
	{
		foreach (QuestCondition item in conditions ?? new QuestCondition[0])
		{
			if (!Evaluate(item))
			{
				return false;
			}
		}
		return true;
	}

	public bool Evaluate(DialogueCondition condition)
	{
		return condition != null && IsAlwaysTrueTestCondition(condition.Type);
	}

	public bool Evaluate(QuestCondition condition)
	{
		return condition != null && IsAlwaysTrueTestCondition(condition.Type);
	}

	private static bool IsAlwaysTrueTestCondition(string conditionType)
	{
		return string.Equals(conditionType, "alwaysTrueTest", StringComparison.OrdinalIgnoreCase) || string.Equals(conditionType, "testAlwaysTrue", StringComparison.OrdinalIgnoreCase);
	}
}
