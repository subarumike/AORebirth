namespace ZoneEngine.Core.Arete.Quests;

public sealed class ObjectiveProgressRecord
{
	public int CharacterId { get; set; }

	public string MissionId { get; set; }

	public string ObjectiveId { get; set; }

	public string ObjectiveType { get; set; }

	public int CurrentCount { get; set; }

	public int RequiredCount { get; set; }

	public bool Completed { get; set; }

	public int MatchedEvidenceCount { get; set; }

	public int IgnoredEvidenceCount { get; set; }

	public string LastMatchedEvidenceReference { get; set; }
}
