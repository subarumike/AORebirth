namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwayVendorWaypointDefinition
{
	internal float X { get; private set; }

	internal float Y { get; private set; }

	internal float Z { get; private set; }

	internal CapturedSubwayVendorWaypointDefinition(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}
