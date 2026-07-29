using System;

namespace AORebirth.Core.Playfields;

internal static class OrdinaryEnemySupportNanoRuntimeRules
{
	internal static double SelectInitialDelaySeconds(OrdinaryEnemySupportNanoProfile profile, Func<int, int> selector)
	{
		if (profile == null)
		{
			throw new ArgumentNullException("profile");
		}
		if (!profile.RandomizeInitialDelay || profile.InitialDelaySeconds <= 0.0)
		{
			return profile.InitialDelaySeconds;
		}
		if (selector == null)
		{
			throw new ArgumentNullException("selector");
		}
		int num = checked((int)Math.Round(profile.InitialDelaySeconds * 1000.0));
		int num2 = selector(num + 1);
		if (num2 < 0 || num2 > num)
		{
			throw new InvalidOperationException("Support nano random selector returned an out-of-range initial phase.");
		}
		return (double)num2 / 1000.0;
	}

	internal static bool RollChance(int chanceBasisPoints, Func<int, int> selector)
	{
		if (chanceBasisPoints <= 0)
		{
			return false;
		}
		if (chanceBasisPoints >= 10000)
		{
			return true;
		}
		if (selector == null)
		{
			throw new ArgumentNullException("selector");
		}
		int num = selector(10000);
		if (num < 0 || num >= 10000)
		{
			throw new InvalidOperationException("Support nano random selector returned an out-of-range chance roll.");
		}
		return num < chanceBasisPoints;
	}

	internal static bool TrySpendNano(int currentNano, int nanoCost, out int remainingNano)
	{
		currentNano = Math.Max(0, currentNano);
		nanoCost = Math.Max(0, nanoCost);
		remainingNano = currentNano;
		if (nanoCost > currentNano)
		{
			return false;
		}
		remainingNano = currentNano - nanoCost;
		return true;
	}

	internal static int ApplyPositiveCappedDelta(int current, int maximum, int delta)
	{
		current = Math.Max(0, current);
		maximum = Math.Max(0, maximum);
		if (delta <= 0 || current >= maximum)
		{
			return Math.Min(current, maximum);
		}
		return current + Math.Min(delta, maximum - current);
	}
}
