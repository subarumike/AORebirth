namespace ZoneEngine.Core.Missions;

public sealed class MissionRewardExecutionContext
{
	public int CharacterId { get; set; }

	public string QuestId { get; set; }

	public string RewardKey { get; set; }

	public string RewardType { get; set; }

	public string ClaimToken { get; set; }

	public int Attempt { get; set; }

	public string PriorEffectReference { get; set; }
}
