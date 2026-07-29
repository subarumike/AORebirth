namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueSession
{
	public string SessionId { get; set; }

	public string NpcIdentity { get; set; }

	public string CurrentNodeId { get; set; }

	public bool IsActive { get; set; }
}
