using System;

namespace AORebirth.Core.Playfields;

internal sealed class SystemPopulationRandomSource : IPopulationRandomSource
{
	private readonly Random random = new Random();

	public double NextUnit()
	{
		return random.NextDouble();
	}
}
