using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class QuestCondition
{
	public string Id { get; set; }

	public string Type { get; set; }

	public string QuestId { get; set; }

	public string StepId { get; set; }

	public string Value { get; set; }

	public IDictionary<string, string> Parameters { get; set; }

	public QuestCondition()
	{
		Parameters = new Dictionary<string, string>();
	}
}
