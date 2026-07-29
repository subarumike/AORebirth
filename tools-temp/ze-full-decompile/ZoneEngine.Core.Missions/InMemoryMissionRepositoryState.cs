using System;
using System.Collections.Generic;

namespace ZoneEngine.Core.Missions;

public sealed class InMemoryMissionRepositoryState
{
	internal readonly object SyncRoot = new object();

	internal Dictionary<MissionKey, MissionStateRecord> Missions = new Dictionary<MissionKey, MissionStateRecord>();

	internal Dictionary<MissionObjectiveKey, MissionObjectiveProgressRecord> Objectives = new Dictionary<MissionObjectiveKey, MissionObjectiveProgressRecord>();

	internal Dictionary<string, MissionObjectiveObservationRecord> Observations = new Dictionary<string, MissionObjectiveObservationRecord>(StringComparer.OrdinalIgnoreCase);

	internal Dictionary<string, MissionFlagRecord> Flags = new Dictionary<string, MissionFlagRecord>(StringComparer.OrdinalIgnoreCase);

	internal Dictionary<string, MissionAccountFlagRecord> AccountFlags = new Dictionary<string, MissionAccountFlagRecord>(StringComparer.OrdinalIgnoreCase);

	internal Dictionary<MissionRewardKey, MissionRewardStageRecord> Rewards = new Dictionary<MissionRewardKey, MissionRewardStageRecord>();

	internal Dictionary<string, long> CharacterStats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
}
