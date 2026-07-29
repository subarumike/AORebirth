using System;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyLevelSelectionState
{
	private OrdinaryEnemySpawnGeneration current;

	internal OrdinaryEnemySpawnGeneration Current => current;

	internal OrdinaryEnemySpawnGeneration ResolveForGeneration(OrdinaryEnemySpawnLevelDefinition definition, int generation, Func<int, int> nextRandom)
	{
		if (definition == null || !definition.IsValid)
		{
			throw new InvalidOperationException("A valid ordinary enemy level definition is required.");
		}
		if (generation <= 0)
		{
			throw new ArgumentOutOfRangeException("generation");
		}
		if (current != null)
		{
			if (generation < current.Number)
			{
				throw new InvalidOperationException("A stale population generation cannot replace the current level selection.");
			}
			if (generation == current.Number)
			{
				return current;
			}
		}
		OrdinaryEnemySpawnVariant selectedVariant = ((current != null && definition.RerollPolicy == OrdinaryEnemyLevelRerollPolicy.Never) ? current.SelectedVariant : definition.SelectVariant(nextRandom));
		current = new OrdinaryEnemySpawnGeneration(generation, selectedVariant);
		return current;
	}
}
