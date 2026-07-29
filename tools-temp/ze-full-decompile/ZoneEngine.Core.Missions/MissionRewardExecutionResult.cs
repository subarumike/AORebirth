using System.Collections.Generic;

namespace ZoneEngine.Core.Missions;

public sealed class MissionRewardExecutionResult
{
	public MissionRewardExecutionStatus Status { get; set; }

	public MissionRewardStageRecord Stage { get; set; }

	public IList<MissionCharacterStatValue> StatValues { get; set; }

	public string Message { get; set; }

	public bool Succeeded => Status == MissionRewardExecutionStatus.Applied || Status == MissionRewardExecutionStatus.AlreadyApplied;
}
