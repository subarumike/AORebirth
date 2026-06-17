namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;

    #endregion

    public sealed class QuestContentPackIdentity
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Source { get; set; }
    }

    public sealed class QuestContentPack
    {
        public QuestContentPack()
        {
            this.Identity = new QuestContentPackIdentity();
            this.SourceCaptures = new List<string>();
            this.Quests = new List<QuestDefinition>();
            this.Links = new List<QuestChainLinkMetadata>();
        }

        public QuestContentPackIdentity Identity { get; set; }

        public IList<string> SourceCaptures { get; set; }

        public IList<QuestDefinition> Quests { get; set; }

        public IList<QuestChainLinkMetadata> Links { get; set; }
    }

    public sealed class QuestDefinition
    {
        public QuestDefinition()
        {
            this.Steps = new List<QuestStep>();
            this.Conditions = new List<QuestCondition>();
            this.Actions = new List<QuestAction>();
            this.UnresolvedFields = new List<string>();
        }

        public string QuestId { get; set; }

        public string Title { get; set; }

        public string TitleConfidence { get; set; }

        public string SourceNpcIdentity { get; set; }

        public string InitialStepId { get; set; }

        public IList<QuestStep> Steps { get; set; }

        public IList<QuestCondition> Conditions { get; set; }

        public IList<QuestAction> Actions { get; set; }

        public IList<string> UnresolvedFields { get; set; }
    }

    public sealed class QuestStep
    {
        public QuestStep()
        {
            this.Objectives = new List<QuestObjective>();
            this.Conditions = new List<QuestCondition>();
            this.Actions = new List<QuestAction>();
        }

        public string StepId { get; set; }

        public string Name { get; set; }

        public IList<QuestObjective> Objectives { get; set; }

        public IList<QuestCondition> Conditions { get; set; }

        public IList<QuestAction> Actions { get; set; }
    }

    public sealed class QuestObjective
    {
        public QuestObjective()
        {
            this.Conditions = new List<QuestCondition>();
        }

        public string ObjectiveId { get; set; }

        public string Type { get; set; }

        public string Description { get; set; }

        public string TargetIdentity { get; set; }

        public int RequiredCount { get; set; }

        public IList<QuestCondition> Conditions { get; set; }
    }

    public sealed class QuestCondition
    {
        public QuestCondition()
        {
            this.Parameters = new Dictionary<string, string>();
        }

        public string Id { get; set; }

        public string Type { get; set; }

        public string QuestId { get; set; }

        public string StepId { get; set; }

        public string Value { get; set; }

        public IDictionary<string, string> Parameters { get; set; }
    }

    public sealed class QuestAction
    {
        public QuestAction()
        {
            this.Parameters = new Dictionary<string, string>();
        }

        public string Id { get; set; }

        public string Type { get; set; }

        public string QuestId { get; set; }

        public string StepId { get; set; }

        public string TargetIdentity { get; set; }

        public IDictionary<string, string> Parameters { get; set; }
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
