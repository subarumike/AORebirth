namespace ZoneEngine.Core.Missions;

public enum MissionRewardExecutionStatus
{
	Applied = 1,
	AlreadyApplied,
	Busy,
	RetryableFailure,
	Rejected,
	Unresolved
}
