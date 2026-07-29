using System;

namespace ZoneEngine.Core.Missions;

public struct MissionRewardKey : IEquatable<MissionRewardKey>
{
	public MissionKey Mission { get; private set; }

	public string RewardKey { get; private set; }

	public MissionRewardKey(MissionKey mission, string rewardKey)
	{
		if (string.IsNullOrWhiteSpace(rewardKey))
		{
			throw new ArgumentException("Reward key is required.", "rewardKey");
		}
		Mission = mission;
		RewardKey = rewardKey.Trim();
	}

	public bool Equals(MissionRewardKey other)
	{
		return Mission.Equals(other.Mission) && string.Equals(RewardKey, other.RewardKey, StringComparison.OrdinalIgnoreCase);
	}

	public override bool Equals(object obj)
	{
		return obj is MissionRewardKey && Equals((MissionRewardKey)obj);
	}

	public override int GetHashCode()
	{
		return (Mission.GetHashCode() * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(RewardKey ?? string.Empty);
	}

	public override string ToString()
	{
		return Mission.ToString() + "|" + RewardKey;
	}
}
