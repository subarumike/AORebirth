namespace ZoneEngine.Core.Playfields;

public sealed class CapturedAreteRobotSpawnDefinition
{
	public int SourceInstance { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public int Health { get; private set; }

	public int Level { get; private set; }

	public int RunSpeed { get; private set; }

	public float PatrolX { get; private set; }

	public float PatrolY { get; private set; }

	public float PatrolZ { get; private set; }

	public CapturedAreteRobotSpawnDefinition(int sourceInstance, float x, float y, float z, int health, int level, int runSpeed, float patrolX, float patrolY, float patrolZ)
	{
		SourceInstance = sourceInstance;
		X = x;
		Y = y;
		Z = z;
		Health = health;
		Level = level;
		RunSpeed = runSpeed;
		PatrolX = patrolX;
		PatrolY = patrolY;
		PatrolZ = patrolZ;
	}
}
