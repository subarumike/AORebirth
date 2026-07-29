using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueNpcEntry
{
	public string Id { get; set; }

	public string NpcIdentity { get; set; }

	public string Name { get; set; }

	public string RootNodeId { get; set; }

	public IList<string> Aliases { get; set; }

	public IList<DialogueNode> Nodes { get; set; }

	public IList<DialogueCondition> Conditions { get; set; }

	public IList<DialogueAction> Actions { get; set; }

	public DialogueNpcEntry()
	{
		Aliases = new List<string>();
		Nodes = new List<DialogueNode>();
		Conditions = new List<DialogueCondition>();
		Actions = new List<DialogueAction>();
	}
}
