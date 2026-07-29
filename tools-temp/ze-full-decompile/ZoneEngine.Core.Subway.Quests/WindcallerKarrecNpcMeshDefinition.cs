namespace ZoneEngine.Core.Subway.Quests;

internal sealed class WindcallerKarrecNpcMeshDefinition
{
	internal int Position { get; private set; }

	internal uint Id { get; private set; }

	internal int OverrideTextureId { get; private set; }

	internal int Layer { get; private set; }

	internal WindcallerKarrecNpcMeshDefinition(int position, uint id, int overrideTextureId, int layer)
	{
		Position = position;
		Id = id;
		OverrideTextureId = overrideTextureId;
		Layer = layer;
	}
}
