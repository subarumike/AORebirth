namespace ZoneEngine.Core.Missions;

public sealed class MissionAccountFlagRecord
{
	public string AccountKey { get; set; }

	public string FlagKey { get; set; }

	public string Value { get; set; }

	public string SourceQuestId { get; set; }

	public long CreatedAtUtcTicks { get; set; }

	public long UpdatedAtUtcTicks { get; set; }

	public long Version { get; set; }

	public MissionAccountFlagRecord Clone()
	{
		return (MissionAccountFlagRecord)MemberwiseClone();
	}
}
