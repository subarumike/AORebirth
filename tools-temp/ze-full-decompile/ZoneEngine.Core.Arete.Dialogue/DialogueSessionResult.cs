using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueSessionResult
{
	public DialogueSession Session { get; private set; }

	public DialogueNode CurrentNode { get; private set; }

	public IList<DialogueOption> AvailableOptions { get; private set; }

	public IList<AreteRecordedAction> RecordedActions { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public DialogueSessionResult(DialogueSession session, DialogueNode currentNode, IEnumerable<DialogueOption> availableOptions, IEnumerable<AreteRecordedAction> recordedActions, AreteValidationResult validation)
	{
		Session = session;
		CurrentNode = currentNode;
		AvailableOptions = new List<DialogueOption>(availableOptions ?? Enumerable.Empty<DialogueOption>());
		RecordedActions = new List<AreteRecordedAction>(recordedActions ?? Enumerable.Empty<AreteRecordedAction>());
		Validation = validation ?? new AreteValidationResult();
	}
}
