using System.Collections.Generic;

namespace ZoneEngine.Core.Missions;

public sealed class MissionRewardDefinition
{
	public string RewardKey { get; set; }

	public string RewardType { get; set; }

	public bool IsResolved { get; set; }

	public IList<MissionCharacterStatMutation> StatMutations { get; set; }

	public MissionRewardDefinition()
	{
		StatMutations = new MissionCharacterStatMutation[0];
	}
}
