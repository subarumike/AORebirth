namespace ZoneEngine.Core.Missions;

public sealed class MissionObjectiveProgressRecord
{
	public int CharacterId { get; set; }

	public string QuestId { get; set; }

	public string ObjectiveId { get; set; }

	public int Progress { get; set; }

	public int RequiredCount { get; set; }

	public string LastObservationKey { get; set; }

	public long CreatedAtUtcTicks { get; set; }

	public long UpdatedAtUtcTicks { get; set; }

	public long Version { get; set; }

	public MissionObjectiveProgressRecord Clone()
	{
		return (MissionObjectiveProgressRecord)MemberwiseClone();
	}
}
