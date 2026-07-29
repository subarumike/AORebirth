namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyRuntimeDefinition
{
	internal OrdinaryEnemySpawnDefinition Spawn { get; private set; }

	internal OrdinaryEnemyProfile Profile { get; private set; }

	internal OrdinaryEnemySpawnGeneration SpawnGeneration { get; private set; }

	internal OrdinaryEnemyRuntimeDefinition(OrdinaryEnemySpawnDefinition spawn, OrdinaryEnemyProfile profile, OrdinaryEnemySpawnGeneration spawnGeneration)
	{
		Spawn = spawn;
		Profile = profile;
		SpawnGeneration = spawnGeneration;
	}
}
