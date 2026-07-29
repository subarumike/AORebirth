using System;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemySpawnGeneration
{
	internal int Number { get; private set; }

	internal OrdinaryEnemySpawnVariant SelectedVariant { get; private set; }

	internal OrdinaryEnemySpawnGeneration(int number, OrdinaryEnemySpawnVariant selectedVariant)
	{
		if (number <= 0)
		{
			throw new ArgumentOutOfRangeException("number");
		}
		if (selectedVariant == null)
		{
			throw new ArgumentNullException("selectedVariant");
		}
		Number = number;
		SelectedVariant = selectedVariant;
	}
}
