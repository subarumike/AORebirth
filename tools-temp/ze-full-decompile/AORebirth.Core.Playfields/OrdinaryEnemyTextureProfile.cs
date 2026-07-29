namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyTextureProfile
{
	internal int Place { get; private set; }

	internal int Id { get; private set; }

	internal int Unknown { get; private set; }

	internal OrdinaryEnemyTextureProfile(int place, int id, int unknown)
	{
		Place = place;
		Id = id;
		Unknown = unknown;
	}
}
