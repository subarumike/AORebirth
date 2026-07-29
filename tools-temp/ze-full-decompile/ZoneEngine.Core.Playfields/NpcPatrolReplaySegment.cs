namespace ZoneEngine.Core.Playfields;

public sealed class NpcPatrolReplaySegment
{
	private const byte DefaultMoveMode = 24;

	public double DelayAfterSeconds { get; private set; }

	public float StartX { get; private set; }

	public float StartY { get; private set; }

	public float StartZ { get; private set; }

	public float EndX { get; private set; }

	public float EndY { get; private set; }

	public float EndZ { get; private set; }

	public byte MoveMode { get; private set; }

	public NpcPatrolReplaySegment(double delayAfterSeconds, float startX, float startY, float startZ, float endX, float endY, float endZ)
		: this(delayAfterSeconds, startX, startY, startZ, endX, endY, endZ, 24)
	{
	}

	public NpcPatrolReplaySegment(double delayAfterSeconds, float startX, float startY, float startZ, float endX, float endY, float endZ, byte moveMode)
	{
		DelayAfterSeconds = delayAfterSeconds;
		StartX = startX;
		StartY = startY;
		StartZ = startZ;
		EndX = endX;
		EndY = endY;
		EndZ = endZ;
		MoveMode = moveMode;
	}
}
