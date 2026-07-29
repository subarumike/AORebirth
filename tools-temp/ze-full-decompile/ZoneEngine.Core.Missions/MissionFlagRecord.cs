namespace ZoneEngine.Core.Missions;

public sealed class MissionFlagRecord
{
	public int CharacterId { get; set; }

	public string QuestId { get; set; }

	public string FlagKey { get; set; }

	public string Value { get; set; }

	public long CreatedAtUtcTicks { get; set; }

	public long UpdatedAtUtcTicks { get; set; }

	public long Version { get; set; }

	public MissionFlagRecord Clone()
	{
		return (MissionFlagRecord)MemberwiseClone();
	}
}
