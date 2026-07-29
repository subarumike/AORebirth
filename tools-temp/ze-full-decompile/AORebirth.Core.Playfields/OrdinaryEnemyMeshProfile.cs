namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyMeshProfile
{
	internal int Position { get; private set; }

	internal uint Id { get; private set; }

	internal int OverrideTextureId { get; private set; }

	internal int Layer { get; private set; }

	internal OrdinaryEnemyMeshProfile(int position, uint id, int overrideTextureId, int layer)
	{
		Position = position;
		Id = id;
		OverrideTextureId = overrideTextureId;
		Layer = layer;
	}
}
