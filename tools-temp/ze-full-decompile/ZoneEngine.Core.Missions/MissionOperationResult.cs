namespace ZoneEngine.Core.Missions;

public sealed class MissionOperationResult
{
	public MissionOperationStatus Status { get; set; }

	public MissionStateRecord Mission { get; set; }

	public MissionObjectiveProgressRecord Objective { get; set; }

	public string Message { get; set; }

	public bool Succeeded => Status == MissionOperationStatus.Applied || Status == MissionOperationStatus.AlreadyApplied || Status == MissionOperationStatus.DuplicateObservation;
}
