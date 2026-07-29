using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class QuestDefinition
{
	public string QuestId { get; set; }

	public string Title { get; set; }

	public string TitleConfidence { get; set; }

	public string SourceNpcIdentity { get; set; }

	public string InitialStepId { get; set; }

	public IList<QuestStep> Steps { get; set; }

	public IList<QuestCondition> Conditions { get; set; }

	public IList<QuestAction> Actions { get; set; }

	public IList<string> UnresolvedFields { get; set; }

	public QuestDefinition()
	{
		Steps = new List<QuestStep>();
		Conditions = new List<QuestCondition>();
		Actions = new List<QuestAction>();
		UnresolvedFields = new List<string>();
	}
}
