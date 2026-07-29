using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueOption
{
	public string Id { get; set; }

	public int Index { get; set; }

	public string Text { get; set; }

	public string TextEvidence { get; set; }

	public string NextNodeId { get; set; }

	public IList<DialogueCondition> Conditions { get; set; }

	public IList<DialogueAction> Actions { get; set; }

	public DialogueOption()
	{
		Conditions = new List<DialogueCondition>();
		Actions = new List<DialogueAction>();
	}
}
