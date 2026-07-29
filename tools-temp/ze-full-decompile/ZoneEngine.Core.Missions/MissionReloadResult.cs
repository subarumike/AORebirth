namespace ZoneEngine.Core.Missions;

public sealed class MissionReloadResult
{
	public int CharacterId { get; set; }

	public MissionReloadReason Reason { get; set; }

	public MissionCharacterSnapshot Snapshot { get; set; }

	public bool ClientJournalReconciliationSupported { get; set; }
}
