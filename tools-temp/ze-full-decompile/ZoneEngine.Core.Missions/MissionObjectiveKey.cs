using System;

namespace ZoneEngine.Core.Missions;

public struct MissionObjectiveKey : IEquatable<MissionObjectiveKey>
{
	public MissionKey Mission { get; private set; }

	public string ObjectiveId { get; private set; }

	public MissionObjectiveKey(MissionKey mission, string objectiveId)
	{
		if (string.IsNullOrWhiteSpace(objectiveId))
		{
			throw new ArgumentException("Objective identity is required.", "objectiveId");
		}
		Mission = mission;
		ObjectiveId = objectiveId.Trim();
	}

	public bool Equals(MissionObjectiveKey other)
	{
		return Mission.Equals(other.Mission) && string.Equals(ObjectiveId, other.ObjectiveId, StringComparison.OrdinalIgnoreCase);
	}

	public override bool Equals(object obj)
	{
		return obj is MissionObjectiveKey && Equals((MissionObjectiveKey)obj);
	}

	public override int GetHashCode()
	{
		return (Mission.GetHashCode() * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(ObjectiveId ?? string.Empty);
	}

	public override string ToString()
	{
		return Mission.ToString() + "|" + ObjectiveId;
	}
}
