using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueCondition
{
	public string Id { get; set; }

	public string Type { get; set; }

	public string QuestId { get; set; }

	public string Value { get; set; }

	public IDictionary<string, string> Parameters { get; set; }

	public DialogueCondition()
	{
		Parameters = new Dictionary<string, string>();
	}
}
