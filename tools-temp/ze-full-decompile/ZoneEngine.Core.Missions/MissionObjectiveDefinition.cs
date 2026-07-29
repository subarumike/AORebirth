namespace ZoneEngine.Core.Missions;

public sealed class MissionObjectiveDefinition
{
	public string ObjectiveId { get; set; }

	public string StepId { get; set; }

	public int RequiredCount { get; set; }

	public bool IsResolved { get; set; }
}
