namespace AORebirth.Core.Playfields;

internal sealed class CapturedEnemyParallelAttackStreamDefinition
{
	internal double InitialDelaySeconds { get; private set; }

	internal CapturedEnemyCombatAttackDefinition Attack { get; private set; }

	internal bool IsValid => InitialDelaySeconds >= 0.0 && Attack != null && Attack.IsValid;

	internal CapturedEnemyParallelAttackStreamDefinition(double initialDelaySeconds, CapturedEnemyCombatAttackDefinition attack)
	{
		InitialDelaySeconds = initialDelaySeconds;
		Attack = attack;
	}
}
