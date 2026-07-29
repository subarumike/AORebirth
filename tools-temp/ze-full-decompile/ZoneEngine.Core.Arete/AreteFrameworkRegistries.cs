using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete;

public sealed class AreteFrameworkRegistries
{
	public DialogueContentRegistry DialogueRegistry { get; private set; }

	public QuestContentRegistry QuestRegistry { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public AreteFrameworkRegistries(DialogueContentRegistry dialogueRegistry, QuestContentRegistry questRegistry, AreteValidationResult validation)
	{
		DialogueRegistry = dialogueRegistry;
		QuestRegistry = questRegistry;
		Validation = validation;
	}
}
