namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System.Linq;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    public sealed class AreteFrameworkRegistries
    {
        public AreteFrameworkRegistries(
            DialogueContentRegistry dialogueRegistry,
            QuestContentRegistry questRegistry,
            AreteValidationResult validation)
        {
            this.DialogueRegistry = dialogueRegistry;
            this.QuestRegistry = questRegistry;
            this.Validation = validation;
        }

        public DialogueContentRegistry DialogueRegistry { get; private set; }

        public QuestContentRegistry QuestRegistry { get; private set; }

        public AreteValidationResult Validation { get; private set; }
    }

    public static class AreteFrameworkBootstrap
    {
        public static AreteFrameworkRegistries InitializeEmptyRegistries()
        {
            var validation = new AreteValidationResult();
            var dialogueRegistry = new DialogueContentRegistry();
            var questRegistry = new QuestContentRegistry();

            validation.AddErrors(dialogueRegistry.Load(Enumerable.Empty<DialogueContentPack>()));
            validation.AddErrors(questRegistry.Load(Enumerable.Empty<QuestContentPack>()));

            return new AreteFrameworkRegistries(dialogueRegistry, questRegistry, validation);
        }
    }
}
