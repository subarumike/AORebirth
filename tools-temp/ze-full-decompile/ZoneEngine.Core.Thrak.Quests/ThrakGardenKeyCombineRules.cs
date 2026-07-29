using System.Collections.Generic;

namespace ZoneEngine.Core.Thrak.Quests;

internal static class ThrakGardenKeyCombineRules
{
	internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)
	{
		if (IsInsignia(sourceHighId) && IsAncientDevice(targetHighId))
		{
			return CreateEntry(sourceHighId, targetHighId);
		}
		if (IsAncientDevice(sourceHighId) && IsInsignia(targetHighId))
		{
			return CreateEntry(sourceHighId, targetHighId);
		}
		return null;
	}

	private static bool IsInsignia(int itemId)
	{
		return itemId == 214789;
	}

	private static bool IsAncientDevice(int itemId)
	{
		return itemId == 214998 || itemId == 214783;
	}

	private static TradeSkillEntry CreateEntry(int id1, int id2)
	{
		return new TradeSkillEntry
		{
			ID1 = id1,
			ID2 = id2,
			DeleteFlag = 3,
			IsImplant = false,
			MaxBump = 0,
			MaxXP = 0,
			MinTargetQL = 0,
			MinXP = 0,
			QLRangePercent = 0,
			ResultLowId = 214785,
			ResultHighId = 214785,
			Skills = new List<TradeSkillSkill>()
		};
	}
}
