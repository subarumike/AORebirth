namespace ZoneEngine.Core.Arete.Quests;

public sealed class MissionStateRecord
{
	public int CharacterId { get; set; }

	public string QuestId { get; set; }

	public string CurrentStepId { get; set; }

	public AreteMissionState State { get; set; }

	public string UnlockedByQuestId { get; set; }

	public string LastTransition { get; set; }
}
