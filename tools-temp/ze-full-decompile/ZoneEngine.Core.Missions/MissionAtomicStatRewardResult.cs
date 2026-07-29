using System.Collections.Generic;

namespace ZoneEngine.Core.Missions;

public sealed class MissionAtomicStatRewardResult
{
	public MissionAtomicRewardStatus Status { get; set; }

	public MissionRewardStageRecord Stage { get; set; }

	public IList<MissionCharacterStatValue> StatValues { get; set; }

	public string Message { get; set; }
}
