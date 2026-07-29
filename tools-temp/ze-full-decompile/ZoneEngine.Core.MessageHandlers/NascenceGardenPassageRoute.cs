namespace ZoneEngine.Core.MessageHandlers;

public sealed class NascenceGardenPassageRoute
{
	public int DestinationPlayfieldId { get; private set; }

	public float DestinationX { get; private set; }

	public float DestinationY { get; private set; }

	public float DestinationZ { get; private set; }

	public string Evidence { get; private set; }

	public NascenceGardenPassageRoute(int destinationPlayfieldId, float destinationX, float destinationY, float destinationZ, string evidence)
	{
		DestinationPlayfieldId = destinationPlayfieldId;
		DestinationX = destinationX;
		DestinationY = destinationY;
		DestinationZ = destinationZ;
		Evidence = evidence;
	}
}
