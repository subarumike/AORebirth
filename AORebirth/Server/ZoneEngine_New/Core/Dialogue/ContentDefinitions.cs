using System.Collections.Generic;

// The namespace and property names are the existing serialized content contract.
// Definitions carry no NPC identities, conversation text or runtime side effects.
namespace ZoneEngine.Core.Arete.Dialogue
{
    public sealed class DialogueContentPackIdentity
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Source { get; set; }
    }

    public sealed class DialogueContentPack
    {
        public DialogueContentPackIdentity Identity { get; set; } = new DialogueContentPackIdentity();
        public IList<string> SourceCaptures { get; set; } = new List<string>();
        public IList<DialogueNpcEntry> Npcs { get; set; } = new List<DialogueNpcEntry>();
    }

    public sealed class DialogueNpcEntry
    {
        public string Id { get; set; }
        public string NpcIdentity { get; set; }
        public string Name { get; set; }
        public string RootNodeId { get; set; }
        public IList<string> Aliases { get; set; } = new List<string>();
        public IList<DialogueNode> Nodes { get; set; } = new List<DialogueNode>();
        public IList<DialogueCondition> Conditions { get; set; } = new List<DialogueCondition>();
        public IList<DialogueAction> Actions { get; set; } = new List<DialogueAction>();
    }

    public sealed class DialogueNode
    {
        public string Id { get; set; }
        public string PromptText { get; set; }
        public string PromptTextConfidence { get; set; }
        public IList<DialoguePromptSegment> PromptSegments { get; set; } = new List<DialoguePromptSegment>();
        public IList<DialogueOption> Options { get; set; } = new List<DialogueOption>();
        public IList<DialogueAction> EnterActions { get; set; } = new List<DialogueAction>();
    }

    public sealed class DialoguePromptSegment
    {
        public string Text { get; set; }
        public int Unknown2 { get; set; }
    }

    public sealed class DialogueOption
    {
        public string Id { get; set; }
        public int Index { get; set; }
        public string Text { get; set; }
        public string TextEvidence { get; set; }
        public bool Hidden { get; set; }
        public string NextNodeId { get; set; }
        public IList<DialogueCondition> Conditions { get; set; } = new List<DialogueCondition>();
        public IList<DialogueAction> Actions { get; set; } = new List<DialogueAction>();
    }

    public sealed class DialogueCondition
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string QuestId { get; set; }
        public string Value { get; set; }
        public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    public sealed class DialogueAction
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string QuestId { get; set; }
        public string TargetNodeId { get; set; }
        public string Text { get; set; }
        public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}
