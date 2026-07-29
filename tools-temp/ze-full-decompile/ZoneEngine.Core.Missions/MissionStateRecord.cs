namespace ZoneEngine.Core.Missions;

public sealed class MissionStateRecord
{
	public int CharacterId { get; set; }

	public string QuestId { get; set; }

	public MissionLifecycleState State { get; set; }

	public string CurrentStepId { get; set; }

	public long OfferedAtUtcTicks { get; set; }

	public long AcceptedAtUtcTicks { get; set; }

	public long CompletedAtUtcTicks { get; set; }

	public long FailedAtUtcTicks { get; set; }

	public long AbandonedAtUtcTicks { get; set; }

	public long CreatedAtUtcTicks { get; set; }

	public long UpdatedAtUtcTicks { get; set; }

	public long Version { get; set; }

	public MissionKey Key => new MissionKey(CharacterId, QuestId);

	public MissionStateRecord Clone()
	{
		return (MissionStateRecord)MemberwiseClone();
	}
}
