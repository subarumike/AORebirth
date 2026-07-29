namespace ZoneEngine.Core.Playfields;

internal sealed class NascenceCoreHecklerSpawnDefinition
{
	internal int SourceIdentity { get; private set; }

	internal string Name { get; private set; }

	internal int Level { get; private set; }

	internal float X { get; private set; }

	internal float Y { get; private set; }

	internal float Z { get; private set; }

	internal int Health { get; private set; }

	internal int RunSpeed { get; private set; }

	internal NascenceCoreHecklerSpawnDefinition(int sourceIdentity, string name, int level, float x, float y, float z, int health, int runSpeed)
	{
		SourceIdentity = sourceIdentity;
		Name = name;
		Level = level;
		X = x;
		Y = y;
		Z = z;
		Health = health;
		RunSpeed = runSpeed;
	}
}
