namespace ZoneEngine.Core.Playfields;

public sealed class CapturedAreteRobotPatrolReplaySegment
{
	public double DelayAfterSeconds { get; private set; }

	public float StartX { get; private set; }

	public float StartY { get; private set; }

	public float StartZ { get; private set; }

	public float EndX { get; private set; }

	public float EndY { get; private set; }

	public float EndZ { get; private set; }

	public CapturedAreteRobotPatrolReplaySegment(double delayAfterSeconds, float startX, float startY, float startZ, float endX, float endY, float endZ)
	{
		DelayAfterSeconds = delayAfterSeconds;
		StartX = startX;
		StartY = startY;
		StartZ = startZ;
		EndX = endX;
		EndY = endY;
		EndZ = endZ;
	}
}
