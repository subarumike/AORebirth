using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Missions;

public sealed class MissionCharacterSnapshot
{
	public int CharacterId { get; private set; }

	public IList<MissionStateRecord> Missions { get; private set; }

	public IList<MissionObjectiveProgressRecord> Objectives { get; private set; }

	public IList<MissionFlagRecord> Flags { get; private set; }

	public IList<MissionRewardStageRecord> Rewards { get; private set; }

	public MissionCharacterSnapshot(int characterId, IEnumerable<MissionStateRecord> missions, IEnumerable<MissionObjectiveProgressRecord> objectives, IEnumerable<MissionFlagRecord> flags, IEnumerable<MissionRewardStageRecord> rewards)
	{
		CharacterId = characterId;
		Missions = (missions ?? Enumerable.Empty<MissionStateRecord>()).Select((MissionStateRecord value) => value.Clone()).ToList();
		Objectives = (objectives ?? Enumerable.Empty<MissionObjectiveProgressRecord>()).Select((MissionObjectiveProgressRecord value) => value.Clone()).ToList();
		Flags = (flags ?? Enumerable.Empty<MissionFlagRecord>()).Select((MissionFlagRecord value) => value.Clone()).ToList();
		Rewards = (rewards ?? Enumerable.Empty<MissionRewardStageRecord>()).Select((MissionRewardStageRecord value) => value.Clone()).ToList();
	}
}
