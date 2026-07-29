namespace ZoneEngine.Core.Playfields;

public sealed class NpcPatrolReplayCoordinator
{
	public delegate void AssignPatrolReplaySegments(NpcPatrolReplaySegment[] segments);

	private readonly CapturedAreteRobotContentProvider capturedRobotContentProvider;

	private readonly CapturedSubwayContentProvider capturedSubwayContentProvider;

	public NpcPatrolReplayCoordinator(CapturedAreteRobotContentProvider capturedRobotContentProvider)
		: this(capturedRobotContentProvider, null)
	{
	}

	internal NpcPatrolReplayCoordinator(CapturedAreteRobotContentProvider capturedRobotContentProvider, CapturedSubwayContentProvider capturedSubwayContentProvider)
	{
		this.capturedRobotContentProvider = capturedRobotContentProvider;
		this.capturedSubwayContentProvider = capturedSubwayContentProvider;
	}

	public NpcPatrolReplaySegment[] BuildCapturedAreteRobotSegments(int sourceInstance)
	{
		CapturedAreteRobotPatrolReplaySegment[] patrolReplaySegments = capturedRobotContentProvider.GetPatrolReplaySegments(sourceInstance);
		NpcPatrolReplaySegment[] array = new NpcPatrolReplaySegment[patrolReplaySegments.Length];
		for (int i = 0; i < patrolReplaySegments.Length; i++)
		{
			array[i] = new NpcPatrolReplaySegment(patrolReplaySegments[i].DelayAfterSeconds, patrolReplaySegments[i].StartX, patrolReplaySegments[i].StartY, patrolReplaySegments[i].StartZ, patrolReplaySegments[i].EndX, patrolReplaySegments[i].EndY, patrolReplaySegments[i].EndZ);
		}
		return array;
	}

	public void AssignCapturedAreteRobotReplay(int sourceInstance, AssignPatrolReplaySegments assignSegments)
	{
		assignSegments(BuildCapturedAreteRobotSegments(sourceInstance));
	}

	internal NpcPatrolReplaySegment[] BuildCapturedSubwaySegments(int sourceInstance)
	{
		CapturedSubwayPatrolReplaySegment[] array = ((capturedSubwayContentProvider == null) ? new CapturedSubwayPatrolReplaySegment[0] : capturedSubwayContentProvider.GetPatrolReplaySegments(sourceInstance));
		NpcPatrolReplaySegment[] array2 = new NpcPatrolReplaySegment[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array2[i] = new NpcPatrolReplaySegment(array[i].DelayAfterSeconds, array[i].StartX, array[i].StartY, array[i].StartZ, array[i].EndX, array[i].EndY, array[i].EndZ, array[i].MoveMode);
		}
		return array2;
	}

	internal void AssignCapturedSubwayReplay(int sourceInstance, AssignPatrolReplaySegments assignSegments)
	{
		assignSegments(BuildCapturedSubwaySegments(sourceInstance));
	}
}
