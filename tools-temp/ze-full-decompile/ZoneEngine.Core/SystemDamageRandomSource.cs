using System;

namespace ZoneEngine.Core;

public sealed class SystemDamageRandomSource : IDamageRandomSource
{
	private readonly Random random;

	private readonly object randomLock;

	public SystemDamageRandomSource()
		: this(new Random())
	{
	}

	public SystemDamageRandomSource(Random random)
	{
		if (random == null)
		{
			throw new ArgumentNullException("random");
		}
		this.random = random;
		randomLock = new object();
	}

	public int NextInclusive(int minimumInclusive, int maximumInclusive)
	{
		if (maximumInclusive < minimumInclusive)
		{
			throw new ArgumentOutOfRangeException("maximumInclusive");
		}
		if (maximumInclusive == minimumInclusive)
		{
			return minimumInclusive;
		}
		lock (randomLock)
		{
			return random.Next(minimumInclusive, maximumInclusive + 1);
		}
	}

	public bool NextChance(int chanceBasisPoints)
	{
		if (chanceBasisPoints <= 0)
		{
			return false;
		}
		if (chanceBasisPoints >= 10000)
		{
			return true;
		}
		lock (randomLock)
		{
			return random.Next(0, 10000) < chanceBasisPoints;
		}
	}
}
