namespace ZoneEngine.Core.Missions;

public sealed class MissionRewardClaimResult
{
	public MissionRewardClaimStatus Status { get; set; }

	public MissionRewardStageRecord Stage { get; set; }

	public string Message { get; set; }
}
