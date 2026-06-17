namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public sealed class DialogueSession
    {
        public string SessionId { get; set; }

        public string NpcIdentity { get; set; }

        public string CurrentNodeId { get; set; }

        public bool IsActive { get; set; }
    }

    public sealed class DialogueSessionResult
    {
        public DialogueSessionResult(
            DialogueSession session,
            DialogueNode currentNode,
            IEnumerable<DialogueOption> availableOptions,
            IEnumerable<AreteRecordedAction> recordedActions,
            AreteValidationResult validation)
        {
            this.Session = session;
            this.CurrentNode = currentNode;
            this.AvailableOptions = new List<DialogueOption>(availableOptions ?? Enumerable.Empty<DialogueOption>());
            this.RecordedActions = new List<AreteRecordedAction>(
                recordedActions ?? Enumerable.Empty<AreteRecordedAction>());
            this.Validation = validation ?? new AreteValidationResult();
        }

        public DialogueSession Session { get; private set; }

        public DialogueNode CurrentNode { get; private set; }

        public IList<DialogueOption> AvailableOptions { get; private set; }

        public IList<AreteRecordedAction> RecordedActions { get; private set; }

        public AreteValidationResult Validation { get; private set; }

        public bool IsValid
        {
            get
            {
                return this.Validation.IsValid;
            }
        }
    }
}
