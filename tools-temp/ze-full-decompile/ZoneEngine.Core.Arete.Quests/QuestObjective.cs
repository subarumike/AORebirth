using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class QuestObjective
{
	public string ObjectiveId { get; set; }

	public string Type { get; set; }

	public string Description { get; set; }

	public string TargetIdentity { get; set; }

	public int RequiredCount { get; set; }

	public IList<QuestCondition> Conditions { get; set; }

	public QuestObjective()
	{
		Conditions = new List<QuestCondition>();
	}
}
