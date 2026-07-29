namespace ZoneEngine.Core.Playfields;

internal struct VisibilityPosition
{
	internal float X { get; private set; }

	internal float Y { get; private set; }

	internal float Z { get; private set; }

	internal VisibilityPosition(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}
