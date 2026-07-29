namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayMeshDefinition
{
	public int Position { get; private set; }

	public uint Id { get; private set; }

	public int OverrideTextureId { get; private set; }

	public int Layer { get; private set; }

	public CapturedSubwayMeshDefinition(int position, uint id, int overrideTextureId, int layer)
	{
		Position = position;
		Id = id;
		OverrideTextureId = overrideTextureId;
		Layer = layer;
	}
}
