using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueAction
{
	public string Id { get; set; }

	public string Type { get; set; }

	public string QuestId { get; set; }

	public string TargetNodeId { get; set; }

	public string Text { get; set; }

	public IDictionary<string, string> Parameters { get; set; }

	public DialogueAction()
	{
		Parameters = new Dictionary<string, string>();
	}
}
