namespace ZoneEngine.Core;

internal sealed class PetShellDefinition
{
	public PetShellKind Kind { get; private set; }

	public int DisplayItemLowId { get; private set; }

	public int DisplayItemHighId { get; private set; }

	public int DisplayQuality { get; private set; }

	public int NanoId { get; private set; }

	public string PetHash { get; set; }

	public int PetTypeId { get; set; }

	public PetShellDefinition(PetShellKind kind, int displayItemLowId, int displayQuality, int nanoId, string petHash, int petTypeId, int displayItemHighId = 0)
	{
		Kind = kind;
		DisplayItemLowId = displayItemLowId;
		DisplayItemHighId = ((displayItemHighId > 0) ? displayItemHighId : displayItemLowId);
		DisplayQuality = displayQuality;
		NanoId = nanoId;
		PetHash = petHash;
		PetTypeId = petTypeId;
	}
}
