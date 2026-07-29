namespace ZoneEngine.Core;

public struct EnemyBehaviorTransition
{
	public EnemyBehaviorState State { get; private set; }

	public string Reason { get; private set; }

	public EnemyBehaviorTransition(EnemyBehaviorState state, string reason)
	{
		State = state;
		Reason = reason;
	}
}
