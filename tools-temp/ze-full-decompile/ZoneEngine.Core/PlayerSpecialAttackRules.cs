namespace ZoneEngine.Core;

internal static class PlayerSpecialAttackRules
{
	internal const int FlingShotLockSeconds = 6;

	internal const int BurstLockSeconds = 17;

	internal const int BurstHitCount = 3;

	internal static bool IsSupportedSpecial(int specialStatId)
	{
		return specialStatId == 150 || specialStatId == 148;
	}

	internal static int ResolveLockSeconds(int specialStatId)
	{
		if (specialStatId == 148)
		{
			return 17;
		}
		return 6;
	}

	internal static int ResolveHitCount(int specialStatId)
	{
		if (specialStatId == 148)
		{
			return 3;
		}
		return 1;
	}
}
