namespace ZoneEngine.Core.Subway.Quests;

internal sealed class WindcallerKarrecNpcPatrolSegment
{
	internal double DelayAfterSeconds { get; private set; }

	internal float StartX { get; private set; }

	internal float StartY { get; private set; }

	internal float StartZ { get; private set; }

	internal float EndX { get; private set; }

	internal float EndY { get; private set; }

	internal float EndZ { get; private set; }

	internal byte MoveMode { get; private set; }

	internal WindcallerKarrecNpcPatrolSegment(double delayAfterSeconds, float startX, float startY, float startZ, float endX, float endY, float endZ, byte moveMode)
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
