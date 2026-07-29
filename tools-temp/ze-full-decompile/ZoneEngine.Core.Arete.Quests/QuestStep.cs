using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class QuestStep
{
	public string StepId { get; set; }

	public string Name { get; set; }

	public IList<QuestObjective> Objectives { get; set; }

	public IList<QuestCondition> Conditions { get; set; }

	public IList<QuestAction> Actions { get; set; }

	public QuestStep()
	{
		Objectives = new List<QuestObjective>();
		Conditions = new List<QuestCondition>();
		Actions = new List<QuestAction>();
	}
}
