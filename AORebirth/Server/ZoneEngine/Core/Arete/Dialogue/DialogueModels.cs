namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System.Collections.Generic;

    #endregion

    public sealed class DialogueContentPackIdentity
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Source { get; set; }
    }

    public sealed class DialogueContentPack
    {
        public DialogueContentPack()
        {
            this.Identity = new DialogueContentPackIdentity();
            this.SourceCaptures = new List<string>();
            this.Npcs = new List<DialogueNpcEntry>();
        }

        public DialogueContentPackIdentity Identity { get; set; }

        public IList<string> SourceCaptures { get; set; }

        public IList<DialogueNpcEntry> Npcs { get; set; }
    }

    public sealed class DialogueNpcEntry
    {
        public DialogueNpcEntry()
        {
            this.Aliases = new List<string>();
            this.Nodes = new List<DialogueNode>();
            this.Conditions = new List<DialogueCondition>();
            this.Actions = new List<DialogueAction>();
        }

        public string Id { get; set; }

        public string NpcIdentity { get; set; }

        public string Name { get; set; }

        public string RootNodeId { get; set; }

        public IList<string> Aliases { get; set; }

        public IList<DialogueNode> Nodes { get; set; }

        public IList<DialogueCondition> Conditions { get; set; }

        public IList<DialogueAction> Actions { get; set; }
    }

    public sealed class DialogueNode
    {
        public DialogueNode()
        {
            this.Options = new List<DialogueOption>();
            this.EnterActions = new List<DialogueAction>();
        }

        public string Id { get; set; }

        public string PromptText { get; set; }

        public string PromptTextConfidence { get; set; }

        public IList<DialogueOption> Options { get; set; }

        public IList<DialogueAction> EnterActions { get; set; }
    }

    public sealed class DialogueOption
    {
        public DialogueOption()
        {
            this.Conditions = new List<DialogueCondition>();
            this.Actions = new List<DialogueAction>();
        }

        public string Id { get; set; }

        public int Index { get; set; }

        public string Text { get; set; }

        public string TextEvidence { get; set; }

        public string NextNodeId { get; set; }

        public IList<DialogueCondition> Conditions { get; set; }

        public IList<DialogueAction> Actions { get; set; }
    }

    public sealed class DialogueCondition
    {
        public DialogueCondition()
        {
            this.Parameters = new Dictionary<string, string>();
        }

        public string Id { get; set; }

        public string Type { get; set; }

        public string QuestId { get; set; }

        public string Value { get; set; }

        public IDictionary<string, string> Parameters { get; set; }
    }

    public sealed class DialogueAction
    {
        public DialogueAction()
        {
            this.Parameters = new Dictionary<string, string>();
        }

        public string Id { get; set; }

        public string Type { get; set; }

        public string QuestId { get; set; }

        public string TargetNodeId { get; set; }

        public string Text { get; set; }

        public IDictionary<string, string> Parameters { get; set; }
    }
}
