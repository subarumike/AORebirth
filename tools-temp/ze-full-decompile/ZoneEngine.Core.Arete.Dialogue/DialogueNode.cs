using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueNode
{
	public string Id { get; set; }

	public string PromptText { get; set; }

	public string PromptTextConfidence { get; set; }

	public IList<DialoguePromptSegment> PromptSegments { get; set; }

	public IList<DialogueOption> Options { get; set; }

	public IList<DialogueAction> EnterActions { get; set; }

	public DialogueNode()
	{
		PromptSegments = new List<DialoguePromptSegment>();
		Options = new List<DialogueOption>();
		EnterActions = new List<DialogueAction>();
	}
}
