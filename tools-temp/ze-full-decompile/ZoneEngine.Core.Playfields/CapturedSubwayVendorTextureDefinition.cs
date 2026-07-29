namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwayVendorTextureDefinition
{
	internal int Place { get; private set; }

	internal int Id { get; private set; }

	internal int Unknown { get; private set; }

	internal CapturedSubwayVendorTextureDefinition(int place, int id, int unknown)
	{
		Place = place;
		Id = id;
		Unknown = unknown;
	}
}
