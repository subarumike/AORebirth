using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueMissionActionAdapterResult
{
	public IList<DialogueMissionActionResult> ActionResults { get; private set; }

	public bool EndedDialogue { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public DialogueMissionActionAdapterResult(IEnumerable<DialogueMissionActionResult> actionResults, bool endedDialogue, AreteValidationResult validation)
	{
		ActionResults = new List<DialogueMissionActionResult>(actionResults ?? Enumerable.Empty<DialogueMissionActionResult>());
		EndedDialogue = endedDialogue;
		Validation = validation ?? new AreteValidationResult();
	}
}
