using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class QuestAction
{
	public string Id { get; set; }

	public string Type { get; set; }

	public string QuestId { get; set; }

	public string StepId { get; set; }

	public string TargetIdentity { get; set; }

	public IDictionary<string, string> Parameters { get; set; }

	public QuestAction()
	{
		Parameters = new Dictionary<string, string>();
	}
}
