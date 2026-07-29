namespace ZoneEngine.Core.Missions;

public sealed class MissionRewardEffectResult
{
	public MissionRewardEffectStatus Status { get; set; }

	public string EffectReference { get; set; }

	public string Error { get; set; }

	public static MissionRewardEffectResult Applied(string effectReference)
	{
		return new MissionRewardEffectResult
		{
			Status = MissionRewardEffectStatus.Applied,
			EffectReference = effectReference
		};
	}

	public static MissionRewardEffectResult AlreadyApplied(string effectReference)
	{
		return new MissionRewardEffectResult
		{
			Status = MissionRewardEffectStatus.AlreadyApplied,
			EffectReference = effectReference
		};
	}

	public static MissionRewardEffectResult RetryableFailure(string error)
	{
		return new MissionRewardEffectResult
		{
			Status = MissionRewardEffectStatus.RetryableFailure,
			Error = error
		};
	}
}
