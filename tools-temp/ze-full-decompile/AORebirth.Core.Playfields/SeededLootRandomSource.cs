using System;

namespace AORebirth.Core.Playfields;

internal sealed class SeededLootRandomSource : ILootRandomSource
{
	private readonly Random random;

	internal SeededLootRandomSource(int seed)
	{
		random = new Random(seed);
	}

	public int Next(int maximumExclusive)
	{
		return random.Next(maximumExclusive);
	}
}
