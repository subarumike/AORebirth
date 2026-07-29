using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete;

public sealed class AreteContentManifestLoadResult
{
	public IList<string> DialoguePackFiles { get; private set; }

	public IList<string> QuestPackFiles { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public AreteContentManifestLoadResult(IEnumerable<string> dialoguePackFiles, IEnumerable<string> questPackFiles, AreteValidationResult validation)
	{
		DialoguePackFiles = new List<string>(dialoguePackFiles ?? Enumerable.Empty<string>());
		QuestPackFiles = new List<string>(questPackFiles ?? Enumerable.Empty<string>());
		Validation = validation ?? new AreteValidationResult();
	}
}
