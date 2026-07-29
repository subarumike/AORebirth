namespace ZoneEngine.Core.MessageHandlers;

public sealed class GardenReturnPosition
{
	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public GardenReturnPosition(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}
