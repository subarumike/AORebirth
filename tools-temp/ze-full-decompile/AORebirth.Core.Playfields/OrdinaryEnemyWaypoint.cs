namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyWaypoint
{
	internal float X { get; private set; }

	internal float Y { get; private set; }

	internal float Z { get; private set; }

	internal OrdinaryEnemyWaypoint(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}
