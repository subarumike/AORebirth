namespace ZoneEngine.Core.Missions;

public sealed class MissionRewardStageRecord
{
	public int CharacterId { get; set; }

	public string QuestId { get; set; }

	public string RewardKey { get; set; }

	public string RewardType { get; set; }

	public MissionRewardStatus Status { get; set; }

	public int Attempts { get; set; }

	public string LastError { get; set; }

	public string EffectReference { get; set; }

	public string ClaimToken { get; set; }

	public long ClaimedAtUtcTicks { get; set; }

	public long ClaimExpiresAtUtcTicks { get; set; }

	public long AppliedAtUtcTicks { get; set; }

	public long CreatedAtUtcTicks { get; set; }

	public long UpdatedAtUtcTicks { get; set; }

	public long Version { get; set; }

	public MissionRewardStageRecord Clone()
	{
		return (MissionRewardStageRecord)MemberwiseClone();
	}
}
