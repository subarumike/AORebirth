namespace ZoneEngine.Core.Arete.Quests;

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
