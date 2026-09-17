using System.Collections.Generic;

// Names and fields are the serialized operator-content schema, not a runtime quest interpreter.
namespace ZoneEngine.Core.Arete.Quests
{
    public sealed class QuestContentPackIdentity
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Source { get; set; }
    }

    public sealed class QuestContentPack
    {
        public QuestContentPackIdentity Identity { get; set; } = new QuestContentPackIdentity();
        public IList<string> SourceCaptures { get; set; } = new List<string>();
        public IList<QuestDefinition> Quests { get; set; } = new List<QuestDefinition>();
        public IList<QuestChainLinkMetadata> Links { get; set; } = new List<QuestChainLinkMetadata>();
    }

    public sealed class QuestDefinition
    {
        public string QuestId { get; set; }
        public string Title { get; set; }
        public string TitleConfidence { get; set; }
        public string SourceNpcIdentity { get; set; }
        public string InitialStepId { get; set; }
        public IList<QuestStep> Steps { get; set; } = new List<QuestStep>();
        public IList<QuestCondition> Conditions { get; set; } = new List<QuestCondition>();
        public IList<QuestAction> Actions { get; set; } = new List<QuestAction>();
        public IList<string> UnresolvedFields { get; set; } = new List<string>();
    }

    public sealed class QuestStep
    {
        public string StepId { get; set; }
        public string Name { get; set; }
        public IList<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
        public IList<QuestCondition> Conditions { get; set; } = new List<QuestCondition>();
        public IList<QuestAction> Actions { get; set; } = new List<QuestAction>();
    }

    public sealed class QuestObjective
    {
        public string ObjectiveId { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string TargetIdentity { get; set; }
        public int RequiredCount { get; set; }
        public IList<QuestCondition> Conditions { get; set; } = new List<QuestCondition>();
    }

    public sealed class QuestCondition
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string QuestId { get; set; }
        public string StepId { get; set; }
        public string Value { get; set; }
        public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    public sealed class QuestAction
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string QuestId { get; set; }
        public string StepId { get; set; }
        public string TargetIdentity { get; set; }
        public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    public sealed class QuestChainLinkMetadata
    {
        public string Id { get; set; }
        public string FromQuestId { get; set; }
        public string FromStepId { get; set; }
        public string ToQuestId { get; set; }
        public string ToStepId { get; set; }
        public string Relationship { get; set; }
        public string Evidence { get; set; }
    }
}
