namespace ZoneEngine.Core.Subway.Quests;

internal sealed class WindcallerKarrecNpcTextureDefinition
{
	internal int Place { get; private set; }

	internal int Id { get; private set; }

	internal int Unknown { get; private set; }

	internal WindcallerKarrecNpcTextureDefinition(int place, int id, int unknown)
	{
		Place = place;
		Id = id;
		Unknown = unknown;
	}
}
