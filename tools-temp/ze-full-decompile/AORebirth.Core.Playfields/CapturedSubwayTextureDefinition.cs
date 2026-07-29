namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayTextureDefinition
{
	public int Place { get; private set; }

	public int Id { get; private set; }

	public int Unknown { get; private set; }

	public CapturedSubwayTextureDefinition(int place, int id, int unknown)
	{
		Place = place;
		Id = id;
		Unknown = unknown;
	}
}
