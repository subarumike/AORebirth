namespace ZoneEngine.Core.Missions;

public sealed class MissionObjectiveObservation
{
	public int CharacterId { get; set; }

	public string QuestId { get; set; }

	public string ObjectiveId { get; set; }

	public string ObservationKey { get; set; }

	public int Amount { get; set; }

	public string EventType { get; set; }

	public string SourceIdentity { get; set; }

	public string TargetIdentity { get; set; }
}
