namespace ZoneEngine.Core.Missions;

public sealed class MissionObjectiveObservationRecord
{
	public int CharacterId { get; set; }

	public string QuestId { get; set; }

	public string ObjectiveId { get; set; }

	public string ObservationKey { get; set; }

	public string EventType { get; set; }

	public string SourceIdentity { get; set; }

	public string TargetIdentity { get; set; }

	public long ObservedAtUtcTicks { get; set; }

	public MissionObjectiveKey ObjectiveKey => new MissionObjectiveKey(new MissionKey(CharacterId, QuestId), ObjectiveId);

	public MissionObjectiveObservationRecord Clone()
	{
		return (MissionObjectiveObservationRecord)MemberwiseClone();
	}
}
