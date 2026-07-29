namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayWaypointDefinition
{
	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public CapturedSubwayWaypointDefinition(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}
